using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Draft rarity — weights how often an augment appears in the level-up draw.</summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// One level-up option and the single canonical unit of in-run progression (GDD §6.6). An augment is a
    /// class-gated, rarity-tiered container of composable <see cref="AugmentEffect"/>s. The pool is assigned
    /// on the draft UI.
    /// </summary>
    [CreateAssetMenu(fileName = "Augment", menuName = "Hordebreakers/Augment")]
    public class Augment : ScriptableObject
    {
        public string title = "Augment";
        [TextArea] public string description = "";
        public Rarity rarity = Rarity.Common;
        [Tooltip("Which class(es) may roll this. Universal rolls for everyone.")]
        public CharacterClass allowedClasses = CharacterClass.Universal;
        [Tooltip("Build tags this augment contributes — read by synergy/evolution effects.")]
        public AugmentTag tags = AugmentTag.None;
        [Tooltip("How many times this augment may be taken in a run. 0 = unlimited.")]
        public int maxStacks = 0;
        [SerializeReference] public List<AugmentEffect> effects = new List<AugmentEffect>();

        /// <summary>Apply every effect on this augment to the player.</summary>
        public void Apply(in AugmentContext ctx)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null) effects[i].Apply(in ctx);
            }
        }

        /// <summary>Whether this augment may appear in the draft given the current build.</summary>
        public bool CanOffer(PlayerLoadout loadout)
        {
            if (loadout == null) return false;
            if ((allowedClasses & loadout.Class) == 0) return false;
            if (maxStacks > 0 && loadout.StacksOf(this) >= maxStacks) return false;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i] != null && !effects[i].CanOffer(loadout)) return false;
                }
            }
            return true;
        }
    }
}
