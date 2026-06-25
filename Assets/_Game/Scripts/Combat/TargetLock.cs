using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Player-initiated lock-on (target focus). Toggling acquires the enemy most centered in view; while locked,
    /// the camera frames the target, the player strafes around it, and attacks aim at it. The lock drops when the
    /// target dies or leaves <see cref="lockBreakRange"/>. Flicking switches to the next target on that side.
    ///
    /// This is the single source of truth other systems query (PlayerController for strafe/facing, PlayerCameraRig
    /// for framing, the reticle UI) via <see cref="Instance"/>. Acquisition is on-demand (a button press), never
    /// per-frame — consistent with Daniel's manual-targeting preference (you choose the focus).
    /// </summary>
    public sealed class TargetLock : MonoBehaviour
    {
        public static TargetLock Instance { get; private set; }

        [Tooltip("Layers that can be locked (the Enemy layer).")]
        [SerializeField] private LayerMask targetMask;
        [Tooltip("Max distance to acquire a target.")]
        [SerializeField] private float lockRange = 16f;
        [Tooltip("Drop the lock once the target gets beyond this.")]
        [SerializeField] private float lockBreakRange = 22f;
        [Range(0f, 180f)]
        [Tooltip("On acquire, the target must be within this angle of where the camera is looking.")]
        [SerializeField] private float maxAcquireAngle = 75f;
        [Tooltip("Aim/frame height above the target's feet (~chest).")]
        [SerializeField] private float targetHeight = 1.2f;

        private Transform _camT;
        private Transform _target;
        private IDamageable _targetDmg;
        private readonly Collider[] _hits = new Collider[48];

        public bool HasTarget => _target != null && _targetDmg != null && _targetDmg.IsAlive;
        public Transform Target => _target;
        public Vector3 TargetPosition => _target != null ? _target.position + Vector3.up * targetHeight : transform.position;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start() { _camT = Camera.main != null ? Camera.main.transform : null; }

        private void LateUpdate()
        {
            if (_target == null) return;
            if (_targetDmg == null || !_targetDmg.IsAlive) { Clear(); return; }
            Vector3 d = _target.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > lockBreakRange * lockBreakRange) Clear();
        }

        /// <summary>Lock if free, unlock if already locked. Returns true if now locked.</summary>
        public bool Toggle()
        {
            if (HasTarget) { Clear(); return false; }
            Transform best = FindBest(null, 0);
            if (best != null) { SetTarget(best); return true; }
            return false;
        }

        /// <summary>Switch to the nearest other target on the given side (+1 = right, -1 = left).</summary>
        public void Cycle(int dir)
        {
            if (!HasTarget || dir == 0) return;
            Transform next = FindBest(_target, dir);
            if (next != null) SetTarget(next);
        }

        public void Clear() { _target = null; _targetDmg = null; }

        private Transform FindBest(Transform exclude, int dir)
        {
            if (_camT == null) _camT = Camera.main != null ? Camera.main.transform : null;
            Vector3 camFwd = _camT != null ? _camT.forward : transform.forward; camFwd.y = 0f; camFwd.Normalize();
            Vector3 camRight = _camT != null ? _camT.right : transform.right; camRight.y = 0f; camRight.Normalize();

            float curSide = 0f;
            if (dir != 0 && _target != null)
            {
                Vector3 curTo = _target.position - transform.position; curTo.y = 0f;
                if (curTo.sqrMagnitude > 0.0001f) curSide = Vector3.Dot(curTo.normalized, camRight);
            }

            int n = Physics.OverlapSphereNonAlloc(transform.position, lockRange, _hits, targetMask);
            Transform best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Transform t = _hits[i].transform;
                if (t == exclude) continue;
                IDamageable dmg = _hits[i].GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive) continue;

                Vector3 to = t.position - transform.position; to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.01f) continue;
                Vector3 toN = to / dist;

                if (dir == 0)
                {
                    // Acquire: the most-centered target in view (small angle to camera-forward), nearer breaks ties.
                    float ang = Vector3.Angle(camFwd, toN);
                    if (ang > maxAcquireAngle) continue;
                    float score = ang + dist * 0.5f;
                    if (score < bestScore) { bestScore = score; best = t; }
                }
                else
                {
                    // Cycle: nearest target whose screen-x is on the `dir` side of the current target.
                    float side = Vector3.Dot(toN, camRight);
                    if (Mathf.Sign(side - curSide) != Mathf.Sign(dir)) continue;
                    float score = Mathf.Abs(side - curSide);
                    if (score < bestScore) { bestScore = score; best = t; }
                }
            }
            return best;
        }

        private void SetTarget(Transform t)
        {
            _target = t;
            _targetDmg = t.GetComponentInParent<IDamageable>();
        }
    }
}
