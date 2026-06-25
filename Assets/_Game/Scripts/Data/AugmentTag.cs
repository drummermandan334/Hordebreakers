using System;

namespace Hordebreakers
{
    /// <summary>
    /// Build tags on an <see cref="Augment"/>. Synergy and evolution effects query the player's loadout
    /// by tag ("how many Detonation augments do I own?") to scale with, combine with, or evolve prior picks.
    /// </summary>
    [Flags]
    public enum AugmentTag
    {
        None = 0,
        Offense = 1 << 0,
        Defense = 1 << 1,
        Mobility = 1 << 2,
        Weapon = 1 << 3,
        Ability = 1 << 4,
        Detonation = 1 << 5,
        Throw = 1 << 6
    }
}
