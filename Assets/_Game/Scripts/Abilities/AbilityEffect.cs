using System;

namespace Hordebreakers
{
    /// <summary>
    /// One composable piece of a grand ability (AoE damage, dash, buff, …). Abilities hold a
    /// <c>[SerializeReference]</c> list of these so designers stack effects — the same pattern as AugmentEffect.
    /// </summary>
    [Serializable]
    public abstract class AbilityEffect
    {
        /// <summary>Run the effect. Driven from <see cref="PlayerController.TryActivateAbility"/> (authoritative).</summary>
        public abstract void Activate(PlayerController player);
    }
}
