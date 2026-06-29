namespace Hordebreakers
{
    /// <summary>
    /// The "body" an enemy behavior graph drives — the mechanics (move, face, telegraph, strike, recover) behind a
    /// uniform verb set, so ONE generic graph + node library works for every archetype (Rusher / Charger / Brute / …).
    /// The graph decides WHAT and WHEN; the implementation (on <see cref="Enemy"/> / <see cref="Brute"/>) does HOW and
    /// reads its numbers from its own EnemyData/EliteData. The hand-rolled FSM drives this SAME interface, so it doubles
    /// as the fallback brain AND validates the API in Play mode before any graph exists.
    /// </summary>
    public interface IEnemyBody
    {
        // --- queries (condition nodes / gating) ---
        bool IsAlive { get; }
        bool IsStaggered { get; }          // flinching from a hit — actions should yield
        bool InAttackRange { get; }        // archetype range: standoff ring / charge-start / slam radius
        bool OffCooldown { get; }          // ready to start another attack

        // --- per-frame movement verbs (action nodes call each tick; they also face the player) ---
        void ApproachStep(float dt);       // close toward the attack position (ring / rush / walk)
        void RepositionStep(float dt);     // in range but cooling: strafe / hold / keep closing

        // --- attack sequence (telegraph -> commit -> recover); the body owns the timers + hit application ---
        bool TryStartTelegraph();          // try to claim an attacker slot + begin the wind-up tell; false = denied (cap full) -> orbit instead
        void CancelTelegraph();            // interrupt the tell (player escaped, or we got hit)
        bool TickTelegraph(float dt);      // true when the wind-up is done (faces the player during it)
        bool TelegraphShouldAbort { get; } // player left the commit window mid-wind-up (archetype-defined)
        void StartAttack();                // commit: lock the strike direction / pick the strike point
        bool TickAttack(float dt);         // true when the strike (dash / slam) is complete; applies hits internally
        void StartRecover();               // arm the recovery timer
        bool TickRecover(float dt);        // true when recovery is done; resets the attack cooldown on finish
    }
}
