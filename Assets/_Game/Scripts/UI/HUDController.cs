using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hordebreakers
{
    /// <summary>
    /// Polling HUD: health / musou / XP sliders + TextMeshPro readouts for the XP count and the level / wave / kills
    /// line, plus the arena (encounter-loop) readouts — objective, the at-risk-spoils / reinforcement-tier line, and
    /// the big ARENA CLEARED / YOU DIED prompt. Strings rebuild only on change (no per-frame GC).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Slider healthSlider;
        [Tooltip("Musou meter bar. Optional — assign a Slider to show it.")]
        [SerializeField] private Slider musouSlider;
        [SerializeField] private Slider xpSlider;
        [Tooltip("XP count label on the XP bar, e.g. \"3 / 7\". Optional.")]
        [SerializeField] private TMP_Text xpText;
        [Tooltip("Level / wave / kills readout. Optional.")]
        [SerializeField] private TMP_Text infoText;

        [Header("Arena (encounter loop)")]
        [SerializeField] private EncounterController encounter;
        [Tooltip("Objective readout, e.g. \"Slay the Commander\".")]
        [SerializeField] private TMP_Text objectiveText;
        [Tooltip("At-risk spoils + reinforcement tier + garrison remaining (the temptation gauge).")]
        [SerializeField] private TMP_Text arenaText;
        [Tooltip("Big center prompt for ARENA CLEARED / YOU DIED.")]
        [SerializeField] private TMP_Text promptText;

        // Cached last values so the text strings are only rebuilt when something changes (no per-frame GC).
        private int _lastXp = -1, _lastXpToNext = -1, _lastLevel = -1, _lastWave = -1, _lastKills = -1;
        private int _lastRisk = -1, _lastGWave = -2, _lastWaveSecs = -1;

        private void Start()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.GetComponent<PlayerController>();
            }
            if (encounter == null) encounter = FindFirstObjectByType<EncounterController>();
            if (encounter != null)
            {
                encounter.OnArenaCleared += HandleCleared;
                encounter.OnPlayerDied += HandleDied;
                encounter.OnArenaReset += HandleReset;
            }
            if (promptText != null) promptText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (encounter != null)
            {
                encounter.OnArenaCleared -= HandleCleared;
                encounter.OnPlayerDied -= HandleDied;
                encounter.OnArenaReset -= HandleReset;
            }
        }

        private void Update()
        {
            if (player != null && healthSlider != null) healthSlider.value = player.HealthNormalized;
            if (player != null && musouSlider != null) musouSlider.value = player.MusouNormalized;

            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (xpSlider != null) xpSlider.value = gm.XpToNext > 0 ? (float)gm.Xp / gm.XpToNext : 0f;

            // Only rebuild label strings when the underlying values change (steady-state is zero-alloc).
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

            // Arena readouts.
            if (encounter != null)
            {
                if (objectiveText != null && encounter.Objective != null && objectiveText.text != encounter.Objective.Description)
                    objectiveText.text = encounter.Objective.Description;

                GarrisonDirector g = encounter.Garrison;
                int secs = g != null ? Mathf.CeilToInt(g.SecondsToNextWave) : 0;
                if (arenaText != null && g != null && (gm.UncommittedXp != _lastRisk || g.WaveNumber != _lastGWave || secs != _lastWaveSecs))
                {
                    _lastRisk = gm.UncommittedXp; _lastGWave = g.WaveNumber; _lastWaveSecs = secs;
                    string waveStr = g.AllWavesDone ? "Final wave"
                        : g.WaveNumber <= 0 ? string.Concat("First wave in ", secs.ToString(), "s")
                        : string.Concat("Wave ", g.WaveNumber.ToString(), "/", g.WaveCount.ToString(), "     Next in ", secs.ToString(), "s");
                    arenaText.text = string.Concat(waveStr, "     At-risk XP ", _lastRisk.ToString());
                }
            }
        }

        private void HandleCleared() => ShowPrompt("ARENA CLEARED");
        private void HandleDied() => ShowPrompt("YOU DIED");
        private void HandleReset() { if (promptText != null) promptText.gameObject.SetActive(false); }

        private void ShowPrompt(string s)
        {
            if (promptText == null) return;
            promptText.text = s;
            promptText.gameObject.SetActive(true);
        }
    }
}
