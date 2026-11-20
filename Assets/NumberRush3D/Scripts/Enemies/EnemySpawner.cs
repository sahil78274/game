using NumberRush3D.Army;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// Spawns enemies on the track AHEAD of the running army. Difficulty is a single 0..1 value
    /// derived from elapsed run time (or from level progress in campaign mode) and it drives the
    /// spawn interval, the burst size and which enemy types are unlocked. Everything spawns
    /// through a pool so a long run never allocates.
    ///
    /// Runner upgrade: the spawn point follows `armyZ + enemySpawnAhead` instead of a fixed world
    /// Z, and a scheduled boss is released once the level progress passes its trigger.
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

        [Header("Boss")]
        [Tooltip("Extra concurrency granted while a boss fight is running.")]
        [Range(0, 20)] [SerializeField] private int bossExtraConcurrency = 4;

        private ObjectPool<Enemy> _pool;
        private GameSettings _settings;
        private IBattleContext _context;
        private ArmyController _army;
        private float _spawnTimer;
        private float _elapsed;
        private float _despawnTimer;
        private int _lastLane = -1;
        private EnemyType[] _levelTypes;
        private EnemyType _pendingBoss;
        private float _pendingBossProgress = 1f;
        private bool _bossSpawned;

        /// <summary>Total enemies spawned this run.</summary>
        public int SpawnedCount { get; private set; }

        /// <summary>Gates further spawning (used by the results screens so no wave arrives during them).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Current difficulty 0..1.</summary>
        public float Difficulty => _settings != null ? _settings.Difficulty01(_elapsed) : 0f;

        /// <summary>True while a scheduled boss exists (spawned or waiting for its trigger).</summary>
        public bool BossPending => _pendingBoss != null;

        public void Initialize(GameSettings settings, IBattleContext context, ArmyController army)
        {
            _settings = settings;
            _context = context;
            _army = army;

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

        /// <summary>
        /// Restricts spawning to a level specific subset. Pass null to fall back to the inspector array.
        /// Boss assets are always excluded from the random pool, they are scheduled explicitly.
        /// </summary>
        public void SetEnabledTypes(EnemyType[] types)
        {
            _levelTypes = types != null && types.Length > 0 ? types : null;
        }

        /// <summary>Called by <see cref="GameManager"/> when a run starts.</summary>
        public void Begin()
        {
            _elapsed = 0f;
            _spawnTimer = 0.9f;
            _despawnTimer = 0f;
            _lastLane = -1;
            SpawnedCount = 0;
            _pendingBoss = null;
            _pendingBossProgress = 1f;
            _bossSpawned = false;
            Enabled = true;
        }

        /// <summary>
        /// Queues a boss encounter. The boss is released as soon as the level progress reaches
        /// <paramref name="atProgress01"/> (0 = immediately). Call this from the level setup.
        /// </summary>
        public void ScheduleBoss(EnemyType bossType, float atProgress01)
        {
            if (bossType == null)
            {
                return;
            }

            _pendingBoss = bossType;
            _pendingBossProgress = Mathf.Clamp01(atProgress01);
            _bossSpawned = false;
        }

        /// <summary>Force-spawns <paramref name="bossType"/> right now, ignoring the scheduled trigger.</summary>
        public Enemy SpawnBoss(EnemyType bossType)
        {
            if (bossType == null || _pool == null)
            {
                return null;
            }

            _bossSpawned = true;
            int lane = Mathf.Max(1, _settings.laneCount) / 2;
            return SpawnAt(bossType, lane, 0f);
        }

        private void Update()
        {
            if (_context == null || _pool == null || _settings == null || !_context.IsPlaying || !Enabled)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _elapsed += deltaTime;

            TrySpawnScheduledBoss();
            SweepStragglers(deltaTime);

            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            float interval = _settings.SpawnIntervalAt(_elapsed) * Mathf.Max(0.1f, _context.SpawnIntervalScale);
            _spawnTimer = Mathf.Max(0.05f, interval);
            SpawnWave();
        }

        private void TrySpawnScheduledBoss()
        {
            if (_pendingBoss == null || _bossSpawned)
            {
                return;
            }

            if (_context.LevelProgress01 < _pendingBossProgress)
            {
                return;
            }

            SpawnBoss(_pendingBoss);
        }

        private void SpawnWave()
        {
            int burst = _settings.SpawnBurstAt(_elapsed);
            int baseCap = _context.MaxConcurrentEnemies > 0 ? _context.MaxConcurrentEnemies : _settings.maxConcurrentEnemies;
            int maxConcurrent = baseCap + (_bossSpawned ? bossExtraConcurrency : 0);

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

            Vector3 position = new Vector3(
                _settings.LaneX(PickLane()) + Random.Range(-_settings.spawnJitterX, _settings.spawnJitterX),
                0f,
                SpawnZ() + Random.Range(-_settings.spawnJitterZ, _settings.spawnJitterZ));

            Enemy enemy = _pool.Get(position, Quaternion.identity);
            if (enemy == null)
            {
                return;
            }

            enemy.Spawn(type, _context, this, position);
            SpawnedCount++;
        }

        private Enemy SpawnAt(EnemyType type, int lane, float extraAhead)
        {
            Vector3 position = new Vector3(
                _settings.LaneX(lane),
                0f,
                SpawnZ() + extraAhead + Mathf.Max(0f, type.formationDepth));

            Enemy enemy = _pool.Get(position, Quaternion.identity);
            if (enemy == null)
            {
                return null;
            }

            enemy.Spawn(type, _context, this, position);
            SpawnedCount++;
            return enemy;
        }

        /// <summary>World Z where new enemies appear: always a fixed distance ahead of the army.</summary>
        private float SpawnZ()
        {
            float armyZ = _army != null ? _army.WorldZ : _settings.armyZ;
            return armyZ + _settings.enemySpawnAhead;
        }

        /// <summary>Recycles enemies the army has run past (defensive: never leave stragglers alive).</summary>
        private void SweepStragglers(float deltaTime)
        {
            _despawnTimer -= deltaTime;
            if (_despawnTimer > 0f)
            {
                return;
            }

            _despawnTimer = 0.5f;

            float armyZ = _army != null ? _army.WorldZ : _settings.armyZ;
            float cutoff = armyZ - _settings.enemyDespawnBehind;

            var all = _context.Enemies.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Enemy enemy = all[i];
                if (enemy == null)
                {
                    continue;
                }

                if (enemy.transform.position.z < cutoff)
                {
                    // Recycle silently: it got behind the army, so it neither scores nor rams.
                    enemy.Despawn();
                }
            }
        }

        /// <summary>Weighted random pick among the types unlocked at the current difficulty.</summary>
        private EnemyType PickType()
        {
            EnemyType[] pool = _levelTypes != null ? _levelTypes : enemyTypes;
            if (pool == null || pool.Length == 0)
            {
                return null;
            }


            float difficulty = _settings.Difficulty01(_elapsed);
            float totalWeight = 0f;

            for (int i = 0; i < pool.Length; i++)
            {
                EnemyType type = pool[i];
                if (type == null || type.isBoss || difficulty < type.minDifficulty)
                {
                    continue;
                }

                totalWeight += Mathf.Max(0.01f, type.spawnWeight);
            }

            if (totalWeight <= 0f)
            {
                // Nothing unlocked yet (or every asset is null) - fall back to the first valid type.
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i] != null && !pool[i].isBoss)
                    {
                        return pool[i];
                    }
                }

                return null;
            }

            float roll = Random.value * totalWeight;
            for (int i = 0; i < pool.Length; i++)
            {
                EnemyType type = pool[i];
                if (type == null || type.isBoss || difficulty < type.minDifficulty)
                {
                    continue;
                }

                roll -= Mathf.Max(0.01f, type.spawnWeight);
                if (roll <= 0f)
                {
                    return type;
                }
            }

            return pool[0];
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
