namespace NumberRush3D.Core
{
    /// <summary>
    /// High level flow of a single run.
    /// </summary>
    public enum GameState
    {
        /// <summary>Scene is loading / systems are being wired. Nothing is simulated.</summary>
        Boot = 0,

        /// <summary>Normal gameplay: enemies spawn, soldiers shoot, the player can swipe.</summary>
        Playing = 1,

        /// <summary>Army was wiped out. Simulation is frozen and the retry panel is visible.</summary>
        GameOver = 2
    }
}
