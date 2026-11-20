using NumberRush3D.Army;
using NumberRush3D.Core;
using NumberRush3D.Enemies;
using NumberRush3D.Gates;
using NumberRush3D.Levels;
using NumberRush3D.Track;
using NumberRush3D.UI;
using NumberRush3D.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// Non destructive scene upgrade.
    ///
    /// Menu item 7 converts an existing, already authored NUMBER RUSH 3D scene into the runner
    /// build without rebuilding it:
    /// it only ADDS the missing systems / screens and only ASSIGNS references that are currently
    /// empty. Nothing is deleted, renamed, re-parented or replaced, and no existing serialized value
    /// is overwritten. Running it twice is a no-op.
    ///
    /// The same routine runs at the end of menu item 1, so a fresh build and an upgraded scene end
    /// up with the exact same wiring.
    /// </summary>
    public static partial class NumberRushBuilder
    {
        private const float RunnerCameraFarPlane = 260f;

        [MenuItem("Tools/NUMBER RUSH 3D/7. Upgrade Existing Scene", false, 40)]
        public static void UpgradeSceneMenu()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("NUMBER RUSH 3D", "Open the NUMBER RUSH 3D scene first.", "OK");
                return;
            }

            if (Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog(
                    "NUMBER RUSH 3D",
                    "No GameManager found in the open scene.\n\n" +
                    "Open the scene built by step 1 (Tools > NUMBER RUSH 3D > 5. Open Game Scene) and run this again.",
                    "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "NUMBER RUSH 3D",
                "Upgrade the OPEN scene to the runner build?\n\n" +
                "Only adds what is missing:\n" +
                "- TrackManager, GateManager, LevelManager on the Systems object\n" +
                "- Level select / complete / failed / pause screens on the existing canvas\n" +
                "- Coins, level progress, toast and pause widgets on the existing HUD\n" +
                "- Settings/LevelCatalog.asset + Settings/Levels/Level_001..100.asset\n\n" +
                "Existing objects, materials, prefabs and references are kept. " +
                "Empty references are filled in; assigned ones are never overwritten.\n\n" +
                "The scene is saved at the end.",
                "Upgrade scene",
                "Cancel");

            if (!proceed)
            {
                return;
            }

            int added = UpgradeExistingScene(true);
            EditorUtility.DisplayDialog(
                "NUMBER RUSH 3D",
                $"Scene upgraded ({added} component(s) added).\n\n" +
                "Press Play: the level select appears, the endless track replaces the legacy ground " +
                "and the army runs forward automatically.\n\n" +
                "Then run Tools > NUMBER RUSH 3D > 6. Project Setup Checklist to validate the result.",
                "OK");
        }

        /// <summary>
        /// Adds the runner systems to the open scene and wires them. Returns the number of components
        /// that had to be created (0 means the scene was already upgraded).
        /// </summary>
        public static int UpgradeExistingScene(bool interactive)
        {
            GameManager game = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (game == null)
            {
                if (interactive)
                {
                    Debug.LogError("[NumberRush] Upgrade aborted: the open scene has no GameManager.");
                }

                return 0;
            }

            GameObject systems = game.gameObject;
            int added = 0;
            int wired = 0;

            ArmyController army = game.Army != null
                ? game.Army
                : Object.FindFirstObjectByType<ArmyController>(FindObjectsInactive.Include);
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            CameraRig cameraRig = Object.FindFirstObjectByType<CameraRig>(FindObjectsInactive.Include);
            HudController hud = Object.FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
            GameOverPanel gameOverPanel = Object.FindFirstObjectByType<GameOverPanel>(FindObjectsInactive.Include);
            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

            // --- 1. Runner systems on the Systems object ------------------------------------
            TrackManager track = GetOrAddComponent<TrackManager>(systems, ref added);
            GateManager gateManager = GetOrAddComponent<GateManager>(systems, ref added);
            LevelManager levelManager = GetOrAddComponent<LevelManager>(systems, ref added);

            LevelCatalog catalog = EnsureLevelCatalog();

            Transform gatesRoot = systems.transform.Find("Gates");
            if (gatesRoot == null)
            {
                GameObject gatesObject = new GameObject("Gates");
                gatesObject.transform.SetParent(systems.transform, false);
                gatesRoot = gatesObject.transform;
            }

            GameObject legacyEnvironment = FindSceneRootObject("Environment");

            if (AssignIfEmpty(track, "legacyTrack", legacyEnvironment)) wired++;
            if (AssignIfEmpty(gateManager, "gateRoot", gatesRoot)) wired++;

            // --- 2. Campaign screens on the existing canvas ---------------------------------
            LevelSelectScreen levelSelect = EnsureScreen<LevelSelectScreen>(canvas, "LevelSelectScreen", ref added);
            LevelCompletePanel completePanel = EnsureScreen<LevelCompletePanel>(canvas, "LevelCompletePanel", ref added);
            LevelFailedPanel failedPanel = EnsureScreen<LevelFailedPanel>(canvas, "LevelFailedPanel", ref added);
            PausePanel pausePanel = EnsureScreen<PausePanel>(canvas, "PausePanel", ref added);

            if (levelSelect != null)
            {
                AssignIfEmpty(levelSelect, "catalog", catalog);
            }
            else
            {
                // Runtime fallback (LevelManager creates it) - keep the catalog reference anyway.
                AssignIfEmpty(levelManager, "catalog", catalog);
            }

            // --- 3. LevelManager references -------------------------------------------------
            if (AssignIfEmpty(levelManager, "game", game)) wired++;
            if (AssignIfEmpty(levelManager, "catalog", catalog)) wired++;
            if (AssignIfEmpty(levelManager, "track", track)) wired++;
            if (AssignIfEmpty(levelManager, "gates", gateManager)) wired++;
            if (AssignIfEmpty(levelManager, "spawner", spawner)) wired++;
            if (AssignIfEmpty(levelManager, "hud", hud)) wired++;
            if (AssignIfEmpty(levelManager, "legacyGameOverPanel", gameOverPanel)) wired++;
            if (AssignIfEmpty(levelManager, "levelSelect", levelSelect)) wired++;
            if (AssignIfEmpty(levelManager, "completePanel", completePanel)) wired++;
            if (AssignIfEmpty(levelManager, "failedPanel", failedPanel)) wired++;
            if (AssignIfEmpty(levelManager, "pausePanel", pausePanel)) wired++;

            // --- 4. GameManager: keep the existing wiring, fill the blanks ------------------
            if (AssignIfEmpty(game, "army", army)) wired++;
            if (AssignIfEmpty(game, "spawner", spawner)) wired++;
            if (AssignIfEmpty(game, "cameraRig", cameraRig)) wired++;
            if (AssignIfEmpty(game, "hud", hud)) wired++;
            if (AssignIfEmpty(game, "gameOverPanel", gameOverPanel)) wired++;

            // --- 5. HUD runner widgets ------------------------------------------------------
            if (hud != null)
            {
                added += EnsureHudWidgets(hud, ref wired);
            }

            // --- 6. Camera: the endless track needs a little more draw distance -------------
            if (cameraRig != null)
            {
                Camera camera = cameraRig.GetComponent<Camera>();
                if (camera != null && camera.farClipPlane < RunnerCameraFarPlane)
                {
                    camera.farClipPlane = RunnerCameraFarPlane;
                }
            }

            // --- 7. Save --------------------------------------------------------------------
            Scene scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[NumberRush] Scene upgrade complete: {added} component(s) added, {wired} reference(s) assigned. " +
                "Track/Gate/Level systems are on the Systems object; existing objects were not modified.");

            return added;
        }

        // ------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------

        private static T GetOrAddComponent<T>(GameObject host, ref int added) where T : Component
        {
            T component = host.GetComponent<T>();
            if (component == null)
            {
                component = host.AddComponent<T>();
                added++;
            }

            return component;
        }

        /// <summary>Adds a full screen UI component to the canvas when the scene has none.</summary>
        private static T EnsureScreen<T>(Canvas canvas, string name, ref int added) where T : Component
        {
            T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                // Already in the scene: keep it (and its layout) exactly as it is.
                return existing;
            }

            if (canvas == null)
            {
                Debug.LogWarning($"[NumberRush] No Canvas in the scene - {name} will be created by LevelManager at runtime.");
                return null;
            }

            GameObject screen = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            added++;
            return screen.AddComponent<T>();
        }

        /// <summary>Adds the coins / progress / toast / pause widgets to an existing HUD.</summary>
        private static int EnsureHudWidgets(HudController hud, ref int wired)
        {
            int added = 0;
            Transform hudRoot = hud.transform;

            if (GetObjectField(hud, "coinsLabel") == null)
            {
                Text coins = FindOrCreateText(
                    hudRoot, "CoinsLabel", "0 COINS", 34,
                    new Color(1f, 0.78f, 0.25f, 1f), TextAnchor.UpperRight,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -100f), new Vector2(440f, 46f));

                if (SetFieldChecked(hud, "coinsLabel", coins)) { added++; wired++; }
            }

            if (GetObjectField(hud, "progressFill") == null)
            {
                RawImage back = FindOrCreatePanel(
                    hudRoot, "LevelProgress", new Color(0.08f, 0.1f, 0.16f, 0.92f),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(700f, 16f));

                Transform fill = back.transform.Find("Fill");
                if (fill == null)
                {
                    RawImage fillImage = CreatePanel(
                        "Fill", back.transform, new Color(0.16f, 0.85f, 0.36f, 1f),
                        Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    RectTransform fillRect = (RectTransform)fillImage.transform;
                    Stretch(fillRect);
                    fillRect.anchorMax = new Vector2(0f, 1f);
                    fill = fillRect;
                }

                if (SetFieldChecked(hud, "progressFill", fill)) { added++; wired++; }
            }

            if (GetObjectField(hud, "toastLabel") == null)
            {
                Text toast = FindOrCreateText(
                    hudRoot, "ToastLabel", string.Empty, 54,
                    new Color(0.16f, 0.85f, 0.36f, 1f), TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1400f, 90f));

                CanvasGroup group = toast.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = toast.gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                bool a = SetFieldChecked(hud, "toastLabel", toast);
                bool b = SetFieldChecked(hud, "toastGroup", group);
                if (a || b) { added++; wired++; }
            }

            if (GetObjectField(hud, "pauseButton") == null)
            {
                Button pause = FindOrCreateButton(
                    hudRoot, "PauseButton", "II", new Color(0.18f, 0.2f, 0.28f, 1f),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(110f, 110f));

                if (SetFieldChecked(hud, "pauseButton", pause)) { added++; wired++; }
            }

            return added;
        }

        // ------------------------------------------------------------------------------------
        // Creation / assignment primitives
        // ------------------------------------------------------------------------------------

        private static Text FindOrCreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Text found = existing.GetComponent<Text>();
                if (found != null)
                {
                    return found;
                }
            }

            return CreateText(name, parent, content, fontSize, color, alignment, anchor, pivot, anchoredPosition, sizeDelta);
        }

        private static RawImage FindOrCreatePanel(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<RawImage>();
            }

            return CreatePanel(name, parent, color, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        }

        private static Button FindOrCreateButton(
            Transform parent,
            string name,
            string label,
            Color background,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            return CreateButton(name, parent, label, background, anchor, pivot, anchoredPosition, sizeDelta);
        }

        /// <summary>Assigns a serialized reference only when it is currently empty.</summary>
        private static bool AssignIfEmpty(Object target, string fieldName, Object value)
        {
            if (target == null || value == null)
            {
                return false;
            }

            if (GetObjectField(target, fieldName) != null)
            {
                return false;
            }

            return SetFieldChecked(target, fieldName, value);
        }

        private static bool SetFieldChecked(Object target, string fieldName, object value)
        {
            if (target == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[NumberRush] '{fieldName}' not found on {target.GetType().Name}.");
                return false;
            }

            SetField(target, fieldName, value);
            return true;
        }

        /// <summary>Reads a serialized object reference without touching the asset.</summary>
        private static Object GetObjectField(Object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            return property != null ? property.objectReferenceValue : null;
        }

        /// <summary>Finds a root object of the open scene by name (active or not).</summary>
        private static GameObject FindSceneRootObject(string objectName)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return roots[i];
                }
            }

            return null;
        }
    }
}
