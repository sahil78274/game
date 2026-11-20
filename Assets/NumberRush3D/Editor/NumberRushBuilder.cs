using NumberRush3D.Army;
using NumberRush3D.Combat;
using NumberRush3D.Core;
using NumberRush3D.Enemies;
using NumberRush3D.UI;
using NumberRush3D.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// One click project generator.
    ///
    /// Creates every material, prefab, settings asset and the complete playable scene with all
    /// inspector references already wired, so the game can be built for Android without touching
    /// the inspector once. Everything it generates can also be created by hand - the documentation
    /// in /Documentation describes both routes.
    ///
    /// Menu: Tools > NUMBER RUSH 3D
    /// </summary>
    public static partial class NumberRushBuilder
    {
        public const string RootFolder = "Assets/NumberRush3D";
        public const string PrefabFolder = RootFolder + "/Prefabs";
        public const string MaterialFolder = RootFolder + "/Materials";
        public const string SettingsFolder = RootFolder + "/Settings";
        public const string SceneFolder = RootFolder + "/Scenes";
        public const string ScenePath = SceneFolder + "/NumberRush3D.unity";
        public const string GameSettingsPath = SettingsFolder + "/GameSettings.asset";
        public const string EnemyLayerName = "Enemy";

        private const string BulletPrefabPath = PrefabFolder + "/Bullet.prefab";
        private const string SparkPrefabPath = PrefabFolder + "/Spark.prefab";
        private const string SoldierPrefabPath = PrefabFolder + "/Soldier.prefab";
        private const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";

        private sealed class Prefabs
        {
            public Bullet bullet;
            public Spark spark;
            public SoldierUnit soldier;
            public Enemy enemy;
        }

        private sealed class Art
        {
            public Material ground;
            public Material lane;
            public Material rail;
            public Material soldierBody;
            public Material soldierHead;
            public Material bullet;
            public Material spark;
            public Material enemyBody;
            public Material enemyVisor;
            public Material barBack;
            public Material barFill;
        }

        [MenuItem("Tools/NUMBER RUSH 3D/1. Build Materials, Prefabs && Scene", false, 1)]
        public static void BuildAllMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "NUMBER RUSH 3D",
                "This creates and overwrites:\n\n" +
                "- Materials/*.mat\n" +
                "- Prefabs/*.prefab\n" +
                "- Settings/*.asset (only when missing)\n" +
                "- Scenes/NumberRush3D.unity\n\n" +
                "Existing tuning values in GameSettings are kept.\nContinue?",
                "Build it",
                "Cancel");

            if (!proceed)
            {
                return;
            }

            BuildAll();
        }

        /// <summary>Builds the whole project content. Safe to run repeatedly.</summary>
        public static void BuildAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("NUMBER RUSH 3D", "Creating folders and layers", 0.05f);

                EnsureFolders();
                int enemyLayer = EnsureLayer(EnemyLayerName);
                GameSettings settings = CreateOrUpdateSettings();

                EditorUtility.DisplayProgressBar("NUMBER RUSH 3D", "Creating materials", 0.2f);
                Art art = CreateMaterials(settings);

                // Prefabs are built inside a real scene, so start from a clean one.
                // Ask first: anything unsaved in the currently open scene would be lost.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EditorUtility.DisplayProgressBar("NUMBER RUSH 3D", "Creating prefabs", 0.4f);
                Prefabs prefabs = CreatePrefabs(art, enemyLayer);

                EditorUtility.DisplayProgressBar("NUMBER RUSH 3D", "Assembling scene", 0.7f);
                BuildSceneObjects(settings, prefabs, art, enemyLayer);

                EditorUtility.DisplayProgressBar("NUMBER RUSH 3D", "Saving scene", 0.95f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                RegisterSceneInBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

                Debug.Log(
                    "[NumberRush] Build complete.\n" +
                    $"Scene: {ScenePath}\n" +
                    "Press Play to run. Use Tools > NUMBER RUSH 3D > 2. Configure Android Build Settings before building.");

                EditorUtility.DisplayDialog(
                    "NUMBER RUSH 3D",
                    "Project generated.\n\nScene: Assets/NumberRush3D/Scenes/NumberRush3D.unity\n\nPress Play to test, then run step 2 to configure Android.",
                    "Nice");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("NUMBER RUSH 3D", "Build failed, see the Console:\n\n" + exception.Message, "OK");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------------------------
        // Assets
        // ------------------------------------------------------------------------------------

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(SceneFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Returns the index of <paramref name="layerName"/>, creating it in the first free user slot.</summary>
        private static int EnsureLayer(string layerName)
        {
            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogWarning("[NumberRush] Could not open TagManager.asset - create the 'Enemy' layer manually, otherwise bullets collide with everything.");
                return 0;
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                Debug.LogWarning("[NumberRush] TagManager has no 'layers' property - create the 'Enemy' layer manually.");
                return 0;
            }

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == layerName)
                {
                    return i;
                }
            }

            // 0-7 are reserved by Unity.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"[NumberRush] Created layer '{layerName}' at index {i}.");
                return i;
            }

            Debug.LogWarning($"[NumberRush] No free layer slot for '{layerName}'. Bullets will test every collider.");
            return 0;
        }

        private static GameSettings CreateOrUpdateSettings()
        {
            GameSettings settings = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
            bool created = false;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameSettings>();
                AssetDatabase.CreateAsset(settings, GameSettingsPath);
                created = true;
            }

            // Always keep the layer name in sync, never touch the rest of the tuning when it exists.
            settings.enemyLayerName = EnemyLayerName;

            if (created)
            {
                Debug.Log($"[NumberRush] Created {GameSettingsPath} with default tuning.");
            }

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Art CreateMaterials(GameSettings settings)
        {
            return new Art
            {
                ground = CreateMaterial("Ground", settings.groundColor, 0.12f),
                lane = CreateMaterial("Lane", settings.laneColor, 0.2f),
                rail = CreateMaterial("Rail", settings.railColor, 0.3f),
                soldierBody = CreateMaterial("SoldierBody", settings.playerColor, 0.35f),
                soldierHead = CreateMaterial("SoldierHead", settings.playerAccentColor, 0.45f),
                bullet = CreateMaterial("Bullet", settings.bulletColor, 0.5f, emissive: true),
                spark = CreateMaterial("Spark", new Color(1f, 0.95f, 0.75f), 0.6f, emissive: true),
                enemyBody = CreateMaterial("EnemyBody", settings.enemyColor, 0.3f),
                enemyVisor = CreateMaterial("EnemyVisor", new Color(0.08f, 0.05f, 0.07f), 0.6f),
                barBack = CreateMaterial("HealthBarBack", new Color(0.05f, 0.06f, 0.09f), 0.1f),
                barFill = CreateMaterial("HealthBarFill", new Color(0.35f, 0.95f, 0.42f), 0.15f)
            };
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, bool emissive = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = FindLitShader();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", color * 1.8f);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindLitShader()
        {
            // URP shaders render magenta when no SRP asset is assigned (the project falls back to
            // the built-in renderer), so the shader choice follows the active pipeline.
            bool scriptablePipeline = GraphicsSettings.defaultRenderPipeline != null || QualitySettings.renderPipeline != null;

            Shader shader = null;

            if (scriptablePipeline)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            if (shader == null)
            {
                throw new System.InvalidOperationException("No usable lit shader found in this project.");
            }

            if (!scriptablePipeline)
            {
                Debug.Log("[NumberRush] No SRP asset assigned - using the Standard shader. See Documentation/01-PROJECT-SETUP.md for the URP setup.");
            }

            return shader;
        }

        // ------------------------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------------------------

        private static Prefabs CreatePrefabs(Art art, int enemyLayer)
        {
            return new Prefabs
            {
                bullet = CreateBulletPrefab(art),
                spark = CreateSparkPrefab(art),
                soldier = CreateSoldierPrefab(art),
                enemy = CreateEnemyPrefab(art, enemyLayer)
            };
        }

        private static Bullet CreateBulletPrefab(Art art)
        {
            GameObject root = new GameObject("Bullet");
            Bullet bullet = root.AddComponent<Bullet>();

            GameObject visual = CreateCube("Visual", root.transform, Vector3.zero, new Vector3(0.16f, 0.16f, 0.75f), art.bullet, castShadows: false);
            SetField(bullet, "bodyRenderer", visual.GetComponent<MeshRenderer>());

            return SavePrefab(root, BulletPrefabPath).GetComponent<Bullet>();
        }

        private static Spark CreateSparkPrefab(Art art)
        {
            GameObject root = new GameObject("Spark");
            Spark spark = root.AddComponent<Spark>();

            GameObject visual = CreateCube("Visual", root.transform, Vector3.zero, Vector3.one * 0.2f, art.spark, castShadows: false);
            SetField(spark, "bodyRenderer", visual.GetComponent<MeshRenderer>());

            return SavePrefab(root, SparkPrefabPath).GetComponent<Spark>();
        }

        private static SoldierUnit CreateSoldierPrefab(Art art)
        {
            GameObject root = new GameObject("Soldier");
            SoldierUnit soldier = root.AddComponent<SoldierUnit>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            CreateCube("Body", visual.transform, new Vector3(0f, 0.33f, 0f), new Vector3(0.44f, 0.66f, 0.44f), art.soldierBody);
            CreateCube("Head", visual.transform, new Vector3(0f, 0.79f, 0f), new Vector3(0.32f, 0.3f, 0.32f), art.soldierHead);

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(visual.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.62f, 0.36f);

            GameObject flash = CreateCube("Flash", visual.transform, new Vector3(0f, 0.62f, 0.4f), Vector3.one * 0.17f, art.bullet, castShadows: false);
            var flashRenderer = flash.GetComponent<MeshRenderer>();
            flashRenderer.enabled = false;

            SetField(soldier, "muzzle", muzzle.transform);
            SetField(soldier, "bodyRoot", visual.transform);
            SetField(soldier, "muzzleFlash", flashRenderer);

            return SavePrefab(root, SoldierPrefabPath).GetComponent<SoldierUnit>();
        }

        private static Enemy CreateEnemyPrefab(Art art, int enemyLayer)
        {
            GameObject root = new GameObject("Enemy") { layer = enemyLayer };
            Health health = root.AddComponent<Health>();
            Enemy enemy = root.AddComponent<Enemy>();

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.constraints = RigidbodyConstraints.FreezeAll;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.62f, 0f);
            collider.size = new Vector3(0.92f, 1.24f, 0.92f);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            GameObject bodyVisual = CreateCube("Body", visual.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.9f, 1.1f, 0.9f), art.enemyBody);

            // The visor is cosmetic and deliberately NOT tinted, so it keeps reading as a "face".
            CreateCube("Visor", visual.transform, new Vector3(0f, 0.8f, -0.47f), new Vector3(0.66f, 0.2f, 0.06f), art.enemyVisor);

            GameObject barRoot = new GameObject("HealthBar");
            barRoot.transform.SetParent(root.transform, false);
            barRoot.transform.localPosition = new Vector3(0f, 1.42f, 0f);
            WorldHealthBar healthBar = barRoot.AddComponent<WorldHealthBar>();

            CreateCube("Back", barRoot.transform, Vector3.zero, new Vector3(1.02f, 0.16f, 0.03f), art.barBack, castShadows: false);

            GameObject fillPivot = new GameObject("FillPivot");
            fillPivot.transform.SetParent(barRoot.transform, false);
            fillPivot.transform.localPosition = new Vector3(-0.5f, 0f, -0.02f);

            GameObject fill = CreateCube("Fill", fillPivot.transform, new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.1f, 0.04f), art.barFill, castShadows: false);

            SetObjectArray(health, "tintRenderers", new UnityEngine.Object[]
            {
                bodyVisual.GetComponent<MeshRenderer>()
            });

            SetField(health, "maxHealth", 3f);
            SetField(enemy, "health", health);
            SetField(enemy, "bodyRoot", visual.transform);
            SetField(enemy, "healthBar", healthBar);
            SetField(healthBar, "fillPivot", fillPivot.transform);
            SetField(healthBar, "fillRenderer", fill.GetComponent<MeshRenderer>());

            return SavePrefab(root, EnemyPrefabPath).GetComponent<Enemy>();
        }

        // ------------------------------------------------------------------------------------
        // Authoring helpers
        // ------------------------------------------------------------------------------------

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool castShadows = true, bool receiveShadows = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;

            Collider collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                // Gameplay colliders are authored explicitly on the enemy only.
                UnityEngine.Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = receiveShadows;
            }

            return cube;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            UnityEngine.Object.DestroyImmediate(root);

            if (!success || asset == null)
            {
                throw new System.InvalidOperationException($"Failed to save prefab at {path}");
            }

            return asset;
        }

        /// <summary>Assign any serialized field by name. Works with private [SerializeField] members.</summary>
        private static void SetField(UnityEngine.Object target, string fieldName, object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[NumberRush] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = System.Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = System.Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value as string;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.LayerMask:
                    property.intValue = System.Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = System.Convert.ToInt32(value);
                    break;
                default:
                    Debug.LogWarning($"[NumberRush] Unsupported field type {property.propertyType} for '{fieldName}'.");
                    break;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string fieldName, UnityEngine.Object[] values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[NumberRush] Array field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == ScenePath)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            System.Array.Copy(existing, updated, existing.Length);
            updated[existing.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
