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
        [Tooltip("Max camera swing rate (deg/sec) while following the heading — caps how fast the view whips when you turn sharply, so you don't lose your bearings. Lower = gentler/easier to track.")]
        [SerializeField] private float followYawMaxSpeed = 130f;
        [Tooltip("Min target speed (m/s) before the stored heading updates — ignores idle jitter / in-place attacks.")]
        [SerializeField] private float headingSpeedThreshold = 0.5f;
        [Tooltip("Don't update the stored heading when movement is more backward than this (dot vs camera-forward) — keeps recenter aimed behind your last forward heading, not your face.")]
        [SerializeField] private float backpedalCutoff = -0.2f;
        [Tooltip("KBM recenter key (gamepad uses L3 / left-stick click). R3 is lock-on.")]
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
        [Range(0f, 1f)]
        [Tooltip("Directional Shake: how much of the kick follows the passed world direction (rest is random spread so it isn't a clean push).")]
        [SerializeField] private float shakeDirectionalBias = 0.75f;
        [Tooltip("Directional Shake: random spread added on top of the biased direction.")]
        [SerializeField] private float shakeRandomSpread = 0.35f;

        [Header("FOV (dynamic kicks — punch on dodge / heavy / finisher / musou; widen on sprint)")]
        [Tooltip("Resting field of view. Auto-initialized from the camera lens in Awake.")]
        [SerializeField] private float baseFov = 50f;
        [Tooltip("Sustained FOV added while sprinting.")]
        [SerializeField] private float sprintFovAdd = 6f;
        [Tooltip("How fast a transient FOV kick relaxes back to rest.")]
        [SerializeField] private float fovKickDecay = 4f;
        [Tooltip("Exponential ease rate of the lens toward the target FOV.")]
        [SerializeField] private float fovBlendSpeed = 8f;

        public static PlayerCameraRig Instance { get; private set; }

        private CinemachineOrbitalFollow _orbit;
        private CinemachineImpulseSource _impulse;
        private CinemachineCamera _cam;    // lens owner on this GameObject — FOV kicks write _cam.Lens.FieldOfView
        private Transform _target;
        private Vector3 _lastTargetPos;
        private float _headingYaw;
        private bool _hasHeading;
        private bool _followMode = true;   // true = stay behind the character (follow); false = manual hold
        private float _fovKick;            // transient additive FOV (dodge/heavy/finisher/musou/kill); decays on its own
        private float _sprintFov;          // eased sustained additive FOV (sprint)
        private bool _sprintFovOn;         // sprint widen target, set each frame by SetSprintFov

        private void Awake()
        {
            Instance = this;
            _orbit = GetComponent<CinemachineOrbitalFollow>();
            _impulse = GetComponent<CinemachineImpulseSource>();
            _cam = GetComponent<CinemachineCamera>();
            if (_cam != null) baseFov = _cam.Lens.FieldOfView;   // respect the authored FOV as the resting value
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

            float yaw = _orbit.HorizontalAxis.Value;
            float pitch = _orbit.VerticalAxis.Value;
            Gamepad pad = Gamepad.current;

            bool locked = TargetLock.Instance != null && TargetLock.Instance.HasTarget;
            if (locked)
            {
                // Locked: frame the focus target (look from behind the player toward it). The right stick is
                // target-switching while locked, so no free-look. Same rate-cap as follow so it eases, not whips.
                Vector3 toT = TargetLock.Instance.TargetPosition - _target.position; toT.y = 0f;
                if (toT.sqrMagnitude > 0.01f)
                {
                    float lockYaw = Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg;
                    float kk = 1f - Mathf.Exp(-recenterStrength * dt);
                    yaw = Mathf.MoveTowardsAngle(yaw, Mathf.LerpAngle(yaw, lockYaw, kk), followYawMaxSpeed * dt);
                    pitch = Mathf.Lerp(pitch, defaultPitch, kk);
                }
                _followMode = true;   // dropping the lock leaves us following, not in a stale manual hold
            }
            else
            {
                // --- orbit (hold Ctrl + mouse, or gamepad right stick); recenter on MMB / L3 ---
                bool orbiting = false;
                if (Input.GetKey(orbitKey))
                {
                    orbiting = true;
                    yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                    pitch += (invertY ? Input.GetAxis("Mouse Y") : -Input.GetAxis("Mouse Y")) * mouseSensitivity;
                }
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

                // FOLLOW (stay behind the heading) vs MANUAL (hold). Recenter (MMB / L3) enters follow; orbiting drops to manual.
                if (Input.GetKeyDown(recenterKey) || (pad != null && pad.leftStickButton.wasPressedThisFrame)) _followMode = true;
                if (orbiting) _followMode = false;

                if (_followMode && _hasHeading)
                {
                    float k = 1f - Mathf.Exp(-recenterStrength * dt);
                    float easedYaw = Mathf.LerpAngle(yaw, _headingYaw, k);
                    // Cap the swing rate so a sharp turn eases the view around instead of whipping it.
                    yaw = Mathf.MoveTowardsAngle(yaw, easedYaw, followYawMaxSpeed * dt);
                    pitch = Mathf.Lerp(pitch, defaultPitch, k);
                }
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            _orbit.HorizontalAxis.Value = Mathf.Repeat(yaw + 180f, 360f) - 180f;   // keep within the wrapped -180..180 range
            _orbit.VerticalAxis.Value = pitch;

            DriveFov();
        }

        /// <summary>Ease the lens FOV toward base + transient kick + sustained sprint widen. Unscaled so it keeps
        /// breathing through hit-stop / slow-mo (which scale Time.deltaTime). The Lens is a struct — read, edit, assign back.</summary>
        private void DriveFov()
        {
            if (_cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (_fovKick > 0.01f) _fovKick = Mathf.MoveTowards(_fovKick, 0f, fovKickDecay * dt * Mathf.Max(1f, _fovKick));
            else _fovKick = 0f;
            _sprintFov = Mathf.MoveTowards(_sprintFov, _sprintFovOn ? sprintFovAdd : 0f, fovBlendSpeed * dt * Mathf.Max(1f, sprintFovAdd));
            _sprintFovOn = false;   // consumed each frame; SetSprintFov re-asserts it while held

            float targetFov = baseFov + _fovKick + _sprintFov;
            LensSettings lens = _cam.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, 1f - Mathf.Exp(-fovBlendSpeed * dt));
            _cam.Lens = lens;
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

        /// <summary>
        /// Directional screen-shake: biases the impulse velocity along <paramref name="worldDir"/> (e.g. away from a hit
        /// source, or along the swing) so the kick reads as coming FROM the threat, with a small random spread kept so it
        /// isn't a clean push. The visible on-screen axis is still filtered by the impulse listener — this is a tendency,
        /// not an exact axis. Falls back to a uniform shake when no direction is given.
        /// </summary>
        public static void Shake(float amount, float duration, Vector3 worldDir)
        {
            if (Instance == null || Instance._impulse == null || !Instance.enableShake) return;
            CinemachineImpulseDefinition def = Instance._impulse.ImpulseDefinition;
            if (def != null) def.ImpulseDuration = Mathf.Max(0.05f, duration);
            Vector3 dir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Random.insideUnitSphere;
            Vector3 vel = (dir * Instance.shakeDirectionalBias + Random.insideUnitSphere * Instance.shakeRandomSpread) * (amount * Instance.shakeForceScale);
            Instance._impulse.GenerateImpulseWithVelocity(vel);
        }

        /// <summary>Transient additive FOV punch (dodge / heavy / finisher / musou / kill). Takes the max so overlapping
        /// kicks don't cancel; decays on its own in <see cref="DriveFov"/>.</summary>
        public static void FovKick(float amount)
        {
            if (Instance != null) Instance._fovKick = Mathf.Max(Instance._fovKick, amount);
        }

        /// <summary>Sustained sprint FOV widen — call every frame sprint is held (the widen eases out on its own
        /// once the calls stop). One-line call from the player's locomotion branch.</summary>
        public static void SetSprintFov(bool sprinting)
        {
            if (Instance != null) Instance._sprintFovOn = sprinting;
        }
    }
}
