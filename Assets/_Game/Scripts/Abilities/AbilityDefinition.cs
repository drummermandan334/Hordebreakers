using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// A Dynasty-Warriors-style grand ability: a cost + cooldown wrapping composable <see cref="AbilityEffect"/>s.
    /// Granted by <c>GrantAbilityEffect</c> augments (the single canonical path — GDD §6), cast by the player.
    /// Activation flows through <see cref="PlayerController.TryActivateAbility"/>, a single authoritative entry
    /// point the host can drive for co-op later (server-authoritative-ready, GDD §8).
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "Hordebreakers/Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        public string displayName = "Ability";
        [TextArea] public string description = "";
        [Tooltip("Seconds before it can be cast again.")]
        public float cooldown = 6f;
        [Tooltip("Stamina spent to cast.")]
        public float staminaCost = 25f;
        [SerializeReference] public List<AbilityEffect> effects = new List<AbilityEffect>();

        public void Activate(PlayerController player)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null) effects[i].Activate(player);
            }
        }
    }
}
