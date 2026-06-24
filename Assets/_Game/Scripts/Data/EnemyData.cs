using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Enemy stats + mark config. One asset per enemy type (Husk, Runner, ...).</summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Hordebreakers/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Stats")]
        public float maxHp = 12f;
        public float moveSpeed = 3f;

        [Header("Crowd separation")]
        public float separationRadius = 0.7f;   // push apart from enemies/player within this
        public float separationForce = 6f;      // separation strength

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

        [Header("Hit reaction")]
        [Tooltip("Recoil impulse (m/s) when struck — adds weight to the player's hits.")]
        public float knockback = 2.5f;
        [Tooltip("How long a hit staggers the enemy (movement paused while it flinches).")]
        public float hitReactTime = 0.3f;

        [Header("Marks")]
        public int markMaxStacks = 10;
        public float markDecayTime = 4f;

        [Header("Death")]
        public float deathDuration = 1.1f;   // death anim plays this long before the body despawns

        [Header("Rewards")]
        public int xpValue = 1;
    }
}
