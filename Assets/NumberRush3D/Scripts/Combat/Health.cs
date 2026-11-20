using System;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Combat
{
    /// <summary>
    /// Minimal damage receiver. Owns its own colour tinting through a shared
    /// <see cref="MaterialPropertyBlock"/>, which means flashing red on hit costs
    /// zero material instantiations and never leaks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

        [Header("Health")]
        [SerializeField] private float maxHealth = 3f;

        [Header("Feedback")]
        [Tooltip("Renderers tinted with the owner colour and flashed on hit. Auto-filled with children when empty.")]
        [SerializeField] private Renderer[] tintRenderers;

        [SerializeField] private Color hitFlashColor = Color.white;

        [Tooltip("How long the hit flash lasts, in seconds.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float flashDuration = 0.07f;

        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private float _flashTimer;
        private bool _deathReported;

        public event Action<Health> Died;
        public event Action<Health, float> Damaged;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(Current / maxHealth);

        /// <summary>
        /// Multiplier applied to incoming damage. 1 for regular units, lower for armoured ones
        /// (a heavy keeps its health bar but soaks part of each bullet).
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        private void Awake()
        {
            if (tintRenderers == null || tintRenderers.Length == 0)
            {
                tintRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        /// <summary>Reset health for a pooled spawn.</summary>
        public void Configure(float newMaxHealth)
        {
            maxHealth = Mathf.Max(1f, newMaxHealth);
            Current = maxHealth;
            _flashTimer = 0f;
            _deathReported = false;
            ApplyTint(_baseColor);
        }

        /// <summary>Colour used when the object is not flashing. Enemies set this to their type colour.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (_flashTimer <= 0f)
            {
                ApplyTint(_baseColor);
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - amount);
            _flashTimer = flashDuration;
            ApplyTint(hitFlashColor);

            Damaged?.Invoke(this, amount);

            if (Current <= 0f && !_deathReported)
            {
                _deathReported = true;
                Died?.Invoke(this);
            }
        }

        /// <summary>Immediate kill that still reports a death event (used for cleanup / debug).</summary>
        public void Kill()
        {
            if (!IsAlive)
            {
                return;
            }

            Current = 0f;
            if (!_deathReported)
            {
                _deathReported = true;
                Died?.Invoke(this);
            }
        }

        private void Update()
        {
            if (_flashTimer <= 0f)
            {
                return;
            }

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                ApplyTint(_baseColor);
            }
        }

        private void ApplyTint(Color color)
        {
            if (tintRenderers == null || tintRenderers.Length == 0)
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

            for (int i = 0; i < tintRenderers.Length; i++)
            {
                Renderer renderer = tintRenderers[i];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_block);
                }
            }
        }
    }
}
