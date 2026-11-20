using NumberRush3D.Army;
using NumberRush3D.Combat;
using NumberRush3D.Enemies;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Every runtime system talks to the rest of the game through this single interface.
    /// <see cref="GameManager"/> is the only implementation, which keeps the gameplay
    /// components free of singletons and makes them trivial to unit test or reuse.
    /// </summary>
    public interface IBattleContext
    {
        GameSettings Settings { get; }
        GameState State { get; }
        bool IsPlaying { get; }
        int Score { get; }
        int Kills { get; }

        ArmyController Army { get; }
        BulletPool Bullets { get; }
        VfxPool Vfx { get; }
        EnemyRegistry Enemies { get; }

        /// <summary>Called by an enemy when it dies from damage (not when it reaches the army line).</summary>
        void ReportKill(EnemyType type);

        /// <summary>Request a camera shake. Intensity is in world units, typical values are 0.1 - 0.5.</summary>
        void ShakeCamera(float intensity);

        /// <summary>Called by the army when the last soldier is gone.</summary>
        void NotifyArmyWipedOut();
    }
}
