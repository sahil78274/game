using System;
using System.Collections;
using NumberRush3D.Army;
using NumberRush3D.Combat;
using NumberRush3D.Controls;
using NumberRush3D.Enemies;
using NumberRush3D.Levels;
using NumberRush3D.UI;
using NumberRush3D.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Composition root of a single run.
    /// It owns the references to every system, injects them in a deterministic order,
    /// tracks score / coins / best score and drives the game over + restart flow.
    ///
    /// Runner upgrade: the manager also owns the run-level counters the new systems need
    /// (<see cref="LevelNumber"/>, <see cref="LevelProgress01"/>, <see cref="Coins"/>), the boss
    /// notifications and a toast channel for gate / level feedback. Free Run (no level selected)
    /// keeps the original endless behaviour.
    ///
    /// Execution order -100 guarantees Awake() runs before every gameplay system.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour, IBattleContext
    {
        private const string BestScoreKey = "NumberRush3D.BestScore";
        private const string CoinsKey = "NumberRush3D.Coins";

        /// <summary>Free Run uses this ramp so the endless mode still reaches full difficulty.</summary>
        private const float FreeRunRampSeconds = 95f;

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
        private LevelDefinition _activeLevel;
        private Enemy _activeBoss;
        private float _bossRumbleTimer;
        private GameState _stateBeforePause = GameState.Playing;

        /// <summary>Latest created manager. Used by UI helpers only - gameplay code uses <see cref="IBattleContext"/>.</summary>
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPlaying => State == GameState.Playing;
        public GameSettings Settings => settings;
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int BestScore { get; private set; }
        public int Coins { get; private set; }
        public float RunTime { get; private set; }

        /// <summary>Campaign level currently running. 0 means Free Run (endless).</summary>
        public int LevelNumber { get; private set; }

        /// <summary>Progress through the current run, 0..1. Drives gates, boss triggers and the HUD bar.</summary>
        public float LevelProgress01 { get; private set; }

        public float EnemyHealthMultiplier { get; private set; } = 1f;
        public float EnemySpeedMultiplier { get; private set; } = 1f;
        public float SpawnIntervalScale { get; private set; } = 1f;
        public int StartingSoldiers { get; private set; }
        public int MaxConcurrentEnemies { get; private set; }

        /// <summary>True when a boss has spawned during this run (level completion needs it dead).</summary>
        public bool BossSpawnedThisRun { get; private set; }

        /// <summary>True while the summoned boss is still alive.</summary>
        public bool BossAlive => _activeBoss != null && _activeBoss.IsAlive;

        /// <summary>
        /// Campaign runs show their own level complete / failed screens; when this is true the
        /// legacy game over panel stays hidden.
        /// </summary>
        public bool SuppressResultPanel { get; set; }

        /// <summary>Level definition currently running (null in Free Run).</summary>
        public LevelDefinition ActiveLevel => _activeLevel;

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

        /// <summary>Wallet changed (level rewards, kills).</summary>
        public event Action<int> CoinsChanged;

        /// <summary>Short piece of feedback for the HUD: (message, isPositive).</summary>
        public event Action<string, bool> ToastRequested;

        /// <summary>Run progress 0..1, polled by the HUD progress bar.</summary>
        public event Action<float> ProgressChanged;

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
            Coins = Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));

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

            ApplyRunProfile();

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
            CoinsChanged?.Invoke(Coins);
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;

                if (_bossRumbleTimer > 0f)
                {
                    _bossRumbleTimer -= Time.deltaTime;
                    if (_bossRumbleTimer <= 0f && cameraRig != null)
                    {
                        cameraRig.SetBossRumble(_activeBoss != null && _activeBoss.IsAlive);
                    }
                }
            }

            UpdateProgress();
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

            if (settings.coinsPerKill > 0)
            {
                AddCoins(settings.coinsPerKill);
            }

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

        public void AddCoins(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            Coins = Mathf.Max(0, Coins + amount);
            PlayerPrefs.SetInt(CoinsKey, Coins);
            CoinsChanged?.Invoke(Coins);
        }

        public void NotifyBossSpawned(Enemy boss)
        {
            if (boss == null)
            {
                return;
            }

            _activeBoss = boss;
            BossSpawnedThisRun = true;
            _bossRumbleTimer = 1.2f;

            if (cameraRig != null)
            {
                cameraRig.SetBossRumble(true);
            }

            ShowToast("BOSS INCOMING", false);
        }

        public void NotifyBossDefeated(Enemy boss)
        {
            if (boss != null && boss != _activeBoss)
            {
                return;
            }

            _activeBoss = null;
            _bossRumbleTimer = 0f;

            if (cameraRig != null)
            {
                cameraRig.SetBossRumble(false);
            }

            ShakeCamera(0.45f);
            ShowToast("BOSS DOWN", true);
        }

        public void ShowToast(string message, bool positive)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            ToastRequested?.Invoke(message, positive);
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

            if (army != null)
            {
                army.StopRunning();
            }

            if (inputSource != null)
            {
                inputSource.SetInteractionEnabled(false);
            }

            SaveBestScore();
            PlayerPrefs.Save();

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

            if (!SuppressResultPanel)
            {
                RunEnded?.Invoke(Score, BestScore);
            }
        }

        /// <summary>
        /// Ends the run as a success: the army stops, input is disabled and the simulation freezes
        /// after a short slow motion beat so the finish reads clearly.
        /// </summary>
        public void CompleteLevel()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            State = GameState.LevelComplete;
            StateChanged?.Invoke(State);

            if (army != null)
            {
                army.StopRunning();
            }

            if (inputSource != null)
            {
                inputSource.SetInteractionEnabled(false);
            }

            SaveBestScore();
            PlayerPrefs.Save();

            if (_gameOverRoutine != null)
            {
                StopCoroutine(_gameOverRoutine);
            }

            _gameOverRoutine = StartCoroutine(LevelCompleteSequence());
        }

        private IEnumerator LevelCompleteSequence()
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, settings.levelEndSlowMotionDuration));
            Time.timeScale = 0f;
            _gameOverRoutine = null;
        }

        /// <summary>Pauses the run (time scale 0) without ending it.</summary>
        public void Pause()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            _stateBeforePause = State;
            State = GameState.Paused;
            Time.timeScale = 0f;
            StateChanged?.Invoke(State);

            if (inputSource != null)
            {
                inputSource.SetInteractionEnabled(false);
            }
        }

        /// <summary>Resumes a paused run.</summary>
        public void Resume()
        {
            if (State != GameState.Paused)
            {
                return;
            }

            State = _stateBeforePause;
            Time.timeScale = 1f;
            StateChanged?.Invoke(State);

            if (inputSource != null)
            {
                inputSource.SetInteractionEnabled(true);
            }
        }

        public void TogglePause()
        {
            if (State == GameState.Paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
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

        private void SaveBestScore()
        {
            if (Score <= BestScore)
            {
                return;
            }

            BestScore = Score;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
        }

        /// <summary>
        /// Free Run progress: elapsed run time against the difficulty ramp. Campaign levels override
        /// this through <see cref="SetRunProfile"/> once a level is running.
        /// </summary>
        private void UpdateProgress()
        {
            float previous = LevelProgress01;

            if (_externalProgress != null)
            {
                LevelProgress01 = Mathf.Clamp01(_externalProgress());
            }
            else
            {
                LevelProgress01 = Mathf.Clamp01(RunTime / FreeRunRampSeconds);
            }

            if (!Mathf.Approximately(previous, LevelProgress01))
            {
                ProgressChanged?.Invoke(LevelProgress01);
            }
        }

        // ------------------------------------------------------------------------------------
        // Campaign hooks (filled in by LevelManager)
        // ------------------------------------------------------------------------------------

        private Func<float> _externalProgress;

        /// <summary>
        /// Reads the level resolved by <see cref="LevelManager"/> (it runs before this Awake) and
        /// installs the run profile: army size, enemy scaling, concurrency and the progress source.
        /// </summary>
        private void ApplyRunProfile()
        {
            _activeLevel = LevelLaunch.ActiveLevel;

            StartingSoldiers = Mathf.Max(1, settings.startingSoldiers);
            MaxConcurrentEnemies = Mathf.Max(2, settings.maxConcurrentEnemies);
            LevelNumber = 0;
            EnemyHealthMultiplier = 1f;
            EnemySpeedMultiplier = 1f;
            SpawnIntervalScale = 1f;
            _externalProgress = null;

            if (_activeLevel == null)
            {
                return;
            }

            LevelNumber = Mathf.Max(1, _activeLevel.levelNumber);
            StartingSoldiers = Mathf.Max(1, _activeLevel.startingSoldiers);
            MaxConcurrentEnemies = Mathf.Max(2, _activeLevel.maxConcurrentEnemies);
            EnemyHealthMultiplier = Mathf.Max(0.05f, _activeLevel.enemyHealthMultiplier);
            EnemySpeedMultiplier = Mathf.Max(0.05f, _activeLevel.enemySpeedMultiplier);
            SpawnIntervalScale = Mathf.Clamp(_activeLevel.spawnIntervalScale, 0.05f, 5f);
            _externalProgress = ReadLevelProgress;

            if (army != null)
            {
                army.OverrideStartSpeed(_activeLevel.runSpeed > 0f ? _activeLevel.runSpeed : settings.runnerStartSpeed);
            }
        }

        private float ReadLevelProgress()
        {
            if (_activeLevel == null || army == null || _activeLevel.trackLength <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(army.DistanceTravelled / _activeLevel.trackLength);
        }

        /// <summary>
        /// Installs the campaign run profile: level number, difficulty scales and the progress source.
        /// Passing <c>null</c> for <paramref name="progress01"/> falls back to the Free Run ramp.
        /// </summary>
        public void SetRunProfile(int levelNumber, float enemyHealthScale, float enemySpeedScale, float spawnIntervalScale, Func<float> progress01)
        {
            LevelNumber = Mathf.Max(0, levelNumber);
            EnemyHealthMultiplier = Mathf.Max(0.05f, enemyHealthScale);
            EnemySpeedMultiplier = Mathf.Max(0.05f, enemySpeedScale);
            SpawnIntervalScale = Mathf.Clamp(spawnIntervalScale, 0.05f, 5f);
            _externalProgress = progress01;
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
