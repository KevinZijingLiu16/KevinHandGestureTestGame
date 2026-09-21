using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pools and spawns falling enemies for a level: how many total, how many fall straight down vs at an
/// angle, and how fast each one falls (independently per spawn, so some drop fast and some drift slow).
/// Spawns are spread out over time from random points just above the camera's top edge.
/// </summary>
public class EnemySpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class LevelConfig
    {
        public string name = "Level";
        [Min(0)] public int verticalCount = 5;
        [Min(0)] public int diagonalCount = 5;
        [Min(0.05f)] public float spawnInterval = 0.8f;
        public float minFallSpeed = 3f;
        public float maxFallSpeed = 7f;
        [Tooltip("How far from straight down a diagonal drop can lean, in degrees.")]
        [Range(1f, 80f)] public float diagonalAngleMin = 15f;
        [Range(1f, 80f)] public float diagonalAngleMax = 45f;

        public int TotalCount => verticalCount + diagonalCount;
    }

    [Header("References")]
    [SerializeField] private EnemyUnit enemyPrefab;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Camera targetCamera;

    [Header("Spawn Area")]
    [SerializeField] private float spawnHeightAboveCamera = 1f;
    [Tooltip("Shrinks the spawn width in from each screen edge, in viewport units (0-0.5).")]
    [SerializeField, Range(0f, 0.45f)] private float horizontalEdgePadding = 0.05f;

    [Header("Levels")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();
    [Tooltip("Index into Levels to kick off automatically on Start. -1 disables auto-start.")]
    [SerializeField] private int autoStartLevelIndex = -1;

    private ObjectPool<EnemyUnit> pool;
    private Coroutine spawnRoutine;
    private readonly List<Collider2D> allColliders = new List<Collider2D>();

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        pool = new ObjectPool<EnemyUnit>(CreateEnemy, OnGetEnemy, OnReleaseEnemy, OnDestroyEnemy, collectionCheck: false);
    }

    private void Start()
    {
        if (autoStartLevelIndex >= 0) StartLevel(autoStartLevelIndex);
    }

    private EnemyUnit CreateEnemy()
    {
        var instance = Instantiate(enemyPrefab, transform);

        // Falling enemies shouldn't collide with each other (they'd jam into a mid-air pile-up and
        // fall asleep before ever reaching the ground/wall), so cross-ignore every pooled instance.
        var col = instance.GetComponent<Collider2D>();
        if (col != null)
        {
            foreach (var other in allColliders) Physics2D.IgnoreCollision(col, other);
            allColliders.Add(col);
        }

        return instance;
    }

    private void OnGetEnemy(EnemyUnit enemy) => enemy.gameObject.SetActive(true);
    private void OnReleaseEnemy(EnemyUnit enemy) => enemy.gameObject.SetActive(false);
    private void OnDestroyEnemy(EnemyUnit enemy) { if (enemy != null) Destroy(enemy.gameObject); }

    /// <summary>Starts spawning the given level (index into <see cref="levels"/>). Cancels any level already running.</summary>
    public void StartLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count) return;
        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        spawnRoutine = StartCoroutine(RunLevel(levels[levelIndex]));
    }

    public void StopSpawning()
    {
        if (spawnRoutine == null) return;
        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private IEnumerator RunLevel(LevelConfig config)
    {
        var queue = new List<bool>(config.TotalCount); // true = diagonal drop
        for (int i = 0; i < config.verticalCount; i++) queue.Add(false);
        for (int i = 0; i < config.diagonalCount; i++) queue.Add(true);
        Shuffle(queue);

        var wait = new WaitForSeconds(config.spawnInterval);
        foreach (var diagonal in queue)
        {
            SpawnOne(config, diagonal);
            yield return wait;
        }
        spawnRoutine = null;
    }

    private static void Shuffle(List<bool> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void SpawnOne(LevelConfig config, bool diagonal)
    {
        if (enemyPrefab == null || targetCamera == null) return;

        float depth = targetCamera.nearClipPlane + 10f;
        Vector3 topLeft = targetCamera.ViewportToWorldPoint(new Vector3(horizontalEdgePadding, 1f, depth));
        Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f - horizontalEdgePadding, 1f, depth));
        float x = Random.Range(topLeft.x, topRight.x);
        float y = topLeft.y + spawnHeightAboveCamera;

        float speed = Random.Range(config.minFallSpeed, config.maxFallSpeed);
        Vector2 velocity;
        if (diagonal)
        {
            float angle = Random.Range(config.diagonalAngleMin, config.diagonalAngleMax);
            if (Random.value < 0.5f) angle = -angle;
            float rad = angle * Mathf.Deg2Rad;
            velocity = new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * speed;
        }
        else
        {
            velocity = new Vector2(0f, -speed);
        }

        var enemy = pool.Get();
        enemy.Launch(new Vector2(x, y), velocity, playerHealth, ReleaseEnemy);
    }

    private void ReleaseEnemy(EnemyUnit enemy) => pool.Release(enemy);
}
