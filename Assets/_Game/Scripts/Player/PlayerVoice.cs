using UnityEngine;
using UnityEngine.Audio;

namespace Hordebreakers
{
    /// <summary>
    /// The player character's vocal grunts — attack efforts (chance-gated so they don't fire on every swing),
    /// hurt reactions scaled to the damage taken, and a defeat cry. One dedicated 2D voice that restarts each
    /// line so grunts never pile up; a short interval gate stops a dense horde from machine-gunning the hurt
    /// vocals. Clips live HERE so they're easy to swap / A-B in one place (mirrors <see cref="CombatAudio"/>).
    /// Driven by <see cref="PlayerController"/> off the same events that fire the swings / hit-react.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVoice : MonoBehaviour
    {
        [Header("Attack efforts (light = jab, medium = heavy, heavy = finisher)")]
        [SerializeField] private AudioClip[] _attackLight;
        [SerializeField] private AudioClip[] _attackMedium;
        [SerializeField] private AudioClip[] _attackHeavy;
        [Range(0f, 1f)]
        [Tooltip("Chance a swing actually grunts — kept below 1 so the player isn't vocalising every single hit.")]
        [SerializeField] private float _attackChance = 0.4f;

        [Header("Hurt reactions (picked by damage taken)")]
        [SerializeField] private AudioClip[] _hurtLight;
        [SerializeField] private AudioClip[] _hurtMedium;
        [SerializeField] private AudioClip[] _hurtHeavy;
        [Tooltip("Damage at or above this plays the medium hurt set.")]
        [SerializeField] private float _hurtMediumThreshold = 12f;
        [Tooltip("Damage at or above this plays the heavy hurt set.")]
        [SerializeField] private float _hurtHeavyThreshold = 25f;

        [Header("Defeat")]
        [SerializeField] private AudioClip[] _defeat;

        [Header("Output (all tunable)")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;
        [Range(0.1f, 2f)]
        [SerializeField] private float _basePitch = 1f;
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchVariance = 0.06f;
        [Range(0f, 1f)]
        [Tooltip("0 = 2D (constant loudness), 1 = fully 3D positional.")]
        [SerializeField] private float _spatialBlend = 0f;
        [Tooltip("Optional mixer group to route the voice through (for master/voice volume control).")]
        [SerializeField] private AudioMixerGroup _output;
        [Tooltip("Minimum seconds between voice lines, so a flurry can't stack / spam grunts.")]
        [SerializeField] private float _minInterval = 0.25f;

        private AudioSource _voice;
        private float _nextTime;

        private void Awake()
        {
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            ApplySourceSettings();
        }

        // Lets the spatial/mixer knobs be tuned live in the Inspector during play.
        private void OnValidate()
        {
            if (_voice != null) ApplySourceSettings();
        }

        private void ApplySourceSettings()
        {
            _voice.spatialBlend = _spatialBlend;
            _voice.outputAudioMixerGroup = _output;
        }

        /// <summary>An attack effort grunt — chance-gated (not every swing). Picks light / medium / heavy by the swing.</summary>
        public void Attack(bool heavy, bool finisher)
        {
            if (Random.value > _attackChance) return;
            AudioClip[] set = finisher ? _attackHeavy : (heavy ? _attackMedium : _attackLight);
            Play(set);
        }

        /// <summary>A hurt reaction scaled to the damage taken. Always tries (hurt is key feedback), still interval-gated.</summary>
        public void Hurt(float amount)
        {
            AudioClip[] set;
            if (amount >= _hurtHeavyThreshold)
            {
                set = _hurtHeavy;
            }
            else if (amount >= _hurtMediumThreshold)
            {
                set = _hurtMedium;
            }
            else
            {
                set = _hurtLight;
            }
            Play(set);
        }

        /// <summary>The death cry — always plays, bypasses the interval gate.</summary>
        public void Defeat()
        {
            AudioClip clip = Pick(_defeat);
            if (clip == null) return;
            // Routed through CombatAudio (a persistent object) on purpose: the player GameObject is deactivated on
            // death, which would instantly cut a clip played on our own AudioSource.
            CombatAudio.PlaySpell(clip, transform.position, _volume, _pitchVariance);
        }

        private AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
        }

        private void Play(AudioClip[] clips)
        {
            if (Time.unscaledTime < _nextTime) return;
            AudioClip clip = Pick(clips);
            if (clip == null) return;
            _voice.Stop();
            _voice.clip = clip;
            _voice.pitch = _basePitch + Random.Range(-_pitchVariance, _pitchVariance);
            _voice.volume = _volume;
            _voice.Play();
            _nextTime = Time.unscaledTime + _minInterval;
        }
    }
}
