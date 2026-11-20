using NumberRush3D.Controls;
using NumberRush3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// End of run panel: final score, best score and the two buttons.
    /// Shown when <see cref="GameManager.RunEnded"/> fires (after the slow-motion beat),
    /// which means it always appears exactly when the simulation freezes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverPanel : MonoBehaviour
    {
        [Header("Roots")]
        [SerializeField] private GameObject panelRoot;

        [Header("Labels")]
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text bestLabel;
        [SerializeField] private Text hintLabel;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button quitButton;

        [Header("Copy")]
        [SerializeField] private string gameOverTitle = "ARMY WIPED OUT";
        [SerializeField] private string newBestTitle = "NEW BEST!";

        private GameManager _game;

        public void Bind(GameManager game)
        {
            Unbind();

            _game = game;
            if (_game == null)
            {
                return;
            }

            _game.RunEnded += HandleRunEnded;

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRetryClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuitClicked);
            }

            if (hintLabel != null)
            {
                hintLabel.text = "TAP RETRY TO PLAY AGAIN";
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Unbind()
        {
            if (_game != null)
            {
                _game.RunEnded -= HandleRunEnded;
                _game = null;
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(HandleRetryClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(HandleQuitClicked);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            // Keyboard shortcut for editor / TV builds. Harmless on touch devices.
            if (_game == null || _game.State != GameState.GameOver)
            {
                return;
            }

            if (PointerInputBridge.ReadRestartShortcut())
            {
                HandleRetryClicked();
            }
        }

        private void HandleRunEnded(int score, int best)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (titleLabel != null)
            {
                bool isNewBest = score > 0 && score >= best;
                titleLabel.text = isNewBest ? newBestTitle : gameOverTitle;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = score.ToString("N0");
            }

            if (bestLabel != null)
            {
                bestLabel.text = $"BEST {best:N0}";
            }
        }

        private void HandleRetryClicked()
        {
            if (_game != null)
            {
                _game.Restart();
            }
        }

        private void HandleQuitClicked()
        {
            if (_game != null)
            {
                _game.QuitApplication();
            }
        }
    }
}
