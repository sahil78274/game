using NumberRush3D.Army;
using NumberRush3D.Combat;
using NumberRush3D.Controls;
using NumberRush3D.Core;
using NumberRush3D.Enemies;
using NumberRush3D.UI;
using NumberRush3D.View;
using UnityEditor;
using UnityEngine;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// Scene assembly half of the generator (see NumberRushBuilder.cs for the entry point).
    /// </summary>
    public static partial class NumberRushBuilder
    {
        private sealed class UiRefs
        {
            public HudController hud;
            public GameOverPanel gameOver;
        }

        private static void BuildSceneObjects(GameSettings settings, Prefabs prefabs, Art art, int enemyLayer)
        {
            GameObject environment = new GameObject("Environment");
            BuildEnvironment(settings, art, environment.transform);

            CameraRig cameraRig = CreateCamera(settings);
            CreateLighting(settings);

            ArmyController army = CreateArmy(settings, prefabs, art);
            EnemySpawner spawner = CreateSpawner(prefabs);
            BulletPool bulletPool = CreateBulletPool(prefabs);
            VfxPool vfxPool = CreateVfxPool(prefabs);
            UiRefs ui = BuildUi(settings);

            // --- Systems (composition root) ------------------------------------------------
            GameObject systems = new GameObject("Systems");
            EnemyRegistry registry = systems.AddComponent<EnemyRegistry>();
            SwipeInput swipeInput = systems.AddComponent<SwipeInput>();
            GameManager manager = systems.AddComponent<GameManager>();

            SetField(manager, "settings", settings);
            SetField(manager, "army", army);
            SetField(manager, "spawner", spawner);
            SetField(manager, "bulletPool", bulletPool);
            SetField(manager, "vfxPool", vfxPool);
            SetField(manager, "enemies", registry);
            SetField(manager, "cameraRig", cameraRig);
            SetField(manager, "inputSource", swipeInput);
            SetField(manager, "hud", ui.hud);
            SetField(manager, "gameOverPanel", ui.gameOver);

            Debug.Log("[NumberRush] Scene assembled.");
        }

        // ------------------------------------------------------------------------------------
        // Environment
        // ------------------------------------------------------------------------------------

        private static void BuildEnvironment(GameSettings settings, Art art, Transform parent)
        {
            float halfWidth = settings.TrackHalfWidth;
            float nearZ = settings.armyZ - 12f;
            float farZ = settings.spawnZ + settings.spawnJitterZ + 8f;
            float length = farZ - nearZ;
            float centerZ = (nearZ + farZ) * 0.5f;

            // Ground slab: top face sits exactly on y = 0.
            CreateCube("Ground", parent,
                new Vector3(0f, -0.5f, centerZ),
                new Vector3(halfWidth * 2f + 26f, 1f, length),
                art.ground, castShadows: false, receiveShadows: true);

            // Lane strips.
            for (int lane = 0; lane < settings.laneCount; lane++)
            {
                CreateCube($"Lane_{lane}", parent,
                    new Vector3(settings.LaneX(lane), 0.02f, centerZ),
                    new Vector3(settings.laneWidth * 0.86f, 0.03f, length),
                    art.lane, castShadows: false, receiveShadows: true);
            }

            // Low rails mark the playable width and give the swipes a visual reference.
            float railX = halfWidth + 0.45f;
            CreateCube("Rail_Left", parent,
                new Vector3(-railX, 0.45f, centerZ),
                new Vector3(0.7f, 0.9f, length),
                art.rail, receiveShadows: true);

            CreateCube("Rail_Right", parent,
                new Vector3(railX, 0.45f, centerZ),
                new Vector3(0.7f, 0.9f, length),
                art.rail, receiveShadows: true);

            // Backdrop behind the spawn line.
            CreateCube("SpawnBackdrop", parent,
                new Vector3(0f, 1.6f, farZ),
                new Vector3(halfWidth * 2f + 26f, 3.2f, 0.8f),
                art.rail);

            // Thin strip showing where the defense line is.
            CreateCube("DefenseLine", parent,
                new Vector3(0f, 0.02f, settings.defenseLineZ),
                new Vector3(halfWidth * 2f, 0.04f, 0.3f),
                art.rail);
        }

        private static CameraRig CreateCamera(GameSettings settings)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = settings.cameraFieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = settings.skyColor;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 220f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            cameraObject.AddComponent<AudioListener>();
            CameraRig rig = cameraObject.AddComponent<CameraRig>();
            SetField(rig, "targetCamera", camera);

            // Preview placement for a 16:9 game view; CameraRig re-solves it at runtime.
            Vector3 position = CameraFraming.SolvePosition(settings, 16f / 9f);
            cameraObject.transform.SetPositionAndRotation(position, CameraFraming.SolveRotation(settings.cameraPitch));

            return rig;
        }

        private static void CreateLighting(GameSettings settings)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            light.shadowBias = 0.04f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.34f, 0.46f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = settings.skyColor;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 120f;
            RenderSettings.skybox = null;
        }

        // ------------------------------------------------------------------------------------
        // Gameplay objects
        // ------------------------------------------------------------------------------------

        private static ArmyController CreateArmy(GameSettings settings, Prefabs prefabs, Art art)
        {
            GameObject armyObject = new GameObject("Army");
            armyObject.transform.position = new Vector3(settings.LaneX(settings.CenterLane), 0f, settings.armyZ);

            ArmyController army = armyObject.AddComponent<ArmyController>();

            GameObject formation = new GameObject("Formation");
            formation.transform.SetParent(armyObject.transform, false);

            GameObject pool = new GameObject("Pool");
            pool.transform.SetParent(armyObject.transform, false);

            SetField(army, "soldierPrefab", prefabs.soldier);
            SetField(army, "formationRoot", formation.transform);
            SetField(army, "poolRoot", pool.transform);

            // A visual anchor so the player can see where the army lives even at 0 soldiers.
            CreateCube("ArmyMarker", armyObject.transform,
                new Vector3(0f, 0.02f, 0.95f),
                new Vector3(settings.laneWidth * 0.9f, 0.03f, 0.35f),
                art.rail);

            return army;
        }

        private static EnemySpawner CreateSpawner(Prefabs prefabs)
        {
            GameObject spawnerObject = new GameObject("EnemySpawner");
            EnemySpawner spawner = spawnerObject.AddComponent<EnemySpawner>();

            GameObject pool = new GameObject("Enemies");
            pool.transform.SetParent(spawnerObject.transform, false);

            SetField(spawner, "enemyPrefab", prefabs.enemy);
            SetField(spawner, "poolRoot", pool.transform);
            SetField(spawner, "prewarm", 14);

            EnemyType[] types = CreateEnemyTypes();
            SetObjectArray(spawner, "enemyTypes", types);

            return spawner;
        }

        private static BulletPool CreateBulletPool(Prefabs prefabs)
        {
            GameObject poolObject = new GameObject("BulletPool");
            BulletPool bulletPool = poolObject.AddComponent<BulletPool>();

            GameObject bullets = new GameObject("Bullets");
            bullets.transform.SetParent(poolObject.transform, false);

            SetField(bulletPool, "bulletPrefab", prefabs.bullet);
            SetField(bulletPool, "poolRoot", bullets.transform);
            SetField(bulletPool, "prewarm", 96);

            return bulletPool;
        }

        private static VfxPool CreateVfxPool(Prefabs prefabs)
        {
            GameObject poolObject = new GameObject("VfxPool");
            VfxPool vfxPool = poolObject.AddComponent<VfxPool>();

            GameObject sparks = new GameObject("Sparks");
            sparks.transform.SetParent(poolObject.transform, false);

            SetField(vfxPool, "sparkPrefab", prefabs.spark);
            SetField(vfxPool, "poolRoot", sparks.transform);

            return vfxPool;
        }

        // ------------------------------------------------------------------------------------
        // Enemy type assets
        // ------------------------------------------------------------------------------------

        private static EnemyType[] CreateEnemyTypes()
        {
            return new[]
            {
                CreateEnemyType("EnemyType_Grunt", "Grunt", 5f, 2.4f, 1f, new Color(0.91f, 0.19f, 0.23f, 1f), 10, 0, 1, 1f, 0f),
                CreateEnemyType("EnemyType_Runner", "Runner", 3f, 4.1f, 0.78f, new Color(1f, 0.56f, 0.16f, 1f), 15, 0, 1, 0.7f, 0.18f),
                CreateEnemyType("EnemyType_Brute", "Brute", 16f, 1.7f, 1.45f, new Color(0.72f, 0.12f, 0.5f, 1f), 30, 1, 2, 0.45f, 0.4f)
            };
        }

        private static EnemyType CreateEnemyType(
            string fileName,
            string displayName,
            float maxHealth,
            float moveSpeed,
            float scale,
            Color color,
            int scoreValue,
            int recruitValue,
            int armyDamage,
            float spawnWeight,
            float minDifficulty)
        {
            string path = $"{SettingsFolder}/{fileName}.asset";
            EnemyType type = AssetDatabase.LoadAssetAtPath<EnemyType>(path);
            bool created = false;

            if (type == null)
            {
                type = ScriptableObject.CreateInstance<EnemyType>();
                AssetDatabase.CreateAsset(type, path);
                created = true;
            }

            // Only author defaults on creation so designer tweaks survive a rebuild.
            if (created)
            {
                type.displayName = displayName;
                type.maxHealth = maxHealth;
                type.moveSpeed = moveSpeed;
                type.scale = scale;
                type.color = color;
                type.scoreValue = scoreValue;
                type.recruitValue = recruitValue;
                type.armyDamage = armyDamage;
                type.spawnWeight = spawnWeight;
                type.minDifficulty = minDifficulty;
                EditorUtility.SetDirty(type);
            }

            return type;
        }
    }
}
