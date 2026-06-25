using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The synergy headliner: requires prerequisite augments already in the build, consumes them, and
    /// applies an evolved result. Proves the "consume prior picks → evolved form" seam (GDD §6.6). The
    /// augment carrying this only appears in the draft once every prerequisite is owned. Consuming removes
    /// the prerequisites from the build; it does not reverse their already-applied stat deltas.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class EvolutionEffect : AugmentEffect
    {
        [Tooltip("Every one of these augments must be owned before this can be offered.")]
        [SerializeField] private List<Augment> prerequisites = new List<Augment>();
        [Tooltip("Remove the prerequisites from the build when this evolves.")]
        [SerializeField] private bool consumePrerequisites = true;
        [Tooltip("Effects applied when the evolution triggers (the evolved bonus).")]
        [SerializeReference] private List<AugmentEffect> evolvedEffects = new List<AugmentEffect>();

        public override bool CanOffer(PlayerLoadout loadout)
        {
            if (loadout == null || prerequisites == null || prerequisites.Count == 0) return false;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (prerequisites[i] == null || !loadout.Has(prerequisites[i])) return false;
            }
            return true;
        }

        public override void Apply(in AugmentContext ctx)
        {
            if (!CanOffer(ctx.Loadout)) return;   // prerequisites must still be present
            if (consumePrerequisites)
            {
                for (int i = 0; i < prerequisites.Count; i++)
                {
                    ctx.Loadout.Remove(prerequisites[i]);
                }
            }
            if (evolvedEffects != null)
            {
                for (int i = 0; i < evolvedEffects.Count; i++)
                {
                    if (evolvedEffects[i] != null) evolvedEffects[i].Apply(in ctx);
                }
            }
        }
    }
}
