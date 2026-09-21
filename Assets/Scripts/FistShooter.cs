using UnityEngine;

/// <summary>Debounces fist samples and fires once until a stable open hand rearms it.</summary>
public class FistShooter : MonoBehaviour
{
    [SerializeField] private HandTrackingManager handTracking;
    [SerializeField] private PlayerEnergy energy;
    [Header("Gesture")]
    [SerializeField] private float closedThreshold = 0.95f;
    [SerializeField] private float openThreshold = 1.15f;
    [SerializeField] private float confirmationSeconds = 0.12f;
    [SerializeField] private float trackingTimeout = 0.3f;
    [SerializeField] private float shotCooldown = 0.35f;
    [Header("Middle Finger Aim")]
    [SerializeField] private float aimSmoothingSpeed = 15f;
    [SerializeField] private float aimMemorySeconds = 1f;
    [Header("Bullet")]
    [SerializeField] private float bulletEnergyCost = 10f;
    [SerializeField] private float bulletSpeed = 12f;
    [SerializeField] private float bulletLifetime = 3f;
    [SerializeField] private Vector2 bulletSize = new Vector2(0.16f, 0.4f);

    private bool armed = true;
    private float candidateSince = -1f;
    private float lastSampleTime = float.NegativeInfinity;
    private float nextShotTime;
    private Sprite bulletSprite;
    private Vector2 aimDirection;
    private float lastAimTime = float.NegativeInfinity;

    private void OnEnable()
    {
        candidateSince = -1f;
        lastAimTime = float.NegativeInfinity;
        if (handTracking != null)
        {
            handTracking.FistSample += OnFistSample;
            handTracking.AimSample += OnAimSample;
        }
    }

    private void OnDisable()
    {
        if (handTracking != null)
        {
            handTracking.FistSample -= OnFistSample;
            handTracking.AimSample -= OnAimSample;
        }
        candidateSince = -1f;
    }

    private void OnAimSample(Vector2? direction)
    {
        if (!direction.HasValue) return; // Keep the pre-fist direction while fingers curl.
        var value = direction.Value;
        if (float.IsNaN(value.x) || float.IsNaN(value.y) ||
            float.IsInfinity(value.x) || float.IsInfinity(value.y) || value.sqrMagnitude < 0.0001f) return;
        float now = Time.unscaledTime;
        if (now - lastAimTime > trackingTimeout)
            aimDirection = value.normalized;
        else
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(value.y, value.x) * Mathf.Rad2Deg;
            angle = Mathf.LerpAngle(angle, target, 1f - Mathf.Exp(-aimSmoothingSpeed * (now - lastAimTime)));
            aimDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }
        lastAimTime = now;
    }

    private void OnFistSample(float? score)
    {
        float now = Time.unscaledTime;
        if (!score.HasValue || now - lastSampleTime > trackingTimeout)
            candidateSince = -1f;
        lastSampleTime = now;
        if (!score.HasValue) lastAimTime = float.NegativeInfinity;
        if (!score.HasValue || float.IsNaN(score.Value) || float.IsInfinity(score.Value)) return;

        bool candidate = armed ? score.Value < closedThreshold : score.Value > openThreshold;
        if (!candidate)
        {
            candidateSince = -1f;
            return;
        }
        if (candidateSince < 0f) candidateSince = now;
        if (now - candidateSince < confirmationSeconds) return;

        if (!armed)
        {
            armed = true;
            candidateSince = -1f;
        }
        else if (Time.time >= nextShotTime && Time.timeScale > 0f &&
            now - lastAimTime <= aimMemorySeconds &&
            (energy == null || energy.TrySpend(bulletEnergyCost)))
        {
            Fire();
            armed = false;
            candidateSince = -1f;
            nextShotTime = Time.time + shotCooldown;
        }
    }

    private void Fire()
    {
        if (bulletSprite == null)
            bulletSprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);

        var playerCollider = GetComponent<Collider2D>();
        Vector2 direction = aimDirection.normalized;
        Vector3 origin = transform.position + (Vector3)direction;
        if (playerCollider != null)
        {
            var bounds = playerCollider.bounds;
            // Support distance of the player's bounds along the shot direction.
            float clearance = Mathf.Abs(direction.x) * bounds.extents.x +
                Mathf.Abs(direction.y) * bounds.extents.y + bulletSize.y * 0.5f + 0.1f;
            origin = new Vector3(bounds.center.x, bounds.center.y, transform.position.z) +
                (Vector3)(direction * clearance);
        }

        var bullet = new GameObject("FistBullet");
        bullet.transform.position = origin;
        bullet.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        bullet.transform.localScale = new Vector3(bulletSize.x, bulletSize.y, 1f);
        var visual = bullet.AddComponent<SpriteRenderer>();
        visual.sprite = bulletSprite;
        visual.color = new Color(1f, 0.85f, 0.15f);
        var playerVisual = GetComponent<SpriteRenderer>();
        if (playerVisual != null)
        {
            visual.sortingLayerID = playerVisual.sortingLayerID;
            visual.sortingOrder = playerVisual.sortingOrder + 1;
        }
        var body = bullet.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.linearVelocity = direction * bulletSpeed;
        var hitbox = bullet.AddComponent<BoxCollider2D>();
        foreach (var ownCollider in GetComponentsInChildren<Collider2D>())
            Physics2D.IgnoreCollision(hitbox, ownCollider);
        bullet.AddComponent<UpwardBullet>();
        Destroy(bullet, bulletLifetime);
    }

    private void OnDestroy()
    {
        if (bulletSprite != null) Destroy(bulletSprite);
    }
}
