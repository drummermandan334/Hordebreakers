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
    }
}
