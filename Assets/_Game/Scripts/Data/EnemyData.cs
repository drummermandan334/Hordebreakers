using UnityEngine;

namespace Hordebreakers
{
    /// <summary>How an enemy commits its attack — chooses the behavior branch in <see cref="Enemy"/>.</summary>
    public enum AttackArchetype
    {
        StandoffLunge,   // holds a ring around the player, circles, telegraphs a short lunge (the Husk)
        Charger,         // rushes in, telegraphs a long committed dash across the gap (a Tier-2 pressure unit)
        Dummy,           // passive training dummy: stands, faces the player, takes hits — never seeks or attacks
        Ranged           // holds at distance, aims a telegraphed shot, KITES when crowded (the Goblin Archer / Shaman)
    }

    /// <summary>Enemy stats. One asset per enemy type (Husk, Charger, ...).</summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Hordebreakers/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Archetype")]
        public AttackArchetype archetype = AttackArchetype.StandoffLunge;
        [Tooltip("Name shown on the target nameplate when the player attacks/locks this unit.")]
        public string displayName = "Enemy";
        [Header("Stats")]
        public float maxHp = 12f;
        public float moveSpeed = 3f;
        [Tooltip("How fast the body turns to face the player (deg/sec). Lower = creatures pivot instead of turret-snapping; lets a strafing player flank them.")]
        public float turnSpeedDeg = 540f;

        [Header("Crowd separation")]
        public float separationRadius = 0.7f;   // push apart from enemies/player within this
        public float separationForce = 6f;      // separation strength
        [Tooltip("Hard minimum distance the body keeps from the player's center — prevents standing inside the player. Keep just under (player CC radius + this body's radius) so it doesn't fight the player's own collision.")]
        public float playerSpacing = 0.6f;

        [Tooltip("This enemy's committed strike (lunge/charge) shatters a held block (guard-break), staggering a turtling player. Chaff lunges leave this off; pressure units (Charger) turn it on.")]
        public bool guardBreaks = false;

        [Header("Aggro (engage range — PASSIVE until alerted)")]
        [Tooltip("The enemy idles at its spawn (no homing) until the player comes within this radius, it gets hit, or a nearby ally raises the alarm. <= 0 = legacy: aggro from spawn (always chases). The master dial against the Vampire-Survivors swarm.")]
        public float aggroRadius = 10f;
        [Tooltip("Once engaged, the enemy de-aggros (back to passive) if the player stays beyond this radius for leashTime. <= 0 = never leash (stays awake once woken).")]
        public float leashRadius = 16f;
        [Tooltip("Seconds the player must stay beyond leashRadius before the enemy gives up.")]
        public float leashTime = 3f;
        [Tooltip("When this enemy engages, it wakes allies within this radius too, so a cluster engages as one (not one-by-one popcorn). <= 0 = no alert propagation.")]
        public float alertRadius = 5f;

        [Header("Patrol (passive wander around the spawn point, until alerted)")]
        [Tooltip("Radius (m) the enemy ambles around its spawn point while passive — a living patrol instead of standing frozen. 0 = stand still.")]
        public float patrolRadius = 4f;
        [Tooltip("Patrol amble speed as a fraction of moveSpeed (slow, unhurried).")]
        public float patrolSpeed = 0.4f;
        [Tooltip("Min pause (s) at each patrol point before ambling to the next.")]
        public float patrolPauseMin = 1f;
        [Tooltip("Max pause (s) at each patrol point.")]
        public float patrolPauseMax = 3f;

        [Header("Morale (Goblin cowardice — data-gated; leave OFF for elites/bosses so the big ones never run)")]
        [Tooltip("Enables the morale break: brave in a pack, gutless alone. Chaff goblins ON; Troll/Ork/King OFF.")]
        public bool hasMorale = false;
        [Tooltip("Radius (m) it counts living allies within to decide whether it's still in a pack.")]
        public float moraleRadius = 6f;
        [Tooltip("Fewer than this many nearby allies (isolated / pack thinned) → morale breaks. HIGHER = breaks more easily (needs a bigger pack to stay brave). Grunt breaks easiest.")]
        public int moraleMinAllies = 2;
        [Tooltip("Seconds a broken goblin cowers/flees before it re-checks and can re-rally.")]
        public float moraleBreakSeconds = 2.5f;
        [Tooltip("Flee speed while broken, as a multiplier on moveSpeed (a panicked scramble away from the player).")]
        public float moraleFleeSpeedMult = 1.3f;
        [Tooltip("v0.1: the ~4x/sec ally scan already catches a nearby ally dying within a quarter second, so a big ally's death routs the pack; kept as a dial for a future instant-panic broadcast.")]
        public bool moralePanicOnAllyDeath = true;

        [Header("Contact damage (strike)")]
        public float contactDamage = 5f;
        public float contactInterval = 1f;
        public float contactRange = 1.4f;

        [Header("Attack pattern (telegraphed lunge)")]
        [Tooltip("Standoff distance the enemy holds at, ringing the player instead of piling on.")]
        public float attackRange = 1.8f;
        [Tooltip("StandoffLunge only: the enemy approaches/holds/strafes at the standoff ring x this (engagement ring). Wider = the crowd spreads out into a bigger circle (less clustered for an AoE) and darts in to strike. Keep <= ~2 so the lunge still reaches across.")]
        public float holdRingMult = 1.6f;
        [Tooltip("StandoffLunge only: at most this many can be mid-attack (telegraph->lunge) at once across the whole arena; the rest keep orbiting until a slot frees. The biggest anti-swarm lever — turns the face-pile into an encirclement.")]
        public int maxSimultaneousAttackers = 3;
        [Tooltip("StandoffLunge only: after arriving in the ring the enemy circles the player for this long (randomized +/-20%) before its FIRST telegraph — so it doesn't strike the instant it reaches you. Re-armed each time it re-enters range. 0 = strike as soon as off cooldown (old behaviour).")]
        public float engageDwell = 0.9f;
        [Tooltip("Telegraph time before the lunge — the dodge window.")]
        public float attackWindup = 0.45f;
        [Tooltip("Recovery after the lunge before it can move/attack again.")]
        public float attackRecovery = 0.45f;
        [Tooltip("Cooldown between attacks once back in standoff.")]
        public float attackCooldown = 1.6f;
        [Tooltip("Forward dash speed during the strike.")]
        public float lungeSpeed = 8f;
        [Tooltip("How long the lunge dash lasts.")]
        public float lungeTime = 0.14f;

        [Header("Charger (used when archetype = Charger)")]
        [Tooltip("Begins its charge wind-up when the player is within this range (rushes in to here, no standoff ring).")]
        public float chargeStartRange = 9f;
        [Tooltip("Charge telegraph length — long and very readable; the dodge/block window.")]
        public float chargeWindup = 0.7f;
        public float chargeSpeed = 16f;
        [Tooltip("How long the charge dash lasts (covers a lot of ground).")]
        public float chargeTime = 0.55f;
        public float chargeDamage = 14f;
        [Tooltip("Recovery after the charge — long, so a whiffed charge is a big punish window.")]
        public float chargeRecovery = 0.9f;
        [Tooltip("The charge connects when it sweeps within this distance of the player (mid-dash, not just at the end).")]
        public float chargeHitRadius = 1.3f;

        [Header("Ranged (used when archetype = Ranged) — reuses attackWindup (aim draw) / attackRecovery / attackCooldown")]
        [Tooltip("The archer holds at distance and shoots when the player is within this range (also its 'in range' gate). Big — it's a ranged threat.")]
        public float shootRange = 12f;
        [Tooltip("If the player closes inside this, the archer BACKS AWAY (kites) instead of holding — keeps its distance.")]
        public float kiteDistance = 5f;
        [Tooltip("Arrow flight speed (m/s). Slow enough to read + dodge after the aim telegraph.")]
        public float arrowSpeed = 16f;
        [Tooltip("Arrow lifetime (s) before it despawns if it hits nothing.")]
        public float arrowLife = 2.5f;
        [Tooltip("Damage per arrow.")]
        public float arrowDamage = 7f;
        [Tooltip("Height (m) above the archer's feet the arrow launches from (~the bow).")]
        public float shootHeight = 1.2f;

        [Header("Hit reaction")]
        [Tooltip("Base recoil impulse (m/s) when struck — adds weight to the player's hits.")]
        public float knockback = 5f;
        [Tooltip("Extra recoil per point of damage, so heavies knock far harder than lights (impulse = knockback + damage * this).")]
        public float knockbackPerDamage = 0.5f;
        [Tooltip("Big recoil impulse (m/s) applied on the killing blow — the body flies back on death.")]
        public float deathKnockback = 16f;
        [Tooltip("How long a hit staggers the enemy (movement paused while it flinches).")]
        public float hitReactTime = 0.3f;

        [Header("Poise (weight — light taps chip, a break is a real stagger)")]
        [Tooltip("Poise pool. A hit depletes it by the damage dealt; emptying it (or a heavy >= bloodMinDamage) is a real stagger. <= 0 = legacy: every hit flinches.")]
        public float maxPoise = 14f;
        [Tooltip("Poise regenerated per second while not staggered, so accumulated chip eventually breaks.")]
        public float poiseRegen = 6f;
        [Tooltip("Stagger duration on a real poise-break — longer than the light hitReactTime micro-flinch.")]
        public float staggerDuration = 0.55f;

        [Header("Death")]
        public float deathDuration = 1.1f;   // death anim plays this long before the body despawns

        [Header("Rewards")]
        public int xpValue = 1;
    }
}
