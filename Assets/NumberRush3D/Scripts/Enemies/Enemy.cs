using NumberRush3D.Combat;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// A red enemy: walks straight down its lane towards the army, rams the defense line when it
    /// gets there and dies (scoring, VFX, pooling) when its <see cref="Health"/> runs out.
    ///
    /// The enemy is purely a brain - damage lives in <see cref="Health"/>, movement lives here,
    /// and everything it needs is injected by the spawner through <see cref="Spawn"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : MonoBehaviour
    {
        [Header("Wiring (auto-found when empty)")]
        [SerializeField] private Health health;

        [Tooltip("Visual root used for the idle bob. Defaults to the first child.")]
        [SerializeField] private Transform bodyRoot;

        [SerializeField] private WorldHealthBar healthBar;

        private EnemyType _type;
        private IBattleContext _context;
        private EnemySpawner _owner;
        private float _speed;
        private float _stopZ;
        private float _aimHeight = 0.6f;
        private float _bobPhase;
        private bool _active;
        private bool _released;
        private Vector3 _baseBodyScale = Vector3.one;
        private Vector3 _baseBodyPosition;

        /// <summary>True while this enemy is alive and on screen.</summary>
        public bool IsAlive => _active && health != null && health.IsAlive;

        /// <summary>Point bullets should aim at.</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * _aimHeight;

        public EnemyType Type => _type;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (bodyRoot == null && transform.childCount > 0)
            {
                bodyRoot = transform.GetChild(0);
            }

            if (bodyRoot != null)
            {
                _baseBodyScale = bodyRoot.localScale;
                _baseBodyPosition = bodyRoot.localPosition;
            }
        }

        /// <summary>Initialise a pooled instance. Called by <see cref="EnemySpawner"/>.</summary>
        public void Spawn(EnemyType type, IBattleContext context, EnemySpawner owner, Vector3 position)
        {
            _type = type;
            _context = context;
            _owner = owner;
            _active = true;
            _released = false;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            transform.position = position;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, type.scale);
            _aimHeight = 0.62f * type.scale;
            _speed = Mathf.Max(0.1f, type.moveSpeed * context.Settings.enemySpeedMultiplier);
            _stopZ = context.Settings.defenseLineZ;

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (health != null)
            {
                health.Died -= HandleDied;
                health.Died += HandleDied;
                health.Configure(type.maxHealth);
                health.SetBaseColor(type.color);
            }

            if (healthBar != null)
            {
                healthBar.Bind(health);
            }

            if (bodyRoot != null)
            {
                bodyRoot.localScale = _baseBodyScale;
                bodyRoot.localPosition = _baseBodyPosition;
            }

            context.Enemies.Register(this);
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Animate(deltaTime);

            if (_context == null || !_context.IsPlaying)
            {
                return;
            }

            Vector3 position = transform.position;
            position.z -= _speed * deltaTime;

            if (position.z <= _stopZ)
            {
                transform.position = position;
                Ram();
                return;
            }

            transform.position = position;
        }

        private void Animate(float deltaTime)
        {
            if (bodyRoot == null)
            {
                return;
            }

            _bobPhase += deltaTime * 6.5f;
            Vector3 offset = _baseBodyPosition;
            offset.y += Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.05f;
            bodyRoot.localPosition = offset;

            // A tiny forward lean sells the "charging" motion.
            bodyRoot.localRotation = Quaternion.Euler(Mathf.Sin(_bobPhase) * 3f, 0f, Mathf.Cos(_bobPhase) * 2f);
        }

        private void Ram()
        {
            if (!_active)
            {
                return;
            }

            if (_context != null)
            {
                if (_context.Vfx != null)
                {
                    _context.Vfx.PlayBurst(AimPoint, _type != null ? _type.color : Color.red, 8, 1.2f);
                }

                _context.ShakeCamera(0.35f);

                if (_context.Army != null && _type != null)
                {
                    _context.Army.ApplyCasualties(_type.armyDamage);
                }
            }

            Release();
        }

        private void HandleDied(Health deadHealth)
        {
            if (!_active)
            {
                return;
            }

            if (_context != null)
            {
                if (_context.Vfx != null)
                {
                    _context.Vfx.PlayBurst(AimPoint, _type != null ? _type.color : Color.red, 7, 1f);
                }

                _context.ShakeCamera(0.07f);
                _context.ReportKill(_type);
            }

            Release();
        }

        /// <summary>Returns this enemy to its pool. Safe to call more than once.</summary>
        private void Release()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _active = false;

            if (health != null)
            {
                health.Died -= HandleDied;
            }

            if (healthBar != null)
            {
                healthBar.Unbind();
            }

            if (_context != null)
            {
                _context.Enemies.Unregister(this);
            }

            if (_owner != null)
            {
                _owner.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Belt and braces: never leave a disabled enemy inside the registry.
            if (_context != null)
            {
                _context.Enemies.Unregister(this);
            }
        }
    }
}
