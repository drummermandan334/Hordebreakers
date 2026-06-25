using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled projectile. Moves via a per-frame SphereCast (robust at speed, no Rigidbody).
    /// On hit: applies damage, then returns itself to the pool.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Sphere radius of the per-frame hit cast (how 'fat' the projectile reads against colliders).")]
        [SerializeField] private float castRadius = 0.25f;

        private Vector3 _dir;
        private float _damage;
        private float _speed;
        private float _life;
        private LayerMask _mask;
        private Action<Projectile> _onComplete;
        private bool _active;

        public void Init(Vector3 dir, float damage, float speed, float life,
                         LayerMask mask, Action<Projectile> onComplete)
        {
            _dir = dir; _damage = damage; _speed = speed;
            _life = life; _mask = mask; _onComplete = onComplete; _active = true;
        }

        private void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            float step = _speed * dt;

            if (Physics.SphereCast(transform.position, castRadius, _dir, out RaycastHit hit, step, _mask,
                                   QueryTriggerInteraction.Ignore))
            {
                Collider col = hit.collider;
                if (col.TryGetComponent(out IDamageable d) && d.IsAlive) d.TakeDamage(_damage, transform.position);
                Done();
                return;
            }

            transform.position += _dir * step;
            _life -= dt;
            if (_life <= 0f) Done();
        }

        private void Done()
        {
            _active = false;
            _onComplete?.Invoke(this);
        }
    }
}
