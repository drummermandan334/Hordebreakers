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
        [SerializeReference] public List<AbilityEffect> effects = new List<AbilityEffect>();

        [Header("Audio")]
        [Tooltip("Played at the player the instant the ability is cast (the incantation / fire whoosh). The booms live on the individual effects.")]
        public AudioClip castClip;
        [Range(0f, 1f)] public float castVolume = 1f;

        public void Activate(PlayerController player)
        {
            if (player != null && castClip != null) CombatAudio.PlaySpell(castClip, player.transform.position, castVolume);
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null) effects[i].Activate(player);
            }
        }
    }
}
