using System;
using System.Collections;
using NumberRush3D.Army;
using NumberRush3D.Combat;
using NumberRush3D.Controls;
using NumberRush3D.Enemies;
using NumberRush3D.UI;
using NumberRush3D.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Composition root of a single run.
    /// It owns the references to every system, injects them in a deterministic order,
    /// tracks score / best score and drives the game over + restart flow.
    ///
    /// Execution order -100 guarantees Awake() runs before every gameplay system.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour, IBattleContext
    {
        private const string BestScoreKey = "NumberRush3D.BestScore";

        [Header("Configuration")]
        [Tooltip("Required. Create it with Tools > NUMBER RUSH 3D > Build ... or right click > Create > NUMBER RUSH 3D > Game Settings.")]
        [SerializeField] private GameSettings settings;

        [Header("Scene Systems (auto-found when left empty)")]
        [SerializeField] private ArmyController army;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private BulletPool bulletPool;
        [SerializeField] private VfxPool vfxPool;
        [SerializeField] private EnemyRegistry enemies;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private SwipeInput inputSource;
        [SerializeField] private HudController hud;
        [SerializeField] private GameOverPanel gameOverPanel;

        [Header("Game Over")]
        [Tooltip("Slow motion factor applied for a moment before the retry panel appears.")]
        [Range(0.02f, 1f)] [SerializeField] private float gameOverSlowMotion = 0.25f;
        [Tooltip("Real time seconds the slow motion lasts.")]
        [Range(0f, 3f)] [SerializeField] private float gameOverSlowMotionDuration = 0.55f;

        private Coroutine _gameOverRoutine;

        /// <summary>Latest created manager. Used by UI helpers only - gameplay code uses <see cref="IBattleContext"/>.</summary>
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPlaying => State == GameState.Playing;
        public GameSettings Settings => settings;
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int BestScore { get; private set; }
        public float RunTime { get; private set; }

        public ArmyController Army => army;
        public BulletPool Bullets => bulletPool;
        public VfxPool Vfx => vfxPool;
        public EnemyRegistry Enemies => enemies;

        /// <summary>Raised whenever the score changes (also fires once on start with 0).</summary>
        public event Action<int> ScoreChanged;

        /// <summary>(current soldiers, max soldiers)</summary>
        public event Action<int, int> ArmyChanged;

        /// <summary>Raised on every state transition.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Screen shake request in world units.</summary>
        public event Action<float> ShakeRequested;

        /// <summary>(score, best) fired once the game over sequence has finished - this is when the retry panel appears.</summary>
        public event Action<int, int> RunEnded;

        private void Awake()
        {
            // A scene reload while frozen would otherwise leave the new run in slow motion.
            Time.timeScale = 1f;

            Instance = this;

            if (settings == null)
            {
                Debug.LogError("[NumberRush] GameManager has no GameSettings assigned. Disabling gameplay.", this);
                enabled = false;
                return;
            }

            EnsureReferences();

            if (army == null || bulletPool == null || spawner == null)
            {
                Debug.LogError("[NumberRush] GameManager is missing Army / BulletPool / EnemySpawner references. Disabling gameplay.", this);
                enabled = false;
                return;
            }

            Application.targetFrameRate = settings.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);

            // Deterministic injection order: pools -> army -> spawner -> camera.
            if (vfxPool != null)
            {
                vfxPool.Initialize();
            }

            bulletPool.Initialize(settings, vfxPool);

            army.Initialize(settings, this, inputSource);
            army.ArmyCountChanged += HandleArmyCountChanged;

            spawner.Initialize(settings, this, army);

            if (cameraRig != null)
            {
                cameraRig.Initialize(settings, army.transform);
            }

            State = GameState.Playing;
            StateChanged?.Invoke(State);
        }

        private void Start()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            army.Begin();
            spawner.Begin();

            if (hud != null)
            {
                hud.Bind(this);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.Bind(this);
            }

            ScoreChanged?.Invoke(Score);
            ArmyChanged?.Invoke(army.SoldierCount, settings.maxSoldiers);
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;

            if (army != null)
            {
                army.ArmyCountChanged -= HandleArmyCountChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------------------------
        // IBattleContext
        // ------------------------------------------------------------------------------------

        public void ReportKill(EnemyType type)
        {
            if (!IsPlaying || type == null)
            {
                return;
            }

            Kills++;
            Score += Mathf.Max(0, type.scoreValue);
            ScoreChanged?.Invoke(Score);

            int recruits = Mathf.Max(0, type.recruitValue);
            if (settings.killsPerRecruit > 0 && Kills % settings.killsPerRecruit == 0)
            {
                recruits++;
            }

            if (recruits > 0 && army != null)
            {
                army.Recruit(recruits);
            }
        }

        public void ShakeCamera(float intensity)
        {
            if (cameraRig != null)
            {
                cameraRig.Shake(intensity);
            }

            ShakeRequested?.Invoke(intensity);
        }

        public void NotifyArmyWipedOut()
        {
            TriggerGameOver();
        }

        // ------------------------------------------------------------------------------------
        // Flow
        // ------------------------------------------------------------------------------------

        /// <summary>Freezes gameplay, saves the best score and reveals the retry panel.</summary>
        public void TriggerGameOver()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            State = GameState.GameOver;
            StateChanged?.Invoke(State);

            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestScoreKey, BestScore);
                PlayerPrefs.Save();
            }

            if (_gameOverRoutine != null)
            {
                StopCoroutine(_gameOverRoutine);
            }

            _gameOverRoutine = StartCoroutine(GameOverSequence());
        }

        private IEnumerator GameOverSequence()
        {
            // Short slow motion hit before the freeze + panel. Uses real time so it works at any timeScale.
            Time.timeScale = gameOverSlowMotion;
            yield return new WaitForSecondsRealtime(gameOverSlowMotionDuration);

            Time.timeScale = 0f;
            _gameOverRoutine = null;
            RunEnded?.Invoke(Score, BestScore);
        }

        /// <summary>Reloads the active scene. This is the whole restart implementation - no state to reset.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex >= 0 ? activeScene.buildIndex : 0);
        }

        public void QuitApplication()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleArmyCountChanged(int current, int max)
        {
            GameState state = State;
            ArmyChanged?.Invoke(current, max);

            if (state == GameState.Playing && current <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// Fills in missing inspector references so the scene keeps working when a reference is
        /// accidentally cleared. Explicit references always win.
        /// </summary>
        private void EnsureReferences()
        {
            if (army == null) army = FindFirstObjectByType<ArmyController>(FindObjectsInactive.Include);
            if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (bulletPool == null) bulletPool = FindFirstObjectByType<BulletPool>(FindObjectsInactive.Include);
            if (vfxPool == null) vfxPool = FindFirstObjectByType<VfxPool>(FindObjectsInactive.Include);
            if (enemies == null) enemies = FindFirstObjectByType<EnemyRegistry>(FindObjectsInactive.Include);
            if (cameraRig == null) cameraRig = FindFirstObjectByType<CameraRig>(FindObjectsInactive.Include);
            if (inputSource == null) inputSource = FindFirstObjectByType<SwipeInput>(FindObjectsInactive.Include);
            if (hud == null) hud = FindFirstObjectByType<HudController>(FindObjectsInactive.Include);
            if (gameOverPanel == null) gameOverPanel = FindFirstObjectByType<GameOverPanel>(FindObjectsInactive.Include);

            if (enemies == null)
            {
                // The registry is a passive book-keeping component, so creating one on the fly is safe.
                enemies = gameObject.AddComponent<EnemyRegistry>();
            }
        }
    }
}
