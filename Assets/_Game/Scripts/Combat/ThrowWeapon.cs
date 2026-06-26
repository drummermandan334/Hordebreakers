using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The battle-sorcerer's magical bolt. Spawns the self-driving fireball FX — which handles flight, trail, and
    /// the collision-triggered explosion itself — aimed at the player's facing, snapping to a forward-cone enemy
    /// for a little aim assist. Driven by PlayerController's cast (the mirrored-L3 animation); damage is dealt by
    /// the fireball's <see cref="FireballProjectile"/> on particle impact.
    /// </summary>
    public class ThrowWeapon : MonoBehaviour
    {
        [Header("Bolt")]
        [Tooltip("The fireball FX prefab (FireballProjectile + the self-driving particle systems).")]
        [SerializeField] private GameObject fireballPrefab;
        [SerializeField] private LayerMask enemyMask;

        [Header("Tuning")]
        [SerializeField] private float damage = 14f;
        [SerializeField] private float speed = 15f;     // vestigial upgrade hook; the FX drives its own flight speed
        [SerializeField] private float cooldown = 0.55f;
        [Tooltip("Auto-destroy the spawned fireball after this long (covers flight + explosion).")]
        [SerializeField] private float fxLifetime = 4f;
        [Tooltip("Snap the throw to the nearest enemy within this range and a forward cone.")]
        [SerializeField] private float aimAssistRange = 14f;
        [Tooltip("Aim-assist cone: enemies whose direction·facing is below this are ignored (higher = narrower).")]
        [SerializeField] private float aimAssistConeDot = 0.4f;
        [Tooltip("Vertical aim is clamped to +/- this so throws stay roughly level.")]
        [SerializeField] private float aimVerticalClamp = 2f;

        private readonly Collider[] _hits = new Collider[64];

        // ---------- Upgrade hooks (driven by WeaponModEffect) ----------
        public float Damage { get => damage; set => damage = Mathf.Max(0f, value); }
        public float ProjectileSpeed { get => speed; set => speed = Mathf.Max(0f, value); }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.05f, value); }

        private void Awake()
        {
            if (fireballPrefab == null)
            {
                Debug.LogError("[ThrowWeapon] Assign the fireball FX prefab.", this);
                enabled = false;
            }
        }

        /// <summary>Spawn the fireball from <paramref name="origin"/> toward <paramref name="dir"/> (snaps to a forward-cone enemy). Driven by the player's cast.</summary>
        public void Fire(Vector3 origin, Vector3 dir)
        {
            if (fireballPrefab == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            Transform target = FindForwardTarget(origin, dir);
            if (target != null)
            {
                Vector3 to = (target.position + Vector3.up) - origin;
                to.y = Mathf.Clamp(to.y, -aimVerticalClamp, aimVerticalClamp);
                if (to.sqrMagnitude > 0.001f) dir = to.normalized;
            }

            GameObject go = Instantiate(fireballPrefab, origin, Quaternion.LookRotation(dir));
            if (go.TryGetComponent(out FireballProjectile fp)) fp.Init(damage, fxLifetime);
        }

        private Transform FindForwardTarget(Vector3 origin, Vector3 fwd)
        {
            int n = Physics.OverlapSphereNonAlloc(origin, aimAssistRange, _hits, enemyMask);
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 to = _hits[i].transform.position - origin; to.y = 0f;
                if (to.sqrMagnitude < 0.01f) continue;
                if (Vector3.Dot(to.normalized, fwd) < aimAssistConeDot) continue;   // forward cone only
                if (to.sqrMagnitude < bestSq) { bestSq = to.sqrMagnitude; best = _hits[i].transform; }
            }
            return best;
        }
    }
}
