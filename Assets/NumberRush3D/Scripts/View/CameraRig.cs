using NumberRush3D.Core;
using UnityEngine;

namespace NumberRush3D.View
{
    /// <summary>
    /// Owns the gameplay camera: aspect aware framing (see <see cref="CameraFraming"/>),
    /// a soft lateral follow, forward follow for the runner and screen shake driven by
    /// <see cref="IBattleContext.ShakeCamera"/>.
    ///
    /// Runner upgrade: the camera now tracks the army's Z exactly (so the army never drifts on
    /// screen) and only damps the lateral axis, which is what a runner needs.
    /// Everything important happens in LateUpdate after the army has moved.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("Camera to drive. Defaults to the Camera on this GameObject.")]
        [SerializeField] private Camera targetCamera;

        private GameSettings _settings;
        private Transform _follow;
        private Vector3 _followOffset;
        private Vector3 _fallbackPosition;
        private Vector3 _shakeOffset;
        private Quaternion _shakeRotation = Quaternion.identity;
        private Quaternion _baseRotation = Quaternion.identity;
        private float _shakeAmount;
        private float _lateralVelocity;
        private bool _bossRumble;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        /// <summary>True while a boss encounter is running (subtle continuous camera rumble).</summary>
        public bool BossRumbleActive => _bossRumble;

        public void Initialize(GameSettings settings, Transform follow)
        {
            _settings = settings;
            _follow = follow;

            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = settings.cameraFieldOfView;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = settings.skyColor;
                targetCamera.nearClipPlane = 0.3f;
                targetCamera.farClipPlane = 250f;
            }

            ApplyFraming();
        }

        /// <summary>Add impulse to the shake. Values around 0.1 - 0.5 feel right.</summary>
        public void Shake(float intensity)
        {
            _shakeAmount = Mathf.Clamp(_shakeAmount + Mathf.Max(0f, intensity), 0f, 1.5f);
        }

        /// <summary>Subtle continuous rumble used during boss encounters.</summary>
        public void SetBossRumble(bool active)
        {
            _bossRumble = active;
        }

        private void LateUpdate()
        {
            if (_settings == null)
            {
                return;
            }

            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                ApplyFraming();
            }

            float deltaTime = Time.unscaledDeltaTime;

            if (_bossRumble)
            {
                Shake(0.008f);
            }

            // Lateral follow, so a swiping army never drifts out of frame.
            float targetX = _follow != null ? _follow.position.x * _settings.cameraLateralFollow : 0f;
            float x = Mathf.SmoothDamp(
                transform.position.x,
                targetX,
                ref _lateralVelocity,
                Mathf.Max(0.01f, _settings.cameraFollowSmoothTime),
                Mathf.Infinity,
                deltaTime);

            // Forward follow is exact: any lag here would make the army slide up/down the screen.
            float z = _follow != null ? _follow.position.z + _followOffset.z : _fallbackPosition.z;
            float y = _fallbackPosition.y;

            // Shake decays in real time so it still animates while the game is frozen.
            if (_shakeAmount > 0f)
            {
                _shakeAmount = Mathf.Max(0f, _shakeAmount - _settings.shakeDecay * deltaTime);
                _shakeOffset = Random.insideUnitSphere * Mathf.Min(_shakeAmount, 1f) * _settings.maxShakeOffset;
                _shakeRotation = Quaternion.Euler(_shakeOffset.y * 12f, 0f, _shakeOffset.x * 16f);
            }
            else
            {
                _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, 1f - Mathf.Exp(-14f * deltaTime));
                _shakeRotation = Quaternion.Slerp(_shakeRotation, Quaternion.identity, 1f - Mathf.Exp(-14f * deltaTime));
            }

            transform.SetPositionAndRotation(
                new Vector3(x + _shakeOffset.x, y + _shakeOffset.y, z + _shakeOffset.z),
                _baseRotation * _shakeRotation);
        }

        private void ApplyFraming()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            float aspect = targetCamera != null
                ? targetCamera.aspect
                : Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);

            _fallbackPosition = CameraFraming.SolvePosition(_settings, aspect);
            _followOffset = new Vector3(0f, _fallbackPosition.y, _fallbackPosition.z - _settings.armyZ);
            _baseRotation = CameraFraming.SolveRotation(_settings.cameraPitch);

            Vector3 position = _follow != null
                ? new Vector3(0f, _followOffset.y, _follow.position.z + _followOffset.z)
                : _fallbackPosition;

            transform.SetPositionAndRotation(position, _baseRotation);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = _settings.cameraFieldOfView;
                targetCamera.backgroundColor = _settings.skyColor;
            }
        }
    }
}
