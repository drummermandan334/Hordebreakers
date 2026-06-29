namespace Hordebreakers
{
    /// <summary>
    /// Default objective: clear the arena by killing the commander. The commander is spawned + tracked by the
    /// <see cref="GarrisonDirector"/>; this just watches it. The commander is the player-controlled "exit" — engage
    /// it whenever, but lingering piles on reinforcements, so the choice of WHEN to commit is the gamble.
    /// </summary>
    public sealed class SlayCommanderObjective : IArenaObjective
    {
        private readonly GarrisonDirector _garrison;

        public SlayCommanderObjective(GarrisonDirector garrison) { _garrison = garrison; }

        public string Description => "Slay the Commander";
        // Complete once the commander has been spawned and is no longer alive (its IsAlive flips on the killing blow).
        public bool IsComplete => _garrison != null && _garrison.CommanderSpawned && !_garrison.CommanderAlive;

        public void OnArenaStart() { }   // the GarrisonDirector (re)spawns the commander on Begin(); nothing else to arm
        public void Tick(float dt) { }   // IsComplete is polled; no per-frame work
    }
}
