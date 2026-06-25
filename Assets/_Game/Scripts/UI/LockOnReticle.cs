using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Screen-space marker pinned over the locked-on target (reads <see cref="TargetLock"/>). Hidden when nothing
    /// is locked or the target is behind the camera. Lives on a Screen-Space-Overlay canvas.
    /// </summary>
    public sealed class LockOnReticle : MonoBehaviour
    {
        [Tooltip("The reticle graphic to move over the target.")]
        [SerializeField] private RectTransform reticle;

        private Camera _cam;

        private void Start()
        {
            _cam = Camera.main;
            if (reticle != null) reticle.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (reticle == null) return;
            if (_cam == null) _cam = Camera.main;

            TargetLock tl = TargetLock.Instance;
            bool show = tl != null && tl.HasTarget && _cam != null;
            if (show)
            {
                Vector3 sp = _cam.WorldToScreenPoint(tl.TargetPosition);
                if (sp.z <= 0f) show = false;            // target is behind the camera
                else reticle.position = sp;
            }
            if (reticle.gameObject.activeSelf != show) reticle.gameObject.SetActive(show);
        }
    }
}
