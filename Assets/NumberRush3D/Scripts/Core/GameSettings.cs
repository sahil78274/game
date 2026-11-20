using UnityEngine;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Single source of truth for every tunable value in the game.
    /// Create one asset (Tools > NUMBER RUSH 3D > Build ... creates it automatically)
    /// and assign it to <see cref="GameManager"/>. Nothing else is allowed to hard-code tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "NUMBER RUSH 3D/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        [Header("Lanes & Track")]
        [Tooltip("How many lanes the army can swipe between.")]
        [Range(3, 9)] public int laneCount = 5;

        [Tooltip("World space distance between two lane centers.")]
        [Range(1f, 6f)] public float laneWidth = 2.8f;

        [Tooltip("Z position of the player army. Enemies walk towards -Z.")]
        public float armyZ = -8f;

        [Tooltip("When an enemy crosses this Z it rams the army instead of walking further.")]
        public float defenseLineZ = -2.5f;

        [Tooltip("Z position where enemies are spawned.")]
        public float spawnZ = 15f;

        [Header("Army")]
        [Range(1, 200)] public int startingSoldiers = 10;
        [Range(1, 300)] public int maxSoldiers = 60;
        [Tooltip("Horizontal spacing between soldiers inside one formation row.")]
        [Range(0.2f, 1.5f)] public float soldierSpacing = 0.46f;
        [Tooltip("Spacing between formation rows (front to back).")]
        [Range(0.2f, 1.5f)] public float soldierRowSpacing = 0.52f;
        [Range(2, 12)] public int maxFormationColumns = 7;
        [Tooltip("SmoothDamp time used when the army slides to the next lane.")]
        [Range(0.02f, 0.6f)] public float laneMoveSmoothTime = 0.13f;
        [Tooltip("How quickly soldiers snap to their formation slot.")]
        [Range(1f, 40f)] public float formationLerpSpeed = 14f;

        [Header("Shooting")]
        [Tooltip("Shots per second fired by a single soldier. 0.9 means one bullet every ~1.1 s.")]
[Range(0.2f, 5f)] public float shotsPerSecond = 0.9f;
        [Tooltip("Damage per bullet.")]
        [Range(0.1f, 50f)] public float bulletDamage = 1f;
        [Range(10f, 120f)] public float bulletSpeed = 42f;
        [Tooltip("Sphere cast radius used for bullet hit detection.")]
        [Range(0.02f, 0.5f)] public float bulletRadius = 0.12f;
        [Range(0.2f, 5f)] public float bulletMaxLifetime = 1.6f;
        [Tooltip("How far a soldier looks for a target.")]
        [Range(5f, 80f)] public float targetSearchRange = 45f;
        [Tooltip("Seconds between target re-acquisitions. Staggered per soldier.")]
        [Range(0.05f, 1f)] public float targetRefreshInterval = 0.22f;
        [Tooltip("Random cone (degrees) added to each shot so the army does not look robotic.")]
        [Range(0f, 6f)] public float aimSpreadDegrees = 1.4f;

        [Header("Enemies")]
        [Tooltip("Layer that bullets collide with. The builder creates it for you.")]
        public string enemyLayerName = "Enemy";
        [Tooltip("Global multiplier on every EnemyType.moveSpeed.")]
        [Range(0.2f, 3f)] public float enemySpeedMultiplier = 1f;
        [Range(0.1f, 5f)] public float spawnStartInterval = 1.35f;
        [Range(0.05f, 2f)] public float spawnMinInterval = 0.34f;
        [Tooltip("Seconds of play needed to reach maximum difficulty.")]
        [Range(10f, 600f)] public float difficultyRampSeconds = 95f;
        [Range(1, 6)] public int spawnBurstMin = 1;
        [Range(1, 6)] public int spawnBurstMax = 3;
        [Range(1, 80)] public int maxConcurrentEnemies = 26;
        [Tooltip("Random +/- offset on the spawn Z so waves do not look perfectly aligned.")]
        [Range(0f, 6f)] public float spawnJitterZ = 2.5f;
        [Tooltip("Random +/- offset on the spawn X so enemies are not pixel perfect on the lane.")]
        [Range(0f, 1.2f)] public float spawnJitterX = 0.35f;

        [Header("Rewards")]
        [Tooltip("Every N kills the army receives one extra soldier.")]
        [Range(1, 100)] public int killsPerRecruit = 8;

        [Header("Camera Framing")]
        [Range(10f, 80f)] public float cameraPitch = 42f;
        [Range(20f, 80f)] public float cameraFieldOfView = 45f;
        [Tooltip("Camera height above the ground plane.")]
        [Range(4f, 40f)] public float cameraHeight = 15f;
        [Tooltip("Extra world units of track kept visible below the army.")]
        [Range(0f, 10f)] public float cameraBottomPadding = 2.5f;
        [Tooltip("Extra width kept visible around the outer lanes (1.0 = exactly the track).")]
        [Range(1f, 1.5f)] public float cameraTrackMargin = 1.12f;
        [Tooltip("0 = static camera, 1 = camera x locked to the army x.")]
        [Range(0f, 1f)] public float cameraLateralFollow = 0.32f;
        [Range(0.02f, 1f)] public float cameraFollowSmoothTime = 0.22f;
        [Range(1f, 30f)] public float shakeDecay = 7f;
        [Tooltip("Maximum camera shake offset in world units.")]
        [Range(0f, 1f)] public float maxShakeOffset = 0.35f;

        [Header("Performance")]
        [Tooltip("Application.targetFrameRate. 60 is the sweet spot for hyper-casual.")]
        [Range(30, 120)] public int targetFrameRate = 60;
        [Tooltip("Maximum number of bullets alive at the same time. The pool hard-caps to this value.")]
        [Range(20, 600)] public int maxBulletsInFlight = 220;

        [Header("Colors")]
        public Color playerColor = new Color(0.16f, 0.45f, 0.95f, 1f);
        public Color playerAccentColor = new Color(0.62f, 0.82f, 1f, 1f);
        public Color enemyColor = new Color(0.91f, 0.19f, 0.23f, 1f);
        public Color bulletColor = new Color(1f, 0.87f, 0.3f, 1f);
        public Color groundColor = new Color(0.09f, 0.11f, 0.17f, 1f);
        public Color laneColor = new Color(0.15f, 0.19f, 0.28f, 1f);
        public Color skyColor = new Color(0.035f, 0.045f, 0.08f, 1f);
        public Color railColor = new Color(0.22f, 0.28f, 0.42f, 1f);

        [Header("Runner")]
        [Tooltip("Forward speed of the army when a level starts (units per second).")]
        [Range(0f, 25f)] public float runnerStartSpeed = 7f;

        [Tooltip("Forward speed at the end of a level. 0 = constant speed. Values above runnerStartSpeed create a speed ramp.")]
        [Range(0f, 30f)] public float runnerEndSpeed = 0f;

        [Header("Infinite Track")]
        [Tooltip("Length of one pooled track segment. Must match the TrackSegment prefab.")]
        [Range(5f, 60f)] public float trackSegmentLength = 20f;

        [Tooltip("How many pooled segments are kept alive. Visible distance / segment length + 3 is a good value.")]
        [Range(3, 20)] public int trackSegmentCount = 8;

        [Tooltip("A segment is recycled once it is this far behind the army.")]
        [Range(5f, 80f)] public float trackRecycleBehind = 30f;

        [Tooltip("Random +/- offset applied to track decorations.")]
        [Range(0f, 1.5f)] public float trackDecorJitter = 0.4f;

        [Header("Runner Enemies")]
        [Tooltip("Enemies spawn this many units ahead of the army.")]
        [Range(10f, 80f)] public float enemySpawnAhead = 30f;

        [Tooltip("Enemies further behind the army than this are recycled (missed encounters).")]
        [Range(2f, 40f)] public float enemyDespawnBehind = 12f;

        [Tooltip("Distance in front of the army where an enemy rams the army.")]
        [Range(0.5f, 12f)] public float ramDistanceAhead = 4f;

        [Header("Number Gates")]
        [Tooltip("Gates are spawned when the army is at most this far from the row.")]
        [Range(10f, 60f)] public float gateLookAhead = 26f;

        [Tooltip("Distance in meters between gate rows.")]
        [Range(8f, 60f)] public float gateSpacing = 22f;

        [Tooltip("Distance from the start line to the first gate row.")]
        [Range(5f, 60f)] public float gateFirstDistance = 18f;

        [Tooltip("Extra gates per row: 0 = 2 gates, 1 = up to 3 gates, 2 = up to 4 gates.")]
        [Range(0, 2)] public int gateExtraGates = 1;

        [Tooltip("Chance that a non-guaranteed lane receives a negative gate.")]
        [Range(0f, 1f)] public float gateNegativeChance = 0.55f;

        [Tooltip("How long the gate feedback toast stays on screen (seconds).")]
        [Range(0.5f, 4f)] public float gateToastDuration = 1.4f;

        [Header("Levels & Economy")]
        [Tooltip("Coins awarded per enemy killed, on top of the level reward.")]
        [Range(0, 10)] public int coinsPerKill = 1;

        [Tooltip("Level loaded when no saved progress exists.")]
        [Range(1, 200)] public int firstLevel = 1;

        [Tooltip("Seconds of slow motion before the level complete / failed panel appears.")]
        [Range(0f, 3f)] public float levelEndSlowMotionDuration = 0.55f;

        [System.NonSerialized] private LayerMask? _enemyMaskCache;

        /// <summary>Half of the total playable width (outer lane center + half a lane).</summary>
        public float TrackHalfWidth => laneCount * laneWidth * 0.5f;

        /// <summary>World X of a lane index (0 = left-most lane).</summary>
        public float LaneX(int laneIndex)
        {
            int clamped = Mathf.Clamp(laneIndex, 0, Mathf.Max(0, laneCount - 1));
            return (clamped - (laneCount - 1) * 0.5f) * laneWidth;
        }

        public int ClampLane(int laneIndex)
        {
            return Mathf.Clamp(laneIndex, 0, Mathf.Max(0, laneCount - 1));
        }

        public int CenterLane => Mathf.Max(0, laneCount - 1) / 2;

        /// <summary>World Z at which an enemy rams an army that is currently at <paramref name="armyZ"/>.</summary>
        public float RamZ(float armyZ)
        {
            return armyZ + ramDistanceAhead;
        }

        /// <summary>0 at run start, 1 once <see cref="difficultyRampSeconds"/> has elapsed.</summary>
        public float Difficulty01(float elapsedSeconds)
        {
            if (difficultyRampSeconds <= 0.01f)
            {
                return 1f;
            }

            return Mathf.Clamp01(elapsedSeconds / difficultyRampSeconds);
        }

        public float SpawnIntervalAt(float elapsedSeconds)
        {
            return Mathf.Lerp(spawnStartInterval, spawnMinInterval, Difficulty01(elapsedSeconds));
        }

        public int SpawnBurstAt(float elapsedSeconds)
        {
            int value = Mathf.RoundToInt(Mathf.Lerp(spawnBurstMin, spawnBurstMax, Difficulty01(elapsedSeconds)));
            return Mathf.Clamp(value, spawnBurstMin, spawnBurstMax);
        }

        /// <summary>
        /// Layer mask resolved from <see cref="enemyLayerName"/>. Falls back to "everything" so a
        /// mis-configured project still shoots (bullets filter by <see cref="IDamageable"/> anyway).
        /// </summary>
        public LayerMask EnemyMask
        {
            get
            {
                if (_enemyMaskCache == null)
                {
                    int mask = LayerMask.GetMask(enemyLayerName);
                    if (mask == 0)
                    {
                        Debug.LogWarning($"[NumberRush] Layer '{enemyLayerName}' does not exist. Bullets will test against every collider.");
                        _enemyMaskCache = ~0;
                    }
                    else
                    {
                        _enemyMaskCache = mask;
                    }
                }

                return _enemyMaskCache.Value;
            }
        }

        private void OnValidate()
        {
            laneCount = Mathf.Clamp(laneCount, 2, 9);
            laneWidth = Mathf.Max(0.5f, laneWidth);
            startingSoldiers = Mathf.Clamp(startingSoldiers, 1, maxSoldiers);
            maxSoldiers = Mathf.Max(startingSoldiers, maxSoldiers);
            maxFormationColumns = Mathf.Max(1, maxFormationColumns);
            spawnStartInterval = Mathf.Max(spawnMinInterval, spawnStartInterval);
            spawnBurstMax = Mathf.Max(spawnBurstMin, spawnBurstMax);
            targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
            _enemyMaskCache = null;
        }
    }
}
