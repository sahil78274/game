using System.Collections.Generic;
using NumberRush3D.Army;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Gates
{
    /// <summary>
    /// Streams number gate rows down the endless track, always a little ahead of the army.
    ///
    /// Each row is two gates spanning the whole track, so the player is forced to choose one of
    /// them. Rows come from a level's scripted pattern when one is supplied (campaign) or are
    /// generated from the tuning asset (Free Run). Every gate is pooled, so a run never allocates
    /// a GameObject after warm up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateManager : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Parent of the pooled gates. Defaults to this transform.")]
        [SerializeField] private Transform gateRoot;

        [Header("Streaming")]
        [Tooltip("Distance between two gate rows, in metres.")]
        [SerializeField] private float rowSpacing = 22f;

        [Tooltip("Distance between the rows of one cluster (when extra gates per row are enabled).")]
        [SerializeField] private float clusterSpacing = 8f;

        [Tooltip("Gates further behind the army than this are recycled.")]
        [SerializeField] private float recycleDistance = 26f;

        [Tooltip("Gates kept warm in the pool.")]
        [Range(2, 32)] [SerializeField] private int prewarm = 8;

        private static readonly GateChoice[] PositivePool =
        {
            new GateChoice(GateKind.Add, 5),
            new GateChoice(GateKind.Add, 10),
            new GateChoice(GateKind.Add, 20),
            new GateChoice(GateKind.Multiply, 2),
            new GateChoice(GateKind.Multiply, 3),
        };

        private static readonly GateChoice[] NegativePool =
        {
            new GateChoice(GateKind.Subtract, 5),
            new GateChoice(GateKind.Subtract, 10),
            new GateChoice(GateKind.Divide, 2),
        };

        private readonly List<NumberGate> _live = new List<NumberGate>(16);

        private ObjectPool<NumberGate> _pool;
        private GameSettings _settings;
        private IBattleContext _context;
        private ArmyController _army;
        private Material _postMaterial;
        private Material _panelMaterial;
        private GateRowDefinition[] _scriptedRows;
        private float _nextRowZ;
        private int _rowIndex;
        private bool _running;

        /// <summary>Rows generated since <see cref="Begin"/>.</summary>
        public int RowsSpawned { get; private set; }

        /// <summary>Gates the army has actually passed through.</summary>
        public int GatesFired { get; private set; }

        /// <summary>Last gate that fired, useful for UI feedback.</summary>
        public NumberGate LastFired { get; private set; }

        public void Initialize(GameSettings settings, IBattleContext context, ArmyController army)
        {
            if (_pool != null)
            {
                // Already initialised: refresh the context only, never duplicate the pool.
                _settings = settings;
                _context = context;
                _army = army;
                return;
            }

            _settings = settings;
            _context = context;
            _army = army;

            if (gateRoot == null)
            {
                gateRoot = transform;
            }

            rowSpacing = Mathf.Max(8f, settings.gateSpacing > 0f ? settings.gateSpacing : rowSpacing);

            _postMaterial = RuntimeAssets.CreateLitMaterial(Color.white, "Gate_Post", 0.2f);
            _panelMaterial = RuntimeAssets.CreateLitMaterial(Color.white, "Gate_Panel", 0.05f);

            NumberGate template = NumberGate.CreateTemplate(transform, _postMaterial, _panelMaterial);
            _pool = new ObjectPool<NumberGate>(
                () => Instantiate(template, gateRoot),
                gateRoot,
                prewarm,
                Mathf.Max(8, prewarm * 2));
        }

        /// <summary>
        /// Overrides the procedural pattern with a level authored sequence. The sequence loops, so a
        /// short list still covers a long track. Pass null (or an empty array) to go procedural.
        /// </summary>
        public void SetScriptedRows(GateRowDefinition[] rows)
        {
            _scriptedRows = rows != null && rows.Length > 0 ? rows : null;
        }

        /// <summary>Puts the gate stream back to its start state.</summary>
        public void Begin()
        {
            Clear();

            if (_pool == null || _settings == null)
            {
                return;
            }

            float armyZ = _army != null ? _army.WorldZ : _settings.armyZ;
            _nextRowZ = armyZ + Mathf.Max(4f, _settings.gateFirstDistance);
            _rowIndex = 0;
            RowsSpawned = 0;
            GatesFired = 0;
            LastFired = null;
            _running = true;

            // Pre-place the first rows so gates are already visible on the starting line.
            EnsureRows(armyZ + Mathf.Max(4f, _settings.gateLookAhead));
        }

        /// <summary>Recycles every live gate (level restart / end of run).</summary>
        public void Clear()
        {
            _running = false;

            for (int i = 0; i < _live.Count; i++)
            {
                NumberGate gate = _live[i];
                if (gate == null)
                {
                    continue;
                }

                gate.Fired -= HandleGateFired;
                if (_pool != null)
                {
                    _pool.Release(gate);
                }
                else
                {
                    gate.gameObject.SetActive(false);
                }
            }

            _live.Clear();
        }

        private void Update()
        {
            if (!_running || _settings == null || _pool == null)
            {
                return;
            }

            float armyZ = _army != null ? _army.WorldZ : _settings.armyZ;

            EnsureRows(armyZ + Mathf.Max(4f, _settings.gateLookAhead));
            RecycleBehind(armyZ - recycleDistance);
        }

        private void EnsureRows(float untilZ)
        {
            float guard = 0f;
            while (_nextRowZ < untilZ && guard < 64f)
            {
                guard += 1f;
                SpawnRow(_nextRowZ);
                _nextRowZ += NextRowGap();
            }
        }

        private void RecycleBehind(float cutoffZ)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                NumberGate gate = _live[i];
                if (gate == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                if (gate.transform.position.z >= cutoffZ)
                {
                    continue;
                }

                gate.Fired -= HandleGateFired;
                _pool.Release(gate);
                _live.RemoveAt(i);
            }
        }

        private float NextRowGap()
        {
            int extra = Mathf.Clamp(_settings.gateExtraGates, 0, 3);
            float cluster = extra * clusterSpacing;
            return Mathf.Max(rowSpacing, cluster + rowSpacing * 0.5f);
        }

        private void SpawnRow(float z)
        {
            GateRowDefinition row = NextRow();
            float halfWidth = _settings.TrackHalfWidth;

            SpawnHalf(row.left, -halfWidth * 0.5f, halfWidth, z, false);
            SpawnHalf(row.right, halfWidth * 0.5f, halfWidth, z, true);

            int extra = Mathf.Clamp(_settings.gateExtraGates, 0, 3);
            for (int i = 1; i <= extra; i++)
            {
                GateRowDefinition clusterRow = NextRow();
                float clusterZ = z + i * clusterSpacing;
                SpawnHalf(clusterRow.left, -halfWidth * 0.5f, halfWidth, clusterZ, false);
                SpawnHalf(clusterRow.right, halfWidth * 0.5f, halfWidth, clusterZ, true);
            }

            RowsSpawned++;
        }

        private void SpawnHalf(GateChoice choice, float centerX, float width, float z, bool isRightHalf)
        {
            NumberGate gate = _pool.Get(new Vector3(centerX, 0f, z), Quaternion.identity);
            if (gate == null)
            {
                return;
            }

            gate.Fired -= HandleGateFired;
            gate.Fired += HandleGateFired;
            gate.Configure(choice, centerX, width, z, _context, _army, isRightHalf);
            _live.Add(gate);
        }

        private GateRowDefinition NextRow()
        {
            if (_scriptedRows != null)
            {
                GateRowDefinition scripted = _scriptedRows[_rowIndex % _scriptedRows.Length];
                _rowIndex++;
                return scripted;
            }

            _rowIndex++;

            GateChoice positive = PositivePool[Random.Range(0, PositivePool.Length)];

            if (Random.value > Mathf.Clamp01(_settings.gateNegativeChance))
            {
                GateChoice anotherPositive = PositivePool[Random.Range(0, PositivePool.Length)];
                return GateRowDefinition.RandomSide(positive, anotherPositive);
            }

            GateChoice negative = NegativePool[Random.Range(0, NegativePool.Length)];
            return GateRowDefinition.RandomSide(positive, negative);
        }

        private void HandleGateFired(NumberGate gate)
        {
            GatesFired++;
            LastFired = gate;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
