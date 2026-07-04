using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pins the DUMMY socket chain's hand node to the character's real right-hand bone, so the spear pack's animated
    /// weapon socket plays on the SYNTY rig. The pack authors its two-hand re-seat as transform curves on
    /// "root/.../hand_r/Weapon_Actor_R" — generic curves bind by PATH, so an empty-GameObject chain matching that path
    /// (built by Hordebreakers → Build Spear Socket Chain) receives them on any rig. This component sits on the dummy
    /// "hand_r": each LateUpdate it copies the real hand bone's world pose (plus a one-time calibration offset — the
    /// 9CG and Synty hand bones have different axes), and the Animator-written socket LOCAL motion rides on top.
    /// The spear prop parents under the dummy "Weapon_Actor_R". Runs after the Animator, before WeaponVfx (60).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class WeaponSocketFollower : MonoBehaviour
    {
        [Tooltip("The character's Animator (humanoid) — supplies the real right-hand bone. Auto-found on parents.")]
        [SerializeField] private Animator animator;
        [Tooltip("Explicit hand bone override; leave empty to use the humanoid RightHand.")]
        [SerializeField] private Transform handOverride;

        [Header("Calibration (one-time, in the editor: dial until the spear lies along the palm)")]
        [Tooltip("Rotation offset (deg) applied after copying the hand pose — compensates the 9CG-vs-Synty hand axis difference.")]
        [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;
        [Tooltip("Position offset in the hand's local space.")]
        [SerializeField] private Vector3 positionOffset = Vector3.zero;

        private Transform _hand;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInParent<Animator>();
            _hand = handOverride != null ? handOverride
                : animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
        }

        private void LateUpdate()
        {
            if (_hand == null) return;
            transform.SetPositionAndRotation(
                _hand.TransformPoint(positionOffset),
                _hand.rotation * Quaternion.Euler(rotationOffsetEuler));
        }
    }
}
