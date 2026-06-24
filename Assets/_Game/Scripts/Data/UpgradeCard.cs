using UnityEngine;

namespace Hordebreakers
{
    public enum UpgradeType
    {
        AutoDamage,
        AutoFireRate,
        ExtraMark,
        DetonationRadius,
        DetonationPower,
        MoveSpeed,
        MaxHealth
    }

    /// <summary>One level-up upgrade option. The pool is assigned on the UpgradeCardUI.</summary>
    [CreateAssetMenu(fileName = "UpgradeCard", menuName = "Hordebreakers/Upgrade Card")]
    public class UpgradeCard : ScriptableObject
    {
        public string title = "Upgrade";
        [TextArea] public string description = "";
        public UpgradeType type;
        public float magnitude = 1f;
    }
}
