using System;
using NumberRush3D.Army;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Gates
{
    /// <summary>
    /// One half of a number gate: a green "+10" frame, a red "÷2" frame, etc.
    ///
    /// The gate is built from primitives at runtime (the project ships no gate art), gets a 3D
    /// <see cref="TextMesh"/> label and applies its operation to the *current* soldier count the
    /// first time the army passes through it. Two independent checks make that reliable:
    /// a trigger collider (for the prefab/physics workflow) and an authoritative forward-crossing
    /// test on the army's Z (works with or without a rigidbody on the army).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NumberGate : MonoBehaviour
    {
        private const float PostHeight = 3.2f;
        private const float PostWidth = 0.3f;
        private const float PanelHeight = 1.5f;

        [Header("Look")]
        [Tooltip("3D label height, roughly in world units.")]
        [SerializeField] private float labelCharacterSize = 0.09f;

        [SerializeField] private int labelFontSize = 100;

        private BoxCollider _trigger;
        private Renderer _panelRenderer;
        private Renderer[] _postRenderers;
        private TextMesh _label;
        private TextMesh _shadowLabel;

        private IBattleContext _context;
        private ArmyController _army;
        private MaterialPropertyBlock _block;
        private GateKind _kind;
        private int _value;
        private bool _negative;
        private bool _fired;
        private Color _color;
        private float _gateZ;
        private float _minX;
        private float _maxX;
        private Vector3 _panelBaseScale;
        private Vector3 _labelBaseLocalPosition;
        private float _pulsePhase;
        private float _flash;

        /// <summary>Raised once, when this gate applies its operation.</summary>
        public event Action<NumberGate> Fired;

        /// <summary>True once the army has passed through this gate.</summary>
        public bool HasFired => _fired;

        public GateKind Kind => _kind;
        public int Value => _value;

        /// <summary>Creates the (inactive) template every pooled gate is cloned from.</summary>
        public static NumberGate CreateTemplate(Transform parent, Material postMaterial, Material panelMaterial)
        {
            GameObject root = new GameObject("GateHalf");
            root.transform.SetParent(parent, false);
            root.SetActive(false);

            NumberGate gate = root.AddComponent<NumberGate>();
            gate.BuildVisuals(postMaterial, panelMaterial);
            return gate;
        }

        /// <summary>Builds one gate half at <paramref name="centerX"/> on row <paramref name="z"/>.</summary>
        public void BuildVisuals(Material postMaterial, Material panelMaterial)
        {
            _postRenderers = new Renderer[3];

            // Outer post, inner post, top beam: a simple goal frame.
           _postRenderers[0] = CreateBox("Post_L", new Vector3(-0.5f, 0f, 0f), new Vector3(PostWidth, PostHeight, PostWidth), postMaterial).GetComponent<Renderer>();
_postRenderers[1] = CreateBox("Post_R", new Vector3(0.5f, 0f, 0f), new Vector3(PostWidth, PostHeight, PostWidth), postMaterial).GetComponent<Renderer>();
_postRenderers[2] = CreateBox("Beam", new Vector3(0f, 0f, 0f), new Vector3(1f, PostWidth, PostWidth), postMaterial).GetComponent<Renderer>();

            Transform panel = CreateBox("Panel", new Vector3(0f, 0f, 0f), new Vector3(0.92f, PanelHeight, 0.12f), panelMaterial);
            _panelRenderer = panel.GetComponent<MeshRenderer>();
            _panelBaseScale = panel.localScale;

            _label = CreateLabel("Label", out Transform labelTransform);
            _labelBaseLocalPosition = labelTransform.localPosition;

            // Soft drop shadow / thickness: a second label slightly behind the first.
            _shadowLabel = CreateLabel("LabelShadow", out Transform shadowTransform);
            shadowTransform.localPosition = _labelBaseLocalPosition + new Vector3(0f, -0.03f, 0.06f);
            if (_shadowLabel != null)
            {
                _shadowLabel.color = new Color(0f, 0f, 0f, 0.35f);
            }

            _trigger = gameObject.AddComponent<BoxCollider>();
            _trigger.isTrigger = true;
        }

        /// <summary>Positions + labels this half. Safe to call on a recycled instance.</summary>
        public void Configure(GateChoice choice, float centerX, float width, float z, IBattleContext context, ArmyController army)
        {
            _context = context;
            _army = army;
            _kind = choice.kind;
            _value = Mathf.Max(1, Mathf.Abs(choice.value));
            _negative = choice.IsNegative;
            _fired = false;
            _flash = 0f;
            _gateZ = z;

            _minX = centerX - width * 0.5f;
            _maxX = centerX + width * 0.5f;

            _color = _negative
                ? new Color(0.93f, 0.24f, 0.26f, 1f)
                : new Color(0.16f, 0.85f, 0.36f, 1f);

            transform.position = new Vector3(centerX, 0f, z);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            ApplyLayout(width);
            ApplyStyle();

            if (_trigger != null)
            {
                _trigger.enabled = true;
            }

            _pulsePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>Width of this half + label text; used by the editor tooling and tests.</summary>
        public string LabelText => FormatText(_kind, _value);

        private void ApplyLayout(float width)
        {
            // The frame pieces are authored around the local origin and resized to the half-width
            // the track uses, so a 5-lane track and a 9-lane track both look right.
            if (_postRenderers != null && _postRenderers.Length == 3)
            {
                Transform left = _postRenderers[0] != null ? _postRenderers[0].transform : null;
                Transform right = _postRenderers[1] != null ? _postRenderers[1].transform : null;
                Transform beam = _postRenderers[2] != null ? _postRenderers[2].transform : null;

                if (left != null)
                {
                    left.localPosition = new Vector3(-width * 0.5f + PostWidth * 0.5f, PostHeight * 0.5f, 0f);
                    left.localScale = new Vector3(PostWidth, PostHeight, PostWidth);
                }

                if (right != null)
                {
                    right.localPosition = new Vector3(width * 0.5f - PostWidth * 0.5f, PostHeight * 0.5f, 0f);
                    right.localScale = new Vector3(PostWidth, PostHeight, PostWidth);
                }

                if (beam != null)
                {
                    beam.localPosition = new Vector3(0f, PostHeight, 0f);
                    beam.localScale = new Vector3(width, PostWidth, PostWidth);
                }
            }

            if (_panelRenderer != null)
            {
                Transform panel = _panelRenderer.transform;
                panel.localPosition = new Vector3(0f, PostHeight * 0.62f, 0f);
                panel.localScale = new Vector3(Mathf.Max(0.5f, width - 0.6f), PanelHeight, 0.12f);
                _panelBaseScale = panel.localScale;
                _panelRenderer.enabled = true;
            }

            if (_trigger != null)
            {
                _trigger.center = new Vector3(0f, PostHeight * 0.5f, 0f);
                _trigger.size = new Vector3(width, PostHeight, 1.6f);
            }
        }

        private void ApplyStyle()
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            if (_postRenderers != null)
            {
                for (int i = 0; i < _postRenderers.Length; i++)
                {
                    RuntimeAssets.Tint(_postRenderers[i], _block, _color);
                }
            }

            // The panel stays white-ish so the coloured frame + label carry the meaning.
            if (_panelRenderer != null)
            {
                RuntimeAssets.Tint(_panelRenderer, _block, Color.Lerp(_color, Color.white, 0.55f));
            }

            string text = FormatText(_kind, _value);
            Color textColor = Color.Lerp(_color, Color.white, 0.25f);

            if (_label != null)
            {
                _label.text = text;
                _label.color = textColor;
            }

            if (_shadowLabel != null)
            {
                _shadowLabel.text = text;
            }
        }

        private void Update()
        {
            Animate();

            if (_fired || _context == null || _army == null || !_context.IsPlaying)
            {
                return;
            }

            // Authoritative crossing test: the army only ever moves forward, so this fires once.
            if (_army.WorldZ >= _gateZ)
            {
                float x = _army.WorldX;
                if (x >= _minX && x <= _maxX)
                {
                    Fire();
                }
                else
                {
                    // Passed outside this half - it can never trigger now.
                    _fired = true;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other == null)
            {
                return;
            }

            if (other.GetComponentInParent<ArmyController>() != null)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (_fired)
            {
                return;
            }

            _fired = true;
            _flash = 1f;

            int current = _army != null ? _army.SoldierCount : 1;
            int result = ArmyController.ComputeGateResult(current, _kind, _value);

            if (_army != null)
            {
                _army.SetSoldierCount(result);
            }

            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            if (_panelRenderer != null)
            {
                // The gate "opens" once used, so the player keeps reading the HUD, not the frame.
                _panelRenderer.enabled = false;
            }

            if (_context != null)
            {
                string text = FormatText(_kind, _value);
                _context.ShowToast($"{text}  →  {result}", !_negative);
                _context.ShakeCamera(_negative ? 0.16f : 0.1f);

                if (_context.Vfx != null)
                {
                    _context.Vfx.PlayBurst(transform.position + Vector3.up * PostHeight * 0.6f, _color, _negative ? 12 : 16, 1.4f);
                }
            }

            Fired?.Invoke(this);
        }

        private void Animate()
        {
            float deltaTime = Time.deltaTime;

            if (_panelRenderer != null && _panelRenderer.enabled)
            {
                _pulsePhase += deltaTime * 3.2f;
                float pulse = 1f + Mathf.Sin(_pulsePhase) * 0.03f;
                _panelRenderer.transform.localScale = new Vector3(
                    _panelBaseScale.x * pulse,
                    _panelBaseScale.y * pulse,
                    _panelBaseScale.z);
            }

            if (_label == null)
            {
                return;
            }

            Transform labelTransform = _label.transform;
            float bob = Mathf.Sin(Time.time * 2.4f + _pulsePhase) * 0.04f;
            labelTransform.localPosition = new Vector3(
                _labelBaseLocalPosition.x,
                _labelBaseLocalPosition.y + bob,
                _labelBaseLocalPosition.z);

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - deltaTime * 3f);
                float scale = 1f + _flash * 0.35f;
                labelTransform.localScale = new Vector3(scale, scale, scale);
            }
            else
            {
                labelTransform.localScale = Vector3.one;
            }
        }

        private Transform CreateBox(string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            Collider boxCollider = box.GetComponent<Collider>();
            if (boxCollider != null)
            {
                Destroy(boxCollider);
            }

            Transform boxTransform = box.transform;
            boxTransform.SetParent(transform, false);
            boxTransform.localPosition = localPosition;
            boxTransform.localScale = localScale;
            boxTransform.localRotation = Quaternion.identity;

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.receiveShadows = true;
            }

            return boxTransform;
        }

        private TextMesh CreateLabel(string name, out Transform labelTransform)
        {
            GameObject labelObject = new GameObject(name);
            labelTransform = labelObject.transform;
            labelTransform.SetParent(transform, false);
            labelTransform.localPosition = new Vector3(0f, PostHeight * 0.62f, -0.2f);
            labelTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            TextMesh textMesh = labelObject.AddComponent<TextMesh>();
            Font font = RuntimeAssets.LegacyFont;
            if (font != null)
            {
                textMesh.font = font;
                MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = font.material;
                }
            }

            textMesh.text = "0";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = labelFontSize;
            textMesh.characterSize = labelCharacterSize;
            textMesh.richText = false;
            textMesh.color = Color.white;

            return textMesh;
        }

        private static string FormatText(GateKind kind, int value)
        {
            switch (kind)
            {
                case GateKind.Add:
                    return "+" + value;
                case GateKind.Subtract:
                    return "-" + value;
                case GateKind.Multiply:
                    return "\u00D7" + value;
                case GateKind.Divide:
                    return "\u00F7" + value;
                default:
                    return value.ToString();
            }
        }
    }
}
