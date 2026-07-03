using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Cheap two-hand weapon alignment (no IK). The spear pack authors two-handed segments (the L3 jump thrust,
    /// heavy grips) by ANIMATING its Weapon_Actor_R socket — generic transform curves on the pack rig's bone path,
    /// which humanoid retargeting silently drops. The hands still do the two-handed motion (that retargets fine),
    /// but the weapon stays glued to the one-handed right-hand socket and skews.
    ///
    /// This reproduces the intent from the retargeted hand positions: whenever the LEFT palm animates onto the
    /// weapon's shaft line, rotate the weapon about its right-hand grip point so the shaft passes through the palm;
    /// ease off when the hand leaves. One-handed segments are untouched, so it's safe to leave enabled for the kit.
    ///
    /// Correctness notes (the v1 "snaps 30–45° after the slam" glitch):
    /// - REBASE EVERY FRAME: during attacks nothing re-poses the prop bone (the grip override layers are suppressed),
    ///   so corrections applied to the live transform ACCUMULATE and then stick after release. We cache the prop's
    ///   authored rest local pose and restore it before applying a fresh, absolute correction each frame — zero
    ///   accumulation, and releasing eases cleanly back to the authored pose.
    /// - NO CHASING: the alignment target is only updated while genuinely gripping; on release the last target is
    ///   frozen and faded out, so the spear never tracks the hand as it LEAVES the shaft.
    /// - DWELL-GATED: the palm must STAY on the shaft for engageDelay before the grip engages — a real grab dwells,
    ///   a hand merely passing near the spear crosses it in 2–3 frames and never engages. (Do NOT gate by correction
    ///   angle: the correction needed during a genuine two-hand segment is large by definition.)
    /// - AIM AT THE PALM: the target is the left PALM (LeftMiddleProximal), not the wrist bone (LeftHand) — the wrist
    ///   sits ~10cm behind where the fingers wrap, so aiming there rides the spear on the forearm instead of the hand.
    /// - PER-STATE GRIP SLIDE: named states (the L3 slam) slide the grip toward the butt so more of the head reaches
    ///   out. Eased and rebased with the alignment, so it can't accumulate.
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
        [SerializeField] private float releaseDistance = 0.45f;
        [Tooltip("Ignore a palm closer to the grip than this along the shaft (degenerate direction / crossing hands).")]
        [SerializeField] private float minHandSeparation = 0.15f;
        [Tooltip("Ignore a palm farther than this along the shaft — beyond the realistic grip range of the haft.")]
        [SerializeField] private float maxHandAlong = 1.1f;
        [Tooltip("The palm must stay on the shaft this long (s) before the grip engages — a real grab dwells, a passing hand crosses in 2–3 frames. (Replaces the v2 angle clamp, which wrongly rejected genuine grabs: the needed correction during a two-hand segment is LARGE by definition.)")]
        [SerializeField] private float engageDelay = 0.06f;
        [Tooltip("How fast the alignment eases in/out (exponential).")]
        [SerializeField] private float blendSpeed = 14f;
        [Tooltip("Only align during Attack-tagged states — locomotion/idle sometimes swings the off hand near the shaft.")]
        [SerializeField] private bool attackStatesOnly = true;

        [Header("Left grip point")]
        [Tooltip("Aim the shaft at the left PALM (middle-finger base) instead of the wrist bone. The wrist sits ~10cm behind where the fingers actually wrap, so aiming there makes the shaft ride the forearm/wrist.")]
        [SerializeField] private bool aimAtPalm = true;
        [Tooltip("Fine-tune where the shaft crosses the left hand, in the left-hand bone's local space (e.g. push a little further into the palm).")]
        [SerializeField] private Vector3 leftGripLocalOffset = Vector3.zero;

        [Header("Per-state grip slide")]
        [Tooltip("Animator state that slides the grip toward the BUTT (spear moves forward along its shaft so the head reaches out) — e.g. the L3 slam. Empty = never.")]
        [SerializeField] private string gripSlideState = "L3";
        [Tooltip("Metres to slide the grip toward the butt during gripSlideState. 0 = off. Eased in/out at blendSpeed.")]
        [SerializeField] private float gripSlideBack = 0.2f;

        private Transform _leftHand;                           // wrist bone — fallback target and local frame for the offset
        private Transform _leftGrip;                           // the point the shaft is aimed through (palm proxy when aimAtPalm)
        private float _weight;
        private bool _gripping;
        private float _dwellTimer;                             // time the palm has been continuously on the shaft (pre-engage)
        private Quaternion _heldDelta = Quaternion.identity;   // frozen on release so we never chase a departing hand
        private float _slide;                                  // eased per-state grip slide amount
        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private bool _restCached;
        private bool _applied;   // we moved the weapon last frame → restore the rest pose before computing anew

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform palm = aimAtPalm ? animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal) : null;
                _leftGrip = palm != null ? palm : _leftHand;   // palm proxy → the shaft sits in the hand, not on the wrist
            }
        }

        private Vector3 LeftGripWorld()
        {
            Vector3 p = _leftGrip.position;
            if (_leftHand != null && leftGripLocalOffset != Vector3.zero) p += _leftHand.TransformVector(leftGripLocalOffset);
            return p;
        }

        private void LateUpdate()
        {
            if (weapon == null || _leftGrip == null) return;

            // Cache the authored rest pose once, and REBASE to it whenever we touched the weapon last frame —
            // nothing else re-poses the prop bone mid-attack, so without this our corrections accumulate and stick.
            if (!_restCached) { _restLocalPos = weapon.localPosition; _restLocalRot = weapon.localRotation; _restCached = true; }
            if (_applied) { weapon.localPosition = _restLocalPos; weapon.localRotation = _restLocalRot; _applied = false; }

            bool eligible = !attackStatesOnly || InAttackState();
            Vector3 gripWorld = weapon.TransformPoint(gripLocalPoint);   // real right-hand grip, at the authored rest pose (pre-slide)
            Vector3 shaftDir = weapon.TransformDirection(shaftLocalAxis).normalized;

            // Per-state grip slide: during gripSlideState (the L3 slam) slide the spear FORWARD along its shaft so the
            // right hand grips further toward the butt and more of the head reaches out. Rebased each frame via _applied
            // so it can't accumulate; gripWorld above stays the real hand, so the rotation below still pivots there.
            float slideTarget = (gripSlideBack != 0f && InState(gripSlideState)) ? gripSlideBack : 0f;
            _slide = Mathf.Lerp(_slide, slideTarget, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            if (Mathf.Abs(_slide) > 0.0005f) { weapon.position += shaftDir * _slide; _applied = true; }

            Vector3 toPalm = LeftGripWorld() - gripWorld;
            float along = Vector3.Dot(toPalm, shaftDir);
            float lineDist = (toPalm - shaftDir * along).magnitude;

            // A grip = palm on the haft segment, near the shaft line (hysteresis on exit), that DWELLS there —
            // a real grab stays on the shaft; a hand passing by crosses it in 2–3 frames and never engages.
            bool nearShaft = eligible && along > minHandSeparation && along < maxHandAlong
                             && lineDist < (_gripping ? releaseDistance : engageDistance);
            _dwellTimer = nearShaft ? _dwellTimer + Time.deltaTime : 0f;
            bool wantGrip = nearShaft && (_gripping || _dwellTimer >= engageDelay);
            _gripping = wantGrip;
            if (wantGrip) _heldDelta = Quaternion.FromToRotation(shaftDir, toPalm.normalized);   // track the palm ONLY while gripping; frozen through the release fade

            _weight = Mathf.Lerp(_weight, wantGrip ? 1f : 0f, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            if (_weight < 0.002f) { _weight = 0f; _heldDelta = Quaternion.identity; return; }   // released — rest pose restored next frame via _applied if the slide moved it

            Quaternion delta = Quaternion.Slerp(Quaternion.identity, _heldDelta, _weight);
            weapon.rotation = delta * weapon.rotation;
            weapon.position = gripWorld + delta * (weapon.position - gripWorld);   // weapon.position already includes the slide
            _applied = true;
        }

        private bool InAttackState()
        {
            if (animator == null) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
        }

        private bool InState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
        }
    }
}
