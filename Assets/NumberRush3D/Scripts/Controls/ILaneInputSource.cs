namespace NumberRush3D.Controls
{
    /// <summary>
    /// Abstraction over "the player wants to change lane".
    /// <see cref="SwipeInput"/> is the shipped implementation; swap it for a bot,
    /// an on-screen button or a replay driver without touching the army code.
    /// </summary>
    public interface ILaneInputSource
    {
        /// <summary>
        /// Returns true at most once per requested lane change.
        /// <paramref name="direction"/> is -1 (left) or +1 (right).
        /// </summary>
        bool TryConsumeLaneStep(out int direction);
    }
}
