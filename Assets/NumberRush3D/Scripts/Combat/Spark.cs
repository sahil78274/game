using UnityEngine;

namespace NumberRush3D.Combat
{
    /// <summary>
    /// Cheap pooled particle substitute: a tiny cube that flies, spins, shrinks and returns to the pool.
    /// One draw call per spark but no ParticleSystem overhead and absolutely no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Spark : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Renderer tinted per spark. Auto-found in children when empty.")]
        [SerializeField] private Renderer bodyRenderer;

        private MaterialPropertyBlock _block;
        private VfxPool _pool;
        private Vector3 _velocity;
        private Vector3 _spinAxis;
        private float _spinSpeed;
        private float _lifeRemaining;
        private float _lifetime = 1f;
        private float _scale = 1f;
        private bool _live;

        private void Awake()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<Renderer>();
            }
        }

        public void Play(VfxPool pool, Color color, float scale, Vector3 velocity, float lifetime)
        {
            _pool = pool;
            _velocity = velocity;
            _scale = Mathf.Max(0.01f, scale);
            _lifetime = Mathf.Max(0.05f, lifetime);
            _lifeRemaining = _lifetime;
            _spinAxis = Random.onUnitSphere;
            _spinSpeed = Random.Range(180f, 540f);
            _live = true;

            transform.localScale = Vector3.one * _scale;
            ApplyColor(color);
        }

        private void Update()
        {
            if (!_live)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            _velocity += Physics.gravity * (deltaTime * 0.55f);
            transform.position += _velocity * deltaTime;
            transform.Rotate(_spinAxis, _spinSpeed * deltaTime, Space.Self);

            _lifeRemaining -= deltaTime;
            if (_lifeRemaining <= 0f)
            {
                _live = false;
                if (_pool != null)
                {
                    _pool.Release(this);
                }
                else
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            float life01 = _lifeRemaining / _lifetime;
            transform.localScale = Vector3.one * (_scale * Mathf.Lerp(0.05f, 1f, life01));
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
