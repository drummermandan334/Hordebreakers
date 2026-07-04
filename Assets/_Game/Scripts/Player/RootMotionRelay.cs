using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Forwards the Animator's root motion to the <see cref="PlayerController"/>. Must live on the SAME GameObject
    /// as the Animator (OnAnimatorMove only fires there). Having this callback puts the Animator in
    /// "root motion handled by script" mode — the controller then decides per attack slot (useRootMotion) whether
    /// the clip's authored travel drives the CharacterController or is ignored. deltaRotation is forwarded but never
    /// applied to the character: the controller integrates it purely to counter-rotate deltaPosition (the clips'
    /// authored travel is relative to their turning root — see PlayerController.OnRootMotion).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class RootMotionRelay : MonoBehaviour
    {
        [Tooltip("Receiver. Auto-found on the parents (the player root).")]
        [SerializeField] private PlayerController controller;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
        }

        private void OnAnimatorMove()
        {
            if (controller != null) controller.OnRootMotion(_animator.deltaPosition, _animator.deltaRotation);
        }
    }
}
