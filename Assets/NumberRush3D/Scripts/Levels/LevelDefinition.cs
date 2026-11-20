using NumberRush3D.Enemies;
using NumberRush3D.Gates;
using UnityEngine;

namespace NumberRush3D.Levels
{
    /// <summary>
    /// Data driven campaign level. One asset per level, all editable in the inspector - nothing
    /// about a level is hardcoded in the gameplay scripts.
    ///
    /// Everything a level changes is expressed here: track length, starting army, enemy mix and
    /// scaling, gate pattern, boss trigger and the coin / star rewards.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "NUMBER RUSH 3D/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("1-based level number. Unique inside the catalog.")]
        [Min(1)] public int levelNumber = 1;

        [Tooltip("Shown on the level select button. Empty = \"LEVEL n\".")]
        public string title = "";

        [Header("Completion")]
        [Tooltip("Distance the army must survive to complete the level, in metres.")]
        [Min(40f)] public float trackLength = 260f;

        [Tooltip("Coin reward granted when the level is completed.")]
        [Min(0)] public int coinReward = 20;

        [Header("Army")]
        [Tooltip("Soldiers the level starts with.")]
        [Min(1)] public int startingSoldiers = 10;

        [Tooltip("Forward speed override in m/s. 0 = use GameSettings.runnerStartSpeed.")]
        [Min(0f)] public float runSpeed = 0f;

        [Header("Stars")]
        [Tooltip("Finish with at least this fraction of the peak army for 3 stars.")]
        [Range(0.1f, 1f)] public float threeStarArmyFraction = 0.8f;

        [Tooltip("Finish with at least this fraction of the peak army for 2 stars.")]
        [Range(0.05f, 1f)] public float twoStarArmyFraction = 0.4f;

        [Header("Enemy scaling")]
        [Tooltip("Multiplier on every EnemyType.maxHealth.")]
        [Range(0.1f, 6f)] public float enemyHealthMultiplier = 1f;

        [Tooltip("Multiplier on every EnemyType.moveSpeed.")]
        [Range(0.1f, 4f)] public float enemySpeedMultiplier = 1f;

        [Tooltip("Multiplier on the spawn interval. Lower = more enemies.")]
        [Range(0.1f, 4f)] public float spawnIntervalScale = 1f;

        [Tooltip("Maximum simultaneous enemies for this level.")]
        [Range(2, 60)] public int maxConcurrentEnemies = 26;

        [Tooltip("Types this level may spawn. Empty = use the spawner inspector list. Bosses go in bossType.")]
        public EnemyType[] enemyTypes;

        [Header("Gates")]
        [Tooltip("Authored gate rows, cycled in order. Empty = procedural rows from GameSettings.")]
        public GateRowDefinition[] gateRows;

        [Header("Boss")]
        [Tooltip("Boss spawned during this level. Empty = no boss.")]
        public EnemyType bossType;

        [Tooltip("Level progress (0..1) at which the boss appears.")]
        [Range(0.1f, 0.95f)] public float bossProgress = 0.75f;

        /// <summary>True when the level ends with a boss fight.</summary>
        public bool HasBoss => bossType != null;

        /// <summary>Display name used by the UI.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(title) ? $"LEVEL {levelNumber}" : title;

        /// <summary>Clamped star thresholds so a designer cannot author an impossible 3 star.</summary>
        public void Sanitize()
        {
            levelNumber = Mathf.Max(1, levelNumber);
            trackLength = Mathf.Max(40f, trackLength);
            startingSoldiers = Mathf.Max(1, startingSoldiers);
            maxConcurrentEnemies = Mathf.Max(2, maxConcurrentEnemies);
            coinReward = Mathf.Max(0, coinReward);
            bossProgress = Mathf.Clamp(bossProgress, 0.1f, 0.95f);

            if (twoStarArmyFraction > threeStarArmyFraction)
            {
                twoStarArmyFraction = threeStarArmyFraction;
            }
        }

        private void OnValidate()
        {
            Sanitize();
        }
    }
}
