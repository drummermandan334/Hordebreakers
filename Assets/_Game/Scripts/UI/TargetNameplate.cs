using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Hordebreakers
{
    /// <summary>Read by the target nameplate — the unit's display name + current/max health.</summary>
    public interface ITargetInfo
    {
        string TargetName { get; }
        float TargetHp { get; }
        float TargetHpMax { get; }
    }

    /// <summary>
    /// HUD nameplate for the unit the player is focused on: the lock-on target (<see cref="TargetLock"/>) if any,
    /// otherwise the most recently STRUCK enemy (lingers a few seconds after the last hit, so it reads while you
    /// swing at an un-locked foe). Shows the unit's name + a health bar; fades out when there's nothing to show.
    /// </summary>
    public sealed class TargetNameplate : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image healthFill;   // Filled/Horizontal image; fillAmount = HP fraction
        [Tooltip("Seconds the plate lingers on the last-hit enemy (when not locked on) after the last hit.")]
        [SerializeField] private float lingerSeconds = 4f;
        [Tooltip("How fast the HP bar eases toward the true value.")]
        [SerializeField] private float barLerp = 14f;
        [Tooltip("Show/hide fade speed.")]
        [SerializeField] private float fadeSpeed = 10f;

        private static TargetNameplate _instance;
        private Component _lastHit;     // most recently struck enemy (implements ITargetInfo + IDamageable)
        private float _lastHitTime;
        private float _shownFill;

        private void Awake() { _instance = this; if (group != null) group.alpha = 0f; }
        private void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>An enemy calls this when the player damages it — it becomes the fallback nameplate target.</summary>
        public static void ReportHit(Component enemy)
        {
            if (_instance == null || enemy == null) return;
            _instance._lastHit = enemy;
            _instance._lastHitTime = Time.time;
        }

        private void Update()
        {
            ITargetInfo info = ResolveTarget();
            float wantAlpha = info != null ? 1f : 0f;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, wantAlpha, Time.deltaTime * fadeSpeed);
            if (info == null) return;

            if (nameLabel != null) nameLabel.text = info.TargetName;
            float f = info.TargetHpMax > 0f ? Mathf.Clamp01(info.TargetHp / info.TargetHpMax) : 0f;
            _shownFill = Mathf.MoveTowards(_shownFill, f, Time.deltaTime * barLerp);
            if (healthFill != null) healthFill.fillAmount = _shownFill;
        }

        /// <summary>Lock-on target wins; else the last-hit enemy while it's alive and within the linger window.</summary>
        private ITargetInfo ResolveTarget()
        {
            TargetLock tl = TargetLock.Instance;
            if (tl != null && tl.HasTarget && tl.Target != null)
            {
                ITargetInfo locked = tl.Target.GetComponentInParent<ITargetInfo>();
                if (locked != null) return locked;
            }
            if (_lastHit != null && Time.time - _lastHitTime <= lingerSeconds
                && _lastHit is IDamageable dmg && dmg.IsAlive && _lastHit is ITargetInfo info)
            {
                return info;
            }
            return null;
        }
    }
}
