using NumberRush3D.Combat;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Enemies
{
    /// <summary>
    /// A red enemy. It spawns ahead of the army (relative to the player's progress), walks towards
    /// it and rams the army's *current* position when it arrives. Dies - scoring, VFX, pooling -
    /// when its <see cref="Health"/> runs out.
    ///
    /// Runner upgrade: the ram line is no longer a fixed world Z (the army now moves), it is
    /// `armyZ + ramDistanceAhead`. Nothing else about the damage pipeline changed: damage lives in
    /// <see cref="Health"/>, movement lives here, and the spawner injects everything via
    /// <see cref="Spawn"/>.
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
        private float _holdDistance = 4f;
        private float _aimHeight = 0.6f;
        private float _bobPhase;
        private float _age;
        private bool _active;
        private bool _released;
        private Vector3 _baseBodyScale = Vector3.one;
        private Vector3 _baseBodyPosition;

        /// <summary>True while this enemy is alive and on screen.</summary>
        public bool IsAlive => _active && health != null && health.IsAlive;

        /// <summary>Point bullets should aim at.</summary>
        public Vector3 AimPoint => transform.position + Vector3.up * _aimHeight;

        public EnemyType Type => _type;

        /// <summary>What the army earns when this enemy dies (score, coins, soldiers).</summary>
        public EnemyType RewardType => _type;

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
            _age = 0f;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);

            GameSettings settings = context.Settings;

            transform.position = position;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, type.scale);
            transform.localRotation = Quaternion.identity;
            _aimHeight = 0.62f * type.scale;
            _speed = Mathf.Max(0.1f, type.moveSpeed * settings.enemySpeedMultiplier * context.EnemySpeedMultiplier);
            _holdDistance = settings.ramDistanceAhead + Mathf.Max(0f, type.formationDepth);

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (health != null)
            {
                health.Died -= HandleDied;
                health.Died += HandleDied;
                health.Configure(type.maxHealth * Mathf.Max(0.1f, context.EnemyHealthMultiplier));
                health.DamageMultiplier = Mathf.Clamp(type.damageResistance, 0.05f, 1f);
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

            if (type.isBoss)
            {
                context.NotifyBossSpawned(this);
                context.ShakeCamera(0.3f);
            }
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _age += deltaTime;
            Animate(deltaTime);

            if (_context == null || !_context.IsPlaying)
            {
                return;
            }

            Vector3 position = transform.position;
            position.z -= _speed * deltaTime;

            // The army is running, so "where the army is now" moves every frame.
            float armyZ = _context.Army != null ? _context.Army.WorldZ : _context.Settings.armyZ;
            if (position.z <= armyZ + _holdDistance)
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

            _bobPhase += deltaTime * (6.5f + _speed * 0.4f);
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

                _context.ShakeCamera(_type != null && _type.isBoss ? 0.65f : 0.35f);

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
                    int sparks = _type != null && _type.isBoss ? 26 : 7;
                    float power = _type != null && _type.isBoss ? 2.4f : 1f;
                    _context.Vfx.PlayBurst(AimPoint, _type != null ? _type.color : Color.red, sparks, power);
                }

                _context.ShakeCamera(_type != null && _type.isBoss ? 0.55f : 0.07f);
                _context.ReportKill(_type);

                if (_type != null && _type.isBoss)
                {
                    _context.NotifyBossDefeated(this);
                }
            }

            Release();
        }

        /// <summary>Recycles this enemy silently (it ran past the army or the level ended).</summary>
        public void Despawn()
        {
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
