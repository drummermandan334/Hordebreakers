using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;   // Vignette, ChromaticAberration, ColorAdjustments

namespace Hordebreakers
{
    /// <summary>
    /// Drives the scene's URP post-processing Volume for cinematic combat feedback. Static facades
    /// (mirrors <see cref="CombatAudio"/> / <see cref="PlayerCameraRig.Shake"/>) so callers fire effects without a
    /// reference. Timer-driven, no coroutines, no per-frame alloc. Grabs the Vignette / ChromaticAberration /
    /// ColorAdjustments overrides off a Volume's runtime profile once in Awake and animates their .value each frame.
    ///
    /// Drop this on a GameObject with a URP <see cref="Volume"/> (e.g. the scene's Global Volume). It self-heals: any
    /// missing override is added to the runtime profile instance — never the shared asset, so nothing dirties on disk.
    /// All animation uses UNSCALED time so the FX keep moving during hit-stop / slow-mo (which scale Time.deltaTime).
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PostFxDirector : MonoBehaviour
    {
        public static PostFxDirector Instance { get; private set; }

        [Header("Damage pulse (red vignette flash on taking a hit)")]
        [SerializeField] private Color damageVignetteColor = new Color(0.6f, 0f, 0f);
        [SerializeField] private float damagePulseIntensity = 0.45f;
        [Tooltip("Higher = snappier fade of the damage flash.")]
        [SerializeField] private float damagePulseDecay = 3.5f;

        [Header("Low-HP framing (sustained vignette as health drops)")]
        [Tooltip("Vignette intensity at 0 HP.")]
        [SerializeField] private float lowHpVignetteMax = 0.35f;
        [Tooltip("HP fraction below which the low-HP vignette starts ramping in.")]
        [Range(0f, 1f)][SerializeField] private float lowHpThreshold = 0.4f;
        [SerializeField] private Color lowHpColor = new Color(0.3f, 0f, 0f);

        [Header("Dodge speed-burst (chromatic aberration kick)")]
        [SerializeField] private float dodgeAberration = 0.7f;
        [SerializeField] private float dodgeAberrationDecay = 6f;

        [Header("Musou color grade (eased in/out while the ultimate is active)")]
        [SerializeField] private float musouSaturation = 25f;
        [SerializeField] private float musouContrast = 15f;
        [SerializeField] private float musouPostExposure = 0.3f;
        [SerializeField] private float musouBlendSpeed = 8f;

        private Volume _volume;
        private Vignette _vignette;
        private ChromaticAberration _aberration;
        private ColorAdjustments _color;

        private float _damagePulse;        // 0..1 current red flash
        private float _healthFrac = 1f;    // driven by SetHealthFraction
        private float _dodgeBurst;         // 0..1 current aberration kick
        private bool _musouOn;
        private float _musouT;             // 0..1 eased musou grade weight

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _volume = GetComponent<Volume>();

            VolumeProfile p = _volume.profile;   // runtime instance Unity clones — safe to mutate (NOT sharedProfile)
            if (p == null) return;
            if (!p.TryGet(out _vignette)) _vignette = p.Add<Vignette>(true);
            if (!p.TryGet(out _aberration)) _aberration = p.Add<ChromaticAberration>(true);
            if (!p.TryGet(out _color)) _color = p.Add<ColorAdjustments>(true);

            // Ensure the parameters we write are active, else URP ignores the .value.
            _vignette.intensity.overrideState = true;
            _vignette.color.overrideState = true;
            _aberration.intensity.overrideState = true;
            _color.saturation.overrideState = true;
            _color.contrast.overrideState = true;
            _color.postExposure.overrideState = true;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (_vignette == null) return;
            float dt = Time.unscaledDeltaTime;   // keep animating through hit-stop / slow-mo

            if (_damagePulse > 0f) _damagePulse = Mathf.MoveTowards(_damagePulse, 0f, damagePulseDecay * dt);
            if (_dodgeBurst > 0f) _dodgeBurst = Mathf.MoveTowards(_dodgeBurst, 0f, dodgeAberrationDecay * dt);
            _musouT = Mathf.MoveTowards(_musouT, _musouOn ? 1f : 0f, musouBlendSpeed * dt);

            // Vignette = the stronger of the sustained low-HP framing and the transient damage flash; color leans red as the flash rises.
            float lowHp = _healthFrac < lowHpThreshold
                ? Mathf.InverseLerp(lowHpThreshold, 0f, _healthFrac) * lowHpVignetteMax : 0f;
            float pulse = _damagePulse * damagePulseIntensity;
            _vignette.intensity.value = Mathf.Max(lowHp, pulse);
            _vignette.color.value = Color.Lerp(lowHpColor, damageVignetteColor, _damagePulse);

            _aberration.intensity.value = _dodgeBurst * dodgeAberration;

            _color.saturation.value = _musouT * musouSaturation;
            _color.contrast.value = _musouT * musouContrast;
            _color.postExposure.value = _musouT * musouPostExposure;
        }

        // ---- static facades ----

        /// <summary>Flash the red damage vignette (strength 0..1). Takes the max so a fresh hit can't dim a brighter ongoing flash.</summary>
        public static void DamagePulse(float strength = 1f)
        {
            if (Instance != null) Instance._damagePulse = Mathf.Max(Instance._damagePulse, Mathf.Clamp01(strength));
        }

        /// <summary>Set the player's health fraction (0..1) — drives the sustained low-HP vignette framing.</summary>
        public static void SetHealthFraction(float frac)
        {
            if (Instance != null) Instance._healthFrac = Mathf.Clamp01(frac);
        }

        /// <summary>Kick the dodge speed-burst (chromatic aberration), decays on its own.</summary>
        public static void DodgeBurst(float strength = 1f)
        {
            if (Instance != null) Instance._dodgeBurst = Mathf.Clamp01(strength);
        }

        /// <summary>Toggle the musou color grade (eased in/out).</summary>
        public static void MusouGrade(bool on)
        {
            if (Instance != null) Instance._musouOn = on;
        }
    }
}
