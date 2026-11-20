namespace NumberRush3D.Core
{
    /// <summary>
    /// High level flow of a single run.
    /// Values 0-2 keep their original meaning so existing scenes keep deserializing correctly;
    /// new states are only ever appended.
    /// </summary>
    public enum GameState
    {
        /// <summary>Scene is loading / systems are being wired. Nothing is simulated.</summary>
        Boot = 0,

        /// <summary>Normal gameplay: the army runs, enemies spawn, soldiers shoot, the player can swipe.</summary>
        Playing = 1,

        /// <summary>The army was wiped out: the level failed. Simulation is frozen and the retry panel is visible.</summary>
        GameOver = 2,

        /// <summary>The player opened a menu. Simulation is frozen with unscaled UI running.</summary>
        Paused = 3,

        /// <summary>The level objective was reached. Simulation is frozen and the results panel is visible.</summary>
        LevelComplete = 4
    }
}
