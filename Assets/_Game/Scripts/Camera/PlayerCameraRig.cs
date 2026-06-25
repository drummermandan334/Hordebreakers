using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Cinemachine;

namespace Hordebreakers
{
    /// <summary>
    /// Drives a Cinemachine <see cref="CinemachineOrbitalFollow"/> rig with the musou "stay behind the
    /// heading" feel of the old hand-rolled camera, while Cinemachine owns the geometry, follow damping,
    /// wall-avoidance (Deoccluder) and screen-shake (Impulse).
    ///
    /// The OrbitalFollow MUST use BindingMode = WorldSpace, so <c>HorizontalAxis.Value</c> is a world yaw
    /// (0 = looking toward +Z, camera behind along -Z) and <c>VerticalAxis.Value</c> is the pitch — this
    /// just writes those two axes each LateUpdate.
    ///
    /// Stay-behind: the heading comes from the target's ACTUAL velocity and deliberately does NOT chase a
    /// straight-back heading (the 180° spin) — backpedalling just moonwalks toward screen. Orbit: HOLD Ctrl
    /// and move the mouse, or push the gamepad right stick; releasing eases the view back behind the heading.
    /// </summary>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class PlayerCameraRig : MonoBehaviour
    {
        [Header("Camera follow / manual (recenter R3 or MMB = follow behind; moving the camera = manual hold)")]
        [Tooltip("Pitch the camera holds in follow mode (degrees; Elden-Ring-adjacent, behind & slightly above).")]
        [SerializeField] private float defaultPitch = 15f;
        [Tooltip("Rate the camera eases to stay behind the heading in FOLLOW mode. Higher = follows/snaps faster.")]
        [FormerlySerializedAs("autoFollowStrength")]
        [SerializeField] private float recenterStrength = 6f;
        [Tooltip("Min target speed (m/s) before the stored heading updates — ignores idle jitter / in-place attacks.")]
        [SerializeField] private float headingSpeedThreshold = 0.5f;
        [Tooltip("Don't update the stored heading when movement is more backward than this (dot vs camera-forward) — keeps recenter aimed behind your last forward heading, not your face.")]
        [SerializeField] private float backpedalCutoff = -0.2f;
        [Tooltip("KBM recenter key (gamepad uses R3 / right-stick click).")]
        [SerializeField] private KeyCode recenterKey = KeyCode.Mouse2;

        [Header("Manual orbit (hold to look around)")]
        [SerializeField] private KeyCode orbitKey = KeyCode.LeftControl;
        [Tooltip("Degrees per unit of mouse delta (mouse delta is per-frame, so no deltaTime).")]
        [SerializeField] private float mouseSensitivity = 3f;
        [Tooltip("Gamepad right-stick orbit speed, degrees/second.")]
        [SerializeField] private float gamepadLookSpeed = 180f;
        [SerializeField] private float stickDeadzone = 0.15f;
        [SerializeField] private bool invertY = true;   // stick/mouse up = look up
        [SerializeField] private float minPitch = -5f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Shake (Cinemachine Impulse)")]
        [Tooltip("Master on/off for camera shake.")]
        [SerializeField] private bool enableShake = true;
        [Tooltip("Scales the legacy Shake(amount, ..) value into impulse velocity (tune to match the old feel).")]
        [SerializeField] private float shakeForceScale = 8f;

        public static PlayerCameraRig Instance { get; private set; }

        private CinemachineOrbitalFollow _orbit;
        private CinemachineImpulseSource _impulse;
        private Transform _target;
        private Vector3 _lastTargetPos;
        private float _headingYaw;
        private bool _hasHeading;
        private bool _followMode = true;   // true = stay behind the character (follow); false = manual hold

        private void Awake()
        {
            Instance = this;
            _orbit = GetComponent<CinemachineOrbitalFollow>();
            _impulse = GetComponent<CinemachineImpulseSource>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            _target = _orbit.FollowTarget;
            if (_target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _target = p.transform;
            }
            if (_target != null)
            {
                _lastTargetPos = _target.position;
                _headingYaw = _target.eulerAngles.y;
                _hasHeading = true;
                _orbit.HorizontalAxis.Value = _headingYaw;
            }
            _orbit.VerticalAxis.Value = defaultPitch;
        }

        private void LateUpdate()
        {
            if (_target == null) { _target = _orbit.FollowTarget; return; }
            float dt = Time.deltaTime;

            // --- heading from ACTUAL movement; don't chase a straight-back heading (prevents the spin) ---
            Vector3 delta = _target.position - _lastTargetPos; delta.y = 0f;
            _lastTargetPos = _target.position;
            if (dt > 0f && delta.magnitude / dt > headingSpeedThreshold)
            {
                Vector3 hdir = delta.normalized;
                float camYaw = _orbit.HorizontalAxis.Value * Mathf.Deg2Rad;
                Vector3 camFwd = new Vector3(Mathf.Sin(camYaw), 0f, Mathf.Cos(camYaw));   // yaw 0 => +Z
                if (Vector3.Dot(hdir, camFwd) >= backpedalCutoff)
                {
                    _headingYaw = Mathf.Atan2(hdir.x, hdir.z) * Mathf.Rad2Deg;
                    _hasHeading = true;
                }
            }

            // --- orbit (hold Ctrl + mouse, or gamepad right stick); recenter only on R3 / MMB ---
            float yaw = _orbit.HorizontalAxis.Value;
            float pitch = _orbit.VerticalAxis.Value;
            bool orbiting = false;

            if (Input.GetKey(orbitKey))
            {
                orbiting = true;
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch += (invertY ? Input.GetAxis("Mouse Y") : -Input.GetAxis("Mouse Y")) * mouseSensitivity;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > stickDeadzone * stickDeadzone)
                {
                    orbiting = true;
                    yaw += rs.x * gamepadLookSpeed * dt;
                    pitch += (invertY ? rs.y : -rs.y) * gamepadLookSpeed * dt;
                }
            }

            // Modes: FOLLOW (stay behind the heading, even as the character turns) vs MANUAL (hold where you
            // put it). Recenter (R3 / MMB) enters follow; moving the camera (orbiting) drops to manual.
            if (Input.GetKeyDown(recenterKey) || (pad != null && pad.rightStickButton.wasPressedThisFrame)) _followMode = true;
            if (orbiting) _followMode = false;

            if (_followMode && _hasHeading)
            {
                float k = 1f - Mathf.Exp(-recenterStrength * dt);
                yaw = Mathf.LerpAngle(yaw, _headingYaw, k);
                pitch = Mathf.Lerp(pitch, defaultPitch, k);
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            _orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;   // keep within the wrapped -180..180 range
            _orbit.VerticalAxis.Value = pitch;
        }

        /// <summary>
        /// Additive screen-shake via a Cinemachine impulse. Kept as a static facade so existing callers
        /// (combat hit-juice, the elite slam) fire camera shake without holding a reference.
        /// </summary>
        public static void Shake(float amount, float duration)
        {
            if (Instance == null || Instance._impulse == null || !Instance.enableShake) return;
            CinemachineImpulseDefinition def = Instance._impulse.ImpulseDefinition;
            if (def != null) def.ImpulseDuration = Mathf.Max(0.05f, duration);
            Instance._impulse.GenerateImpulseWithVelocity(Random.insideUnitSphere * (amount * Instance.shakeForceScale));
        }
    }
}
