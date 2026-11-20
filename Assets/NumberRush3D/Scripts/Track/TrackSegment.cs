using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NumberRush3D.Track
{
    /// <summary>Colours + dimensions a <see cref="TrackSegment"/> is built from.</summary>
    public struct TrackSpec
    {
        public float Length;
        public float HalfWidth;
        public float LaneWidth;
        public int LaneCount;
        public Material Ground;
        public Material Stripe;
        public Material Rail;
        public Material Decor;
    }

    /// <summary>
    /// One 20 m slice of the endless track, built entirely from primitives at runtime (the project
    /// ships no track art). Segments are pooled and recycled by <see cref="TrackManager"/>:
    /// nothing is ever instantiated once the pool is warm.
    ///
    /// The slice is authored in local space from z = 0 (near edge) to z = Length (far edge), so
    /// moving the root is the only thing needed to recycle it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackSegment : MonoBehaviour
    {
        private const int DecorCount = 7;
        private const float FloorThickness = 0.4f;
        private const float FloorTop = 0.02f;

        private readonly List<Transform> _decor = new List<Transform>(DecorCount);

        /// <summary>Length of this slice in metres.</summary>
        public float Length { get; private set; }

        /// <summary>World Z of the near edge.</summary>
        public float StartZ => transform.position.z;

        /// <summary>World Z of the far edge.</summary>
        public float EndZ => transform.position.z + Length;

        /// <summary>Builds the geometry once, when the segment is created by the pool factory.</summary>
        public void Build(TrackSpec spec)
        {
            Length = Mathf.Max(1f, spec.Length);
            _decor.Clear();

            // Wide enough to carry the decoration on both sides, not just the driving surface.
            float width = spec.HalfWidth * 2f + 20f;
            float centerZ = Length * 0.5f;

            // Floor. Top surface sits 2 cm above the legacy ground plane so the two never z-fight.
            CreatePart(
                "Floor",
                new Vector3(0f, FloorTop - FloorThickness * 0.5f, centerZ),
                new Vector3(width, FloorThickness, Length),
                spec.Ground,
                PrimitiveType.Cube,
                castsShadows: false);

            // Lane dividers.
            int dividers = Mathf.Max(0, spec.LaneCount - 1);
            for (int i = 1; i <= dividers; i++)
            {
                float x = -spec.HalfWidth + spec.LaneWidth * i;
                CreatePart(
                    $"LaneDivider_{i}",
                    new Vector3(x, FloorTop + 0.01f, centerZ),
                    new Vector3(0.08f, 0.02f, Length - 0.4f),
                    spec.Stripe,
                    PrimitiveType.Cube,
                    castsShadows: false);
            }

            // Kerbs / barriers on both sides.
            float railX = spec.HalfWidth + 0.45f;
            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(
                    side < 0 ? "Rail_L" : "Rail_R",
                    new Vector3(railX * side, FloorTop + 0.16f, centerZ),
                    new Vector3(0.7f, 0.32f, Length),
                    spec.Rail,
                    PrimitiveType.Cube,
                    castsShadows: false);
            }

            // Decoration slots, repositioned on every recycle.
            for (int i = 0; i < DecorCount; i++)
            {
                // Cube and sphere are exactly 1 unit tall at scale 1, so scale.y * 0.5 rests them
                // on the floor. Cylinders are 2 units tall and would half sink - keep them out.
                PrimitiveType shape = i % 3 == 1 ? PrimitiveType.Sphere : PrimitiveType.Cube;

                Transform decor = CreatePart(
                    $"Decor_{i}",
                    new Vector3(0f, 0.5f, 0f),
                    Vector3.one,
                    spec.Decor,
                    shape,
                    castsShadows: true);

                _decor.Add(decor);
            }
        }

        /// <summary>Shuffles the decoration of this slice. Deterministic per seed, allocation free.</summary>
        public void RandomizeDecor(int seed, in TrackSpec spec, float jitter)
        {
            if (_decor.Count == 0)
            {
                return;
            }

            System.Random random = new System.Random(seed);
            float minX = spec.HalfWidth + 2.5f;
            float maxX = spec.HalfWidth + 7.5f;

            for (int i = 0; i < _decor.Count; i++)
            {
                Transform decor = _decor[i];
                if (decor == null)
                {
                    continue;
                }

                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * Mathf.Lerp(minX, maxX, (float)random.NextDouble());
                float z = Mathf.Lerp(0f, Length, (float)random.NextDouble());

                float height = 0.6f + (float)random.NextDouble() * 2.2f;
                float radius = 0.35f + (float)random.NextDouble() * 0.5f;

                float jitteredX = x + ((float)random.NextDouble() * 2f - 1f) * jitter;
                decor.localPosition = new Vector3(jitteredX, height * 0.5f + FloorTop, z);
                decor.localScale = new Vector3(radius * 2f, height, radius * 2f);
                decor.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
        }

        private Transform CreatePart(string name, Vector3 localPosition, Vector3 localScale, Material material, PrimitiveType shape, bool castsShadows)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;

            // Nothing on the track needs a collider: bullets use only the Enemy layer and the army
            // never does physics queries.
            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null)
            {
                Destroy(partCollider);
            }

            Transform partTransform = part.transform;
            partTransform.SetParent(transform, false);
            partTransform.localPosition = localPosition;
            partTransform.localScale = localScale;
            partTransform.localRotation = Quaternion.identity;

            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = !castsShadows;
            }

            return partTransform;
        }
    }
}
