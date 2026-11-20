using NumberRush3D.Core;
using NumberRush3D.Enemies;
using NumberRush3D.Gates;
using NumberRush3D.Levels;
using UnityEditor;
using UnityEngine;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// Level content generation: the catalog asset plus 100 data driven
    /// <see cref="LevelDefinition"/> assets.
    ///
    /// The generator writes defaults on creation only, so a designer who tunes a level keeps those
    /// values when the generator runs again (exactly like the EnemyType assets).
    /// </summary>
    public static partial class NumberRushBuilder
    {
        public const string LevelFolder = SettingsFolder + "/Levels";
        public const string LevelCatalogPath = SettingsFolder + "/LevelCatalog.asset";
        public const int GeneratedLevelCount = 100;

        /// <summary>Loads or creates the level catalog and guarantees it contains the 100 levels.</summary>
        public static LevelCatalog EnsureLevelCatalog(int levelCount = GeneratedLevelCount)
        {
            EnsureFolder(LevelFolder);

            LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, LevelCatalogPath);
            }

            EnemyType grunt = LoadEnemyType("EnemyType_Grunt");
            EnemyType runner = LoadEnemyType("EnemyType_Runner");
            EnemyType brute = LoadEnemyType("EnemyType_Brute");
            EnemyType armored = LoadEnemyType("EnemyType_Armored");
            EnemyType boss = LoadEnemyType("EnemyType_Boss");

            LevelDefinition[] levels = new LevelDefinition[Mathf.Max(1, levelCount)];
            int created = 0;

            for (int i = 0; i < levels.Length; i++)
            {
                int number = i + 1;
                string path = LevelAssetPath(number);

                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    level.name = $"Level_{number:000}";

                    ApplyLevelPreset(level, number, grunt, runner, brute, armored, boss);
                    AssetDatabase.CreateAsset(level, path);
                    created++;
                }

                levels[i] = level;
            }

            catalog.levels = levels;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[NumberRush] Level catalog ready: {levels.Length} levels at {LevelCatalogPath} ({created} created).");
            return catalog;
        }

        private static string LevelAssetPath(int levelNumber)
        {
            return $"{LevelFolder}/Level_{levelNumber:000}.asset";
        }

        private static EnemyType LoadEnemyType(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<EnemyType>($"{SettingsFolder}/{fileName}.asset");
        }

        /// <summary>
        /// Starting point for a generated level: long track, slowly rising enemy scaling, a gate
        /// pattern cycle and a boss every fifth level from level 5. All of it is editable afterwards.
        /// </summary>
        private static void ApplyLevelPreset(
            LevelDefinition level,
            int number,
            EnemyType grunt,
            EnemyType runner,
            EnemyType brute,
            EnemyType armored,
            EnemyType boss)
        {
            bool bossLevel = number >= 5 && number % 5 == 0 && boss != null;

            level.levelNumber = number;
            level.title = bossLevel ? $"LEVEL {number}  ·  BOSS" : $"LEVEL {number}";

            // Distance to run. Chapter 1 is short, later levels are noticeably longer.
            level.trackLength = 220f + 8f * (number - 1);
            level.coinReward = 20 + 2 * (number - 1);

            level.startingSoldiers = Mathf.Clamp(8 + number / 6, 8, 24);
            level.runSpeed = 0f; // 0 = use GameSettings.runnerStartSpeed

            level.maxConcurrentEnemies = Mathf.Clamp(20 + number / 5, 20, 44);
            level.enemyHealthMultiplier = Mathf.Min(3.2f, 1f + 0.02f * (number - 1));
            level.enemySpeedMultiplier = Mathf.Min(1.7f, 1f + 0.007f * (number - 1));
            level.spawnIntervalScale = Mathf.Max(0.45f, 1f - 0.0055f * (number - 1));

            level.threeStarArmyFraction = 0.8f;
            level.twoStarArmyFraction = 0.4f;

            level.enemyTypes = UnlockedEnemyTypes(number, grunt, runner, brute, armored);
            level.gateRows = BuildGateRows(number);

            level.bossType = bossLevel ? boss : null;
            level.bossProgress = 0.75f;

            level.Sanitize();
        }

        /// <summary>Level 1 starts with grunts only; the rest of the roster unlocks in steps.</summary>
        private static EnemyType[] UnlockedEnemyTypes(int number, EnemyType grunt, EnemyType runner, EnemyType brute, EnemyType armored)
        {
            if (number < 4)
            {
                return new[] { grunt };
            }

            if (number < 8)
            {
                return new[] { grunt, runner };
            }

            if (number < 12)
            {
                return new[] { grunt, runner, brute };
            }

            return new[] { grunt, runner, brute, armored };
        }

        /// <summary>
        /// Three authored gate patterns cycled across the campaign. Each row offers one positive and
        /// one negative gate, with the positive side alternating so muscle memory never wins.
        /// </summary>
        private static GateRowDefinition[] BuildGateRows(int number)
        {
            GateChoice add5 = new GateChoice(GateKind.Add, 5);
            GateChoice add10 = new GateChoice(GateKind.Add, 10);
            GateChoice add20 = new GateChoice(GateKind.Add, 20);
            GateChoice mul2 = new GateChoice(GateKind.Multiply, 2);
            GateChoice mul3 = new GateChoice(GateKind.Multiply, 3);
            GateChoice sub5 = new GateChoice(GateKind.Subtract, 5);
            GateChoice sub10 = new GateChoice(GateKind.Subtract, 10);
            GateChoice div2 = new GateChoice(GateKind.Divide, 2);

            switch (number % 3)
            {
                case 0:
                    return new[]
                    {
                        Row(add10, div2, true),
                        Row(mul2, sub10, false),
                        Row(add20, sub5, true),
                    };
                case 1:
                    return new[]
                    {
                        Row(add5, sub5, true),
                        Row(mul2, div2, false),
                        Row(add10, sub10, true),
                        Row(mul3, div2, false),
                    };
                default:
                    return new[]
                    {
                        Row(mul3, sub10, false),
                        Row(add5, div2, true),
                        Row(add10, sub5, false),
                    };
            }
        }

        private static GateRowDefinition Row(GateChoice positive, GateChoice negative, bool positiveOnLeft)
        {
            return positiveOnLeft
                ? new GateRowDefinition(positive, negative)
                : new GateRowDefinition(negative, positive);
        }
    }
}
