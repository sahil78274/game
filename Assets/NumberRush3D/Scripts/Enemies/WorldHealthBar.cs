using NumberRush3D.Combat;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// Two quads that face the camera and show an enemy's remaining health.
    /// Rebuilt entirely from three transforms (bar root, fill pivot, fill) so it needs no sprites,
    /// no canvas and no material instances.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Wiring")]
        [Tooltip("Empty transform placed at the LEFT edge of the bar. Scaling it on X shrinks the fill.")]
        [SerializeField] private Transform fillPivot;

        [SerializeField] private Renderer fillRenderer;

        [Header("Look")]
        [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color highHealthColor = new Color(0.35f, 0.95f, 0.42f, 1f);
        [SerializeField] private bool billboardToCamera = true;

        private Health _health;
        private Camera _camera;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            if (billboardToCamera)
            {
                _camera = Camera.main;
            }
        }

        /// <summary>Called by the enemy after its health component is configured.</summary>
        public void Bind(Health health)
        {
            Unbind();

            _health = health;
            if (_health != null)
            {
                _health.Damaged += HandleDamaged;
            }

            Refresh();
        }

        public void Unbind()
        {
            if (_health != null)
            {
                _health.Damaged -= HandleDamaged;
                _health = null;
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            if (!billboardToCamera)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }

        private void HandleDamaged(Health health, float amount)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_health == null || fillPivot == null)
            {
                return;
            }

            float normalized = _health.Normalized;
            fillPivot.localScale = new Vector3(Mathf.Max(0.001f, normalized), 1f, 1f);

            if (fillRenderer == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            Color color = Color.Lerp(lowHealthColor, highHealthColor, normalized);
            _block.Clear();
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            fillRenderer.SetPropertyBlock(_block);
        }
    }
}
