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

        // Cached last values so the text strings are only rebuilt when something changes (no per-frame GC).
        private int _lastXp = -1, _lastXpToNext = -1, _lastLevel = -1, _lastWave = -1, _lastKills = -1;

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

            // Only rebuild the label strings when the underlying values change — these ran every frame and
            // allocated 6 strings/frame (GC churn). Steady-state is now zero-alloc.
            if (xpText != null && (gm.Xp != _lastXp || gm.XpToNext != _lastXpToNext))
            {
                _lastXp = gm.Xp; _lastXpToNext = gm.XpToNext;
                xpText.text = string.Concat(gm.Xp.ToString(), " / ", gm.XpToNext.ToString());
            }
            if (infoText != null && (gm.Level != _lastLevel || gm.Wave != _lastWave || gm.Kills != _lastKills))
            {
                _lastLevel = gm.Level; _lastWave = gm.Wave; _lastKills = gm.Kills;
                infoText.text = string.Concat("Lv ", gm.Level.ToString(), "     Wave ", gm.Wave.ToString(), "     Kills ", gm.Kills.ToString());
            }
        }
    }
}
