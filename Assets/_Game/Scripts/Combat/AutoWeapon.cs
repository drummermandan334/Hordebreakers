using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// VS-style auto-weapon: fires on a timer at the nearest enemy in range.
    /// Each shot applies Marks (set up the synergy loop with the player's detonation).
    /// </summary>
    public class AutoWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponData data;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private int poolSize = 32;
        [Header("Tuning")]
        [Tooltip("Retry delay when there is no target in range before checking again.")]
        [SerializeField] private float noTargetRetryInterval = 0.1f;
        [Tooltip("Vertical aim is clamped to +/- this so shots stay roughly level.")]
        [SerializeField] private float aimVerticalClamp = 2f;

        private ObjectPool<Projectile> _pool;
        private Action<Projectile> _returnAction;
        private float _timer;
        private readonly Collider[] _hits = new Collider[64];

        public WeaponData RuntimeData => data;

        private void Awake()
        {
            if (data == null || data.projectilePrefab == null)
            {
                Debug.LogError("[AutoWeapon] Assign WeaponData with a projectile prefab.", this);
                enabled = false;
                return;
            }
            data = Instantiate(data);        // runtime copy so upgrades don't dirty the source asset
            _pool = new ObjectPool<Projectile>(data.projectilePrefab, poolSize);
            _returnAction = _pool.Return;   // cached delegate (no per-fire alloc)
            _timer = data.fireInterval;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            Transform target = FindNearest(data.range);
            if (target == null) { _timer = noTargetRetryInterval; return; }   // nothing in range; retry soon

            Fire(target);
            _timer = data.fireInterval;
        }

        private void Fire(Transform target)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 dir = (target.position + Vector3.up) - origin;
            dir.y = Mathf.Clamp(dir.y, -aimVerticalClamp, aimVerticalClamp);
            dir.Normalize();

            Projectile p = _pool.Get();
            p.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            p.Init(dir, data.damage, data.marksPerHit, data.projectileSpeed,
                   data.projectileLifetime, enemyMask, _returnAction);
        }

        private Transform FindNearest(float range)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, range, _hits, enemyMask);
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float sq = (_hits[i].transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = _hits[i].transform; }
            }
            return best;
        }
    }
}
