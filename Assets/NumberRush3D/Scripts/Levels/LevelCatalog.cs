using UnityEngine;

namespace NumberRush3D.Levels
{
    /// <summary>
    /// Ordered list of every campaign level. The setup menu fills this with 100 generated levels;
    /// add, remove or reorder them like any other array and the game picks the change up.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "NUMBER RUSH 3D/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Tooltip("Levels in play order. Level 1 is the first entry.")]
        public LevelDefinition[] levels;

        /// <summary>Number of authored levels.</summary>
        public int Count => levels != null ? levels.Length : 0;

        /// <summary>Level with the requested 1-based number, or null.</summary>
        public LevelDefinition Find(int levelNumber)
        {
            if (levelNumber <= 0 || levels == null)
            {
                return null;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                LevelDefinition level = levels[i];
                if (level != null && level.levelNumber == levelNumber)
                {
                    return level;
                }
            }

            return null;
        }

        /// <summary>Level that follows <paramref name="levelNumber"/> in catalog order, or null.</summary>
        public LevelDefinition Next(int levelNumber)
        {
            LevelDefinition current = Find(levelNumber);
            if (current == null)
            {
                return null;
            }

            int index = System.Array.IndexOf(levels, current);
            return index >= 0 && index + 1 < levels.Length ? levels[index + 1] : null;
        }
    }
}
