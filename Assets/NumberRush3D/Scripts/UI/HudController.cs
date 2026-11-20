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

        private GameManager _game;
        private float _armyPunch;

        public void Bind(GameManager game)
        {
            Unbind();

            _game = game;
            if (_game == null)
            {
                return;
            }

            _game.ScoreChanged += HandleScoreChanged;
            _game.ArmyChanged += HandleArmyChanged;
            _game.StateChanged += HandleStateChanged;

            HandleScoreChanged(_game.Score);
            HandleArmyChanged(_game.Army != null ? _game.Army.SoldierCount : 0, _game.Settings.maxSoldiers);
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

            float punchT = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            _armyPunch = Mathf.Lerp(_armyPunch, 0f, punchT);

            if (armyLabel != null)
            {
                armyLabel.rectTransform.localScale = Vector3.one * (1f + _armyPunch * 0.35f);
            }

            if (waveLabel != null)
            {
                float difficulty = _game.Settings.Difficulty01(_game.RunTime);
                waveLabel.text = $"WAVE {1 + Mathf.FloorToInt(difficulty * 9f)}";
            }

            if (_game.Settings.killsPerRecruit > 0)
            {
                int perRecruit = _game.Settings.killsPerRecruit;
                float progress = (_game.Kills % perRecruit) / (float)perRecruit;
                UpdateRecruit(progress);
            }
        }

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

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                UpdateBestLabel();
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
    }
}
