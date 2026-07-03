using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Cheap two-hand weapon alignment (no IK). The spear pack authors two-handed segments (the L3 jump thrust,
    /// heavy grips) by ANIMATING its Weapon_Actor_R socket — generic transform curves on the pack rig's bone path,
    /// which humanoid retargeting silently drops. The hands still do the two-handed motion (that retargets fine),
    /// but the weapon stays glued to the one-handed right-hand socket and skews.
    ///
    /// This reproduces the intent from the retargeted hand positions instead: whenever the LEFT palm animates close
    /// to the weapon's shaft line, rotate the weapon about its right-hand grip point so the shaft passes through the
    /// left palm; ease off when the hand leaves. One-handed segments are untouched (left palm far → weight 0), so it
    /// is safe to leave enabled for the whole kit — every current and future two-handed clip just works.
    /// Runs in LateUpdate after the Animator pose and before WeaponVfx (order 60) reads the blade transform.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public class TwoHandGrip : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Humanoid animator — supplies the left palm bone. Auto-found in children.")]
        [SerializeField] private Animator animator;
        [Tooltip("The weapon prop transform (the same one WeaponVfx uses as bladeSocket — a child of the right-hand socket).")]
        [SerializeField] private Transform weapon;

        [Header("Shaft")]
        [Tooltip("Shaft direction in weapon-local space (+Y on Synty props: blade/shaft runs along the prop's up).")]
        [SerializeField] private Vector3 shaftLocalAxis = Vector3.up;
        [Tooltip("Right-hand hold point on the shaft, weapon-local (0,0,0 = the prop's origin, which sits in the palm on Synty rigs).")]
        [SerializeField] private Vector3 gripLocalPoint = Vector3.zero;

        [Header("Engage")]
        [Tooltip("Left palm within this distance of the shaft LINE → the clip is doing a two-handed grab: align.")]
        [SerializeField] private float engageDistance = 0.35f;
        [Tooltip("Once gripping, keep aligning until the palm is beyond this (hysteresis — no flicker at the edge).")]
        [SerializeField] private float releaseDistance = 0.5f;
        [Tooltip("Ignore a palm closer to the grip than this along the shaft (degenerate direction / crossing hands).")]
        [SerializeField] private float minHandSeparation = 0.15f;
        [Tooltip("How fast the alignment eases in/out (exponential).")]
        [SerializeField] private float blendSpeed = 14f;
        [Tooltip("Only align during Attack-tagged states — locomotion/idle sometimes swings the off hand near the shaft.")]
        [SerializeField] private bool attackStatesOnly = true;

        private Transform _leftHand;
        private Vector3 _baseLocalPos;      // the prop's authored seat under the hand
        private Quaternion _baseLocalRot;   // restored each frame so our correction never compounds / leaves residual skew
        private bool _haveBase;
        private float _weight;
        private bool _gripping;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        private void LateUpdate()
        {
            if (weapon == null || _leftHand == null) return;

            // The humanoid Animator never re-drives this generic child prop, so a world-space write here bakes into the
            // prop's localRotation and would compound frame-over-frame — leaving the spear cocked at the last grip angle
            // once a two-handed segment eases out. Snapshot the authored local seat once, then restore it every frame so
            // the prop rigidly follows the hand and each frame's alignment starts clean (one-handed pose stays authored).
            if (!_haveBase) { _baseLocalPos = weapon.localPosition; _baseLocalRot = weapon.localRotation; _haveBase = true; }
            weapon.localPosition = _baseLocalPos;
            weapon.localRotation = _baseLocalRot;

            bool eligible = !attackStatesOnly || InAttackState();
            Vector3 gripWorld = weapon.TransformPoint(gripLocalPoint);
            Vector3 shaftDir = weapon.TransformDirection(shaftLocalAxis).normalized;
            Vector3 toPalm = _leftHand.position - gripWorld;
            float along = Vector3.Dot(toPalm, shaftDir);
            float lineDist = (toPalm - shaftDir * along).magnitude;

            // Two-handed when the palm is near the shaft line, ahead of the grip by a real hand-width (hysteresis on exit).
            bool wantGrip = eligible && along > minHandSeparation && lineDist < (_gripping ? releaseDistance : engageDistance);
            _gripping = wantGrip;
            _weight = Mathf.Lerp(_weight, wantGrip ? 1f : 0f, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            if (_weight < 0.001f) return;

            // Rotate the weapon about the grip point so the shaft passes through the left palm (roll preserved).
            Vector3 want = toPalm.normalized;
            Quaternion delta = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(shaftDir, want), _weight);
            weapon.rotation = delta * weapon.rotation;
            weapon.position = gripWorld + delta * (weapon.position - gripWorld);
        }

        private bool InAttackState()
        {
            if (animator == null) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
        }
    }
}
