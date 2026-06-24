using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Cheap, shader-agnostic hit feedback: on Flash() the renderers pulse an emissive tint via a
    /// MaterialPropertyBlock (no material instances) and the visual gives a small scale "punch" so the
    /// hit reads even on materials without emission. Pooled-object safe (no allocations after Awake).
    /// Put this on enemies and the player; call Flash() from TakeDamage.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private float flashDuration = 0.09f;
        [SerializeField] private float emissionIntensity = 3.5f;
        [Tooltip("Extra scale at the peak of the flash (0 = none).")]
        [SerializeField] private float scalePunch = 0.12f;
        [Tooltip("Transform to scale-punch. Defaults to this transform.")]
        [SerializeField] private Transform punchTarget;

        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Vector3 _baseScale;
        private float _t;
        private bool _active;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            if (punchTarget == null) punchTarget = transform;
            _baseScale = punchTarget.localScale;
        }

        public void Flash()
        {
            _t = flashDuration;
            _active = true;
        }

        private void LateUpdate()
        {
            if (!_active) return;
            _t -= Time.deltaTime;
            float k = Mathf.Clamp01(_t / flashDuration);     // 1 -> 0 over the flash

            Color e = flashColor * (emissionIntensity * k);
            Color tint = Color.Lerp(Color.white, flashColor, k);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(Emission, e);
                _mpb.SetColor(BaseColor, tint);
                r.SetPropertyBlock(_mpb);
            }
            if (scalePunch > 0f) punchTarget.localScale = _baseScale * (1f + scalePunch * k);

            if (_t <= 0f)
            {
                // restore
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer r = _renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(Emission, Color.black);
                    _mpb.SetColor(BaseColor, Color.white);
                    r.SetPropertyBlock(_mpb);
                }
                punchTarget.localScale = _baseScale;
                _active = false;
            }
        }
    }
}
