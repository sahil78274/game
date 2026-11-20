using UnityEngine;

namespace NumberRush3D.Levels
{
    /// <summary>
    /// Campaign progress persistence. Three PlayerPrefs values only - unlocked level, the best star
    /// count per level, and nothing else - so saves stay tiny and forward compatible.
    ///
    /// The wallet lives in <see cref="Core.GameManager"/> (key "NumberRush3D.Coins") so coins keep
    /// accumulating across levels and sessions.
    /// </summary>
    public static class LevelProgressStore
    {
        private const string UnlockedKey = "NumberRush3D.UnlockedLevel";
        private const string StarsKeyPrefix = "NumberRush3D.Stars.";

        /// <summary>Highest level the player may enter. Always at least 1.</summary>
        public static int UnlockedLevel => Mathf.Max(1, PlayerPrefs.GetInt(UnlockedKey, 1));

        /// <summary>True when the level is playable.</summary>
        public static bool IsUnlocked(int levelNumber)
        {
            return levelNumber <= UnlockedLevel;
        }

        /// <summary>Best star count (0..3) earned on a level. 0 = never completed.</summary>
        public static int GetStars(int levelNumber)
        {
            if (levelNumber <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(PlayerPrefs.GetInt(StarsKeyPrefix + levelNumber, 0), 0, 3);
        }

        /// <summary>Total stars across the campaign, used by the level select header.</summary>
        public static int TotalStars(int levelCount)
        {
            int total = 0;
            for (int i = 1; i <= levelCount; i++)
            {
                total += GetStars(i);
            }

            return total;
        }

        /// <summary>
        /// Records a completed level: keeps the best star count and unlocks the next level.
        /// </summary>
        public static void CompleteLevel(int levelNumber, int stars)
        {
            if (levelNumber <= 0)
            {
                return;
            }

            stars = Mathf.Clamp(stars, 1, 3);

            if (stars > GetStars(levelNumber))
            {
                PlayerPrefs.SetInt(StarsKeyPrefix + levelNumber, stars);
            }

            if (levelNumber + 1 > UnlockedLevel)
            {
                PlayerPrefs.SetInt(UnlockedKey, levelNumber + 1);
            }

            PlayerPrefs.Save();
        }

        /// <summary>Wipes campaign progress (editor / QA helper, also exposed by the setup menu).</summary>
        public static void ResetAll(int levelCount)
        {
            PlayerPrefs.DeleteKey(UnlockedKey);

            for (int i = 1; i <= Mathf.Max(levelCount, 100); i++)
            {
                PlayerPrefs.DeleteKey(StarsKeyPrefix + i);
            }

            PlayerPrefs.Save();
        }
    }
}
