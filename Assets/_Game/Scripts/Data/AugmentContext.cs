namespace Hordebreakers
{
    /// <summary>
    /// Everything an <see cref="AugmentEffect"/> may touch when an augment is picked: the player, their
    /// mutable runtime stats, and their build loadout (weapons, abilities, taken augments). Built by
    /// <see cref="PlayerController"/> and passed by ref. Effects receive only what this hands them — the
    /// loadout is reached through here, never as a global.
    /// </summary>
    public readonly struct AugmentContext
    {
        public readonly PlayerController Player;
        public readonly PlayerCombatData Stats;   // runtime clone — safe to mutate
        public readonly PlayerLoadout Loadout;    // the run-scoped build model

        public AugmentContext(PlayerController player, PlayerCombatData stats, PlayerLoadout loadout)
        {
            Player = player;
            Stats = stats;
            Loadout = loadout;
        }
    }
}
