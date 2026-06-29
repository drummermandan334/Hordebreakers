using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Tuning for the Brute elite: a slow, high-HP bruiser with a telegraphed AoE slam.</summary>
    [CreateAssetMenu(fileName = "EliteData", menuName = "Hordebreakers/Elite Data")]
    public class EliteData : ScriptableObject
    {
        [Header("Stats")]
        public float maxHp = 120f;
        public float moveSpeed = 2.2f;
        [Tooltip("How fast the brute turns to face the player (deg/sec). Slow — a heavy bruiser pivots, so a quick player can get around it.")]
        public float turnSpeedDeg = 200f;

        [Header("Crowd separation")]
        public float separationRadius = 1.1f;   // the brute is bigger — wider personal space
        public float separationForce = 6f;

        [Tooltip("The slam shatters a held block (guard-break) — turtling through an elite's slam should fail. Elites set this on.")]
        public bool guardBreaks = true;

        [Header("Slam (telegraphed AoE)")]
        public float slamRange = 3.5f;      // begins a slam when the player is within this
        public float slamRadius = 3.5f;     // damage + telegraph radius
        public float slamDamage = 35f;
        public float slamWindup = 1.1f;     // telegraph duration = the dodge window
        public float slamRecovery = 0.8f;
        public float slamCooldown = 2.5f;

        [Header("Hit reaction (poise — never flinches mid-slam)")]
        [Tooltip("How long a hit staggers the brute when it's not slamming.")]
        public float hitReactTime = 0.3f;
        [Tooltip("Poise pool. Outside a slam, a hit depletes it by the damage dealt; emptying it (or a heavy >= bloodMinDamage) is a real stagger. <= 0 = legacy: every (non-slam) hit flinches.")]
        public float maxPoise = 60f;
        [Tooltip("Poise regenerated per second while not staggered.")]
        public float poiseRegen = 10f;
        [Tooltip("Stagger duration on a real poise-break.")]
        public float staggerDuration = 0.6f;

        [Header("Death / Reward")]
        public float deathDuration = 1.6f;
        [Tooltip("Recoil impulse (m/s) on the killing blow — the body slides back on death. Lower than chaff (heavy elite).")]
        public float deathKnockback = 10f;
        public int xpValue = 10;
    }
}
