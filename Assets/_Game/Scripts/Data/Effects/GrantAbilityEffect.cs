using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Grants (or levels) a grand ability — the seam that keeps abilities on the single canonical leveling
    /// path (GDD §6.6) instead of a parallel system. Until the ability system (Task A) exists, the grant is
    /// recorded on the loadout so nothing is lost; later it forwards to that system.
    /// </summary>
    [Serializable]
    public sealed class GrantAbilityEffect : AugmentEffect
    {
        [Tooltip("Stable id of the ability to grant. Resolved by the ability system once it exists (Task A).")]
        [SerializeField] private string abilityId = "";
        [SerializeField] private int level = 1;

        public override void Apply(in AugmentContext ctx)
        {
            if (ctx.Loadout == null) return;
            ctx.Loadout.GrantAbility(abilityId, level);
        }
    }
}
