using System.Collections.Generic;
using UnityEngine;

/// <summary>A prop that bursts into flying fragments when one of the player's bullets hits it.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ShatterOnHit : MonoBehaviour
{
    [Header("Fragments")]
    [SerializeField] private Vector2Int fragmentGrid = new Vector2Int(3, 3);
    [SerializeField] private float fragmentLifetime = 2.5f;
    [SerializeField] private float fadeSeconds = 0.8f;
    [SerializeField] private float fragmentGravityScale = 2.5f;
    [SerializeField] private bool fragmentsCollideWithEachOther;

    [Header("Burst")]
    [SerializeField] private float burstSpeed = 6f;
    [SerializeField] private float upwardBoost = 2.5f;
    [SerializeField] private float spinSpeed = 360f;
    [SerializeField] private float scatter = 0.35f;

    [Header("Respawn")]
    [Tooltip("Seconds before the prop comes back. 0 or less removes it for good.")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Pooling")]
    [Tooltip("When true, this component only spawns fragments and fires Shattered; it never destroys " +
        "or auto-restores the GameObject itself, leaving disposal (e.g. returning to an object pool) to the owner.")]
    [SerializeField] private bool externallyManaged = false;

    /// <summary>Fired right after fragments are spawned, before any respawn/destroy handling.</summary>
    public event System.Action<Vector2> Shattered;

    private SpriteRenderer visual;
    private Collider2D body;
    private bool broken;
    private Sprite[] cachedSlices;
    private bool slicesReady;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsPlayerBullet(collision.collider))
            Shatter(collision.GetContact(0).point, collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerBullet(other))
            Shatter(other.bounds.center, other);
    }

    private bool IsPlayerBullet(Collider2D other)
    {
        return !broken && other != null && other.GetComponentInParent<UpwardBullet>() != null;
    }

    /// <summary>
    /// Replaces the prop with a grid of loose pieces pushed away from <paramref name="impact"/>.
    /// <paramref name="hitBy"/>, if given, is ignored against every fragment so a piercing
    /// projectile (e.g. the energy wave) isn't physically blocked by the debris it just created.
    /// </summary>
    public void Shatter(Vector2 impact, Collider2D hitBy = null)
    {
        if (broken) return;
        broken = true;

        SpawnFragments(impact, hitBy);
        Shattered?.Invoke(impact);
        if (externallyManaged) return;

        if (respawnDelay > 0f)
        {
            if (visual != null) visual.enabled = false;
            if (body != null) body.enabled = false;
            Invoke(nameof(Restore), respawnDelay);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Re-arms the prop: visible, solid, and able to shatter again. Safe to call from pool reuse.</summary>
    public void Restore()
    {
        if (visual != null) visual.enabled = true;
        if (body != null) body.enabled = true;
        broken = false;
    }

    private void SpawnFragments(Vector2 impact, Collider2D hitBy)
    {
        var sprite = visual != null ? visual.sprite : null;
        if (sprite == null) return;

        int columns = Mathf.Max(1, fragmentGrid.x);
        int rows = Mathf.Max(1, fragmentGrid.y);
        Vector2 localSize = sprite.bounds.size;
        Vector2 localMin = sprite.bounds.min;
        Vector2 cell = new Vector2(localSize.x / columns, localSize.y / rows);
        Vector3 lossy = transform.lossyScale;

        // Slicing needs pixel access; the built-in sprites are not readable, so fall back to shrunken copies.
        var slices = GetSlices(sprite, columns, rows);
        Vector3 fragmentScale = slices != null
            ? lossy
            : new Vector3(lossy.x / columns, lossy.y / rows, 1f);

        var pieces = new List<Collider2D>(columns * rows);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                Vector2 localCenter = localMin + new Vector2((x + 0.5f) * cell.x, (y + 0.5f) * cell.y);
                Vector3 world = transform.TransformPoint(localCenter);

                var piece = new GameObject(name + "_Fragment");
                piece.layer = gameObject.layer;
                piece.transform.SetPositionAndRotation(world, transform.rotation);
                piece.transform.localScale = fragmentScale;

                int sliceIndex = y * columns + x;
                var pieceVisual = piece.AddComponent<SpriteRenderer>();
                pieceVisual.sprite = (slices != null && sliceIndex < slices.Length) ? slices[sliceIndex] : sprite;
                pieceVisual.sharedMaterial = visual.sharedMaterial;
                pieceVisual.sortingLayerID = visual.sortingLayerID;
                pieceVisual.sortingOrder = visual.sortingOrder + 1;
                float shade = Random.Range(0.75f, 1f);
                var tint = visual.color;
                pieceVisual.color = new Color(tint.r * shade, tint.g * shade, tint.b * shade, tint.a);

                var pieceBody = piece.AddComponent<Rigidbody2D>();
                pieceBody.gravityScale = fragmentGravityScale;
                pieceBody.mass = 0.2f;
                Vector2 away = (Vector2)world - impact;
                if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
                away = (away.normalized + Random.insideUnitCircle * scatter).normalized;
                pieceBody.linearVelocity = away * burstSpeed + Vector2.up * upwardBoost;
                pieceBody.angularVelocity = Random.Range(-spinSpeed, spinSpeed);

                var pieceCollider = piece.AddComponent<BoxCollider2D>();
                if (slices == null) pieceCollider.size = sprite.bounds.size;
                if (body != null) Physics2D.IgnoreCollision(pieceCollider, body);
                if (hitBy != null) Physics2D.IgnoreCollision(pieceCollider, hitBy);
                pieces.Add(pieceCollider);

                piece.AddComponent<ShatterFragment>().Launch(fragmentLifetime, fadeSeconds);
            }
        }

        if (fragmentsCollideWithEachOther) return;
        for (int i = 0; i < pieces.Count; i++)
            for (int j = i + 1; j < pieces.Count; j++)
                Physics2D.IgnoreCollision(pieces[i], pieces[j]);
    }

    private Sprite[] GetSlices(Sprite sprite, int columns, int rows)
    {
        int expected = columns * rows;
        // Invalidate the cache if fragmentGrid changed at runtime between shatters.
        if (slicesReady && (cachedSlices == null || cachedSlices.Length == expected)) return cachedSlices;
        slicesReady = true;
        if (cachedSlices != null)
        {
            foreach (var stale in cachedSlices)
                if (stale != null) Destroy(stale);
            cachedSlices = null;
        }

        var texture = sprite.texture;
        if (texture == null || !texture.isReadable) return null;

        Rect source = sprite.textureRect;
        var slices = new Sprite[columns * rows];
        float sliceWidth = source.width / columns;
        float sliceHeight = source.height / rows;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var rect = new Rect(source.x + x * sliceWidth, source.y + y * sliceHeight, sliceWidth, sliceHeight);
                slices[y * columns + x] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit);
            }
        }
        cachedSlices = slices;
        return cachedSlices;
    }

    private void OnDestroy()
    {
        if (cachedSlices == null) return;
        foreach (var slice in cachedSlices)
            if (slice != null) Destroy(slice);
    }
}
