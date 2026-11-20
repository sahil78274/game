using NumberRush3D.Core;
using NumberRush3D.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// Pause overlay. It listens to <see cref="GameManager.StateChanged"/>, so the panel and the
    /// pause state can never disagree - the button in the HUD only ever calls
    /// <see cref="GameManager.TogglePause"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PausePanel : MonoBehaviour
    {
        [Header("Wiring (auto-built when empty)")]
        [SerializeField] private RectTransform root;

        private GameManager _game;
        private Button _resumeButton;
        private Button _restartButton;
        private Button _levelsButton;
        private bool _built;

        /// <summary>True while the panel is visible.</summary>
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public void Bind(GameManager game)
        {
            BuildIfNeeded();
            Hide();

            if (_game != null)
            {
                _game.StateChanged -= HandleStateChanged;
            }

            _game = game;

            if (_game != null)
            {
                _game.StateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= HandleStateChanged;
                _game = null;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            bool paused = state == GameState.Paused;
            if (root != null && root.gameObject.activeSelf != paused)
            {
                root.gameObject.SetActive(paused);
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
                root = UiFactory.MakeStretch("PausePanel", transform);
            }

            UiFactory.MakeDim(root, 0.72f);

            RectTransform card = UiFactory.MakeCard(root, new Vector2(760f, 600f));

            Text title = UiFactory.MakeLabel(card, "Title", "PAUSED", 60, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(680f, 84f));

            _resumeButton = UiFactory.MakeButton(card, "ResumeButton", "RESUME", UiFactory.PrimaryButton, 42);
            UiFactory.Place(_resumeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(520f, 104f));
            _resumeButton.onClick.AddListener(HandleResumeClicked);

            _restartButton = UiFactory.MakeButton(card, "RestartButton", "RESTART", UiFactory.SecondaryButton, 38);
            UiFactory.Place(_restartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(520f, 96f));
            _restartButton.onClick.AddListener(LevelLaunch.Replay);

            _levelsButton = UiFactory.MakeButton(card, "LevelsButton", "LEVELS", UiFactory.SecondaryButton, 38);
            UiFactory.Place(_levelsButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -196f), new Vector2(520f, 96f));
            _levelsButton.onClick.AddListener(LevelLaunch.BackToMenu);
        }

        private void HandleResumeClicked()
        {
            if (_game != null)
            {
                _game.Resume();
            }
            else
            {
                Hide();
            }
        }
    }
}
