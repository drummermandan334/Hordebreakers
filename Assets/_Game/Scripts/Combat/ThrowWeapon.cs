using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The player's thrown weapon — now the battle-sorcerer's magical bolt. Reuses the pooled
    /// <see cref="Projectile"/>. <see cref="Fire"/> is driven by PlayerController's cast (the mirrored-L3
    /// animation), spawning from the left hand toward an aim direction, snapping to a forward-cone enemy for a
    /// little aim assist. The cooldown lives on the player's cast gate; this just spawns.
    /// </summary>
    public class ThrowWeapon : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private Projectile projectilePrefab;   // dagger prefab with a Projectile component
        [SerializeField] private LayerMask enemyMask;
        [Tooltip("Facing source for the throw direction (the player's ModelRoot).")]
        [SerializeField] private Transform aimRoot;

        [Header("Tuning")]
        [SerializeField] private float damage = 14f;
        [SerializeField] private float speed = 22f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float cooldown = 0.55f;
        [Tooltip("Snap the throw to the nearest enemy within this range and a forward cone.")]
        [SerializeField] private float aimAssistRange = 14f;
        [Tooltip("Aim-assist cone width: enemies whose direction·facing is below this are ignored (higher = narrower cone).")]
        [SerializeField] private float aimAssistConeDot = 0.4f;
        [Tooltip("Vertical aim is clamped to +/- this so throws stay roughly level.")]
        [SerializeField] private float aimVerticalClamp = 2f;
        [SerializeField] private int poolSize = 16;

        private ObjectPool<Projectile> _pool;
        private Action<Projectile> _return;
        private readonly Collider[] _hits = new Collider[64];

        // ---------- Upgrade hooks (driven by ThrowWeaponBuffEffect) ----------
        public float Damage { get => damage; set => damage = Mathf.Max(0f, value); }
        public float ProjectileSpeed { get => speed; set => speed = Mathf.Max(0f, value); }
        public float Cooldown { get => cooldown; set => cooldown = Mathf.Max(0.05f, value); }

        private void Awake()
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[ThrowWeapon] Assign a dagger projectile prefab.", this);
                enabled = false;
                return;
            }
            _pool = new ObjectPool<Projectile>(projectilePrefab, poolSize);
            _return = _pool.Return;
            if (aimRoot == null) aimRoot = transform;
        }

        /// <summary>Spawn a bolt from <paramref name="origin"/> toward <paramref name="dir"/> (snaps to a forward-cone enemy). Driven by the player's cast.</summary>
        public void Fire(Vector3 origin, Vector3 dir)
        {
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

            Projectile p = _pool.Get();
            p.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            p.Init(dir, damage, speed, lifetime, enemyMask, _return);
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
