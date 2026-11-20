using NumberRush3D.Core;
using NumberRush3D.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.EditorTools
{
    /// <summary>
    /// UI half of the generator: canvas, safe area, HUD and the game over panel,
    /// all wired to <see cref="HudController"/> / <see cref="GameOverPanel"/>.
    ///
    /// The layout is authored against a 1920x1080 reference and scales with the screen,
    /// which is the standard landscape mobile setup.
    /// </summary>
    public static partial class NumberRushBuilder
    {
        private static Font _cachedFont;

        private static UiRefs BuildUi(GameSettings settings)
        {
            UiRefs refs = new UiRefs();

            GameObject canvasObject = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateEventSystem();

            RectTransform safeArea = CreateUiObject("SafeArea", canvasObject.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            refs.hud = BuildHud(safeArea, settings);
            refs.gameOver = BuildGameOverPanel(canvasObject.transform, safeArea);

            return refs;
        }

        private static HudController BuildHud(RectTransform parent, GameSettings settings)
        {
            RectTransform hudRoot = CreateUiObject("HUD", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(hudRoot);

            HudController hud = hudRoot.gameObject.AddComponent<HudController>();

            Color captionColor = new Color(1f, 1f, 1f, 0.55f);
            Color labelColor = new Color(1f, 1f, 1f, 0.96f);

            // --- Army counter (top left) ---------------------------------------------------
            RectTransform armyGroup = CreateUiObject("ArmyGroup", hudRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -40f), new Vector2(420f, 180f));

            Text armyCaption = CreateText("ArmyCaption", armyGroup, "ARMY", 30, captionColor, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -6f), new Vector2(320f, 34f));

            Text armyLabel = CreateText("ArmyLabel", armyGroup, settings.startingSoldiers.ToString(), 104, settings.playerAccentColor, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -44f), new Vector2(420f, 130f));

            // --- Score (top center) --------------------------------------------------------
            RectTransform scoreGroup = CreateUiObject("ScoreGroup", hudRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(560f, 200f));

            CreateText("ScoreCaption", scoreGroup, "SCORE", 30, captionColor, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(360f, 34f));

            Text scoreLabel = CreateText("ScoreLabel", scoreGroup, "0", 104, labelColor, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(560f, 140f));

            // --- Best / wave (top right) ---------------------------------------------------
            RectTransform rightGroup = CreateUiObject("RightGroup", hudRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -40f), new Vector2(440f, 150f));

            Text bestLabel = CreateText("BestLabel", rightGroup, "BEST 0", 36, labelColor, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(440f, 46f));

            Text waveLabel = CreateText("WaveLabel", rightGroup, "WAVE 1", 30, captionColor, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(440f, 40f));

            // --- Recruit progress (bottom center) ------------------------------------------
            RectTransform bottomBar = CreateUiObject("RecruitBar", hudRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(620f, 80f));

            RawImage barBack = CreatePanel("BarBack", bottomBar, new Color(0.08f, 0.1f, 0.16f, 0.92f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(620f, 16f));

            RawImage barFill = CreatePanel("BarFill", barBack.transform, settings.playerAccentColor,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)barFill.transform);

            Text recruitLabel = CreateText("RecruitLabel", bottomBar, "8 KILLS TO +1", 24, captionColor, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(620f, 30f));

            SetField(hud, "armyLabel", armyLabel);
            SetField(hud, "scoreLabel", scoreLabel);
            SetField(hud, "bestLabel", bestLabel);
            SetField(hud, "waveLabel", waveLabel);
            SetField(hud, "recruitFill", (RectTransform)barFill.transform);
            SetField(hud, "recruitLabel", recruitLabel);

            return hud;
        }

        private static GameOverPanel BuildGameOverPanel(Transform canvasRoot, RectTransform safeArea)
        {
            GameOverPanel panel = canvasRoot.gameObject.AddComponent<GameOverPanel>();

            RectTransform panelRoot = CreateUiObject("GameOverPanel", safeArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(panelRoot);

            RawImage dim = CreatePanel("Dim", panelRoot, new Color(0.01f, 0.02f, 0.05f, 0.78f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            Stretch((RectTransform)dim.transform);

            RectTransform card = CreateUiObject("Card", panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 640f));
            RawImage cardBackground = card.gameObject.AddComponent<RawImage>();
            cardBackground.color = new Color(0.06f, 0.08f, 0.15f, 0.98f);
            cardBackground.raycastTarget = true;

            Text titleLabel = CreateText("Title", card, "ARMY WIPED OUT", 62, new Color(1f, 0.42f, 0.36f, 1f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(940f, 80f));

            CreateText("ScoreCaption", card, "SCORE", 28, new Color(1f, 1f, 1f, 0.55f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(400f, 34f));

            Text scoreLabel = CreateText("ScoreValue", card, "0", 120, new Color(1f, 1f, 1f, 0.98f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(940f, 150f));

            Text bestLabel = CreateText("BestValue", card, "BEST 0", 38, new Color(0.75f, 0.85f, 1f, 0.9f), TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(600f, 54f));

            Button retryButton = CreateButton("RetryButton", card, "RETRY", new Color(0.16f, 0.45f, 0.95f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(480f, 112f));

            Text hintLabel = CreateText("Hint", card, "TAP RETRY TO PLAY AGAIN", 24, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(600f, 30f));

            Button quitButton = CreateButton("QuitButton", card, "QUIT", new Color(0.18f, 0.2f, 0.28f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(180f, 62f));

            panelRoot.gameObject.SetActive(false);

            SetField(panel, "panelRoot", panelRoot.gameObject);
            SetField(panel, "titleLabel", titleLabel);
            SetField(panel, "scoreLabel", scoreLabel);
            SetField(panel, "bestLabel", bestLabel);
            SetField(panel, "hintLabel", hintLabel);
            SetField(panel, "retryButton", retryButton);
            SetField(panel, "quitButton", quitButton);

            return panel;
        }

        // ------------------------------------------------------------------------------------
        // UI primitives
        // ------------------------------------------------------------------------------------

        private static void CreateEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        private static RectTransform CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            RectTransform rect = CreateUiObject(name, parent, anchor, anchor, pivot, anchoredPosition, sizeDelta);

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = GetBuiltinFont();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;

            return text;
        }

        private static RawImage CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, bool raycastTarget = false)
        {
            RectTransform rect = CreateUiObject(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.color = color;
            image.raycastTarget = raycastTarget;

            return image;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color background, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            RawImage backgroundImage = CreatePanel(name, parent, background, anchor, anchor, pivot, anchoredPosition, sizeDelta, true);

            Button button = backgroundImage.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.transition = Selectable.Transition.ColorTint;

            RectTransform labelRect = CreateUiObject("Label", backgroundImage.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(labelRect);

            Text labelText = labelRect.gameObject.AddComponent<Text>();
            labelText.font = GetBuiltinFont();
            labelText.text = label;
            labelText.fontSize = 44;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.raycastTarget = false;

            return button;
        }

        private static Font GetBuiltinFont()
        {
            if (_cachedFont != null)
            {
                return _cachedFont;
            }

            // Unity 2022+ renamed Arial.ttf to LegacyRuntime.ttf; try both so the generator
            // works on Unity 6.3 and on older editors.
            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                _cachedFont = null;
            }

            if (_cachedFont == null)
            {
                try
                {
                    _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (System.Exception)
                {
                    _cachedFont = null;
                }
            }

            if (_cachedFont == null)
            {
                Debug.LogWarning("[NumberRush] Built-in UI font not found. Assign a font to the HUD Text components manually.");
            }

            return _cachedFont;
        }
    }
}
