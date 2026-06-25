using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hordebreakers
{
    /// <summary>
    /// Polling HUD driving the Synty Fantasy Warrior widgets: a health <see cref="Slider"/>, an XP
    /// <see cref="Slider"/>, and TextMeshPro readouts for the XP count and a level / wave / kills line.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Slider healthSlider;
        [Tooltip("Stamina bar. Optional — assign a Slider to show it.")]
        [SerializeField] private Slider staminaSlider;
        [Tooltip("Musou meter bar. Optional — assign a Slider to show it.")]
        [SerializeField] private Slider musouSlider;
        [SerializeField] private Slider xpSlider;
        [Tooltip("XP count label on the XP bar, e.g. \"3 / 7\". Optional.")]
        [SerializeField] private TMP_Text xpText;
        [Tooltip("Level / wave / kills readout. Optional.")]
        [SerializeField] private TMP_Text infoText;

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
            if (player != null && healthSlider != null) healthSlider.value = player.HealthNormalized;
            if (player != null && staminaSlider != null) staminaSlider.value = player.StaminaNormalized;
            if (player != null && musouSlider != null) musouSlider.value = player.MusouNormalized;

            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (xpSlider != null) xpSlider.value = gm.XpToNext > 0 ? (float)gm.Xp / gm.XpToNext : 0f;
            if (xpText != null) xpText.text = gm.Xp + " / " + gm.XpToNext;
            if (infoText != null) infoText.text = "Lv " + gm.Level + "     Wave " + gm.Wave + "     Kills " + gm.Kills;
        }
    }
}
