using NumberRush3D.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// Campaign failure panel: how far the army got, the score, and the Retry / Levels buttons.
    /// Built at runtime with <see cref="UiFactory"/> for the same reason as the complete panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelFailedPanel : MonoBehaviour
    {
        [Header("Wiring (auto-built when empty)")]
        [SerializeField] private RectTransform root;

        [Header("Copy")]
        [SerializeField] private string titleText = "LEVEL FAILED";

        private Text _titleLabel;
        private Text _progressLabel;
        private Text _scoreLabel;
        private Button _retryButton;
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
        public void Show(string levelName, float progress01, int score, int army)
        {
            BuildIfNeeded();

            if (_titleLabel != null)
            {
                _titleLabel.text = titleText;
            }

            if (_progressLabel != null)
            {
                _progressLabel.text = $"{levelName}   {Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f)}%";
            }

            if (_scoreLabel != null)
            {
                _scoreLabel.text = $"SCORE {score:N0}   ·   {army} SOLDIERS";
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
                root = UiFactory.MakeStretch("LevelFailedPanel", transform);
            }

            UiFactory.MakeDim(root);

            RectTransform card = UiFactory.MakeCard(root, new Vector2(940f, 560f));

            _titleLabel = UiFactory.MakeLabel(card, "Title", titleText, 58, UiFactory.DangerColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(860f, 84f));

            _progressLabel = UiFactory.MakeLabel(card, "Progress", string.Empty, 40, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place(_progressLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(820f, 56f));

            _scoreLabel = UiFactory.MakeLabel(card, "Score", string.Empty, 30, UiFactory.CaptionColor, TextAnchor.MiddleCenter);
            UiFactory.Place(_scoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(820f, 48f));

            _retryButton = UiFactory.MakeButton(card, "RetryButton", "RETRY", UiFactory.PrimaryButton, 42);
            UiFactory.Place(_retryButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(190f, 96f), new Vector2(360f, 108f));
            _retryButton.onClick.AddListener(LevelLaunch.Replay);

            _levelsButton = UiFactory.MakeButton(card, "LevelsButton", "LEVELS", UiFactory.SecondaryButton, 34);
            UiFactory.Place(_levelsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-190f, 96f), new Vector2(360f, 108f));
            _levelsButton.onClick.AddListener(LevelLaunch.BackToMenu);
        }
    }
}
