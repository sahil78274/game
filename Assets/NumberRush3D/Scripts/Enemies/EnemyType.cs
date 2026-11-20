using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// Data definition for one kind of red enemy. Author as many as you want -
    /// <see cref="EnemySpawner"/> picks them by weight and unlocks the heavier ones
    /// as the run gets harder.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyType", menuName = "NUMBER RUSH 3D/Enemy Type")]
    public sealed class EnemyType : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Grunt";

        [Tooltip("Colour tint applied to the enemy body at spawn.")]
        public Color color = new Color(0.91f, 0.19f, 0.23f, 1f);

        [Header("Combat")]
        [Tooltip("Bullets needed to kill this enemy (with 1 damage per bullet).")]
        [Range(1f, 60f)] public float maxHealth = 3f;

        [Tooltip("World units per second. The army stands at z = -8, enemies spawn around z = +15.")]
        [Range(0.5f, 12f)] public float moveSpeed = 2.4f;

        [Tooltip("Spatial scale multiplier applied to the enemy prefab.")]
        [Range(0.4f, 3f)] public float scale = 1f;

        [Tooltip("How many soldiers this enemy kills if it reaches the army line.")]
        [Range(1, 20)] public int armyDamage = 1;

        [Header("Rewards")]
        [Tooltip("Score awarded when destroyed by bullets.")]
        [Range(0, 500)] public int scoreValue = 10;

        [Tooltip("Extra soldiers granted to the player when destroyed.")]
        [Range(0, 10)] public int recruitValue = 0;

        [Header("Spawning")]
        [Tooltip("Relative chance to be picked versus the other types.")]
        [Range(0.01f, 20f)] public float spawnWeight = 1f;

        [Tooltip("Difficulty (0 = start of the run, 1 = fully ramped) from which this type can spawn.")]
        [Range(0f, 1f)] public float minDifficulty = 0f;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
