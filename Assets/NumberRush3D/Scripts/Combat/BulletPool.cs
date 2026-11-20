using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Combat
{
    /// <summary>
    /// Central bullet factory. Every soldier fires through this pool, so the number of live
    /// projectiles is hard-capped by <see cref="GameSettings.maxBulletsInFlight"/> and the
    /// GC never sees a bullet allocation after warm up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletPool : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Bullet prefab (must contain a Bullet component).")]
        [SerializeField] private Bullet bulletPrefab;

        [Tooltip("Parent for pooled bullets. Defaults to this transform.")]
        [SerializeField] private Transform poolRoot;

        [Header("Pooling")]
        [Tooltip("Bullets created up front. Keep it around 25-30% of maxBulletsInFlight.")]
        [Range(0, 400)] [SerializeField] private int prewarm = 80;

        private ObjectPool<Bullet> _pool;
        private GameSettings _settings;
        private VfxPool _vfx;
        private LayerMask _hitMask;
        private bool _initialized;

        /// <summary>Bullets currently flying.</summary>
        public int ActiveCount => _pool != null ? _pool.ActiveCount : 0;

        /// <summary>Bullets instantiated so far (grows lazily up to the cap).</summary>
        public int CreatedCount => _pool != null ? _pool.TotalCreated : 0;

        public void Initialize(GameSettings settings, VfxPool vfx)
        {
            if (_initialized)
            {
                return;
            }

            _settings = settings;
            _vfx = vfx;

            if (bulletPrefab == null)
            {
                Debug.LogError("[NumberRush] BulletPool has no bullet prefab assigned - soldiers will not shoot.", this);
                return;
            }

            if (poolRoot == null)
            {
                poolRoot = transform;
            }

            _hitMask = settings != null ? settings.EnemyMask : (LayerMask)(~0);

            _pool = new ObjectPool<Bullet>(
                () => Instantiate(bulletPrefab, poolRoot),
                poolRoot,
                prewarm,
                Mathf.Max(8, settings != null ? settings.maxBulletsInFlight : 260));

            _initialized = true;
        }

        /// <summary>
        /// Fire one bullet. Returns null when the pool is exhausted, in which case the soldier
        /// simply skips the shot (this is the frame budget guard, not an error).
        /// </summary>
        public Bullet Spawn(Vector3 origin, Vector3 direction, float speed, float damage)
        {
            if (_pool == null || _settings == null)
            {
                return null;
            }

            Bullet bullet = _pool.Get(origin, Quaternion.identity);
            if (bullet == null)
            {
                return null;
            }

            bullet.Launch(
                this,
                origin,
                direction,
                speed,
                damage,
                _settings.bulletMaxLifetime,
                _settings.bulletRadius,
                _settings.bulletColor,
                _hitMask);

            return bullet;
        }

        public void Release(Bullet bullet)
        {
            _pool?.Release(bullet);
        }

        /// <summary>Small impact spark, routed through the VFX pool so bullets stay unaware of VFX.</summary>
        public void PlayImpact(Vector3 position)
        {
            if (_vfx != null && _settings != null)
            {
                _vfx.PlayImpact(position, _settings.bulletColor, 0.7f);
            }
        }
    }
}
