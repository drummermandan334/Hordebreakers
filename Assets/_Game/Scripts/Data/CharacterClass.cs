using System;

namespace Hordebreakers
{
    /// <summary>
    /// Playable character classes. An <see cref="Augment"/> declares which class(es) may roll it and the
    /// draft filters to the active character's class. Flags so an augment can allow several at once.
    /// Roster tracked in Docs/CHARACTER_CLASSES_PLAN.md (only BattleSorcerer is playable today).
    /// </summary>
    [Flags]
    public enum CharacterClass
    {
        None = 0,
        BattleSorcerer = 1 << 0,
        Knight = 1 << 1,
        Barbarian = 1 << 2,
        Rogue = 1 << 3,
        Mage = 1 << 4,
        Wizard = 1 << 5,
        Archer = 1 << 6,
        Universal = ~0   // rolls for every class
    }
}
