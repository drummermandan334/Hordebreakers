using System;

namespace Hordebreakers
{
    /// <summary>
    /// One composable piece of an <see cref="Augment"/>. Augments hold a <c>[SerializeReference]</c> list
    /// of these, so a single augment can stack heterogeneous effects (stat, weapon, ability, evolution).
    /// </summary>
    [Serializable]
    public abstract class AugmentEffect
    {
        /// <summary>Apply this effect to the player. Runs once, on pick — not a hot path.</summary>
        public abstract void Apply(in AugmentContext ctx);

        /// <summary>
        /// Whether this effect permits its augment to appear in the draft given the current build.
        /// Default true; evolution effects override to require prerequisites.
        /// </summary>
        public virtual bool CanOffer(PlayerLoadout loadout) => true;
    }
}
