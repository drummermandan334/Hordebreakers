using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Anything that can be hit by melee or projectiles.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Deal damage from an unknown direction (reacts as a frontal hit).</summary>
        void TakeDamage(float amount);

        /// <summary>Deal damage from a known world-space source, so the victim can play a directional hit-react.</summary>
        void TakeDamage(float amount, Vector3 sourcePos);

        /// <summary>
        /// Deal damage that may break a held guard. Only the player blocks, so non-blocking victims (enemies, dummies)
        /// ignore <paramref name="guardBreak"/> and just take the hit — they delegate to the 2-arg overload.
        /// </summary>
        void TakeDamage(float amount, Vector3 sourcePos, bool guardBreak);
    }
}
