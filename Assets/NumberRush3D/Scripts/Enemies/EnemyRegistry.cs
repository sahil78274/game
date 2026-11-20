using System.Collections.Generic;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// Flat list of every live enemy plus a nearest-target query.
    ///
    /// With the level cap (26 concurrent enemies) a linear scan is faster and much simpler than
    /// any spatial partition, and soldiers only query every ~0.2 s. The list is cleared on Awake
    /// so a scene reload can never keep stale (destroyed) references around.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRegistry : MonoBehaviour
    {
        private readonly List<Enemy> _enemies = new List<Enemy>(64);

        /// <summary>Number of live enemies.</summary>
        public int Count => _enemies.Count;

        /// <summary>Read-only view, handy for debugging overlays.</summary>
        public IReadOnlyList<Enemy> All => _enemies;

        private void Awake()
        {
            _enemies.Clear();
        }

        private void OnDisable()
        {
            _enemies.Clear();
        }

        public void Register(Enemy enemy)
        {
            if (enemy == null || _enemies.Contains(enemy))
            {
                return;
            }

            _enemies.Add(enemy);
        }

        public void Unregister(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            int index = _enemies.IndexOf(enemy);
            if (index < 0)
            {
                return;
            }

            // Swap-remove keeps it O(1) and order does not matter for target picking.
            int lastIndex = _enemies.Count - 1;
            _enemies[index] = _enemies[lastIndex];
            _enemies.RemoveAt(lastIndex);
        }

        /// <summary>
        /// Closest living enemy to <paramref name="from"/> within <paramref name="maxRange"/>.
        /// Enemies with a Z lower than <paramref name="minZ"/> are ignored (already behind the army).
        /// </summary>
        public Enemy FindNearest(Vector3 from, float maxRange, float minZ)
        {
            Enemy best = null;
            float bestSqrDistance = maxRange * maxRange;

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                Vector3 position = enemy.transform.position;
                if (position.z < minZ)
                {
                    continue;
                }

                float sqrDistance = (position - from).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                best = enemy;
            }

            return best;
        }

        /// <summary>Closest enemy inside a lane band. Used by enemies that shoot back (extension point).</summary>
        public Enemy FindNearestInLane(Vector3 from, float laneX, float laneHalfWidth, float maxRange)
        {
            Enemy best = null;
            float bestSqrDistance = maxRange * maxRange;

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                Vector3 position = enemy.transform.position;
                if (Mathf.Abs(position.x - laneX) > laneHalfWidth)
                {
                    continue;
                }

                float sqrDistance = (position - from).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                best = enemy;
            }

            return best;
        }
    }
}
