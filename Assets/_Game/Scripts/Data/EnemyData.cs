using UnityEngine;

namespace Hordebreakers
{
    /// <summary>How an enemy commits its attack — chooses the behavior branch in <see cref="Enemy"/>.</summary>
    public enum AttackArchetype
    {
        StandoffLunge,   // holds a ring around the player, circles, telegraphs a short lunge (the Husk)
        Charger,         // rushes in, telegraphs a long committed dash across the gap (a Tier-2 pressure unit)
        Dummy            // passive training dummy: stands, faces the player, takes hits — never seeks or attacks
    }

    /// <summary>Enemy stats. One asset per enemy type (Husk, Charger, ...).</summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Hordebreakers/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Archetype")]
        public AttackArchetype archetype = AttackArchetype.StandoffLunge;
        [Header("Stats")]
        public float maxHp = 12f;
        public float moveSpeed = 3f;

        [Header("Crowd separation")]
        public float separationRadius = 0.7f;   // push apart from enemies/player within this
        public float separationForce = 6f;      // separation strength
        [Tooltip("Hard minimum distance the body keeps from the player's center — prevents standing inside the player. Keep just under (player CC radius + this body's radius) so it doesn't fight the player's own collision.")]
        public float playerSpacing = 0.6f;

        [Header("Contact damage (strike)")]
        public float contactDamage = 5f;
        public float contactInterval = 1f;
        public float contactRange = 1.4f;

        [Header("Attack pattern (telegraphed lunge)")]
        [Tooltip("Standoff distance the enemy holds at, ringing the player instead of piling on.")]
        public float attackRange = 1.8f;
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

        [Header("Hit reaction")]
        [Tooltip("Base recoil impulse (m/s) when struck — adds weight to the player's hits.")]
        public float knockback = 5f;
        [Tooltip("Extra recoil per point of damage, so heavies knock far harder than lights (impulse = knockback + damage * this).")]
        public float knockbackPerDamage = 0.5f;
        [Tooltip("Big recoil impulse (m/s) applied on the killing blow — the body flies back on death.")]
        public float deathKnockback = 16f;
        [Tooltip("How long a hit staggers the enemy (movement paused while it flinches).")]
        public float hitReactTime = 0.3f;

        [Header("Death")]
        public float deathDuration = 1.1f;   // death anim plays this long before the body despawns

        [Header("Rewards")]
        public int xpValue = 1;
    }
}
