using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Grants a grand ability — the seam that keeps abilities on the single canonical leveling path (GDD §6.6)
    /// instead of a parallel system. References an <see cref="AbilityDefinition"/> directly; the player can cast
    /// it once granted.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class GrantAbilityEffect : AugmentEffect
    {
        [Tooltip("The grand ability this augment grants.")]
        [SerializeField] private AbilityDefinition ability;

        public override void Apply(in AugmentContext ctx)
        {
            if (ctx.Loadout != null && ability != null) ctx.Loadout.GrantAbility(ability);
        }
    }
}
