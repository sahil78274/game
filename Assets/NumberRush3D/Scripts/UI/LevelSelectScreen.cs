using System.Collections.Generic;
using NumberRush3D.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// Campaign level select. Built entirely at runtime: the catalog holds 100 levels, so the grid
    /// is generated from data instead of authoring 100 buttons by hand.
    ///
    /// Locked levels are dimmed and non-interactable, unlocked ones show the best star count.
    /// The whole screen lives inside the game canvas, so it scales with the same CanvasScaler as
    /// the HUD (1920x1080 reference, match 0.5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        private const int Columns = 9;

        [Header("Wiring (auto-built when empty)")]
        [Tooltip("Campaign catalog. Defaults to the one assigned on LevelManager.")]
        [SerializeField] private LevelCatalog catalog;

        [Tooltip("Optional pre-authored root. Leave empty to build the screen at runtime.")]
        [SerializeField] private RectTransform root;

        [Header("Copy")]
        [SerializeField] private string starGlyph = "\u2605";
        [SerializeField] private string emptyStarGlyph = "\u2606";

        private sealed class Entry
        {
            public Button button;
            public Text numberLabel;
            public Text starsLabel;
            public int levelNumber;
        }

        private readonly List<Entry> _entries = new List<Entry>(128);

        private RectTransform _card;
        private RectTransform _grid;
        private Text _headerLabel;
        private Text _starsLabel;
        private Button _freeRunButton;
        private bool _built;

        /// <summary>True while the screen is on.</summary>
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        /// <summary>Assigns the catalog and builds the hierarchy. Safe to call more than once.</summary>
        public void Bind(LevelCatalog levelCatalog)
        {
            if (levelCatalog != null)
            {
                catalog = levelCatalog;
            }

            BuildIfNeeded();
            Hide();
        }

        public void Show()
        {
            BuildIfNeeded();
            Refresh();

            if (root != null)
            {
                root.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            if (root == null)
            {
                root = UiFactory.MakeStretch("LevelSelectScreen", transform);
            }

            UiFactory.MakeDim(root, 0.88f);

            _card = UiFactory.MakeCard(root, new Vector2(1660f, 940f));

            _headerLabel = UiFactory.MakeLabel(_card, "Header", "SELECT LEVEL", 58, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place(_headerLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 70f));

            _starsLabel = UiFactory.MakeLabel(_card, "Stars", string.Empty, 32, UiFactory.GoldColor, TextAnchor.MiddleRight);
            UiFactory.Place(_starsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(900f, 40f));

            // Scroll view: viewport masks, content is laid out by a grid layout group.
            RectTransform scroll = UiFactory.MakeRect(
                "Scroll", _card,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -18f), new Vector2(-90f, -330f));

            RectTransform viewport = UiFactory.MakeStretch("Viewport", scroll);
            viewport.gameObject.AddComponent<RectMask2D>();

            _grid = UiFactory.MakeRect(
                "Content", viewport,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 0f));

            GridLayoutGroup layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(150f, 150f);
            layout.spacing = new Vector2(20f, 20f);
            layout.padding = new RectOffset(24, 24, 16, 24);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.childAlignment = TextAnchor.UpperCenter;

            ContentSizeFitter fitter = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = _grid;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 34f;
            scrollRect.elasticity = 0.08f;

            _freeRunButton = UiFactory.MakeButton(_card, "FreeRunButton", "FREE RUN", UiFactory.SecondaryButton, 34);
            UiFactory.Place(_freeRunButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(420f, 84f));
            _freeRunButton.onClick.AddListener(LevelLaunch.StartFreeRun);
        }

        /// <summary>Rebuilds the grid from the catalog and refreshes every lock / star state.</summary>
        public void Refresh()
        {
            BuildIfNeeded();

            if (catalog == null)
            {
                if (_headerLabel != null)
                {
                    _headerLabel.text = "NO LEVEL CATALOG ASSIGNED";
                }

                return;
            }

            int count = catalog.Count;
            EnsureEntries(count);

            int unlocked = LevelProgressStore.UnlockedLevel;
            int totalStars = LevelProgressStore.TotalStars(count);

            if (_headerLabel != null)
            {
                _headerLabel.text = "SELECT LEVEL";
            }

            if (_starsLabel != null)
            {
                _starsLabel.text = $"{starGlyph} {totalStars} / {count * 3}";
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.button == null)
                {
                    continue;
                }

                LevelDefinition level = catalog.levels != null && i < catalog.levels.Length ? catalog.levels[i] : null;
                int levelNumber = entry.levelNumber;
                bool isUnlocked = levelNumber <= unlocked;
                int stars = LevelProgressStore.GetStars(levelNumber);

                UiFactory.SetButtonState(
                    entry.button,
                    isUnlocked ? UiFactory.PrimaryButton : new Color(0.16f, 0.18f, 0.24f, 1f),
                    isUnlocked);

                if (entry.numberLabel != null)
                {
                    entry.numberLabel.text = levelNumber.ToString();
                    entry.numberLabel.color = isUnlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                }

                if (entry.starsLabel != null)
                {
                    entry.starsLabel.text = isUnlocked ? StarRow(stars) : "LOCKED";
                    entry.starsLabel.color = stars > 0 ? UiFactory.GoldColor : new Color(1f, 1f, 1f, 0.45f);
                }

                if (level == null)
                {
                    UiFactory.SetButtonState(entry.button, new Color(0.16f, 0.18f, 0.24f, 1f), false);
                }
            }
        }

        private void EnsureEntries(int count)
        {
            while (_entries.Count < count)
            {
                int index = _entries.Count;
                int levelNumber = index + 1;

                Button button = UiFactory.MakeButton(_grid, $"Level{levelNumber}", levelNumber.ToString(), UiFactory.PrimaryButton, 46);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.localScale = Vector3.one;

                Text numberLabel = button.GetComponentInChildren<Text>();
                if (numberLabel != null)
                {
                    UiFactory.Place(numberLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(140f, 56f));
                }

                Text starsLabel = UiFactory.MakeLabel(button.transform, "Stars", string.Empty, 24, UiFactory.GoldColor, TextAnchor.MiddleCenter);
                UiFactory.Place(starsLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(140f, 34f));

                button.onClick.AddListener(() => HandleLevelClicked(levelNumber));

                _entries.Add(new Entry
                {
                    button = button,
                    numberLabel = numberLabel,
                    starsLabel = starsLabel,
                    levelNumber = levelNumber,
                });
            }

            // Hide surplus buttons if the catalog shrinks.
            for (int i = 0; i < _entries.Count; i++)
            {
                bool used = i < count;
                if (_entries[i].button != null && _entries[i].button.gameObject.activeSelf != used)
                {
                    _entries[i].button.gameObject.SetActive(used);
                }
            }
        }

        private string StarRow(int stars)
        {
            if (stars <= 0)
            {
                return emptyStarGlyph + emptyStarGlyph + emptyStarGlyph;
            }

            string filled = string.Empty;
            for (int i = 0; i < 3; i++)
            {
                filled += i < stars ? starGlyph : emptyStarGlyph;
            }

            return filled;
        }

        private void HandleLevelClicked(int levelNumber)
        {
            if (!LevelProgressStore.IsUnlocked(levelNumber))
            {
                return;
            }

            Hide();
            LevelLaunch.StartLevel(levelNumber);
        }
    }
}
