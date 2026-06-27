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
        [Tooltip("Pooled voices for spells/explosions, so a big boom isn't stolen by a flurry of hit sounds.")]
        [SerializeField] private int spellVoices = 3;
        [Range(0f, 0.5f)]
        [SerializeField] private float defaultPitchVariance = 0.08f;

        [Header("Swing — whoosh on each attack (drop in clips to A/B; multiple = random variety)")]
        [SerializeField] private AudioClip[] swingClips;
        [Tooltip("Heavier whoosh for heavy swings — falls back to the light swing clips if left empty.")]
        [SerializeField] private AudioClip[] heavySwingClips;
        [Range(0f, 1f)]
        [SerializeField] private float swingVolume = 1f;

        [Header("Hit — melee impact on connect (sword hits)")]
        [SerializeField] private AudioClip[] hitClips;
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 1f;

        [Header("Fireball — cast whoosh (the bolt leaving the hand)")]
        [SerializeField] private AudioClip[] fireballCastClips;
        [Range(0f, 1f)]
        [SerializeField] private float fireballCastVolume = 1f;
        [Tooltip("Seconds of dead air skipped at the START of the cast clip, so the whoosh lands ON the cast animation instead of arriving late. Raise until it syncs.")]
        [SerializeField] private float fireballCastStartOffset = 0f;

        [Header("Fire impact — the fireball's explosion on hit (separate from melee hits)")]
        [SerializeField] private AudioClip[] fireImpactClips;
        [Range(0f, 1f)]
        [SerializeField] private float fireImpactVolume = 1f;

        [Header("Musou — screen-clear ultimate boom")]
        [SerializeField] private AudioClip[] musouClips;
        [Range(0f, 1f)]
        [SerializeField] private float musouVolume = 1f;

        [Header("Dodge — roll whoosh (NOT a footstep)")]
        [SerializeField] private AudioClip[] dodgeClips;
        [Range(0f, 1f)]
        [SerializeField] private float dodgeVolume = 1f;

        private AudioSource[] _hitPool;
        private int _next;
        private AudioSource[] _spellPool;
        private int _spellNext;
        private AudioSource _swingVoice;   // dedicated so each swing cuts the previous whoosh (no mash overlap)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _hitPool = new AudioSource[Mathf.Max(1, hitVoices)];
            for (int i = 0; i < _hitPool.Length; i++) _hitPool[i] = NewVoice("SfxHit" + i);
            _spellPool = new AudioSource[Mathf.Max(1, spellVoices)];
            for (int i = 0; i < _spellPool.Length; i++) _spellPool[i] = NewVoice("SfxSpell" + i);
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

        /// <summary>Play a swing whoosh. Restarts the dedicated swing voice, so mashing yields one clean whoosh per swing.
        /// Heavy swings draw from the heavier whoosh set (falling back to the light set when none is assigned).</summary>
        public static void PlaySwing(Vector3 position, bool heavy = false)
        {
            if (Instance == null) return;
            AudioClip[] set = heavy && Instance.heavySwingClips != null && Instance.heavySwingClips.Length > 0
                ? Instance.heavySwingClips
                : Instance.swingClips;
            AudioClip clip = Instance.Pick(set);
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

        /// <summary>Play a spell / explosion one-shot on the dedicated spell pool (won't be cut by hit sounds).</summary>
        public static void PlaySpell(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = -1f)
        {
            if (Instance == null || clip == null) return;
            Instance.PlaySpellVoice(clip, position, volume, pitchVariance < 0f ? Instance.defaultPitchVariance : pitchVariance, 0f);
        }

        /// <summary>Fireball cast whoosh — clips + lead-in trim live on CombatAudio so the bolt's launch sound stays in sync.</summary>
        public static void PlayFireballCast(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.fireballCastClips);
            if (clip == null) return;
            Instance.PlaySpellVoice(clip, position, Instance.fireballCastVolume, Instance.defaultPitchVariance, Instance.fireballCastStartOffset);
        }

        /// <summary>Fireball impact explosion — its own editable clip set, distinct from the melee hit sounds.</summary>
        public static void PlayFireImpact(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.fireImpactClips);
            if (clip == null) return;
            Instance.PlaySpellVoice(clip, position, Instance.fireImpactVolume, Instance.defaultPitchVariance, 0f);
        }

        /// <summary>Musou ultimate boom.</summary>
        public static void PlayMusou(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.musouClips);
            if (clip == null) return;
            Instance.PlaySpellVoice(clip, position, Instance.musouVolume, Instance.defaultPitchVariance, 0f);
        }

        /// <summary>Dodge-roll whoosh — its own clip set (a footstep here would make no sense).</summary>
        public static void PlayDodge(Vector3 position)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.Pick(Instance.dodgeClips);
            if (clip == null) return;
            Instance.PlaySpellVoice(clip, position, Instance.dodgeVolume, Instance.defaultPitchVariance, 0f);
        }

        private void PlaySpellVoice(AudioClip clip, Vector3 position, float volume, float pitchVariance, float startTime)
        {
            AudioSource a = _spellPool[_spellNext];
            _spellNext = (_spellNext + 1) % _spellPool.Length;
            PlayOn(a, clip, position, volume, pitchVariance, startTime);
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

        private void PlayOn(AudioSource a, AudioClip clip, Vector3 position, float volume, float pitchVariance, float startTime = 0f)
        {
            a.Stop();
            a.transform.position = position;
            a.clip = clip;
            a.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            a.volume = volume;
            a.Play();
            if (startTime > 0f && startTime < clip.length) a.time = startTime;   // skip dead air at the head so the sound lands on-beat
        }
    }
}
