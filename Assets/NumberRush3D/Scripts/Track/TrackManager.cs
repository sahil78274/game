using System.Collections.Generic;
using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.Track
{
    /// <summary>
    /// Endless runner track.
    ///
    /// A fixed ring of pooled <see cref="TrackSegment"/> slices follows the army: slices are placed
    /// contiguously ahead of the player and the ones that fall behind are teleported to the front,
    /// so there is never a gap and the number of instantiated objects is constant
    /// (segmentCount, 8 by default). No physics, no colliders, four shared materials in total.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackManager : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Optional: the legacy static Track object built by the setup menu. Hidden at Begin so only the endless slices render.")]
        [SerializeField] private GameObject legacyTrack;

        [Tooltip("Parent of the pooled slices. Defaults to this transform.")]
        [SerializeField] private Transform segmentRoot;

        [Header("Streaming")]
        [Tooltip("How far in front of the army slices are kept alive, in metres.")]
        [SerializeField] private float lookAhead = 70f;

        [Tooltip("Slices are recycled once their far edge is this far behind the army.")]
        [SerializeField] private float recycleBehind = 30f;

        [Tooltip("Colour of the driving surface.")]
        [SerializeField] private Color groundTint = Color.white;

        private readonly Queue<TrackSegment> _active = new Queue<TrackSegment>(16);

        private ObjectPool<TrackSegment> _pool;
        private GameSettings _settings;
        private IBattleContext _context;
        private TrackSpec _spec;
        private TrackSegment _template;
        private Material _groundMaterial;
        private Material _stripeMaterial;
        private Material _railMaterial;
        private Material _decorMaterial;
        private float _frontierZ;
        private int _seed;
        private bool _running;

        /// <summary>Number of slices currently in the world.</summary>
        public int ActiveSegments => _active.Count;

        /// <summary>World Z of the far edge of the track that exists right now.</summary>
        public float TrackEndZ => _frontierZ;

        public void Initialize(GameSettings settings, IBattleContext context)
        {
            if (_pool != null)
            {
                // Already initialised (another manager or a second call): never build twice.
                _settings = settings;
                _context = context;
                return;
            }

            _settings = settings;
            _context = context;

            if (segmentRoot == null)
            {
                segmentRoot = transform;
            }

            float segmentLength = Mathf.Max(6f, settings.trackSegmentLength);
            lookAhead = Mathf.Max(lookAhead, segmentLength);
            recycleBehind = Mathf.Max(recycleBehind, segmentLength * 0.5f);

            _groundMaterial = RuntimeAssets.CreateLitMaterial(settings.groundColor * groundTint, "Track_Ground");
            _stripeMaterial = RuntimeAssets.CreateLitMaterial(settings.laneColor, "Track_Stripe");
            _railMaterial = RuntimeAssets.CreateLitMaterial(settings.railColor, "Track_Rail");
            _decorMaterial = RuntimeAssets.CreateLitMaterial(settings.playerAccentColor, "Track_Decor");

            _spec = new TrackSpec
            {
                Length = segmentLength,
                HalfWidth = settings.TrackHalfWidth,
                LaneWidth = settings.laneWidth,
                LaneCount = settings.laneCount,
                Ground = _groundMaterial,
                Stripe = _stripeMaterial,
                Rail = _railMaterial,
                Decor = _decorMaterial,
            };

            int count = Mathf.Clamp(settings.trackSegmentCount, 3, 20);
            _template = BuildTemplate();
            _pool = new ObjectPool<TrackSegment>(
                () => Instantiate(_template, segmentRoot),
                segmentRoot,
                count,
                count);
        }

        /// <summary>Puts the track back to the start line. Called by <see cref="GameManager"/>.</summary>
        public void Begin(float armyZ)
        {
            if (_pool == null || _settings == null)
            {
                return;
            }

            if (legacyTrack != null)
            {
                legacyTrack.SetActive(false);
            }

            while (_active.Count > 0)
            {
                _pool.Release(_active.Dequeue());
            }

            int count = Mathf.Clamp(_settings.trackSegmentCount, 3, 20);
            float segmentLength = _spec.Length;

            // Start one slice behind the army so the player never sees the world begin.
            _frontierZ = armyZ - segmentLength;

            for (int i = 0; i < count; i++)
            {
                TrackSegment segment = _pool.Get(new Vector3(0f, 0f, _frontierZ), Quaternion.identity);
                if (segment == null)
                {
                    break;
                }

                Place(segment, _frontierZ);
                _active.Enqueue(segment);
                _frontierZ += segmentLength;
            }

            _running = true;
        }

        private void Update()
        {
            if (!_running || _settings == null || _pool == null)
            {
                return;
            }

            float armyZ = _context != null && _context.Army != null
                ? _context.Army.WorldZ
                : _settings.armyZ;

            // Extend forward.
            while (_frontierZ < armyZ + lookAhead)
            {
                TrackSegment segment = _pool.Get(new Vector3(0f, 0f, _frontierZ), Quaternion.identity);
                if (segment == null)
                {
                    break;
                }

                Place(segment, _frontierZ);
                _active.Enqueue(segment);
                _frontierZ += _spec.Length;
            }

            // Recycle whatever the army has left behind. Peek() is always the oldest slice.
            while (_active.Count > 0 && _active.Peek().EndZ < armyZ - recycleBehind)
            {
                TrackSegment segment = _active.Dequeue();
                Place(segment, _frontierZ);
                _active.Enqueue(segment);
                _frontierZ += _spec.Length;
            }
        }

        private void Place(TrackSegment segment, float z)
        {
            segment.transform.position = new Vector3(0f, 0f, z);
            _seed = unchecked(_seed * 1103515245 + 12345);
            segment.RandomizeDecor(_seed, _spec, _settings.trackDecorJitter);
        }

        private TrackSegment BuildTemplate()
        {
            GameObject root = new GameObject("TrackSegmentTemplate");
            root.transform.SetParent(transform, false);
            root.SetActive(false);

            TrackSegment segment = root.AddComponent<TrackSegment>();
            segment.Build(_spec);
            return segment;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_groundMaterial);
            DestroyMaterial(_stripeMaterial);
            DestroyMaterial(_railMaterial);
            DestroyMaterial(_decorMaterial);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
