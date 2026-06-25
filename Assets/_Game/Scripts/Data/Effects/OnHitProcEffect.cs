using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Augment effect that registers an on-hit proc onto the player's loadout. The proc itself is a
    /// <c>[SerializeReference]</c> field, so one effect type covers every proc behavior.
    /// </summary>
    [Serializable]
    public sealed class OnHitProcEffect : AugmentEffect
    {
        [SerializeReference] private OnHitProc proc;

        public override void Apply(in AugmentContext ctx)
        {
            if (ctx.Loadout != null && proc != null) ctx.Loadout.AddOnHitProc(proc);
        }
    }
}
