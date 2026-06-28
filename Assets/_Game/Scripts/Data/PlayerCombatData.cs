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

        [Header("Dodge  (distance ~= dodgeSpeed * dodgeDuration; gated by charges that refill over the cooldown)")]
        public float dodgeSpeed = 16f;
        public float dodgeDuration = 0.25f;
        public float dodgeIFrames = 0.2f;   // must be < dodgeDuration (i-frames are a window within the dodge)
        [Tooltip("Seconds to refill one dodge charge. The dodge gate (no stamina now): a charge is spent per roll and refills over this cooldown.")]
        public float dodgeCooldown = 1.0f;
        [Tooltip("How many dodges can be banked. 1 = a single cooldown-gated roll (baseline); Augments raise it for an 'extra dodge'.")]
        public int dodgeMaxCharges = 1;

        [Header("Block (hold to mitigate; chips through; guard-breakable by heavy/elite attacks)")]
        [Range(0f, 1f)] public float blockMitigation = 0.7f;        // fraction of a blocked hit absorbed (rest chips through)
        [Range(0f, 1f)] public float blockMoveSpeedMult = 0.4f;     // movement slows while blocking
        [Tooltip("Block only stops hits within this dot of your facing (frontal). Side/back hits aren't blocked.")]
        [Range(-1f, 1f)] public float blockFrontDot = 0.25f;
        [Tooltip("When a guard-breaking attack lands on a held block, the block fails: damage is multiplied by this (1 = full, unmitigated) and the player is staggered. Augment GuardBreakResist negates the break.")]
        [Range(0f, 2f)] public float guardBreakDamageMult = 1f;
        [Tooltip("Seconds the player is staggered (hit-react flinch held) when a block is guard-broken.")]
        public float guardBreakStagger = 0.5f;

        [Header("Poise (Augment payoff — both default off; loosen the defensive gates)")]
        [Tooltip("> 0 negates guard-break: a held frontal block holds even against guard-breaking attacks.")]
        public float guardBreakResist = 0f;
        [Tooltip("> 0 grants hyperarmor during heavy swings: a hit mid-heavy still deals damage but doesn't flinch you out of the swing.")]
        public float heavyHyperarmor = 0f;

        [Header("Musou meter (earned screen-clear; builds from dealing AND taking hits)")]
        public float maxMusou = 100f;
        public float musouGainDealtPerDamage = 0.9f;   // meter gained per point of damage dealt — aggression is the accelerator (Augment-tunable via MusouGainDealt)
        public float musouGainTakenPerDamage = 1.0f;   // meter gained per point of damage taken (Augment-tunable via MusouGainTaken)
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
        [Tooltip("Minimum seconds between swings — hard floor so mashing (esp. in the gaps after a combo / out of a dodge, where the swing has ended) can't fire several swings + whooshes per second. Keep below the natural combo pace (~0.45s).")]
        public float minSwingInterval = 0.25f;
        public float attackSteerSpeed = 1.5f;     // slight mid-swing steer speed; the attack lunge overrides full move input
        [Range(0f, 1f)]
        [Tooltip("Commitment brake (replaces the stamina cost on attacks): a swing can only be dodge-cancelled once it reaches this fraction of its clip — i.e. in RECOVERY, after the hit lands. A dodge pressed earlier buffers and fires the instant the window opens. Lower = looser cancels (an Augment dial).")]
        public float dodgeCancelPhase = 0.55f;
        [Tooltip("Playback-speed multiplier for attack swings, driven onto the Animator's 'AttackSpeed' float param. 1 = authored speed; Augments raise it. minSwingInterval (wall-clock) still floors the effective rate.")]
        public float attackSpeedMult = 1f;
    }
}
