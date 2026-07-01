using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Tuning for the Boss/Commander archetype (<see cref="BossEnemy"/>): a multi-attack duel — a telegraphed AoE slam,
    /// a committed charge across the gap, and a wide frontal cleave — that ENRAGES below a health threshold (faster
    /// wind-ups/cooldowns, harder hits). Used by the Ork Champion (sub-boss) and, with phases layered on, the Goblin King.
    /// </summary>
    [CreateAssetMenu(fileName = "BossData", menuName = "Hordebreakers/Boss Data")]
    public class BossData : ScriptableObject
    {
        [Tooltip("Name shown on the target nameplate when the player attacks/locks this unit.")]
        public string displayName = "Boss";
        [Header("Stats")]
        public float maxHp = 320f;
        public float moveSpeed = 2.6f;
        [Tooltip("Turn rate (deg/sec) — slow enough that a quick player can flank the wind-up.")]
        public float turnSpeedDeg = 240f;

        [Header("Crowd separation")]
        public float separationRadius = 1.2f;
        public float separationForce = 6f;
        [Tooltip("Hard minimum distance kept from the player's center (no standing inside the player).")]
        public float playerSpacing = 0.75f;
        [Tooltip("The boss's committed strikes shatter a held block (guard-break) — turtling through them fails.")]
        public bool guardBreaks = true;

        [Header("Aggro / engage (a boss usually spawns already hunting: aggroRadius huge)")]
        public float aggroRadius = 60f;
        public float leashRadius = 0f;     // 0 = never leash (a boss doesn't wander off)
        public float leashTime = 4f;
        public float alertRadius = 8f;

        [Header("Patrol (only relevant if it can go passive; a hunting boss won't)")]
        public float patrolRadius = 0f;
        public float patrolSpeed = 0.4f;
        public float patrolPauseMin = 1.5f;
        public float patrolPauseMax = 4f;

        [Header("Prowl (circle the player at a hold ring between attacks)")]
        [Tooltip("After arriving in range it prowls this long (randomized +/-20%) before its FIRST attack.")]
        public float engageDwell = 1.1f;
        [Tooltip("Hold ring as a fraction of the widest attack range — it strafes here between commits.")]
        public float holdRingFraction = 0.85f;
        public float strafeFraction = 0.5f;
        public float ringHoldStrength = 0.5f;
        [Tooltip("Seconds between ANY two attacks (the global attack cadence).")]
        public float globalCooldown = 1.6f;
        [Tooltip("Grace before the first attack after spawning.")]
        public float initialCooldown = 1.5f;

        [Header("Slam (telegraphed AoE at the player's spot — the default close-range attack)")]
        public float slamRange = 3.6f;      // will slam when the player is within this
        public float slamRadius = 3.6f;
        public float slamDamage = 34f;
        public float slamWindup = 1.1f;
        public float slamRecovery = 0.8f;

        [Header("Charge (committed dash — preferred when the player is far)")]
        [Tooltip("Prefers a charge when the player is at least this far (and within aggro).")]
        public float chargeMinRange = 6f;
        public float chargeWindup = 0.9f;
        public float chargeSpeed = 15f;
        public float chargeTime = 0.6f;
        public float chargeDamage = 26f;
        [Tooltip("The charge connects when it sweeps within this of the player (mid-dash).")]
        public float chargeHitRadius = 1.6f;
        public float chargeRecovery = 1f;

        [Header("Cleave (wide frontal arc — a close-range mix-up vs the slam)")]
        [Tooltip("Reach of the cleave arc (m).")]
        public float cleaveRange = 3f;
        [Tooltip("Half-angle (deg) of the frontal arc the cleave sweeps.")]
        public float cleaveHalfAngleDeg = 70f;
        public float cleaveDamage = 24f;
        public float cleaveWindup = 0.75f;
        public float cleaveRecovery = 0.7f;
        [Range(0f, 1f)]
        [Tooltip("At close range, chance to pick Cleave instead of Slam (the rest of the time it slams).")]
        public float cleaveChance = 0.5f;

        [Header("Enrage (low-HP phase)")]
        [Range(0f, 1f)]
        [Tooltip("Enrages when HP drops to/below this fraction. 0 = never enrage.")]
        public float enrageHpFraction = 0.4f;
        [Tooltip("Wind-ups + cooldowns are multiplied by this while enraged (<1 = faster/scarier).")]
        public float enrageSpeedMult = 0.7f;
        [Tooltip("Attack damage is multiplied by this while enraged.")]
        public float enrageDamageMult = 1.25f;

        [Header("Commander phase (Goblin King P1 'behind the muscle': hold + summon above the threshold, personal melee below)")]
        [Tooltip("Enables the two-phase King behavior. The Ork Champion leaves this OFF (pure melee). NOTE: a commander-phase boss must use the FSM brain.")]
        public bool hasCommanderPhase = false;
        [Range(0f, 1f)]
        [Tooltip("Stays in the summon/hold commander phase while HP is ABOVE this fraction; drops to the personal melee fight (P2) below it.")]
        public float commanderUntilHpFraction = 0.6f;
        [Tooltip("Seconds between commander casts (summon a batch + lob a hazard bolt).")]
        public float commanderCastInterval = 4f;
        [Tooltip("Goblins summoned per commander cast (they pour in from the stronghold around the King).")]
        public int commanderSummonCount = 2;
        [Tooltip("Ring radius (m) around the King the summoned goblins appear on.")]
        public float commanderSummonRadius = 3.5f;
        [Tooltip("While commanding, the King backs off to keep at least this far from the player (fights from his perch).")]
        public float commanderHoldRange = 9f;
        public float commanderBoltDamage = 10f;
        public float commanderBoltSpeed = 12f;

        [Header("Hit reaction (poise — high; a boss shrugs off chip)")]
        public float hitReactTime = 0.12f;
        public float maxPoise = 90f;
        public float poiseRegen = 12f;
        public float staggerDuration = 0.6f;

        [Header("Death / Reward")]
        public float deathDuration = 2f;
        public float deathKnockback = 6f;
        public int xpValue = 40;
    }
}
