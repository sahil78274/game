using System;
using System.Collections.Generic;
using NumberRush3D.Controls;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Army
{
    /// <summary>
    /// The blue crowd: continuous forward running, lane movement, formation layout,
    /// size bookkeeping (recruits / casualties / gates) and the pool of <see cref="SoldierUnit"/> instances.
    ///
    /// Runner upgrade: the army now integrates its own forward motion every frame
    /// (<see cref="ForwardSpeed"/>, default 7 u/s from <see cref="GameSettings.runnerStartSpeed"/>)
    /// and reports <see cref="DistanceTravelled"/> so gates, the track and the level manager can all
    /// be driven from a single source of truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmyController : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Soldier prefab. Must contain a SoldierUnit component.")]
        [SerializeField] private SoldierUnit soldierPrefab;

        [Tooltip("Parent of the living soldiers. Defaults to this transform.")]
        [SerializeField] private Transform formationRoot;

        [Tooltip("Parent of the sleeping pooled soldiers. Defaults to this transform.")]
        [SerializeField] private Transform poolRoot;

        private readonly List<SoldierUnit> _soldiers = new List<SoldierUnit>(64);
        private readonly List<Vector3> _slots = new List<Vector3>(64);

        private ObjectPool<SoldierUnit> _pool;
        private GameSettings _settings;
        private IBattleContext _context;
        private ILaneInputSource _input;
        private int _targetLane;
        private float _laneVelocity;
        private bool _slotsDirty = true;
        private bool _running;
        private float _startZ;
        private float _forwardSpeed;
        private float _distanceTravelled;

        /// <summary>Number of living soldiers.</summary>
        public int SoldierCount => _soldiers.Count;

        /// <summary>Lane the army is moving towards.</summary>
        public int TargetLane => _targetLane;

        /// <summary>Current world X of the army.</summary>
        public float WorldX => transform.position.x;

        /// <summary>Current world Z of the army. Enemies, gates and the camera all read this.</summary>
        public float WorldZ => transform.position.z;

        /// <summary>Meters travelled since the level started.</summary>
        public float DistanceTravelled => _distanceTravelled;

        /// <summary>Current forward speed in units per second.</summary>
        public float ForwardSpeed => _forwardSpeed;

        /// <summary>Configured maximum army size (from GameSettings).</summary>
        public int MaxSoldiers => _settings != null ? _settings.maxSoldiers : 0;

        /// <summary>(current, max)</summary>
        public event Action<int, int> ArmyCountChanged;

        /// <summary>Injected by <see cref="GameManager"/> before Start().</summary>
        public void Initialize(GameSettings settings, IBattleContext context, ILaneInputSource input)
        {
            _settings = settings;
            _context = context;
            _input = input;

            if (formationRoot == null)
            {
                formationRoot = transform;
            }

            if (poolRoot == null)
            {
                poolRoot = transform;
            }
        }

        /// <summary>Called by <see cref="GameManager"/> once every system is wired.</summary>
        public void Begin()
        {
            if (_settings == null || _context == null)
            {
                Debug.LogError("[NumberRush] ArmyController.Begin() called before Initialize().", this);
                return;
            }

            if (soldierPrefab == null)
            {
                Debug.LogError("[NumberRush] ArmyController has no soldier prefab assigned.", this);
                return;
            }

            if (_pool == null)
            {
                _pool = new ObjectPool<SoldierUnit>(
                    CreateSoldier,
                    poolRoot,
                    _settings.startingSoldiers,
                    _settings.maxSoldiers + 8);
            }

            _targetLane = _settings.CenterLane;
            _startZ = _settings.armyZ;
            _distanceTravelled = 0f;
            _forwardSpeed = Mathf.Max(0f, _settings.runnerStartSpeed);
            transform.position = new Vector3(_settings.LaneX(_targetLane), transform.position.y, _startZ);

            _soldiers.Clear();
            _slotsDirty = true;
            AddSoldiers(_settings.startingSoldiers, false);
            _running = true;

            ArmyCountChanged?.Invoke(_soldiers.Count, _settings.maxSoldiers);
        }

        private SoldierUnit CreateSoldier()
        {
            SoldierUnit soldier = Instantiate(soldierPrefab, poolRoot);
            soldier.name = $"Soldier_{soldierPrefab.name}";
            soldier.Initialize(_context, this);
            return soldier;
        }

        private void Update()
        {
            if (!_running || _context == null || _settings == null)
            {
                return;
            }

            if (_context.IsPlaying)
            {
                if (_input != null && _input.TryConsumeLaneStep(out int direction))
                {
                    _targetLane = _settings.ClampLane(_targetLane + direction);
                }

                MoveForward(Time.deltaTime);
            }

            MoveToLane(Time.deltaTime);
            UpdateFormation(Time.deltaTime);
        }

        /// <summary>Automatic forward motion - the player never controls this.</summary>
        private void MoveForward(float deltaTime)
        {
            float speed = _forwardSpeed;
            if (speed <= 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            float step = speed * deltaTime;
            position.z += step;
            transform.position = position;
            _distanceTravelled += step;
        }

        private void MoveToLane(float deltaTime)
        {
            float targetX = _settings.LaneX(_targetLane);
            Vector3 position = transform.position;
            float x = Mathf.SmoothDamp(
                position.x,
                targetX,
                ref _laneVelocity,
                _settings.laneMoveSmoothTime,
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime));

            transform.position = new Vector3(Mathf.Clamp(x, -_settings.TrackHalfWidth, _settings.TrackHalfWidth), position.y, position.z);
        }

        private void UpdateFormation(float deltaTime)
        {
            if (_soldiers.Count == 0)
            {
                return;
            }

            RebuildSlotsIfNeeded();

            float lerp = 1f - Mathf.Exp(-_settings.formationLerpSpeed * Mathf.Max(0.0001f, deltaTime));
            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierUnit soldier = _soldiers[i];
                if (soldier == null)
                {
                    continue;
                }

                int slotIndex = Mathf.Min(i, _slots.Count - 1);
                soldier.transform.localPosition = Vector3.Lerp(
                    soldier.transform.localPosition,
                    _slots[slotIndex],
                    lerp);
            }
        }

        private void RebuildSlotsIfNeeded()
        {
            if (!_slotsDirty)
            {
                return;
            }

            _slotsDirty = false;
            _slots.Clear();
            for (int i = 0; i < _soldiers.Count; i++)
            {
                _slots.Add(ArmyFormation.SlotLocalPosition(i, _soldiers.Count, _settings));
            }
        }

        // ------------------------------------------------------------------------------------
        // Runner control (used by the level manager)
        // ------------------------------------------------------------------------------------

        /// <summary>Sets the forward speed immediately. 0 stops the army.</summary>
        public void SetForwardSpeed(float speed)
        {
            _forwardSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>Stops forward motion (level complete / failed).</summary>
        public void StopRunning()
        {
            _forwardSpeed = 0f;
        }

        /// <summary>Teleports the army to a lane without easing. Used when a level is (re)started.</summary>
        public void SetLane(int lane, bool immediate)
        {
            if (_settings == null)
            {
                return;
            }

            _targetLane = _settings.ClampLane(lane);
            if (immediate)
            {
                _laneVelocity = 0f;
                Vector3 position = transform.position;
                transform.position = new Vector3(_settings.LaneX(_targetLane), position.y, position.z);
            }
        }

        // ------------------------------------------------------------------------------------
        // Army size
        // ------------------------------------------------------------------------------------

        /// <summary>Adds soldiers up to <see cref="GameSettings.maxSoldiers"/>.</summary>
        public void Recruit(int amount)
        {
            if (amount <= 0 || _pool == null || _settings == null)
            {
                return;
            }

            if (_soldiers.Count >= _settings.maxSoldiers)
            {
                return;
            }

            if (AddSoldiers(amount, false) > 0)
            {
                ArmyCountChanged?.Invoke(_soldiers.Count, _settings.maxSoldiers);
            }
        }

        /// <summary>
        /// Forces the army to an exact size, clamped to [1, maxSoldiers].
        /// Number gates use this path so a gate can never wipe the army out - only enemies can.
        /// </summary>
        public void SetSoldierCount(int target)
        {
            if (_pool == null || _settings == null)
            {
                return;
            }

            int clamped = Mathf.Clamp(target, 1, _settings.maxSoldiers);
            int difference = clamped - _soldiers.Count;

            if (difference > 0)
            {
                AddSoldiers(difference, false);
            }
            else if (difference < 0)
            {
                RemoveSoldiers(-difference, false);
            }

            ArmyCountChanged?.Invoke(_soldiers.Count, _settings.maxSoldiers);
        }

        /// <summary>Removes soldiers from the back of the formation. Triggers game over when the army is empty.</summary>
        public void ApplyCasualties(int amount)
        {
            if (amount <= 0 || !_running || _context == null || !_context.IsPlaying)
            {
                return;
            }

            int removed = RemoveSoldiers(amount, true);
            if (removed == 0)
            {
                return;
            }

            _context.ShakeCamera(0.18f + 0.06f * removed);
            ArmyCountChanged?.Invoke(_soldiers.Count, _settings.maxSoldiers);

            if (_soldiers.Count <= 0)
            {
                _context.NotifyArmyWipedOut();
            }
        }

        /// <summary>Applies a gate operation to a soldier count. Never returns less than 1.</summary>
        public static int ComputeGateResult(int currentSoldiers, GateKind kind, int value)
        {
            int count = Mathf.Max(1, currentSoldiers);
            int amount = Mathf.Max(1, Mathf.Abs(value));

            switch (kind)
            {
                case GateKind.Add:
                    count += amount;
                    break;
                case GateKind.Subtract:
                    count -= amount;
                    break;
                case GateKind.Multiply:
                    count *= amount;
                    break;
                case GateKind.Divide:
                    count /= amount;
                    break;
            }

            return Mathf.Max(1, count);
        }

        private int AddSoldiers(int amount, bool playFx)
        {
            int added = 0;
            for (int i = 0; i < amount; i++)
            {
                if (_soldiers.Count >= _settings.maxSoldiers)
                {
                    break;
                }

                SoldierUnit soldier = _pool.Get(transform.position, Quaternion.identity);
                if (soldier == null)
                {
                    Debug.LogWarning("[NumberRush] Soldier pool exhausted - increase maxSoldiers or the pool capacity.", this);
                    break;
                }

                soldier.transform.SetParent(formationRoot, false);
                soldier.transform.localPosition = ArmyFormation.SlotLocalPosition(_soldiers.Count, _soldiers.Count + 1, _settings);
                soldier.ResetForSpawn();
                _soldiers.Add(soldier);
                added++;
            }

            if (added > 0)
            {
                _slotsDirty = true;
            }

            return added;
        }

        private int RemoveSoldiers(int amount, bool playFx)
        {
            int removed = 0;
            for (int i = 0; i < amount && _soldiers.Count > 0; i++)
            {
                RemoveLastSoldier(playFx);
                removed++;
            }

            return removed;
        }

        private void RemoveLastSoldier(bool playFx)
        {
            int lastIndex = _soldiers.Count - 1;
            SoldierUnit soldier = _soldiers[lastIndex];
            _soldiers.RemoveAt(lastIndex);

            if (soldier == null)
            {
                return;
            }

            if (playFx && _context != null && _context.Vfx != null)
            {
                _context.Vfx.PlayBurst(soldier.transform.position + Vector3.up * 0.45f, _settings.playerColor, 4, 0.75f);
            }

            _pool.Release(soldier);
            _slotsDirty = true;
        }
    }
}
