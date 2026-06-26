using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// A meteor: a big fireball FX falling from the sky onto a strike point. On impact — whichever comes first, the
    /// fireball's own particle collision or a fallback timer — it deals AoE damage at the strike point, spawns a
    /// fire explosion, and shakes the camera. Spawned by <see cref="MeteorEffect"/> (the sorcerer's Cataclysm).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class Meteor : MonoBehaviour
    {
        private PlayerController _player;
        private Vector3 _strike;
        private float _radius, _damage, _timer;
        private GameObject _impactVfx;
        private float _impactScale;
        private Vector2 _shake;
        private bool _struck;

        public void Init(PlayerController player, Vector3 strike, float radius, float damage,
                         GameObject impactVfx, float impactScale, Vector2 shake, float strikeDelay, float autoDestroy)
        {
            _player = player; _strike = strike; _radius = radius; _damage = damage;
            _impactVfx = impactVfx; _impactScale = impactScale; _shake = shake;
            _timer = strikeDelay; _struck = false;
            Destroy(gameObject, autoDestroy);
        }

        private void Update()
        {
            if (_struck) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Strike();   // fallback if the particle never collides (e.g. no ground collider)
        }

        private void OnParticleCollision(GameObject other) => Strike();

        private void Strike()
        {
            if (_struck) return;
            _struck = true;
            if (_player != null) _player.DealAreaDamage(_strike, _radius, _damage);
            if (_impactVfx != null)
            {
                GameObject go = Instantiate(_impactVfx, _strike, Quaternion.identity);
                if (!Mathf.Approximately(_impactScale, 1f)) go.transform.localScale *= _impactScale;
                Destroy(go, 3f);
            }
            PlayerCameraRig.Shake(_shake.x, _shake.y);
        }
    }
}
