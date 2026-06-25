using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Tiny pooled SFX player for combat one-shots, with the clips exposed HERE so they're easy to swap/A-B in
    /// one place (drop several into an array → random variety; per-sound volume). Static facades
    /// (<see cref="PlaySwing"/>/<see cref="PlayHit"/>) so callers fire sounds without a reference (mirrors
    /// <see cref="PlayerCameraRig.Shake"/>).
    ///
    /// Voices use clip+<c>Play</c> (NOT PlayOneShot) so pitch never bleeds across overlapping one-shots. The SWING
    /// has its OWN voice that restarts each swing — so mashing the combo gives one clean whoosh per swing instead of
    /// stacked/phase-cancelling whooshes. Hits use a small round-robin pool so simultaneous impacts can layer.
    /// 2D by default (combat is player-centric).
    /// </summary>
    public class CombatAudio : MonoBehaviour
    {
        public static CombatAudio Instance { get; private set; }

        [Header("Voices")]
        [Tooltip("Pooled voices for hits (lets simultaneous impacts layer).")]
        [SerializeField] private int hitVoices = 6;
        [Range(0f, 0.5f)]
        [SerializeField] private float defaultPitchVariance = 0.08f;

        [Header("Swing — whoosh on each attack (drop in clips to A/B; multiple = random variety)")]
        [SerializeField] private AudioClip[] swingClips;
        [Range(0f, 1f)]
        [SerializeField] private float swingVolume = 1f;

        [Header("Hit — impact on connect")]
        [SerializeField] private AudioClip[] hitClips;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 1f;

        private AudioSource[] _hitPool;
        private int _next;
        private AudioSource _swingVoice;   // dedicated so each swing cuts the previous whoosh (no mash overlap)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _hitPool = new AudioSource[Mathf.Max(1, hitVoices)];
            for (int i = 0; i < _hitPool.Length; i++) _hitPool[i] = NewVoice("SfxHit" + i);
            _swingVoice = NewVoice("SfxSwing");
        }

        private AudioSource NewVoice(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;   // 2D
            return a;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Play a swing whoosh. Restarts the dedicated swing voice, so mashing yields one clean whoosh per swing.</summary>
        public static void PlaySwing(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.swingClips);
            if (clip == null) return;
            Instance.PlayOn(Instance._swingVoice, clip, position, Instance.swingVolume, Instance.defaultPitchVariance);
        }

        /// <summary>Play a hit impact (round-robin pool so simultaneous hits layer).</summary>
        public static void PlayHit(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.hitClips);
            if (clip == null) return;
            Instance.PlayPooled(clip, position, Instance.hitVolume, Instance.defaultPitchVariance);
        }

        /// <summary>Generic one-shot for any other caller (explicit clip, round-robin pool).</summary>
        public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = -1f)
        {
            if (Instance == null || clip == null) return;
            Instance.PlayPooled(clip, position, volume, pitchVariance < 0f ? Instance.defaultPitchVariance : pitchVariance);
        }

        private AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
        }

        private void PlayPooled(AudioClip clip, Vector3 position, float volume, float pitchVariance)
        {
            AudioSource a = _hitPool[_next];
            _next = (_next + 1) % _hitPool.Length;
            PlayOn(a, clip, position, volume, pitchVariance);
        }

        private void PlayOn(AudioSource a, AudioClip clip, Vector3 position, float volume, float pitchVariance)
        {
            a.Stop();
            a.transform.position = position;
            a.clip = clip;
            a.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            a.volume = volume;
            a.Play();
        }
    }
}
