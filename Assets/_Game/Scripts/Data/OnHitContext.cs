using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Passed to every <see cref="OnHitProc"/> when one of the player's melee hits lands on an enemy.
    /// A small by-value struct (cheap to copy per hit, and directly testable).
    /// </summary>
    public readonly struct OnHitContext
    {
        public readonly PlayerController Player;
        public readonly IDamageable Victim;
        public readonly Vector3 HitPoint;
        public readonly float Damage;       // damage the swing dealt to this victim
        public readonly bool IsHeavy;
        public readonly bool IsFinisher;

        public OnHitContext(PlayerController player, IDamageable victim, Vector3 hitPoint, float damage, bool isHeavy, bool isFinisher)
        {
            Player = player;
            Victim = victim;
            HitPoint = hitPoint;
            Damage = damage;
            IsHeavy = isHeavy;
            IsFinisher = isFinisher;
        }
    }
}
