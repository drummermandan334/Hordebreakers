using UnityEngine;
using UnityEngine.UI;

namespace Hordebreakers
{
    /// <summary>Lightweight polling HUD: health bar, XP bar, and a level/wave/kills readout.</summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image xpFill;
        [SerializeField] private Text infoText;

        private void Start()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            if (player != null && healthFill != null) healthFill.fillAmount = player.HealthNormalized;

            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (xpFill != null) xpFill.fillAmount = gm.XpToNext > 0 ? (float)gm.Xp / gm.XpToNext : 0f;
            if (infoText != null) infoText.text = "Lv " + gm.Level + "    Wave " + gm.Wave + "    Kills " + gm.Kills;
        }
    }
}
