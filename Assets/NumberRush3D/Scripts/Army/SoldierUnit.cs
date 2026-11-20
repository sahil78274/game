using NumberRush3D.Combat;
using NumberRush3D.Core;
using NumberRush3D.Enemies;
using UnityEngine;

namespace NumberRush3D.Army
{
    /// <summary>
    /// One blue soldier. Fully autonomous: it runs with the army, picks the closest enemy in front,
    /// fires the shared <see cref="BulletPool"/> and plays a small pop / muzzle flash animation.
    ///
    /// Target acquisition is staggered across soldiers (random phase) and sticky, so the crowd
    /// spreads its fire instead of converging on a single enemy. The run cycle is procedural
    /// (limb swing driven by <see cref="ArmyController.ForwardSpeed"/>) because the prototype has
    /// no animation clips - assign the four limb transforms to enable it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoldierUnit : MonoBehaviour
    {
        private const float PopDuration = 0.22f;
        private const float MuzzleFlashDuration = 0.045f;

        [Header("Wiring (auto-found when empty)")]
        [Tooltip("Empty transform at the tip of the weapon. Bullets spawn here.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Root of the visual body. Used for the spawn pop + idle bob. Defaults to this transform.")]
        [SerializeField] private Transform bodyRoot;

        [Tooltip("Renderer toggled for a couple of frames when the soldier fires.")]
        [SerializeField] private Renderer muzzleFlash;

        [Header("Procedural run (optional)")]
        [Tooltip("Left leg transform, swung on the local X axis. Leave empty to disable the run cycle.")]
        [SerializeField] private Transform leftLeg;

        [Tooltip("Right leg transform, swung opposite to the left leg.")]
        [SerializeField] private Transform rightLeg;

        [Tooltip("Left arm transform, swung opposite to the left leg.")]
        [SerializeField] private Transform leftArm;

        [Tooltip("Right arm transform.")]
        [SerializeField] private Transform rightArm;

        [Tooltip("Leg swing amplitude in degrees at full speed.")]
        [Range(0f, 60f)] [SerializeField] private float runSwingDegrees = 30f;

        [Tooltip("Run cycle speed (radians/second) at zero and at full army speed.")]
        [SerializeField] private Vector2 runCycleSpeed = new Vector2(5f, 13f);

        private IBattleContext _context;
        private ArmyController _army;
        private Enemy _target;
        private float _retargetTimer;
        private float _fireTimer;
        private float _flashTimer;
        private float _spawnTime;
        private bool _alive;
        private bool _popSettled;
        private float _bobPhase;
        private float _runPhase;
        private Vector3 _baseBodyScale = Vector3.one;
        private Vector3 _baseBodyLocalPosition;
        private Quaternion _baseRotation = Quaternion.identity;

        /// <summary>Current target, null when nothing is in range. Exposed for debugging / tests.</summary>
        public Enemy Target => _target;

        /// <summary>Called once when the instance is created by the pool.</summary>
        public void Initialize(IBattleContext context, ArmyController army)
        {
            _context = context;
            _army = army;

            if (muzzle == null)
            {
                muzzle = transform.Find("Visual/Muzzle");
            }

            if (bodyRoot == null)
            {
                bodyRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (muzzleFlash == null && bodyRoot != null)
            {
                Transform flash = bodyRoot.Find("Flash");
                if (flash != null)
                {
                    muzzleFlash = flash.GetComponent<Renderer>();
                }
            }

            if (bodyRoot != null)
            {
                // The advanced prefab keeps the limbs under Visual/Limbs.
                Transform limbs = bodyRoot.Find("Limbs");
                if (limbs != null)
                {
                    if (leftLeg == null) leftLeg = limbs.Find("LegL");
                    if (rightLeg == null) rightLeg = limbs.Find("LegR");
                    if (leftArm == null) leftArm = limbs.Find("ArmL");
                    if (rightArm == null) rightArm = limbs.Find("ArmR");
                }
            }

            _baseBodyScale = bodyRoot != null ? bodyRoot.localScale : Vector3.one;
            _baseBodyLocalPosition = bodyRoot != null ? bodyRoot.localPosition : Vector3.zero;
            _baseRotation = transform.localRotation;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
            _runPhase = Random.Range(0f, Mathf.PI * 2f);
            _alive = true;
        }

        /// <summary>Called every time the soldier is pulled out of the pool (recruit / first spawn).</summary>
        public void ResetForSpawn()
        {
            _target = null;
            _retargetTimer = Random.Range(0f, 0.25f);
            _fireTimer = Random.Range(0f, 0.3f);
            _flashTimer = 0f;
            _spawnTime = Time.time;
            _popSettled = false;

            if (muzzleFlash != null)
            {
                muzzleFlash.enabled = false;
            }

            if (bodyRoot != null)
            {
                bodyRoot.localScale = _baseBodyScale * 0.3f;
            }

            transform.localRotation = _baseRotation;
        }

        private void OnDisable()
        {
            _alive = false;
            _target = null;
        }

        private void OnEnable()
        {
            _alive = true;
        }

        private void Update()
        {
            if (!_alive || _context == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Animate(deltaTime);

            if (!_context.IsPlaying)
            {
                return;
            }

            GameSettings settings = _context.Settings;

            // Sticky targeting: a soldier keeps shooting the enemy it picked until that enemy dies.
            _retargetTimer -= deltaTime;
            if (_retargetTimer <= 0f && _target == null)
            {
                _retargetTimer = settings.targetRefreshInterval;
                AcquireTarget(settings);
            }

            if (_target != null && !_target.IsAlive)
            {
                _target = null;
                _retargetTimer = 0f;
            }

            _fireTimer -= deltaTime;
            if (_fireTimer > 0f)
            {
                return;
            }

            if (_target == null)
            {
                _fireTimer = 0.1f;
                return;
            }

            Fire(settings);
        }

        private void AcquireTarget(GameSettings settings)
        {
            EnemyRegistry registry = _context.Enemies;
            if (registry == null)
            {
                return;
            }

            Vector3 origin = muzzle != null ? muzzle.position : transform.position;

            // The army keeps moving, so "in front" must be relative to its current position.
            float minZ = (_army != null ? _army.WorldZ : settings.armyZ) - 1f;
            _target = registry.FindNearest(origin, settings.targetSearchRange, minZ);
        }

        private void Fire(GameSettings settings)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.6f;
            Vector3 aimPoint = _target != null ? _target.AimPoint : origin + Vector3.forward * 10f;
            Vector3 direction = aimPoint - origin;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            direction = ApplySpread(direction.normalized, settings.aimSpreadDegrees);

            float shotInterval = 1f / Mathf.Max(0.05f, settings.shotsPerSecond);
            _fireTimer = shotInterval * Random.Range(0.88f, 1.12f);

            _context.Bullets.Spawn(origin, direction, settings.bulletSpeed, settings.bulletDamage);

            if (muzzleFlash != null)
            {
                muzzleFlash.enabled = true;
                _flashTimer = MuzzleFlashDuration;
            }
        }

        private static Vector3 ApplySpread(Vector3 direction, float spreadDegrees)
        {
            if (spreadDegrees <= 0.01f)
            {
                return direction;
            }

            float yaw = Random.Range(-spreadDegrees, spreadDegrees);
            float pitch = Random.Range(-spreadDegrees, spreadDegrees);
            return Quaternion.Euler(pitch, yaw, 0f) * direction;
        }

        private void Animate(float deltaTime)
        {
            if (muzzleFlash != null && _flashTimer > 0f)
            {
                _flashTimer -= deltaTime;
                if (_flashTimer <= 0f)
                {
                    muzzleFlash.enabled = false;
                }
            }

            AnimateRun(deltaTime);

            if (bodyRoot == null)
            {
                return;
            }

            // Spawn pop.
            float popT = Mathf.Clamp01((Time.time - _spawnTime) / PopDuration);
            if (popT < 1f)
            {
                float eased = 1f - Mathf.Pow(1f - popT, 3f);
                float overshoot = 1f + 0.22f * Mathf.Sin(popT * Mathf.PI) * (1f - popT);
                bodyRoot.localScale = _baseBodyScale * Mathf.Lerp(0.3f, 1f, eased) * overshoot;
            }
            else if (!_popSettled)
            {
                bodyRoot.localScale = _baseBodyScale;
                _popSettled = true;
            }

            // Idle bob keeps the crowd alive without any animation clips.
            _bobPhase += deltaTime * 7f;
            Vector3 bobbed = _baseBodyLocalPosition;
            bobbed.y += Mathf.Sin(_bobPhase) * 0.02f;
            bodyRoot.localPosition = bobbed;

            // Lean slightly towards the target so the crowd reads as "aiming".
            if (_target != null && _army != null)
            {
                Vector3 toTarget = _target.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    yaw = Mathf.Clamp(yaw, -22f, 22f);
                    transform.localRotation = Quaternion.Slerp(
                        transform.localRotation,
                        _baseRotation * Quaternion.Euler(0f, yaw, 0f),
                        1f - Mathf.Exp(-8f * deltaTime));
                }
            }
            else
            {
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    _baseRotation,
                    1f - Mathf.Exp(-8f * deltaTime));
            }
        }

        /// <summary>Procedural run cycle: legs and arms swing in opposite phase, faster with speed.</summary>
        private void AnimateRun(float deltaTime)
        {
            if (leftLeg == null && rightLeg == null)
            {
                return;
            }

            float speed = _army != null ? _army.ForwardSpeed : 0f;
            float speed01 = Mathf.Clamp01(speed / 10f);
            float cycleSpeed = Mathf.Lerp(runCycleSpeed.x, runCycleSpeed.y, speed01);

            _runPhase += deltaTime * cycleSpeed;

            float swing = Mathf.Sin(_runPhase) * runSwingDegrees;
            float armSwing = swing * 0.7f;

            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-armSwing, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(armSwing, 0f, 0f);
        }
    }
}
