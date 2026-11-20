using NumberRush3D.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// End of level panel: stars, score, coin reward, and the Next / Replay / Levels buttons.
    /// The layout is generated at runtime with <see cref="UiFactory"/> so it exists even in a scene
    /// that was upgraded by the setup menu instead of rebuilt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelCompletePanel : MonoBehaviour
    {
        [Header("Wiring (auto-built when empty)")]
        [SerializeField] private RectTransform root;

        [Header("Copy")]
        [SerializeField] private string starGlyph = "\u2605";
        [SerializeField] private string emptyStarGlyph = "\u2606";

        private Text _titleLabel;
        private Text _starsLabel;
        private Text _scoreLabel;
        private Text _coinsLabel;
        private Text _armyLabel;
        private Button _nextButton;
        private Button _replayButton;
        private Button _levelsButton;
        private bool _built;

        /// <summary>True while the panel is visible.</summary>
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public void Bind()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>Fills the panel in and reveals it.</summary>
        public void Show(LevelDefinition level, int stars, int score, int coins, int army, bool hasNextLevel)
        {
            BuildIfNeeded();

            if (_titleLabel != null)
            {
                _titleLabel.text = level != null ? $"{level.DisplayName} COMPLETE" : "LEVEL COMPLETE";
            }

            if (_starsLabel != null)
            {
                _starsLabel.text = BuildStarRow(stars);
            }

            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"SCORE {score:N0}";
            }

            if (_coinsLabel != null)
            {
                _coinsLabel.text = $"+{coins} COINS";
            }

            if (_armyLabel != null)
            {
                _armyLabel.text = $"{army} SOLDIERS LEFT";
            }

            if (_nextButton != null)
            {
                _nextButton.gameObject.SetActive(hasNextLevel);
            }

            if (root != null)
            {
                root.gameObject.SetActive(true);
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
                root = UiFactory.MakeStretch("LevelCompletePanel", transform);
            }

            UiFactory.MakeDim(root);

            RectTransform card = UiFactory.MakeCard(root, new Vector2(1080f, 700f));

            _titleLabel = UiFactory.MakeLabel(card, "Title", "LEVEL COMPLETE", 56, UiFactory.SuccessColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(980f, 80f));

            _starsLabel = UiFactory.MakeLabel(card, "Stars", string.Empty, 96, UiFactory.GoldColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_starsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(980f, 120f));

            _scoreLabel = UiFactory.MakeLabel(card, "Score", "SCORE 0", 44, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place(_scoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(900f, 56f));

            _coinsLabel = UiFactory.MakeLabel(card, "Coins", "+0 COINS", 38, UiFactory.GoldColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_coinsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -382f), new Vector2(900f, 50f));

            _armyLabel = UiFactory.MakeLabel(card, "Army", "0 SOLDIERS LEFT", 30, UiFactory.CaptionColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_armyLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -436f), new Vector2(900f, 44f));

            _nextButton = UiFactory.MakeButton(card, "NextButton", "NEXT", UiFactory.PrimaryButton, 42);
            UiFactory.Place(_nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(300f, 96f), new Vector2(360f, 108f));
            _nextButton.onClick.AddListener(HandleNextClicked);

            _replayButton = UiFactory.MakeButton(card, "ReplayButton", "REPLAY", UiFactory.SecondaryButton, 42);
            UiFactory.Place(_replayButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-110f, 96f), new Vector2(360f, 108f));
            _replayButton.onClick.AddListener(LevelLaunch.Replay);

            _levelsButton = UiFactory.MakeButton(card, "LevelsButton", "LEVELS", UiFactory.SecondaryButton, 34);
            UiFactory.Place(_levelsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-420f, 96f), new Vector2(240f, 108f));
            _levelsButton.onClick.AddListener(LevelLaunch.BackToMenu);
        }

        private void HandleNextClicked()
        {
            LevelDefinition active = LevelLaunch.ActiveLevel;
            if (active == null)
            {
                LevelLaunch.BackToMenu();
                return;
            }

            LevelLaunch.StartLevel(active.levelNumber + 1);
        }

        private string BuildStarRow(int stars)
        {
            string row = string.Empty;
            for (int i = 0; i < 3; i++)
            {
                row += i < stars ? starGlyph : emptyStarGlyph;
            }

            return row;
        }
    }
}
