using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Which weapon value a <see cref="WeaponModEffect"/> changes.</summary>
    public enum WeaponStat
    {
        Damage,
        ProjectileSpeed,
        Cooldown
    }

    /// <summary>
    /// Buffs the player's throw weapon (the only weapon today; structured so future weapons slot in).
    /// No-op if the player has no throw weapon.
    /// </summary>
    [Serializable]
    public sealed class WeaponModEffect : AugmentEffect
    {
        [SerializeField] private WeaponStat target;
        [SerializeField] private StatOp op = StatOp.Add;
        [SerializeField] private float amount = 1f;

        public override void Apply(in AugmentContext ctx)
        {
            ThrowWeapon t = ctx.Loadout != null ? ctx.Loadout.Throw : null;
            if (t == null) return;
            switch (target)
            {
                case WeaponStat.Damage: t.Damage = Combine(t.Damage); break;
                case WeaponStat.ProjectileSpeed: t.ProjectileSpeed = Combine(t.ProjectileSpeed); break;
                case WeaponStat.Cooldown: t.Cooldown = Combine(t.Cooldown); break;
            }
        }

        private float Combine(float current)
        {
            return op == StatOp.Add ? current + amount : current * amount;
        }
    }
}
