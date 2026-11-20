using System;
using System.Collections.Generic;
using NumberRush3D.Controls;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Army
{
    /// <summary>
    /// The blue crowd: lane movement, formation layout, size bookkeeping (recruits / casualties)
    /// and the pool of <see cref="SoldierUnit"/> instances.
    ///
    /// The controller never talks to enemies or bullets directly - soldiers pull their own targets
    /// and the army asks the <see cref="IBattleContext"/> for a camera shake.
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

        /// <summary>Number of living soldiers.</summary>
        public int SoldierCount => _soldiers.Count;

        /// <summary>Lane the army is moving towards.</summary>
        public int TargetLane => _targetLane;

        /// <summary>Current world X of the army.</summary>
        public float WorldX => transform.position.x;

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
            transform.position = new Vector3(_settings.LaneX(_targetLane), transform.position.y, _settings.armyZ);

            _soldiers.Clear();
            _slotsDirty = true;
            AddSoldiers(_settings.startingSoldiers);
            _running = true;
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

            if (_context.IsPlaying && _input != null && _input.TryConsumeLaneStep(out int direction))
            {
                _targetLane = _settings.ClampLane(_targetLane + direction);
            }

            MoveArmy();
            UpdateFormation(Time.deltaTime);
        }

        private void MoveArmy()
        {
            float targetX = _settings.LaneX(_targetLane);
            Vector3 position = transform.position;
            float x = Mathf.SmoothDamp(position.x, targetX, ref _laneVelocity, _settings.laneMoveSmoothTime, Mathf.Infinity, Time.deltaTime);
            transform.position = new Vector3(x, position.y, _settings.armyZ);
        }

        private void UpdateFormation(float deltaTime)
        {
            if (_soldiers.Count == 0)
            {
                return;
            }

            RebuildSlotsIfNeeded();

            float lerp = 1f - Mathf.Exp(-_settings.formationLerpSpeed * deltaTime);
            for (int i = 0; i < _soldiers.Count; i++)
            {
                SoldierUnit soldier = _soldiers[i];
                if (soldier == null)
                {
                    continue;
                }

                // Small per-soldier delay makes the crowd ripple instead of moving as one block.
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
        // Army size
        // ------------------------------------------------------------------------------------

        /// <summary>Adds soldiers up to <see cref="GameSettings.maxSoldiers"/>.</summary>
        public void Recruit(int amount)
        {
            if (amount <= 0 || _pool == null)
            {
                return;
            }

            if (_soldiers.Count >= _settings.maxSoldiers)
            {
                return;
            }

            AddSoldiers(amount);
        }

        /// <summary>Removes soldiers from the back of the formation. Triggers game over when the army is empty.</summary>
        public void ApplyCasualties(int amount)
        {
            if (amount <= 0 || !_running || _context == null || !_context.IsPlaying)
            {
                return;
            }

            int removed = 0;
            for (int i = 0; i < amount && _soldiers.Count > 0; i++)
            {
                RemoveLastSoldier();
                removed++;
            }

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

        private void AddSoldiers(int amount)
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

            if (added == 0)
            {
                return;
            }

            _slotsDirty = true;
            ArmyCountChanged?.Invoke(_soldiers.Count, _settings.maxSoldiers);
        }

        private void RemoveLastSoldier()
        {
            int lastIndex = _soldiers.Count - 1;
            SoldierUnit soldier = _soldiers[lastIndex];
            _soldiers.RemoveAt(lastIndex);

            if (soldier == null)
            {
                return;
            }

            if (_context.Vfx != null)
            {
                _context.Vfx.PlayBurst(soldier.transform.position + Vector3.up * 0.45f, _settings.playerColor, 4, 0.75f);
            }

            _pool.Release(soldier);
            _slotsDirty = true;
        }
    }
}
