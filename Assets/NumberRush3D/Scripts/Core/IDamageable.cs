using UnityEngine;

namespace NumberRush3D.Core
{
    /// <summary>
    /// Anything a bullet can hurt. Implemented by <see cref="NumberRush3D.Combat.Health"/>.
    /// Keeping this an interface means weapons never need to know what they are shooting at.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>False once the target has been destroyed (but the object may still exist in a pool).</summary>
        bool IsAlive { get; }

        /// <summary>Apply damage. <paramref name="hitPoint"/> is the world space contact position, used for VFX.</summary>
        void TakeDamage(float amount, Vector3 hitPoint);
    }
}
