using UnityEngine;

namespace Hordebreakers
{
    /// <summary>How an attack's hit volume is shaped. Sweep = the classic sphere + front arc (sword arcs, wide cuts).
    /// Thrust = a narrow forward capsule (spear stabs) — long reach that skewers in a LINE and whiffs beside you.</summary>
    public enum AttackHitShape { Sweep, Thrust }

    /// <summary>
    /// Per-attack-slot tuning (L1/L2/L3, H1/H2/H3, and the jump attacks). Each combo slot maps to a different clip, so
    /// each gets its own contact timing, forward drive, hit shape and chain window — two global phases can't express a
    /// six-clip kit. Damage/reach are MULTIPLIERS over the light/heavy base stats so the augment dials keep working.
    /// </summary>
    [System.Serializable]
    public class AttackSlotTuning
    {
        [Range(0f, 1f)]
        [Tooltip("Fraction of THIS slot's clip at which the hit lands (thrusts peak early ~0.30–0.40; big sweeps ~0.45–0.55). Keep in sync with the matching WeaponVfx strike fraction.")]
        public float contactPhase = 0.35f;
        [Tooltip("Damage = (lightDamage|heavyDamage) x this — a multiplier so augment damage dials still apply.")]
        public float damageMult = 1f;
        [Tooltip("Reach = (lightReach|heavyReach) x this.")]
        public float reachMult = 1f;
        [Tooltip("Forward drive (m/s) while this swing's step lasts — thrusts step in harder than close sweeps.")]
        public float stepSpeed = 2.5f;
        [Tooltip("How long the forward step lasts, then hard stop.")]
        public float stepTime = 0.12f;
        [Tooltip("Sweep = sphere + front arc (arcs/cuts). Thrust = narrow forward capsule (stabs) — skewers in a line.")]
        public AttackHitShape hitShape = AttackHitShape.Sweep;
        [Range(0f, 1f)]
        [Tooltip("Fraction of THIS clip that plays before the next attack can chain (earlier presses buffer). Per-clip so the combo rhythm matches each clip's real length.")]
        public float chainOpen = 0.75f;
    }

    /// <summary>Data-driven player combat tuning. Mirror the values in HORDEBREAKERS_Tuning.xlsx.</summary>
    [CreateAssetMenu(fileName = "PlayerCombatData", menuName = "Hordebreakers/Player Combat Data")]
    public class PlayerCombatData : ScriptableObject
    {
        [Header("Survival")]
        public float maxHp = 100f;

        [Header("Movement")]
        public float moveSpeed = 5f;

        [Header("Movement weight / momentum (locomotion only — dodge/attack/musou stay instant)")]
        [Tooltip("Ease rate toward target velocity while speeding up on the ground (higher = snappier).")]
        public float groundAccel = 60f;
        [Tooltip("Ease rate toward target velocity while slowing on the ground. Keep BELOW groundAccel so you slide to a stop — that asymmetry is the weight.")]
        public float groundDecel = 35f;
        [Tooltip("Single lazy ease rate while airborne — low so you can't fully redirect mid-air.")]
        public float airAccel = 12f;

        [Header("Sprint")]
        [Tooltip("Top speed while sprinting (moveSpeed is the walk/jog top). Drives the Animator Speed param past 1.")]
        public float sprintSpeed = 8f;
        [Tooltip("Min analog left-stick magnitude before a gamepad sprint engages (no sprint on a half-push). KBM sprints on the hold key.")]
        [Range(0f, 1f)] public float sprintStickThreshold = 0.6f;

        [Header("Air / jump forgiveness")]
        [Tooltip("Grace after walking off a ledge where a jump press still fires (coyote time).")]
        public float coyoteTime = 0.12f;
        [Tooltip("A jump press this long before landing still fires on touchdown (input buffer).")]
        public float jumpBuffer = 0.12f;
        [Tooltip("Airtime below this on landing = no landing FX (small hops stay silent).")]
        public float landSoftAirTime = 0.25f;
        [Tooltip("Airtime at/above this on landing = full-strength landing FX (shake/dust/SFX scale up to here).")]
        public float landHardAirTime = 0.7f;

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

        [Header("Per-attack slots (index 0..2 = combo hits 1..3) — each clip gets its own timing/drive/hit shape")]
        [Tooltip("Light combo slots L1/L2/L3. Defaults reproduce the old globals (contact 0.35, step 2.5, finisher lunge 4).")]
        public AttackSlotTuning[] lightSlots =
        {
            new AttackSlotTuning { contactPhase = 0.35f, stepSpeed = 2.5f },
            new AttackSlotTuning { contactPhase = 0.35f, stepSpeed = 2.5f },
            new AttackSlotTuning { contactPhase = 0.35f, stepSpeed = 4.0f },   // 3rd hit = the lunge
        };
        [Tooltip("Heavy combo slots H1/H2/H3. Defaults reproduce the old globals (contact 0.5, step 2.5, finisher lunge 4).")]
        public AttackSlotTuning[] heavySlots =
        {
            new AttackSlotTuning { contactPhase = 0.5f, stepSpeed = 2.5f },
            new AttackSlotTuning { contactPhase = 0.5f, stepSpeed = 2.5f },
            new AttackSlotTuning { contactPhase = 0.5f, stepSpeed = 4.0f },
        };
        [Tooltip("Airborne light attack (the JumpAttack state).")]
        public AttackSlotTuning jumpLightSlot = new AttackSlotTuning { contactPhase = 0.35f, stepSpeed = 2.5f };
        [Tooltip("Airborne heavy attack (the JumpAttackHeavy state).")]
        public AttackSlotTuning jumpHeavySlot = new AttackSlotTuning { contactPhase = 0.5f, stepSpeed = 2.5f };

        [Header("Thrust hitbox (hitShape = Thrust: a narrow forward capsule from the chest to reach)")]
        [Tooltip("Capsule radius (m) of a thrust — the spear's effective shaft width. Narrow, so stabs whiff enemies beside you.")]
        public float thrustRadius = 0.5f;
        [Tooltip("Front-arc gate for thrust hits (tighter than the sweep's meleeArcDot) — keeps the stab reading as a LINE.")]
        [Range(-1f, 1f)] public float thrustArcDot = 0.55f;

        /// <summary>The tuning slot for a swing: combo step 1..3 picks the light/heavy slot; airborne uses the jump slots.
        /// Never returns null — falls back to a default-constructed slot if an array is left short in the inspector.</summary>
        public AttackSlotTuning GetAttackSlot(int comboStep, bool heavy, bool airborne)
        {
            if (airborne) return heavy ? jumpHeavySlot : jumpLightSlot;
            AttackSlotTuning[] set = heavy ? heavySlots : lightSlots;
            int i = Mathf.Clamp(comboStep - 1, 0, 2);
            if (set != null && i < set.Length && set[i] != null) return set[i];
            return heavy ? new AttackSlotTuning { contactPhase = 0.5f } : new AttackSlotTuning();
        }

        [Header("Impact scaling (heavier hits read heavier)")]
        [Tooltip("Damage / lightDamage is clamped to this ceiling when scaling hit-stop & shake, so a huge heavy doesn't lock the screen.")]
        public float hitStopDamageScaleMax = 4f;

        [Header("Kill confirm (x = seconds, y = timeScale) — a distinct beat on a killing blow")]
        [Tooltip("Brief slow-mo on a normal killing blow.")]
        public Vector2 killHitStop = new Vector2(0.05f, 0.15f);
        [Tooltip("Deeper slow-mo on a finisher killing blow.")]
        public Vector2 finisherKillHitStop = new Vector2(0.09f, 0.08f);
    }
}
