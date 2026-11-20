using System.Collections;
using NumberRush3D.Army;
using NumberRush3D.Core;
using NumberRush3D.Enemies;
using NumberRush3D.Gates;
using NumberRush3D.Track;
using NumberRush3D.UI;
using UnityEngine;

namespace NumberRush3D.Levels
{
    /// <summary>
    /// Campaign flow controller. One instance lives on the Systems object next to
    /// <see cref="GameManager"/>.
    ///
    /// Responsibilities:
    /// * resolve the requested level during Awake (execution order -110, so GameManager's -100 Awake
    ///   can already read <see cref="LevelLaunch.ActiveLevel"/> and build the run profile),
    /// * start the endless track + gate streams (also in the menu state, so the scene is never empty),
    /// * apply the level's enemy types, boss schedule and gate pattern,
    /// * detect completion (distance reached + boss resolved), award stars / coins and show the
    ///   level complete or failed screen,
    /// * fall back to Free Run when no level is requested.
    ///
    /// Everything is optional: every reference is resolved defensively, so the component is safe to
    /// drop into an existing scene (that is exactly what menu item 7 does).
    /// </summary>
    [DefaultExecutionOrder(-110)]
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour
    {
        [Header("Wiring (auto-found when empty)")]
        [SerializeField] private GameManager game;
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private TrackManager track;
        [SerializeField] private GateManager gates;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private HudController hud;
        [SerializeField] private GameOverPanel legacyGameOverPanel;

        [Header("Screens (created at runtime when empty)")]
        [SerializeField] private LevelSelectScreen levelSelect;
        [SerializeField] private LevelCompletePanel completePanel;
        [SerializeField] private LevelFailedPanel failedPanel;
        [SerializeField] private PausePanel pausePanel;

        [Header("Flow")]
        [Tooltip("Open the level select when the scene starts without a pending level request.")]
        [SerializeField] private bool showLevelSelectOnBoot = true;

        [Tooltip("Create the campaign screens at runtime when they are not present in the scene.")]
        [SerializeField] private bool buildMissingScreens = true;

        [Tooltip("Extra coins per earned star.")]
        [Range(0, 100)] [SerializeField] private int coinBonusPerStar = 10;

        private LevelDefinition _level;
        private ArmyController _army;
        private bool _bootMenu;
        private bool _started;
        private int _peakArmy;
        private int _lastStars;
        private int _lastCoins;
        private bool _completionHandled;
        private bool _failureHandled;

        /// <summary>Level currently being played, or null in Free Run.</summary>
        public LevelDefinition CurrentLevel => _level;

        /// <summary>Catalog used by the level select screen.</summary>
        public LevelCatalog Catalog => catalog;

        // ------------------------------------------------------------------------------------
        // Boot
        // ------------------------------------------------------------------------------------

        private void Awake()
        {
            _level = catalog != null ? catalog.Find(LevelLaunch.RequestedLevel) : null;

            // Publish before GameManager's Awake (-100) so it can install the run profile.
            LevelLaunch.ActiveLevel = _level;

            bool wantsFreeRun = LevelLaunch.RequestedFreeRun;
            _bootMenu = _level == null && !wantsFreeRun && showLevelSelectOnBoot && catalog != null;
        }

        private void Update()
        {
            if (!_started)
            {
                StartRun();
                return;
            }

            if (_level == null || game == null || !game.IsPlaying || _completionHandled)
            {
                return;
            }

            if (game.LevelProgress01 < 1f)
            {
                return;
            }

            // Boss levels only finish once the boss is gone (it is scheduled before the finish line).
            if (_level.HasBoss && game.BossSpawnedThisRun && game.BossAlive)
            {
                return;
            }

            HandleLevelCompleted();
        }

        /// <summary>
        /// Real start. It runs from the first Update instead of Start() because Start() order follows
        /// execution order, and this component (-110) must not run before GameManager (-100) has
        /// begun the army and the spawner.
        /// </summary>
        private void StartRun()
        {
            _started = true;

            ResolveReferences();

            if (game == null)
            {
                // ResolveReferences already logged the reason; nothing else can run without a context.
                enabled = false;
                return;
            }

            ApplyLevelSetup();
            StartStreaming();
            BindScreens();

            if (game != null)
            {
                game.ArmyChanged += HandleArmyChanged;
                game.StateChanged += HandleStateChanged;
            }

            _army = game != null ? game.Army : FindFirstObjectByType<ArmyController>();
            _peakArmy = _army != null ? _army.SoldierCount : 1;

            if (_bootMenu)
            {
                // Freeze gameplay but keep the streaming systems alive so the scene renders behind
                // the menu (track slices, gates, lighting) instead of showing an empty void.
                if (game != null)
                {
                    game.Pause();
                }

                if (pausePanel != null)
                {
                    pausePanel.Hide();
                }

                if (levelSelect != null)
                {
                    levelSelect.Show();
                }
            }
            else if (levelSelect != null)
            {
                levelSelect.Hide();
            }
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.ArmyChanged -= HandleArmyChanged;
                game.StateChanged -= HandleStateChanged;
                game.SuppressResultPanel = false;
            }
        }

        // ------------------------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------------------------

        private void ResolveReferences()
        {
            if (game == null)
            {
                game = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            }

            if (game == null)
            {
                Debug.LogError("[NumberRush] LevelManager needs a GameManager in the scene. Campaign disabled.", this);
                enabled = false;
                return;
            }

            _army = game.Army;

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<EnemySpawner>();
            }

            if (track == null)
            {
                track = GetComponent<TrackManager>();
                if (track == null)
                {
                    track = FindFirstObjectByType<TrackManager>();
                }
            }

            if (gates == null)
            {
                gates = GetComponent<GateManager>();
                if (gates == null)
                {
                    gates = FindFirstObjectByType<GateManager>();
                }
            }

            if (hud == null)
            {
                hud = FindFirstObjectByType<HudController>();
            }

            if (legacyGameOverPanel == null)
            {
                legacyGameOverPanel = FindFirstObjectByType<GameOverPanel>();
            }

            if (catalog == null)
            {
                // Last resort: the first catalog asset in the project.
                LevelCatalog[] catalogs = Resources.FindObjectsOfTypeAll<LevelCatalog>();
                if (catalogs != null && catalogs.Length > 0)
                {
                    catalog = catalogs[0];
                    LevelLaunch.ActiveLevel = _level = catalog.Find(LevelLaunch.RequestedLevel);
                }
            }
        }

        private void ApplyLevelSetup()
        {
            // Campaign runs use their own result screens instead of the legacy game over panel.
            game.SuppressResultPanel = _level != null;

            if (_level == null)
            {
                return;
            }

            if (spawner != null)
            {
                spawner.SetEnabledTypes(_level.enemyTypes);
                spawner.ScheduleBoss(_level.bossType, _level.bossProgress);
            }

            if (gates != null)
            {
                gates.SetScriptedRows(_level.gateRows);
            }

            if (_army != null && _level.runSpeed > 0f)
            {
                _army.SetForwardSpeed(_level.runSpeed);
            }

            if (hud != null)
            {
                hud.Bind(game);
            }
        }

        private void StartStreaming()
        {
            GameSettings settings = game.Settings;

            if (track != null)
            {
                track.Initialize(settings, game);
                track.Begin(_army != null ? _army.WorldZ : settings.armyZ);
            }

            if (gates != null)
            {
                gates.Initialize(settings, game, _army);
                gates.Begin();
            }
        }

        private void BindScreens()
        {
            if (buildMissingScreens)
            {
                EnsureScreens();
            }

            if (levelSelect != null)
            {
                levelSelect.Bind(catalog);
                levelSelect.Hide();
            }

            if (completePanel != null)
            {
                completePanel.Bind();
            }

            if (failedPanel != null)
            {
                failedPanel.Bind();
            }

            if (pausePanel != null)
            {
                pausePanel.Bind(game);
            }
        }

        /// <summary>Creates any missing campaign screen on the scene canvas, above the HUD.</summary>
        private void EnsureScreens()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (levelSelect == null)
            {
                levelSelect = FindFirstObjectByType<LevelSelectScreen>(FindObjectsInactive.Include);
            }

            if (completePanel == null)
            {
                completePanel = FindFirstObjectByType<LevelCompletePanel>(FindObjectsInactive.Include);
            }

            if (failedPanel == null)
            {
                failedPanel = FindFirstObjectByType<LevelFailedPanel>(FindObjectsInactive.Include);
            }

            if (pausePanel == null)
            {
                pausePanel = FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            }

            if (levelSelect == null)
            {
                levelSelect = CreateScreen<LevelSelectScreen>(canvas.transform, "LevelSelectScreen");
            }

            if (completePanel == null)
            {
                completePanel = CreateScreen<LevelCompletePanel>(canvas.transform, "LevelCompletePanel");
            }

            if (failedPanel == null)
            {
                failedPanel = CreateScreen<LevelFailedPanel>(canvas.transform, "LevelFailedPanel");
            }

            if (pausePanel == null)
            {
                pausePanel = CreateScreen<PausePanel>(canvas.transform, "PausePanel");
            }
        }

        private static T CreateScreen<T>(Transform parent, string name) where T : Component
        {
            GameObject screen = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)screen.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return screen.AddComponent<T>();
        }

        // ------------------------------------------------------------------------------------
        // Completion / failure
        // ------------------------------------------------------------------------------------

        private void HandleArmyChanged(int current, int max)
        {
            if (current > _peakArmy)
            {
                _peakArmy = current;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver || _level == null || _failureHandled)
            {
                return;
            }

            _failureHandled = true;
            StartCoroutine(ShowFailedAfterBeat());
        }

        private IEnumerator ShowFailedAfterBeat()
        {
            // Wait out the hit-stop so the panel appears exactly when the simulation freezes.
            yield return new WaitForSecondsRealtime(0.5f);

            if (failedPanel == null)
            {
                yield break;
            }

            int army = _army != null ? _army.SoldierCount : 0;
            failedPanel.Show(_level != null ? _level.DisplayName : "FREE RUN", game.LevelProgress01, game.Score, army);
        }

        private void HandleLevelCompleted()
        {
            _completionHandled = true;

            int stars = ComputeStars();
            int coins = (_level != null ? _level.coinReward : 0) + stars * Mathf.Max(0, coinBonusPerStar);
            int army = _army != null ? _army.SoldierCount : 0;

            _lastStars = stars;
            _lastCoins = coins;

            if (_level != null)
            {
                LevelProgressStore.CompleteLevel(_level.levelNumber, stars);
            }

            if (coins > 0)
            {
                game.AddCoins(coins);
            }

            game.CompleteLevel();

            StartCoroutine(ShowCompleteAfterBeat(stars, coins, army));
        }

        private IEnumerator ShowCompleteAfterBeat(int stars, int coins, int army)
        {
            yield return new WaitForSecondsRealtime(game.Settings.levelEndSlowMotionDuration + 0.1f);

            if (completePanel == null)
            {
                yield break;
            }

            bool hasNext = catalog != null && _level != null && catalog.Next(_level.levelNumber) != null;
            completePanel.Show(_level, stars, game.Score, coins, army, hasNext);
        }

        /// <summary>3 stars = kept most of the peak army, 2 = kept a good part, 1 = finished.</summary>
        private int ComputeStars()
        {
            if (_level == null || _army == null || _peakArmy <= 0)
            {
                return 1;
            }

            float ratio = Mathf.Clamp01(_army.SoldierCount / (float)_peakArmy);

            if (ratio >= _level.threeStarArmyFraction)
            {
                return 3;
            }

            return ratio >= _level.twoStarArmyFraction ? 2 : 1;
        }

        // ------------------------------------------------------------------------------------
        // Public helpers (used by tooling / tests)
        // ------------------------------------------------------------------------------------

        /// <summary>Stars awarded by the last completed level (0 before the first completion).</summary>
        public int LastStars => _lastStars;

        /// <summary>Coins awarded by the last completed level.</summary>
        public int LastCoins => _lastCoins;
    }
}
