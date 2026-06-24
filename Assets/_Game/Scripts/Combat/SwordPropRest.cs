using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Holds the virtual Prop_R bone at the sword's rest grip pose whenever the character isn't mid-attack,
    /// so the sword seated on the Prop_R socket sits in the palm at rest instead of falling back to the
    /// bone's default (which lands at the wrist). During attacks the animation clip drives Prop_R — the
    /// orientation that makes thrusts/slashes look correct — so we leave it alone then.
    ///
    /// Runs in LateUpdate at a negative execution order so it writes Prop_R AFTER the Animator applies the
    /// pose but BEFORE the PropBoneBinder (order 0) reads it to drive the socket.
    /// Attack states on the base layer must be tagged "Attack".
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SwordPropRest : MonoBehaviour
    {
        [Tooltip("Base-layer combat animator (attack states tagged 'Attack').")]
        [SerializeField] private Animator animator;
        [Tooltip("The virtual Prop_R bone the Prop Bone Binder drives the socket from.")]
        [SerializeField] private Transform propBone;
        [Tooltip("Prop_R local pose that seats the sword in the palm (from A_Idle_Base_Sword).")]
        [SerializeField] private Vector3 restLocalPosition = new Vector3(-0.0328f, 0.0917f, 0.0050f);
        [SerializeField] private Vector3 restLocalEuler = new Vector3(296.65f, 290.53f, 65.06f);

        private void LateUpdate()
        {
            if (animator == null || propBone == null || IsAttacking()) return;
            propBone.localPosition = restLocalPosition;
            propBone.localRotation = Quaternion.Euler(restLocalEuler);
        }

        private bool IsAttacking()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
        }
    }
}
