using UnityEngine;
using UnityEngine.SceneManagement;

namespace NumberRush3D.Levels
{
    /// <summary>
    /// The one and only way a run starts.
    ///
    /// <see cref="Core.GameManager.Restart"/> already relies on reloading the scene to reset every
    /// pooled system, so campaign levels use the same trick: a level is requested here, the scene
    /// reloads, and <see cref="LevelManager"/> picks the request up during Awake.
    /// </summary>
    public static class LevelLaunch
    {
        /// <summary>Level requested for the next scene load. 0 = Free Run.</summary>
        public static int RequestedLevel { get; private set; }

        /// <summary>True when the player explicitly asked for Free Run (endless mode).</summary>
        public static bool RequestedFreeRun { get; private set; }

        /// <summary>Level resolved by <see cref="LevelManager"/> for the current run. Null = Free Run.</summary>
        public static LevelDefinition ActiveLevel { get; internal set; }

        /// <summary>Requests a campaign level and reloads the scene.</summary>
        public static void StartLevel(int levelNumber)
        {
            RequestedLevel = Mathf.Max(0, levelNumber);
            RequestedFreeRun = false;
            ReloadScene();
        }

        /// <summary>Requests endless mode and reloads the scene.</summary>
        public static void StartFreeRun()
        {
            RequestedLevel = 0;
            RequestedFreeRun = true;
            ReloadScene();
        }

        /// <summary>Returns to the level select screen (which is the boot state of the scene).</summary>
        public static void BackToMenu()
        {
            RequestedLevel = 0;
            RequestedFreeRun = false;
            ActiveLevel = null;
            ReloadScene();
        }

        /// <summary>Replays the active run.</summary>
        public static void Replay()
        {
            ReloadScene();
        }

        /// <summary>Clears the request without reloading (used by tests / editor tooling).</summary>
        public static void Clear()
        {
            RequestedLevel = 0;
            RequestedFreeRun = false;
            ActiveLevel = null;
        }

        private static void ReloadScene()
        {
            Time.timeScale = 1f;

            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex >= 0 ? activeScene.buildIndex : 0);
        }
    }
}
