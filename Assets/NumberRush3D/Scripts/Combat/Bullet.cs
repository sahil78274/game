using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Combat
{
    /// <summary>
    /// A pooled, physics-free projectile.
    ///
    /// Instead of a Rigidbody + trigger we sweep a sphere along this frame's travel distance
    /// with <see cref="Physics.SphereCastNonAlloc"/>:
    /// * no tunnelling at 40 m/s on a 30 fps device,
    /// * no rigidbody / trigger matrix setup to get wrong,
    /// * no per-bullet allocation (one shared hit buffer).
    ///
    /// Only colliders on the "Enemy" layer are tested, and only objects implementing
    /// <see cref="IDamageable"/> actually take damage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bullet : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        [Tooltip("Renderer tinted with the bullet colour. Auto-found in children when empty.")]
        [SerializeField] private Renderer bodyRenderer;

        private MaterialPropertyBlock _block;
        private BulletPool _pool;
        private Vector3 _velocity;
        private float _damage;
        private float _lifeRemaining;
        private float _radius = 0.12f;
        private LayerMask _hitMask = ~0;
        private bool _live;

        private void Awake()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<Renderer>();
            }
        }

        /// <summary>Called by the pool right after the object is activated.</summary>
        public void Launch(BulletPool pool, Vector3 origin, Vector3 direction, float speed, float damage, float lifetime, float radius, Color color, LayerMask hitMask)
        {
            _pool = pool;
            _damage = damage;
            _lifeRemaining = lifetime;
            _radius = Mathf.Max(0.01f, radius);
            _hitMask = hitMask;
            _live = true;

            Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _velocity = forward * speed;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward, Vector3.up));

            ApplyColor(color);
        }

        private void Update()
        {
            if (!_live)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 step = _velocity * deltaTime;
            float distance = step.magnitude;

            if (distance > 0.0001f)
            {
                int hitCount = Physics.SphereCastNonAlloc(
                    transform.position,
                    _radius,
                    step / distance,
                    HitBuffer,
                    distance,
                    _hitMask,
                    QueryTriggerInteraction.Ignore);

                if (TryResolveHit(hitCount, out IDamageable target, out Vector3 hitPoint))
                {
                    target.TakeDamage(_damage, hitPoint);

                    if (_pool != null)
                    {
                        _pool.PlayImpact(hitPoint);
                    }

                    Release();
                    return;
                }
            }

            transform.position += step;

            _lifeRemaining -= deltaTime;
            if (_lifeRemaining <= 0f)
            {
                Release();
            }
        }

        private bool TryResolveHit(int hitCount, out IDamageable target, out Vector3 hitPoint)
        {
            target = null;
            hitPoint = default;

            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = HitBuffer[i];
                if (hit.collider == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                IDamageable candidate = hit.collider.GetComponentInParent<IDamageable>();
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                bestDistance = hit.distance;
                target = candidate;
                hitPoint = hit.point;
            }

            return target != null;
        }

        private void Release()
        {
            if (!_live)
            {
                return;
            }

            _live = false;
            if (_pool != null)
            {
                _pool.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyColor(Color color)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_block);
        }
    }
}
