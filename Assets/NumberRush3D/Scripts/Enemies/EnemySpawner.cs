using NumberRush3D.Army;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// Spawns waves from the far end of the track. Difficulty is a single 0..1 value derived from
    /// elapsed run time and it drives the spawn interval, the burst size and which enemy types are
    /// unlocked. Everything spawns through a pool so a long run never allocates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Enemy prefab. Must contain Enemy + Health and a collider on the Enemy layer.")]
        [SerializeField] private Enemy enemyPrefab;

        [Tooltip("All enemy kinds that may spawn. Weights and difficulty gates live on the assets.")]
        [SerializeField] private EnemyType[] enemyTypes;

        [Tooltip("Parent for pooled enemies. Defaults to this transform.")]
        [SerializeField] private Transform poolRoot;

        [Header("Pooling")]
        [Range(0, 80)] [SerializeField] private int prewarm = 12;

        private ObjectPool<Enemy> _pool;
        private GameSettings _settings;
        private IBattleContext _context;
        private float _spawnTimer;
        private float _elapsed;
        private int _lastLane = -1;

        /// <summary>Total enemies spawned this run.</summary>
        public int SpawnedCount { get; private set; }

        /// <summary>Current difficulty 0..1.</summary>
        public float Difficulty => _settings != null ? _settings.Difficulty01(_elapsed) : 0f;

        public void Initialize(GameSettings settings, IBattleContext context, ArmyController army)
        {
            _settings = settings;
            _context = context;

            if (enemyPrefab == null)
            {
                Debug.LogError("[NumberRush] EnemySpawner has no enemy prefab assigned - nothing will spawn.", this);
                return;
            }

            if (poolRoot == null)
            {
                poolRoot = transform;
            }

            _pool = new ObjectPool<Enemy>(
                () => Instantiate(enemyPrefab, poolRoot),
                poolRoot,
                prewarm,
                Mathf.Max(8, settings.maxConcurrentEnemies + 8));
        }

        /// <summary>Called by <see cref="GameManager"/> when the run starts.</summary>
        public void Begin()
        {
            _elapsed = 0f;
            _spawnTimer = 0.9f;
            _lastLane = -1;
            SpawnedCount = 0;
        }

        private void Update()
        {
            if (_context == null || _pool == null || _settings == null || !_context.IsPlaying)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            _spawnTimer -= Time.deltaTime;

            if (_spawnTimer > 0f)
            {
                return;
            }

            _spawnTimer = _settings.SpawnIntervalAt(_elapsed);
            SpawnWave();
        }

        private void SpawnWave()
        {
            int burst = _settings.SpawnBurstAt(_elapsed);
            int maxConcurrent = _settings.maxConcurrentEnemies;

            for (int i = 0; i < burst; i++)
            {
                if (_context.Enemies.Count >= maxConcurrent)
                {
                    return;
                }

                SpawnOne();
            }
        }

        private void SpawnOne()
        {
            EnemyType type = PickType();
            if (type == null)
            {
                return;
            }

            int lane = PickLane();
            Vector3 position = new Vector3(
                _settings.LaneX(lane) + Random.Range(-_settings.spawnJitterX, _settings.spawnJitterX),
                0f,
                _settings.spawnZ + Random.Range(-_settings.spawnJitterZ, _settings.spawnJitterZ));

            Enemy enemy = _pool.Get(position, Quaternion.identity);
            if (enemy == null)
            {
                return;
            }

            enemy.Spawn(type, _context, this, position);
            SpawnedCount++;
        }

        /// <summary>Weighted random pick among the types unlocked at the current difficulty.</summary>
        private EnemyType PickType()
        {
            if (enemyTypes == null || enemyTypes.Length == 0)
            {
                return null;
            }

            float difficulty = _settings.Difficulty01(_elapsed);
            float totalWeight = 0f;

            for (int i = 0; i < enemyTypes.Length; i++)
            {
                EnemyType type = enemyTypes[i];
                if (type == null || difficulty < type.minDifficulty)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0.01f, type.spawnWeight);
            }

            if (totalWeight <= 0f)
            {
                // Nothing unlocked yet (or every asset is null) - fall back to the first valid type.
                for (int i = 0; i < enemyTypes.Length; i++)
                {
                    if (enemyTypes[i] != null)
                    {
                        return enemyTypes[i];
                    }
                }

                return null;
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                EnemyType type = enemyTypes[i];
                if (type == null || difficulty < type.minDifficulty)
                {
                    continue;
                }

                roll -= Mathf.Max(0.01f, type.spawnWeight);
                if (roll <= 0f)
                {
                    return type;
                }
            }

            return enemyTypes[0];
        }

        /// <summary>Random lane, avoiding an immediate repeat so waves spread out visually.</summary>
        private int PickLane()
        {
            int laneCount = Mathf.Max(1, _settings.laneCount);
            int lane = Random.Range(0, laneCount);

            if (laneCount > 1 && lane == _lastLane)
            {
                lane = (lane + Random.Range(1, laneCount)) % laneCount;
            }

            _lastLane = lane;
            return lane;
        }

        /// <summary>Called by <see cref="Enemy"/> when it dies or rams the army.</summary>
        public void Release(Enemy enemy)
        {
            _pool?.Release(enemy);
        }
    }
}
