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
        public float dodgeCooldown = 1.0f;

        [Header("Light Attack (fast combo)")]
        public float lightDamage = 8f;
        public float lightReach = 2.2f;

        [Header("Heavy Attack (combo hits; the heavy finisher detonates Marks)")]
        public float heavyDamage = 30f;
        public float heavyReach = 2.6f;

        [Header("Combo feel")]
        [Range(0f, 1f)] public float comboChainOpen = 0.75f;  // fraction of a swing that plays before the next attack chains (earlier presses buffer); higher = fuller swings
        public float attackSteerSpeed = 1.5f;     // slight mid-swing steer speed; the attack lunge overrides full move input

        [Header("Detonation  (burst = flat + stacks * perStack)")]
        public float detonationRadius = 4f;
        public float detonationDamagePerStack = 6f;
        public float detonationFlatDamage = 5f;

        [Header("Targeting")]
        public float softTargetRange = 5f;
    }
}
