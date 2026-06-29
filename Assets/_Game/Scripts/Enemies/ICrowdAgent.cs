using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The minimal surface the <see cref="CrowdDirector"/> needs from an enemy to coordinate it — kept as a tiny
    /// interface so <see cref="Enemy"/> and <see cref="Brute"/> opt in WITHOUT a shared base class (they already share
    /// the <see cref="IEnemyBody"/> verb set; this adds only the crowd-coordination hooks). The director never drives
    /// movement through this — each enemy still moves itself; the director only ADVISES (grants attack tokens, assigns
    /// approach-ring slots).
    /// </summary>
    public interface ICrowdAgent
    {
        /// <summary>Stable id handed out by the director on register; -1 while unmanaged.</summary>
        int AgentId { get; }
        bool IsAlive { get; }
        Transform AgentTransform { get; }

        /// <summary>True for ring-waiters that should hold a spread approach slot (chaff rushers; not chargers/elites/dummies).</summary>
        bool WantsSlot { get; }
        /// <summary>0..1 aggression used to prioritise the scarce slots.</summary>
        float Aggression { get; }

        /// <summary>Director assigns the absolute bearing (deg, 0 = +Z) of this agent's approach slot around the player.</summary>
        void AssignSlotAngle(float deg);
        /// <summary>Director clears the assignment (agent falls back to its own circling).</summary>
        void ClearSlot();
    }
}
