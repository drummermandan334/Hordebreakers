using UnityEngine;
using UnityEngine.InputSystem;

namespace Hordebreakers
{
    /// <summary>
    /// Musou / Dynasty-Warriors-style action camera: behind-and-slightly-above the player at a
    /// SHALLOW downward tilt, pulled back with a wide FOV so the surrounding horde reads, player
    /// low-center.
    ///
    /// Default behavior: the camera STAYS BEHIND the player's movement heading (derived from the
    /// target's actual velocity). It deliberately does NOT re-aim when the player moves back toward
    /// the camera (holding "back") — chasing a straight-back heading is a 180-degree instability that
    /// spins the view; instead the player just moonwalks toward screen, musou-style.
    ///
    /// Look around: HOLD the orbit key (CTRL) and mouse X/Y orbits, OR push the gamepad right stick.
    /// Releasing returns the camera behind the player. Wall avoidance: a spherecast from the pivot to
    /// the desired position pulls the camera in so it never clips through level geometry. Position
    /// follow is SmoothDamp; Shake() is additive on unscaled time so it survives hit-stop.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Rig (musou defaults)")]
        [Tooltip("Downward tilt of the rig. Shallow for musou (not top-down).")]
        [SerializeField] private float pitch = 20f;
        [Tooltip("Distance the camera sits behind/above the pivot.")]
        [SerializeField] private float distance = 8f;
        [Tooltip("Height of the orbit pivot above the player.")]
        [SerializeField] private float height = 2.3f;
        [Tooltip("Look-at point height above the player's feet (keeps the player low-center).")]
        [SerializeField] private float lookAtHeight = 1.3f;
        [Tooltip("Wide FOV so a chunk of the horde stays on screen. Pushed to the Camera live.")]
        [SerializeField] private float fieldOfView = 65f;
        [Tooltip("SmoothDamp time for position follow.")]
        [SerializeField] private float followSmooth = 0.12f;

        [Header("Stay-behind")]
        [Tooltip("Rate the camera eases back behind the heading when NOT orbiting (and after releasing the orbit input). Higher = snaps behind faster.")]
        [SerializeField] private float autoFollowStrength = 5f;
        [Tooltip("Min player speed (m/s) before heading updates — ignores idle jitter and in-place attack faces.")]
        [SerializeField] private float headingSpeedThreshold = 0.5f;
        [Tooltip("Stop re-aiming behind when movement is more backward than this (dot vs camera-forward). Prevents the spin when holding 'back'. -1 = always follow, 0 = only follow the forward hemisphere.")]
        [SerializeField] private float backpedalCutoff = -0.2f;

        [Header("Manual orbit (hold to look around)")]
        [Tooltip("Hold this to orbit with the mouse; release returns the camera behind the player.")]
        [SerializeField] private KeyCode orbitKey = KeyCode.LeftControl;
        [Tooltip("Degrees per unit of mouse delta. Mouse X/Y is already per-frame, so no Time.deltaTime.")]
        [SerializeField] private float mouseSensitivity = 3f;
        [Tooltip("Gamepad right-stick orbit speed, degrees/second.")]
        [SerializeField] private float gamepadLookSpeed = 180f;
        [Tooltip("Right-stick deflection below this is ignored (deadzone).")]
        [SerializeField] private float stickDeadzone = 0.15f;
        [SerializeField] private bool invertMouseY = false;
        [SerializeField] private float minPitch = -5f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Wall avoidance")]
        [Tooltip("Layers the camera collides with. Set to your level geometry (Default) — NOT player/enemies.")]
        [SerializeField] private LayerMask collisionMask = 1;        // Default layer
        [Tooltip("Spherecast radius so the near plane doesn't poke through walls.")]
        [SerializeField] private float collisionRadius = 0.3f;
        [Tooltip("Extra gap kept between the camera and a wall it hits.")]
        [SerializeField] private float collisionBuffer = 0.25f;
        [Tooltip("Never pull the camera closer to the pivot than this.")]
        [SerializeField] private float collisionMinDistance = 0.8f;

        public static ThirdPersonCamera Instance { get; private set; }

        private Camera _cam;
        private float _yaw;
        private float _pitchNow;
        private float _headingYaw;
        private Vector3 _vel;
        private Vector3 _smoothPos;     // logical smoothed position (pre-collision)
        private Vector3 _lastTargetPos;
        private float _shakeAmt, _shakeDur, _shakeTime;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _smoothPos = transform.position;
        }

        public void SetTarget(Transform t)
        {
            target = t;
            if (t != null)
            {
                _lastTargetPos = t.position;
                _headingYaw = t.eulerAngles.y;
                _yaw = _headingYaw;
            }
        }

        /// <summary>Additive positional screen-shake; runs on unscaled time so it survives hit-stop.</summary>
        public void Shake(float amount, float duration)
        {
            _shakeAmt = Mathf.Max(_shakeAmt, amount);
            _shakeDur = Mathf.Max(duration, 0.0001f);
            _shakeTime = _shakeDur;
        }

        private void Start()
        {
            if (target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
            }
            if (target != null)
            {
                _lastTargetPos = target.position;
                _headingYaw = target.eulerAngles.y;
            }
            _yaw = _headingYaw;
            _pitchNow = pitch;
            _smoothPos = transform.position;
        }

        /// <summary>Mouse look delta (per-frame). Legacy axis; keyboard/mouse path.</summary>
        private Vector2 ReadMouseDelta()
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (_cam != null && !Mathf.Approximately(_cam.fieldOfView, fieldOfView))
                _cam.fieldOfView = fieldOfView;

            // --- heading from ACTUAL movement; don't chase a straight-back heading (prevents the spin) ---
            Vector3 delta = target.position - _lastTargetPos; delta.y = 0f;
            _lastTargetPos = target.position;
            if (dt > 0f && delta.magnitude / dt > headingSpeedThreshold)
            {
                Vector3 hdir = delta.normalized;
                Vector3 camFwd = transform.forward; camFwd.y = 0f;
                bool movingBackToward = camFwd.sqrMagnitude > 0.0001f &&
                                        Vector3.Dot(hdir, camFwd.normalized) < backpedalCutoff;
                if (!movingBackToward)
                    _headingYaw = Mathf.Atan2(hdir.x, hdir.z) * Mathf.Rad2Deg;
            }

            // --- orbit (mouse while CTRL held, or gamepad right stick); else ease back behind ---
            bool orbiting = false;
            if (Input.GetKey(orbitKey))
            {
                orbiting = true;
                Vector2 look = ReadMouseDelta();
                _yaw += look.x * mouseSensitivity;
                _pitchNow += (invertMouseY ? look.y : -look.y) * mouseSensitivity;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 rs = pad.rightStick.ReadValue();
                if (rs.sqrMagnitude > stickDeadzone * stickDeadzone)
                {
                    orbiting = true;
                    _yaw += rs.x * gamepadLookSpeed * dt;
                    _pitchNow += (invertMouseY ? rs.y : -rs.y) * gamepadLookSpeed * dt;
                }
            }
            if (orbiting)
            {
                _pitchNow = Mathf.Clamp(_pitchNow, minPitch, maxPitch);
            }
            else
            {
                float k = 1f - Mathf.Exp(-autoFollowStrength * dt);
                _yaw = Mathf.LerpAngle(_yaw, _headingYaw, k);
                _pitchNow = Mathf.Lerp(_pitchNow, pitch, k);
            }

            // --- compose rig: behind + above the pivot ---
            Quaternion orbit = Quaternion.Euler(_pitchNow, _yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * height;
            Vector3 desired = pivot + orbit * Vector3.back * distance;

            // smooth the (un-collided) position, then hard-clamp against walls so it never clips
            _smoothPos = Vector3.SmoothDamp(_smoothPos, desired, ref _vel, followSmooth);
            Vector3 finalPos = _smoothPos;
            Vector3 dir = _smoothPos - pivot;
            float d = dir.magnitude;
            if (d > 0.0001f)
            {
                dir /= d;
                if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, d, collisionMask, QueryTriggerInteraction.Ignore))
                    finalPos = pivot + dir * Mathf.Max(hit.distance - collisionBuffer, collisionMinDistance);
            }

            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.unscaledDeltaTime;
                float s = Mathf.Clamp01(_shakeTime / _shakeDur);
                finalPos += UnityEngine.Random.insideUnitSphere * (_shakeAmt * s);
                if (_shakeTime <= 0f) _shakeAmt = 0f;
            }

            transform.position = finalPos;
            Vector3 lookAt = target.position + Vector3.up * lookAtHeight;
            transform.rotation = Quaternion.LookRotation(lookAt - finalPos);
        }
    }
}
