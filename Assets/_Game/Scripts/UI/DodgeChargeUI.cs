using UnityEngine;
using UnityEngine.UI;

namespace Hordebreakers
{
    /// <summary>
    /// HUD readout for the dodge charge bank. A left-to-right row of pip "fill" images: charges you have show full,
    /// the one currently recharging shows its progress, the rest empty. Pips beyond the current max (the bank can
    /// grow via the DodgeMaxCharges Augment) are hidden. Each fill must be an Image with Image Type = Filled, wrapped
    /// in a pip root (its parent) that holds the dim slot background. Mirrors HUDController's read-the-player pattern;
    /// no per-frame allocation.
    /// </summary>
    public sealed class DodgeChargeUI : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("The Filled 'fill' image of each pip, left-to-right. Array length = the most charges the row can show. Each fill's PARENT is the pip root (slot bg) that gets shown/hidden with the bank size.")]
        [SerializeField] private Image[] fills;
        [Tooltip("Fill color of an available (ready) charge.")]
        [SerializeField] private Color readyColor = new Color(0.35f, 0.85f, 1f, 1f);
        [Tooltip("Fill color of the charge currently recharging.")]
        [SerializeField] private Color rechargingColor = new Color(0.35f, 0.85f, 1f, 0.55f);

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
        }

        private void Update()
        {
            if (player == null || fills == null) return;
            int charges = player.DodgeCharges;
            int max = player.DodgeMaxCharges;
            float recharge = player.DodgeRechargeProgress;

            for (int i = 0; i < fills.Length; i++)
            {
                Image fill = fills[i];
                if (fill == null) continue;

                // Hide pips past the current bank size (and show them again if an Augment grew it).
                Transform root = fill.transform.parent != null ? fill.transform.parent : fill.transform;
                bool show = i < max;
                if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
                if (!show) continue;

                if (i < charges)            // banked
                {
                    fill.fillAmount = 1f;
                    fill.color = readyColor;
                }
                else if (i == charges)      // the one currently recharging
                {
                    fill.fillAmount = recharge;
                    fill.color = rechargingColor;
                }
                else                        // empty, waiting its turn
                {
                    fill.fillAmount = 0f;
                }
            }
        }
    }
}
