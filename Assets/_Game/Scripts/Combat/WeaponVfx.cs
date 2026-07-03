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
        [SerializeField] private GameObject slashVfx;     // every swing except the stab states
        [SerializeField] private GameObject stabVfx;      // the stab attacks only
        [Tooltip("Animator states that use the stab instead of the slash. A sword kit stabs on one hit (L3); a SPEAR kit stabs on most (e.g. L1, L2, L3, H3).")]
        [SerializeField] private string[] stabStates = { "L3" };

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

        [Header("Timing (fraction of the attack clip when it strikes) — KEEP each slot in sync with PlayerCombatData's matching AttackSlotTuning.contactPhase so the hit and VFX land together")]
        [Tooltip("Per-slot strike fraction for L1/L2/L3 (index 0..2). Empty/short array falls back to lightStrikeFraction.")]
        [SerializeField] private float[] lightStrikeFractions = { 0.35f, 0.35f, 0.35f };
        [Tooltip("Per-slot strike fraction for H1/H2/H3 (index 0..2). Empty/short array falls back to heavyStrikeFraction.")]
        [SerializeField] private float[] heavyStrikeFractions = { 0.5f, 0.5f, 0.5f };
        [Tooltip("Strike fraction for the airborne JumpAttack state.")]
        [Range(0f, 1f)][SerializeField] private float jumpLightStrikeFraction = 0.35f;
        [Tooltip("Strike fraction for the airborne JumpAttackHeavy state.")]
        [Range(0f, 1f)][SerializeField] private float jumpHeavyStrikeFraction = 0.5f;
        [Tooltip("Fallback when the light array is empty/short (the old global).")]
        [Range(0f, 1f)][SerializeField] private float lightStrikeFraction = 0.35f;
        [Tooltip("Fallback when the heavy array is empty/short (the old global).")]
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

            if (Mathf.Repeat(st.normalizedTime, 1f) < StrikeFraction(st)) return;

            bool isStab = IsStabState(st);
            Spawn(isStab ? stabVfx : slashVfx, isStab);
            _firedThisSwing = true;
        }

        /// <summary>Per-state strike fraction (mirrors PlayerCombatData's per-slot contact phases). Unknown Attack-tagged
        /// states fall back to the legacy light/heavy globals so a new state never silently loses its VFX.</summary>
        private float StrikeFraction(AnimatorStateInfo st)
        {
            if (st.IsName("L1")) return SlotFrac(lightStrikeFractions, 0, lightStrikeFraction);
            if (st.IsName("L2")) return SlotFrac(lightStrikeFractions, 1, lightStrikeFraction);
            if (st.IsName("L3")) return SlotFrac(lightStrikeFractions, 2, lightStrikeFraction);
            if (st.IsName("H1")) return SlotFrac(heavyStrikeFractions, 0, heavyStrikeFraction);
            if (st.IsName("H2")) return SlotFrac(heavyStrikeFractions, 1, heavyStrikeFraction);
            if (st.IsName("H3")) return SlotFrac(heavyStrikeFractions, 2, heavyStrikeFraction);
            if (st.IsName("JumpAttack")) return jumpLightStrikeFraction;
            if (st.IsName("JumpAttackHeavy")) return jumpHeavyStrikeFraction;
            return lightStrikeFraction;
        }

        private static float SlotFrac(float[] set, int i, float fallback)
            => set != null && i < set.Length ? set[i] : fallback;

        private bool IsStabState(AnimatorStateInfo st)
        {
            if (stabStates == null) return false;
            for (int i = 0; i < stabStates.Length; i++)
                if (!string.IsNullOrEmpty(stabStates[i]) && st.IsName(stabStates[i])) return true;
            return false;
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
