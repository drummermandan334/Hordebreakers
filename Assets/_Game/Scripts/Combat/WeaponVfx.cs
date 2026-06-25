using UnityEngine;
using UnityEngine.Serialization;

namespace Hordebreakers
{
    /// <summary>
    /// Spawns a one-shot attack VFX once per swing — a slash swipe, or a stab on the configured state — at the
    /// moment the swing connects, parented to the blade so it rides the sword. Slash and stab have SEPARATE
    /// placement (position / rotation / scale) so each can be aimed at the blade. <see cref="alignToBlade"/>
    /// also orients the particles with the blade so the effect doesn't "leave" the sword on fast swings.
    /// Driven off the Animator's attack states (no per-clip events needed). Every knob is a SerializedField.
    /// </summary>
    [DefaultExecutionOrder(60)]   // after the Animator so the state read is current
    public class WeaponVfx : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Animator with the attack states. Auto-found in children if empty.")]
        [SerializeField] private Animator animator;
        [Tooltip("The blade the VFX is parented to (e.g. Sword_Prop) so it rides the sword and angles with it.")]
        [SerializeField] private Transform bladeSocket;

        [Header("Effects")]
        [SerializeField] private GameObject slashVfx;     // every swing except the stab state
        [SerializeField] private GameObject stabVfx;      // the stab attack only
        [Tooltip("Animator state that uses the stab instead of the slash (e.g. the light combo's 3rd hit).")]
        [SerializeField] private string stabState = "L3";

        [Header("Slash placement (local to the blade)")]
        [FormerlySerializedAs("localPosition")]
        [SerializeField] private Vector3 slashLocalPosition = new Vector3(0f, 0.4f, 0f);
        [FormerlySerializedAs("localEuler")]
        [SerializeField] private Vector3 slashLocalEuler = Vector3.zero;
        [FormerlySerializedAs("scale")]
        [SerializeField] private float slashScale = 0.4f;

        [Header("Stab placement (local to the blade)")]
        [SerializeField] private Vector3 stabLocalPosition = new Vector3(0f, 0.7f, 0f);
        [Tooltip("Rotates the stab so its thrust points along the blade (+Y). ~(-90,0,0) turns a +Z-forward effect onto the blade.")]
        [SerializeField] private Vector3 stabLocalEuler = new Vector3(-90f, 0f, 0f);
        [SerializeField] private float stabScale = 0.4f;

        [Header("Behaviour")]
        [Tooltip("Orient the spawned particles WITH the blade (not just position) so the effect follows the sword instead of leaving it on fast swings. Turn off if a billboard looks edge-on/flat.")]
        [SerializeField] private bool alignToBlade = true;
        [Tooltip("Slash playback speed. <1 slows the particles so the effect reads as the sword's MOTION instead of a quick spell flash.")]
        [SerializeField] private float slashSpeed = 0.55f;
        [Tooltip("Stab playback speed. <1 slows it to read as motion (same idea as the slash).")]
        [SerializeField] private float stabSpeed = 0.55f;
        [Tooltip("Seconds before the spawned VFX is destroyed (give the slowed slash room to finish).")]
        [SerializeField] private float lifetime = 0.8f;

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

            bool isStab = st.IsName(stabState);
            Spawn(isStab ? stabVfx : slashVfx, isStab);
            _firedThisSwing = true;
        }

        private void Spawn(GameObject prefab, bool isStab)
        {
            if (prefab == null || bladeSocket == null) return;
            GameObject fx = Instantiate(prefab);
            fx.transform.SetParent(bladeSocket, false);
            fx.transform.localPosition = isStab ? stabLocalPosition : slashLocalPosition;
            fx.transform.localRotation = Quaternion.Euler(isStab ? stabLocalEuler : slashLocalEuler);
            fx.transform.localScale = Vector3.one * (isStab ? stabScale : slashScale);

            float speed = isStab ? stabSpeed : slashSpeed;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;   // ride the blade (position)
                main.simulationSpeed = speed;                                 // <1 = slower, reads as motion not a spell flash
                if (alignToBlade)
                {
                    ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
                    // World/View-aligned billboards & meshes don't rotate with the blade; pin them to Local so they do.
                    // Velocity-aligned sparks are left alone (they should shoot along their own motion).
                    if (psr != null && (psr.alignment == ParticleSystemRenderSpace.World || psr.alignment == ParticleSystemRenderSpace.View))
                        psr.alignment = ParticleSystemRenderSpace.Local;
                }
            }
            Destroy(fx, lifetime);
        }
    }
}
