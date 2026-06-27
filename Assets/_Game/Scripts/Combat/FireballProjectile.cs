using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Damage + cleanup for the self-driving fireball FX (FX_Fireball_Shooting_Straight_01). The FX does all the
    /// visuals itself — the body shoots forward, Birth sub-emitters make the trail/streak, and a Collision
    /// sub-emitter detonates the explosion wherever the particle hits. This only deals game damage when the
    /// particle strikes an enemy (via the same particle collision) and self-destructs after the effect plays out.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FireballProjectile : MonoBehaviour
    {
        private float _damage;
        private bool _spent;

        public void Init(float damage, float autoDestroy)
        {
            _damage = damage;
            _spent = false;
            Destroy(gameObject, autoDestroy);
        }

        private void OnParticleCollision(GameObject other)
        {
            if (_spent) return;   // one bolt, one hit (the body also dies on collision)
            IDamageable d = other.GetComponentInParent<IDamageable>();
            if (d != null && d.IsAlive)
            {
                _spent = true;
                CombatAudio.PlayFireImpact(transform.position);   // explosion, distinct from melee hits (clips live on CombatAudio)
                d.TakeDamage(_damage, transform.position);
            }
        }
    }
}
