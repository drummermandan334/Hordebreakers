using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Spawns a one-shot attack VFX once per swing — a single slash swipe (or a stab) — at the moment the
    /// swing connects, parented to the blade so it sits on the sword and inherits its angle. Driven off the
    /// Animator's attack states (no per-clip events needed). Every feel knob is a SerializedField so it can
    /// be tuned in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(60)]   // after the Animator so the state read is current
    public class WeaponVfx : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Animator with the attack states. Auto-found in children if empty.")]
        [SerializeField] private Animator animator;
        [Tooltip("The blade the VFX is parented to (e.g. Sword_Prop) so it sits on the sword and angles with it.")]
        [SerializeField] private Transform bladeSocket;

        [Header("Effects")]
        [SerializeField] private GameObject slashVfx;     // every swing
        [SerializeField] private GameObject stabVfx;      // the stab attack only
        [Tooltip("Animator state that uses the stab instead of the slash (e.g. the light combo's 3rd hit).")]
        [SerializeField] private string stabState = "L3";

        [Header("Placement (local to the blade)")]
        [SerializeField] private Vector3 localPosition = new Vector3(0f, 0.4f, 0f);
        [SerializeField] private Vector3 localEuler = Vector3.zero;
        [SerializeField] private float scale = 1f;
        [Tooltip("Seconds before the spawned VFX is destroyed.")]
        [SerializeField] private float lifetime = 1f;

        [Header("Timing (fraction of the attack clip when it strikes)")]
        [Range(0f, 1f)][SerializeField] private float lightStrikeFraction = 0.35f;
        [Range(0f, 1f)][SerializeField] private float heavyStrikeFraction = 0.5f;

        private int _lastStateHash;
        private bool _firedThisSwing;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (animator == null) return;
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsTag("Attack") || st.IsName("Dodge")) { _firedThisSwing = false; _lastStateHash = 0; return; }

            if (st.fullPathHash != _lastStateHash) { _lastStateHash = st.fullPathHash; _firedThisSwing = false; }   // new swing started
            if (_firedThisSwing) return;

            bool heavy = st.IsName("H1") || st.IsName("H2") || st.IsName("H3") || st.IsName("JumpAttackHeavy");
            float frac = heavy ? heavyStrikeFraction : lightStrikeFraction;
            if (Mathf.Repeat(st.normalizedTime, 1f) < frac) return;

            Spawn(st.IsName(stabState) ? stabVfx : slashVfx);
            _firedThisSwing = true;
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null || bladeSocket == null) return;
            GameObject fx = Instantiate(prefab);
            fx.transform.SetParent(bladeSocket, false);
            fx.transform.localPosition = localPosition;
            fx.transform.localRotation = Quaternion.Euler(localEuler);
            fx.transform.localScale = Vector3.one * scale;
            Destroy(fx, lifetime);
        }
    }
}
