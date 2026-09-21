using UnityEngine;

/// <summary>
/// A falling enemy: drifts down (straight or diagonal) until it lands on the ground or bumps into a
/// wall, then loops a small "repeated attack" animation in place (a hop on the ground, a bump against
/// a wall) while chipping away at the player's health. Touching the player directly also hurts them.
/// Getting hit by a player projectile shatters it (reusing <see cref="ShatterOnHit"/>) and hands the
/// GameObject back to whoever spawned it instead of destroying it, so <see cref="EnemySpawnManager"/>
/// can pool it.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(ShatterOnHit))]
public class EnemyUnit : MonoBehaviour
{
    private enum AttackSurface { None, Ground, Wall, Crate }

    [Header("Tags")]
    [SerializeField] private string groundTag = "Ground";
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private string playerTag = "Player";

    [Header("Damage")]
    [SerializeField] private float attackDamage = 6f;
    [SerializeField] private float contactDamage = 10f;
    [Tooltip("Minimum seconds between two damage ticks from this enemy, whether from the attack loop or direct contact.")]
    [SerializeField] private float damageInterval = 0.5f;

    [Header("Ground hop animation")]
    [SerializeField] private float hopHeight = 0.45f;
    [SerializeField] private float hopDuration = 0.22f;

    [Header("Wall bump animation")]
    [SerializeField] private float bumpDistance = 0.4f;
    [SerializeField] private float bumpDuration = 0.18f;

    [Header("Crate attack")]
    [Tooltip("How many bumps it takes to break a breakable crate it runs into.")]
    [SerializeField] private int crateHitsToBreak = 5;

    [Header("Fall animation")]
    [SerializeField] private float fallWobbleDegrees = 12f;
    [SerializeField] private float fallWobbleSeconds = 0.6f;

    private Rigidbody2D body;
    private ShatterOnHit shatter;
    private PlayerHealth playerHealth;
    private System.Action<EnemyUnit> releaseToPool;

    private AttackSurface surface = AttackSurface.None;
    private float lastDamageTime = float.NegativeInfinity;
    private float nextAttackTickTime;
    private Vector2 launchVelocity;
    private ShatterOnHit targetCrate;
    private int crateHitsRemaining;

    // Drives the repeated hop/bump motion by hand (plain Time-based math, recomputed every Update)
    // instead of leaving it to a tween library's own lifecycle, so it can never silently stop moving
    // regardless of how this pooled instance was reused.
    private float attackStartTime;
    private Vector2 attackRestPosition;
    private Vector2 attackAxis;
    private float attackAmplitude;
    private float attackCycleDuration;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        shatter = GetComponent<ShatterOnHit>();
    }

    private void OnEnable()
    {
        shatter.Shattered += OnShattered;
    }

    private void OnDisable()
    {
        shatter.Shattered -= OnShattered;
        LeanTween.cancel(gameObject);
    }

    /// <summary>Called by the spawner right after this instance is pulled from the pool.</summary>
    public void Launch(Vector2 position, Vector2 velocity, PlayerHealth playerHealth, System.Action<EnemyUnit> releaseToPool)
    {
        this.playerHealth = playerHealth;
        this.releaseToPool = releaseToPool;

        LeanTween.cancel(gameObject);
        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        shatter.Restore();

        surface = AttackSurface.None;
        targetCrate = null;
        lastDamageTime = float.NegativeInfinity;
        launchVelocity = velocity;

        body.simulated = true;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = velocity;
        body.angularVelocity = 0f;

        StartFallWobble(velocity);
    }

    /// <summary>A gentle rocking wobble while it falls so the drop doesn't look static.</summary>
    private void StartFallWobble(Vector2 velocity)
    {
        float sign = velocity.x >= 0f ? 1f : -1f;
        LeanTween.rotateZ(gameObject, sign * fallWobbleDegrees, fallWobbleSeconds)
            .setEase(LeanTweenType.easeInOutSine)
            .setLoopPingPong();
    }

    /// <summary>Drops back into free fall (e.g. after chewing through a crate) using the last launch velocity.</summary>
    private void ResumeFalling()
    {
        surface = AttackSurface.None;
        targetCrate = null;

        LeanTween.cancel(gameObject);
        transform.rotation = Quaternion.identity;

        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = launchVelocity;
        body.angularVelocity = 0f;

        StartFallWobble(launchVelocity);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var other = collision.collider;
        if (other.CompareTag(playerTag))
        {
            TryDamagePlayer(contactDamage);
            return;
        }

        if (surface != AttackSurface.None) return;

        if (other.CompareTag(groundTag)) { BeginAttacking(AttackSurface.Ground); return; }
        if (other.CompareTag(wallTag)) { BeginAttacking(AttackSurface.Wall); return; }

        var crate = other.GetComponentInParent<ShatterOnHit>();
        if (crate != null && crate != shatter) BeginAttackingCrate(crate);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider.CompareTag(playerTag)) TryDamagePlayer(contactDamage);
    }

    private void BeginAttacking(AttackSurface onSurface)
    {
        Vector2 axis = onSurface == AttackSurface.Ground ? Vector2.up : Vector2.right * (body.position.x < 0f ? -1f : 1f);
        float amplitude = onSurface == AttackSurface.Ground ? hopHeight : bumpDistance;
        float duration = onSurface == AttackSurface.Ground ? hopDuration : bumpDuration;
        StartAttackMotion(onSurface, axis, amplitude, duration);
    }

    private void BeginAttackingCrate(ShatterOnHit crate)
    {
        targetCrate = crate;
        crateHitsRemaining = crateHitsToBreak;

        Vector2 toward = (Vector2)crate.transform.position - body.position;
        Vector2 axis = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector2.right;
        StartAttackMotion(AttackSurface.Crate, axis, bumpDistance, bumpDuration);
    }

    private void StartAttackMotion(AttackSurface onSurface, Vector2 axis, float amplitude, float duration)
    {
        surface = onSurface;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Kinematic;

        LeanTween.cancel(gameObject);
        transform.rotation = Quaternion.identity;

        attackStartTime = Time.time;
        attackRestPosition = body.position;
        attackAxis = axis;
        attackAmplitude = amplitude;
        attackCycleDuration = Mathf.Max(0.01f, duration);

        nextAttackTickTime = Time.time + damageInterval;
    }

    private void Update()
    {
        if (surface == AttackSurface.None) return;

        // Driven by hand every frame (not a tween library callback) so the repeated hop/bump motion
        // can never get stuck partway regardless of how this pooled instance was reused before.
        float elapsed = Time.time - attackStartTime;
        float phase = Mathf.PingPong(elapsed / attackCycleDuration, 1f);
        float eased = phase * phase * (3f - 2f * phase); // smoothstep
        body.MovePosition(attackRestPosition + attackAxis * (attackAmplitude * eased));

        if (Time.time < nextAttackTickTime) return;
        nextAttackTickTime = Time.time + damageInterval;

        if (surface == AttackSurface.Crate) TickCrateAttack();
        else TryDamagePlayer(attackDamage);
    }

    private void TickCrateAttack()
    {
        if (targetCrate == null)
        {
            // Someone else broke it (or it despawned) while we were still bumping it.
            ResumeFalling();
            return;
        }

        crateHitsRemaining--;
        if (crateHitsRemaining > 0) return;

        var crate = targetCrate;
        targetCrate = null;
        crate.Shatter(transform.position);
        ResumeFalling();
    }

    private void TryDamagePlayer(float amount)
    {
        if (playerHealth == null) return;
        if (Time.time - lastDamageTime < damageInterval) return;

        lastDamageTime = Time.time;
        playerHealth.TakeDamage(amount);
    }

    private void OnShattered(Vector2 impact)
    {
        LeanTween.cancel(gameObject);
        body.simulated = false;
        surface = AttackSurface.None;
        releaseToPool?.Invoke(this);
    }
}
