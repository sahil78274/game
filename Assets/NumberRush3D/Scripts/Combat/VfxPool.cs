using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Combat
{
    /// <summary>
    /// Pool for the one-shot <see cref="Spark"/> effect. Exposes two intents:
    /// a single impact spark and a burst (enemy death, soldier lost).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxPool : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Spark sparkPrefab;
        [Tooltip("Parent for pooled sparks. Defaults to this transform.")]
        [SerializeField] private Transform poolRoot;

        [Header("Pooling")]
        [Range(0, 300)] [SerializeField] private int prewarm = 64;
        [Range(16, 600)] [SerializeField] private int maxSize = 220;

        private ObjectPool<Spark> _pool;

        public void Initialize()
        {
            if (_pool != null)
            {
                return;
            }

            if (sparkPrefab == null)
            {
                Debug.LogWarning("[NumberRush] VfxPool has no spark prefab - effects are disabled.", this);
                return;
            }

            if (poolRoot == null)
            {
                poolRoot = transform;
            }

            _pool = new ObjectPool<Spark>(
                () => Instantiate(sparkPrefab, poolRoot),
                poolRoot,
                prewarm,
                maxSize);
        }

        /// <summary>Single small spark, used for bullet impacts.</summary>
        public void PlayImpact(Vector3 position, Color color, float scale = 1f)
        {
            Spark spark = GetSpark(position);
            if (spark == null)
            {
                return;
            }

            Vector3 velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 4f), Random.Range(-1.5f, 0.5f));
            spark.Play(this, color, 0.14f * scale, velocity, Random.Range(0.14f, 0.26f));
        }

        /// <summary>Radial burst, used for deaths and lost soldiers.</summary>
        public void PlayBurst(Vector3 position, Color color, int count = 6, float scale = 1f)
        {
            int safeCount = Mathf.Clamp(count, 1, 24);
            for (int i = 0; i < safeCount; i++)
            {
                Spark spark = GetSpark(position);
                if (spark == null)
                {
                    return;
                }

                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.35f;
                Vector3 velocity = direction * Random.Range(2.5f, 6f);

                spark.Play(this, color, 0.2f * scale, velocity, Random.Range(0.25f, 0.45f));
            }
        }

        public void Release(Spark spark)
        {
            _pool?.Release(spark);
        }

        private Spark GetSpark(Vector3 position)
        {
            if (_pool == null)
            {
                return null;
            }

            return _pool.Get(position, Random.rotationUniform);
        }
    }
}
