using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Restores player health on pick. Combines a flat amount with a fraction of max HP.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class HealEffect : AugmentEffect
    {
        [SerializeField] private float flat = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float fractionOfMax = 0f;

        public override void Apply(in AugmentContext ctx)
        {
            if (ctx.Stats == null) return;
            float total = flat + fractionOfMax * ctx.Stats.maxHp;
            ctx.Player.Heal(total);
        }
    }
}
