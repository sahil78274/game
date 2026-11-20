using NumberRush3D.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NumberRush3D.UI
{
    /// <summary>
    /// Top-of-screen HUD. It only listens to <see cref="GameManager"/> events - it never polls
    /// gameplay systems - and it works with the legacy UI Text component so the project needs
    /// no TextMeshPro essential-resource import to run.
    ///
    /// Runner upgrade: the same component also drives the coins counter, the level progress bar,
    /// the gate / level toast and the pause button. Those four widgets are optional: when a scene
    /// upgraded through "Tools > NUMBER RUSH 3D > 7. Upgrade Existing Scene" does not contain them,
    /// they are created at runtime with <see cref="UiFactory"/> so the HUD is never half wired.
    ///
    /// Prefer TextMeshPro? Swap the field types for TMP_Text, everything else stays identical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private Text armyLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text bestLabel;
        [SerializeField] private Text waveLabel;

        [Header("Recruit progress")]
        [Tooltip("Fill bar drawn with anchorMax.x. Leave empty to hide the progress bar.")]
        [SerializeField] private RectTransform recruitFill;

        [SerializeField] private Text recruitLabel;

        [Header("Runner HUD (auto-created when empty)")]
        [Tooltip("Coin wallet counter, bottom of the right column.")]
        [SerializeField] private Text coinsLabel;

        [Tooltip("Level progress fill, drawn with anchorMax.x like the recruit bar.")]
        [SerializeField] private RectTransform progressFill;

        [Tooltip("Opens the pause overlay. Calls GameManager.TogglePause().")]
        [SerializeField] private Button pauseButton;

        [Header("Toast")]
        [Tooltip("Canvas group faded in and out for gate / level feedback.")]
        [SerializeField] private CanvasGroup toastGroup;

        [SerializeField] private Text toastLabel;

        private GameManager _game;
        private float _armyPunch;
        private float _toastTimer;
        private float _toastDuration = 1.4f;
        private bool _widgetsBuilt;

        public void Bind(GameManager game)
        {
            Unbind();

            _game = game;
            if (_game == null)
            {
                return;
            }

            EnsureWidgets();

            _game.ScoreChanged += HandleScoreChanged;
            _game.ArmyChanged += HandleArmyChanged;
            _game.StateChanged += HandleStateChanged;
            _game.CoinsChanged += HandleCoinsChanged;
            _game.ProgressChanged += HandleProgressChanged;
            _game.ToastRequested += HandleToastRequested;

            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(HandlePauseClicked);
                pauseButton.onClick.AddListener(HandlePauseClicked);
            }

            HandleScoreChanged(_game.Score);
            HandleArmyChanged(_game.Army != null ? _game.Army.SoldierCount : 0, _game.Settings.maxSoldiers);
            HandleCoinsChanged(_game.Coins);
            HandleProgressChanged(_game.LevelProgress01);
            UpdateBestLabel();
            UpdateRecruit(0f);
        }

        private void Unbind()
        {
            if (_game == null)
            {
                return;
            }

            _game.ScoreChanged -= HandleScoreChanged;
            _game.ArmyChanged -= HandleArmyChanged;
            _game.StateChanged -= HandleStateChanged;
            _game.CoinsChanged -= HandleCoinsChanged;
            _game.ProgressChanged -= HandleProgressChanged;
            _game.ToastRequested -= HandleToastRequested;
            _game = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (_game == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;

            float punchT = 1f - Mathf.Exp(-10f * deltaTime);
            _armyPunch = Mathf.Lerp(_armyPunch, 0f, punchT);

            if (armyLabel != null)
            {
                armyLabel.rectTransform.localScale = Vector3.one * (1f + _armyPunch * 0.35f);
            }

            if (waveLabel != null)
            {
                if (_game.LevelNumber > 0)
                {
                    waveLabel.text = $"LEVEL {_game.LevelNumber}";
                }
                else
                {
                    float difficulty = _game.Settings.Difficulty01(_game.RunTime);
                    waveLabel.text = $"WAVE {1 + Mathf.FloorToInt(difficulty * 9f)}";
                }
            }

            if (_game.Settings.killsPerRecruit > 0)
            {
                int perRecruit = _game.Settings.killsPerRecruit;
                float progress = (_game.Kills % perRecruit) / (float)perRecruit;
                UpdateRecruit(progress);
            }

            UpdateToast(deltaTime);
        }

        // ------------------------------------------------------------------------------------
        // Event handlers
        // ------------------------------------------------------------------------------------

        private void HandleScoreChanged(int score)
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = score.ToString("N0");
            }

            UpdateBestLabel();
        }

        private void HandleArmyChanged(int current, int max)
        {
            if (armyLabel != null)
            {
                armyLabel.text = current.ToString();
            }

            _armyPunch = 1f;
        }

        private void HandleCoinsChanged(int coins)
        {
            if (coinsLabel != null)
            {
                coinsLabel.text = $"{coins:N0} COINS";
            }
        }

        private void HandleProgressChanged(float progress01)
        {
            if (progressFill != null)
            {
                progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress01), progressFill.anchorMax.y);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                UpdateBestLabel();
            }
        }

        private void HandleToastRequested(string message, bool positive)
        {
            if (toastLabel == null)
            {
                return;
            }

            toastLabel.text = message;
            toastLabel.color = positive ? UiFactory.SuccessColor : UiFactory.DangerColor;

            _toastDuration = _game != null && _game.Settings != null
                ? Mathf.Max(0.4f, _game.Settings.gateToastDuration)
                : 1.4f;

            _toastTimer = _toastDuration;

            if (toastGroup != null)
            {
                toastGroup.alpha = 1f;
            }
        }

        private void HandlePauseClicked()
        {
            if (_game != null)
            {
                _game.TogglePause();
            }
        }

        private void UpdateToast(float deltaTime)
        {
            if (_toastTimer <= 0f)
            {
                return;
            }

            _toastTimer -= deltaTime;

            if (toastGroup != null)
            {
                // Full alpha for most of the duration, then a quick fade on the last 0.35 s.
                toastGroup.alpha = Mathf.Clamp01(_toastTimer / 0.35f);
            }
        }

        private void UpdateBestLabel()
        {
            if (bestLabel != null && _game != null)
            {
                bestLabel.text = $"BEST {_game.BestScore:N0}";
            }
        }

        private void UpdateRecruit(float progress)
        {
            if (recruitFill != null)
            {
                recruitFill.anchorMax = new Vector2(Mathf.Clamp01(progress), recruitFill.anchorMax.y);
            }

            if (recruitLabel != null && _game != null)
            {
                int perRecruit = Mathf.Max(1, _game.Settings.killsPerRecruit);
                int remaining = perRecruit - (_game.Kills % perRecruit);
                recruitLabel.text = $"{remaining} KILLS TO +1";
            }
        }

        // ------------------------------------------------------------------------------------
        // Runtime widgets (upgraded scenes)
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Creates the runner widgets that are missing from the scene. Assigned fields (authored HUD)
        /// are never touched, so this is safe to run on every Bind.
        /// </summary>
        private void EnsureWidgets()
        {
            if (_widgetsBuilt)
            {
                return;
            }

            _widgetsBuilt = true;

            if (coinsLabel == null)
            {
                coinsLabel = UiFactory.MakeLabel(transform, "CoinsLabel", "0 COINS", 34, UiFactory.GoldColor, TextAnchor.UpperRight);
                UiFactory.Place(coinsLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -100f), new Vector2(440f, 46f));
            }

            if (progressFill == null)
            {
                RawImage back = UiFactory.MakePanel("LevelProgress", transform, new Color(0.08f, 0.1f, 0.16f, 0.92f));
                UiFactory.Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(700f, 16f));

                RawImage fill = UiFactory.MakePanel("Fill", back.transform, UiFactory.SuccessColor);
                RectTransform fillRect = fill.rectTransform;
                UiFactory.Stretch(fillRect);
                fillRect.anchorMax = new Vector2(0f, 1f);
                progressFill = fillRect;
            }

            if (toastLabel == null)
            {
                toastLabel = UiFactory.MakeLabel(transform, "ToastLabel", string.Empty, 54, UiFactory.SuccessColor, TextAnchor.MiddleCenter);
                UiFactory.Place(toastLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1400f, 90f));

                toastGroup = toastLabel.gameObject.GetComponent<CanvasGroup>();
                if (toastGroup == null)
                {
                    toastGroup = toastLabel.gameObject.AddComponent<CanvasGroup>();
                }

                toastGroup.alpha = 0f;
                toastGroup.blocksRaycasts = false;
                toastGroup.interactable = false;
            }
            else if (toastGroup == null)
            {
                toastGroup = toastLabel.gameObject.GetComponent<CanvasGroup>();
            }

            if (pauseButton == null)
            {
                pauseButton = UiFactory.MakeButton(transform, "PauseButton", "II", UiFactory.SecondaryButton, 38);
                UiFactory.Place(pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 48f), new Vector2(110f, 110f));
            }
        }
    }
}
