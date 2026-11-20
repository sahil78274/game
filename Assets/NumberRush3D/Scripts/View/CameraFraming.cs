using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.View
{
    /// <summary>
    /// Solves the camera distance that keeps the whole lane track on screen for any aspect ratio.
    ///
    /// Two constraints are evaluated and the closer (more negative Z) wins:
    /// 1. Horizontal fit: at the far spawn line the frustum must be at least
    ///    (trackHalfWidth * margin) wide, otherwise enemies pop in from the screen edges.
    /// 2. Vertical fit: the bottom edge of the screen must land behind the army line,
    ///    otherwise the player's own soldiers are cut off in tall/landscape-shifted windows.
    ///
    /// Because the vertical field of view is constant, the horizontal one grows with the aspect
    /// ratio - which is exactly why ultra-wide phones need no manual tuning.
    /// </summary>
    public static class CameraFraming
    {
        public static Vector3 SolvePosition(GameSettings settings, float aspect)
        {
            return SolvePosition(
                settings.cameraPitch,
                settings.cameraFieldOfView,
                aspect,
                settings.cameraHeight,
                settings.TrackHalfWidth * settings.cameraTrackMargin,
                settings.spawnZ + settings.spawnJitterZ,
                settings.armyZ,
                settings.cameraBottomPadding);
        }

        /// <param name="pitchDegrees">Camera pitch, looking down.</param>
        /// <param name="verticalFieldOfView">Vertical FOV in degrees.</param>
        /// <param name="aspect">Screen width / height.</param>
        /// <param name="height">Camera height above the ground plane.</param>
        /// <param name="halfWidth">Half of the track width that must stay visible.</param>
        /// <param name="farZ">The farthest Z that must be inside the frustum (spawn line).</param>
        /// <param name="nearZ">The closest Z that must be inside the frustum (army line).</param>
        /// <param name="nearPadding">Extra world units kept visible behind <paramref name="nearZ"/>.</param>
        public static Vector3 SolvePosition(
            float pitchDegrees,
            float verticalFieldOfView,
            float aspect,
            float height,
            float halfWidth,
            float farZ,
            float nearZ,
            float nearPadding)
        {
            float pitch = Mathf.Clamp(pitchDegrees, 5f, 85f) * Mathf.Deg2Rad;
            float halfVertical = Mathf.Clamp(verticalFieldOfView, 10f, 110f) * 0.5f * Mathf.Deg2Rad;
            float safeAspect = Mathf.Max(0.2f, aspect);

            // Constraint 1 - horizontal fit at the far line.
            float tanHorizontal = Mathf.Tan(halfVertical) * safeAspect;
            float depthNeeded = Mathf.Max(0.01f, halfWidth) / Mathf.Max(0.05f, tanHorizontal);
            float fitZ = farZ - (depthNeeded - height * Mathf.Sin(pitch)) / Mathf.Cos(pitch);

            // Constraint 2 - bottom edge of the screen must sit behind the army.
            float bottomAngle = Mathf.Clamp(pitch + halfVertical, 0.1f, 1.5f);
            float nearFitZ = nearZ - Mathf.Max(0f, nearPadding) - height / Mathf.Tan(bottomAngle);

            return new Vector3(0f, height, Mathf.Min(fitZ, nearFitZ));
        }

        public static Quaternion SolveRotation(float pitchDegrees)
        {
            return Quaternion.Euler(Mathf.Clamp(pitchDegrees, 5f, 85f), 0f, 0f);
        }
    }
}
