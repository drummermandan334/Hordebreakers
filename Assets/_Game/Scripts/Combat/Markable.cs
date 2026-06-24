using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Holds stacking Marks applied by auto-weapons and consumed by the heavy-attack detonation.
    /// Optional emissive glow scales with stacks via MaterialPropertyBlock (batching-friendly).
    /// </summary>
    public class Markable : MonoBehaviour
    {
        [SerializeField] private Renderer markRenderer;          // optional; emissive-capable material
        [SerializeField] private Color markColor = new Color(1f, 0.5f, 0f);
        [Tooltip("Emissive glow intensity at full stacks (markColor is multiplied by this * stackFraction).")]
        [SerializeField] private float emissionIntensity = 2f;

        private int _stacks;
        private int _max = 10;
        private float _decay = 4f;
        private float _lastMarkTime;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public int Stacks => _stacks;

        public void Configure(int max, float decay)
        {
            _max = Mathf.Max(1, max);
            _decay = decay;
        }

        public void AddMark(int stacks)
        {
            _stacks = Mathf.Min(_stacks + stacks, _max);
            _lastMarkTime = Time.time;
            UpdateVisual();
        }

        public int ConsumeAll()
        {
            int s = _stacks;
            _stacks = 0;
            UpdateVisual();
            return s;
        }

        public void ClearMarks()
        {
            _stacks = 0;
            UpdateVisual();
        }

        private void Update()
        {
            if (_stacks > 0 && Time.time - _lastMarkTime > _decay)
            {
                _stacks = 0;
                UpdateVisual();
            }
        }

        private void UpdateVisual()
        {
            if (markRenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            markRenderer.GetPropertyBlock(_mpb);
            float t = _max > 0 ? (float)_stacks / _max : 0f;
            _mpb.SetColor(EmissionId, markColor * (t * emissionIntensity));
            markRenderer.SetPropertyBlock(_mpb);
        }
    }
}
