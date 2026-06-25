using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Heals the player for a fraction of the damage each melee hit deals.</summary>
    [Serializable]
    public sealed class LifestealProc : OnHitProc
    {
        [Range(0f, 1f)]
        [SerializeField] private float fraction = 0.1f;

        public override void OnHit(OnHitContext ctx)
        {
            if (ctx.Player == null || fraction <= 0f) return;
            ctx.Player.Heal(fraction * ctx.Damage);
        }
    }
}
