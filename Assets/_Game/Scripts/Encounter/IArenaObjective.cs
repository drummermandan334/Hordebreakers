namespace Hordebreakers
{
    /// <summary>
    /// One arena win-condition. The encounter loop is objective-agnostic: "Slay" ships now; Destroy / Hold /
    /// Reach plug in later behind this same interface (the EncounterController only knows IArenaObjective).
    /// </summary>
    public interface IArenaObjective
    {
        /// <summary>HUD readout, e.g. "Slay the Commander".</summary>
        string Description { get; }

        /// <summary>True once the win-condition is met (polled by the EncounterController).</summary>
        bool IsComplete { get; }

        /// <summary>(Re)arm at arena start / reset.</summary>
        void OnArenaStart();

        /// <summary>Per-frame progress for objectives that need it (Slay just polls IsComplete; no work here).</summary>
        void Tick(float dt);
    }
}
