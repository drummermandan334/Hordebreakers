using System;

namespace Hordebreakers
{
    /// <summary>
    /// A triggered behavior that fires when one of the player's melee hits lands. Registered on the
    /// <see cref="PlayerLoadout"/> by an <see cref="OnHitProcEffect"/>; dispatched from the melee hit loop.
    /// Concrete procs are <c>[SerializeReference]</c> so augments can carry any proc.
    /// </summary>
    [Serializable]
    public abstract class OnHitProc
    {
        /// <summary>Runs once per enemy hit. Keep it allocation-free — this is on the combat path.</summary>
        public abstract void OnHit(OnHitContext ctx);
    }
}
