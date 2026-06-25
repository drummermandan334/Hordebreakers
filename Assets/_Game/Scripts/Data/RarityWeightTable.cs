using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Designer-tunable draft weights per <see cref="Rarity"/>. The level-up draft rolls weighted by these
    /// — the deliberate engine of run-to-run difficulty variance (GDD §6.6). Higher weight = more common.
    /// </summary>
    [CreateAssetMenu(fileName = "RarityWeightTable", menuName = "Hordebreakers/Rarity Weight Table")]
    public sealed class RarityWeightTable : ScriptableObject
    {
        [Tooltip("Draw weight per rarity, indexed Common..Legendary. Higher = appears more often in the draft.")]
        [SerializeField] private float[] weightPerRarity = { 100f, 55f, 25f, 10f, 3f };

        public float Weight(Rarity rarity)
        {
            int i = (int)rarity;
            if (weightPerRarity == null || i < 0 || i >= weightPerRarity.Length) return 1f;
            return Mathf.Max(0f, weightPerRarity[i]);
        }
    }
}
