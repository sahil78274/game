using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Army
{
    /// <summary>
    /// Pure layout math for the crowd. No MonoBehaviour, no state - easy to reason about and reuse.
    /// The formation grows outwards and backwards from the army root: column 0 is the left most,
    /// row 0 is the front line (closest to the enemies).
    /// </summary>
    public static class ArmyFormation
    {
        /// <summary>Columns / rows used for a given soldier count. Grows towards a square-ish block.</summary>
        public static void ResolveGrid(int count, GameSettings settings, out int columns, out int rows)
        {
            int safeCount = Mathf.Max(1, count);
            int maxColumns = Mathf.Max(1, settings.maxFormationColumns);

            // sqrt keeps the crowd roughly square; the 1.35 factor makes it slightly wider than deep,
            // which reads better from the pitched camera.
            columns = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sqrt(safeCount * 1.35f)), 1, maxColumns);
            columns = Mathf.Min(columns, safeCount);
            rows = Mathf.CeilToInt(safeCount / (float)columns);
        }

        /// <summary>Local space position of the soldier at <paramref name="index"/> for the current count.</summary>
        public static Vector3 SlotLocalPosition(int index, int count, GameSettings settings)
        {
            ResolveGrid(count, settings, out int columns, out int rows);

            int row = index / columns;
            int column = index % columns;
            int soldiersInRow = Mathf.Min(columns, count - row * columns);

            float x = (column - (soldiersInRow - 1) * 0.5f) * settings.soldierSpacing;

            // Rows are centred on the army root so a large crowd never sinks below the camera.
            float z = ((rows - 1) * 0.5f - row) * settings.soldierRowSpacing;

            // Deterministic jitter: the crowd looks hand placed but never jitters between frames.
            float jitterX = (Hash01(index * 3 + 1) - 0.5f) * 0.09f;
            float jitterZ = (Hash01(index * 7 + 11) - 0.5f) * 0.09f;

            return new Vector3(x + jitterX, 0f, z + jitterZ);
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint hash = (uint)seed * 2654435761u;
                hash ^= hash >> 13;
                hash *= 2246822519u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }
    }
}
