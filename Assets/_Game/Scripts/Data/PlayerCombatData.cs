using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Data-driven player combat tuning. Mirror the values in HORDEBREAKERS_Tuning.xlsx.</summary>
    [CreateAssetMenu(fileName = "PlayerCombatData", menuName = "Hordebreakers/Player Combat Data")]
    public class PlayerCombatData : ScriptableObject
    {
        [Header("Survival")]
        public float maxHp = 100f;

        [Header("Movement")]
        public float moveSpeed = 5f;

        [Header("Dodge  (distance ~= dodgeSpeed * dodgeDuration)")]
        public float dodgeSpeed = 16f;
        public float dodgeDuration = 0.25f;
        public float dodgeIFrames = 0.2f;   // must be < dodgeDuration (i-frames are a window within the dodge)
        public float dodgeCooldown = 1.0f;  // NOTE: with stamina gating dodge now, consider lowering toward 0 so stamina is the rhythm

        [Header("Stamina (rhythm, not a noose — only attacks/dodge cost it; movement is always free)")]
        public float maxStamina = 100f;
        [Tooltip("Stamina restored per second once regen kicks in.")]
        public float staminaRegen = 45f;
        [Tooltip("Delay after spending before stamina begins to regen (short — empty is a brief 'breathe' beat).")]
        public float staminaRegenDelay = 0.5f;
        public float lightStaminaCost = 12f;
        [Tooltip("A chunk — big swings are decisions, not free spam.")]
        public float heavyStaminaCost = 28f;
        public float dodgeStaminaCost = 22f;
        [Header("Empty != defenseless: a too-tired dodge still works, just weaker (the 'stumble')")]
        [Range(0f, 1f)] public float stumbleDodgeSpeedMult = 0.55f;
        [Range(0f, 1f)] public float stumbleDodgeIFrameMult = 0.5f;

        [Header("Block (hold to mitigate; chips through + costs stamina; partial when empty)")]
        [Range(0f, 1f)] public float blockMitigation = 0.7f;        // fraction of a blocked hit absorbed (rest chips through)
        [Range(0f, 1f)] public float emptyBlockMitigation = 0.35f;  // reduced mitigation when out of stamina — never fails fully open
        public float blockStaminaPerHit = 10f;                      // stamina spent per blocked hit
        [Range(0f, 1f)] public float blockMoveSpeedMult = 0.4f;     // movement slows while blocking
        [Tooltip("Block only stops hits within this dot of your facing (frontal). Side/back hits aren't blocked.")]
        [Range(-1f, 1f)] public float blockFrontDot = 0.25f;

        [Header("Musou meter (earned screen-clear; builds from dealing AND taking hits)")]
        public float maxMusou = 100f;
        public float musouGainDealtPerDamage = 0.6f;   // meter gained per point of damage dealt
        public float musouGainTakenPerDamage = 1.0f;   // meter gained per point of damage taken
        public float musouRadius = 6f;                 // screen-clear AoE radius
        public float musouDamage = 60f;
        public float musouDuration = 1.2f;             // i-frame window / committed finisher length

        [Header("Light Attack (fast combo)")]
        public float lightDamage = 8f;
        public float lightReach = 2.2f;

        [Header("Heavy Attack (combo hits; the heavy finisher is a lunging strike)")]
        public float heavyDamage = 30f;
        public float heavyReach = 2.6f;

        [Header("Combo feel")]
        [Range(0f, 1f)] public float comboChainOpen = 0.75f;  // fraction of a swing that plays before the next attack chains (earlier presses buffer); higher = fuller swings
        public float attackSteerSpeed = 1.5f;     // slight mid-swing steer speed; the attack lunge overrides full move input
    }
}
