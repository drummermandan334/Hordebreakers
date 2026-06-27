using UnityEngine;
using UnityEngine.Audio;

namespace Hordebreakers
{
    /// <summary>
    /// Speed-driven footsteps: accumulates ground distance from the CharacterController's velocity and emits a
    /// step every stride, so steps stay in sync with movement at any speed without authoring animation events.
    /// Fires the FIRST step the instant you start moving (otherwise the opening step is a whole stride late).
    /// Alternates through the clip set for variety; its own 2D voice (restart per step, no pitch bleed). Every
    /// output knob (volume / base pitch / pitch variance / spatial blend / mixer group) is exposed here so the
    /// AudioSource — which is created at runtime — is fully tunable from the Inspector.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip[] _steps;

        [Header("Cadence")]
        [Tooltip("Metres travelled between footfalls — larger = fewer steps. Tune to the run animation's cadence.")]
        [SerializeField] private float _strideLength = 2.2f;
        [Tooltip("Below this horizontal speed (m/s) no steps play (idle / barely shuffling).")]
        [SerializeField] private float _minSpeed = 1.5f;
        [Tooltip("Play a footstep the instant you start moving, so the first step isn't a whole stride late.")]
        [SerializeField] private bool _stepOnStart = true;
        [Tooltip("Minimum real seconds between footsteps — stops movement-state flicker (e.g. attack-steering) from machine-gunning the step SFX.")]
        [SerializeField] private float _minStepInterval = 0.2f;

        [Header("Combat")]
        [Range(0f, 1f)]
        [Tooltip("Footstep volume MULTIPLIER while the player is attacking, so steps don't crowd combat. 0 = silent, 0.125 = 1/8 volume.")]
        [SerializeField] private float _attackVolumeScale = 0f;

        [Header("Output (all tunable)")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.6f;
        [Range(0.1f, 2f)]
        [SerializeField] private float _basePitch = 1f;
        [Range(0f, 0.5f)]
        [SerializeField] private float _pitchVariance = 0.1f;
        [Range(0f, 1f)]
        [Tooltip("0 = 2D (constant loudness), 1 = fully 3D positional.")]
        [SerializeField] private float _spatialBlend = 0f;
        [Tooltip("Optional mixer group to route footsteps through (for master/SFX volume control).")]
        [SerializeField] private AudioMixerGroup _output;

        private CharacterController _cc;
        private PlayerController _player;
        private AudioSource _voice;
        private float _distance;
        private int _next;
        private bool _wasMoving;
        private float _nextStepTime;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _player = GetComponent<PlayerController>();   // optional — used to duck steps while attacking
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

        private void Update()
        {
            // A dodge roll has its own whoosh — never footsteps. Reset so the first step after the roll is fresh.
            if (_player != null && _player.IsDodging)
            {
                _distance = 0f;
                _wasMoving = false;
                return;
            }

            Vector3 v = _cc.velocity;
            v.y = 0f;
            float speed = v.magnitude;
            bool moving = _cc.isGrounded && speed >= _minSpeed;

            if (moving && !_wasMoving)
            {
                if (_stepOnStart) Step();   // first footfall the instant you move — no full-stride delay
                _distance = 0f;
            }
            else if (moving)
            {
                _distance += speed * Time.deltaTime;
                if (_distance >= _strideLength)
                {
                    _distance -= _strideLength;
                    Step();
                }
            }
            else
            {
                _distance = 0f;
            }

            _wasMoving = moving;
        }

        private void Step()
        {
            if (_steps == null || _steps.Length == 0) return;
            if (Time.time < _nextStepTime) return;   // anti-machine-gun: a step can't retrigger faster than _minStepInterval
            _nextStepTime = Time.time + _minStepInterval;

            float vol = _volume;
            if (_player != null && _player.IsAttacking) vol *= _attackVolumeScale;   // duck (or mute) so steps don't crowd combat
            if (vol <= 0.0001f) return;

            AudioClip clip = _steps.Length == 1 ? _steps[0] : _steps[_next];
            _next = (_next + 1) % _steps.Length;
            if (clip == null) return;
            _voice.Stop();
            _voice.clip = clip;
            _voice.pitch = _basePitch + Random.Range(-_pitchVariance, _pitchVariance);
            _voice.volume = vol;
            _voice.Play();
        }
    }
}
