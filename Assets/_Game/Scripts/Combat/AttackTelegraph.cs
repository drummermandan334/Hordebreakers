using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// A wind-up "I'm about to hit you" tell that reads at the shallow musou camera (where ground decals
    /// foreshorten): the whole enemy throb-glows via emissive over the wind-up, crescendoing as the strike
    /// nears. Shader-agnostic MaterialPropertyBlock (no material instances), pooled-safe. Runs before
    /// <see cref="HitFlash"/> so a hit-flash visibly overrides the glow when both fire the same frame.
    /// Call <see cref="Begin"/> when a telegraphed attack winds up; <see cref="Cancel"/> if it's interrupted.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class AttackTelegraph : MonoBehaviour
    {
        [SerializeField] private Color telegraphColor = new Color(1f, 0.45f, 0.1f);   // orange warning
        [SerializeField] private float emissionIntensity = 2.6f;
        [Tooltip("Throb speed (pulses per second).")]
        [SerializeField] private float pulseHz = 5f;
        [Tooltip("Subtle scale swell at the pulse peak (0 = none).")]
        [SerializeField] private float scalePulse = 0.05f;
        [Tooltip("Transform to scale-pulse. Defaults to this transform.")]
        [SerializeField] private Transform pulseTarget;

        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Vector3 _baseScale;
        private float _timer;
        private float _duration;
        private float _phase;
        private bool _active;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            if (pulseTarget == null) pulseTarget = transform;
            _baseScale = pulseTarget.localScale;
        }

        /// <summary>Start telegraphing for <paramref name="duration"/> seconds (match the attack's wind-up).</summary>
        public void Begin(float duration)
        {
            _timer = duration;
            _duration = Mathf.Max(0.0001f, duration);
            _phase = 0f;
            _active = true;
        }

        /// <summary>Stop early (e.g. the wind-up was interrupted) and restore the look.</summary>
        public void Cancel()
        {
            if (!_active) return;
            _active = false;
            Restore();
        }

        private void LateUpdate()
        {
            if (!_active) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();   // non-serializable: a domain reload mid-play nulls it without re-running Awake
            float dt = Time.deltaTime;
            _timer -= dt;
            _phase += dt * pulseHz * (Mathf.PI * 2f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(_phase);                 // 0..1 throb
            float crescendo = 1f - Mathf.Clamp01(_timer / _duration);      // ramps 0 -> 1 as the strike nears
            float k = pulse * (0.45f + 0.55f * crescendo);                 // brighter the closer the hit

            Color e = telegraphColor * (emissionIntensity * k);
            Color tint = Color.Lerp(Color.white, telegraphColor, k * 0.6f);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(Emission, e);
                _mpb.SetColor(BaseColor, tint);
                r.SetPropertyBlock(_mpb);
            }
            if (scalePulse > 0f) pulseTarget.localScale = _baseScale * (1f + scalePulse * k);

            if (_timer <= 0f) { _active = false; Restore(); }
        }

        private void Restore()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();   // guard the same domain-reload null as LateUpdate
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(Emission, Color.black);
                _mpb.SetColor(BaseColor, Color.white);
                r.SetPropertyBlock(_mpb);
            }
            pulseTarget.localScale = _baseScale;
        }
    }
}
