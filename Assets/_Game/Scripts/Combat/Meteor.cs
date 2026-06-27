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
        [Header("Lingering fire (dissipates after the strike)")]
        [Tooltip("Fire patches left at the impact (e.g. FX_Fire_Big_02 / _03). A random one is picked per patch.")]
        [SerializeField] private GameObject[] lingerVfx;
        [SerializeField] private int lingerCount = 4;
        [SerializeField] private float lingerRadius = 5f;
        [SerializeField] private float lingerScale = 1.5f;
        [SerializeField] private float lingerLifetime = 6f;

        private PlayerController _player;
        private Vector3 _strike;
        private float _radius, _damage, _timer;
        private GameObject _impactVfx;
        private float _impactScale;
        private Vector2 _shake;
        private AudioClip _impactClip;
        private float _impactVolume;
        private bool _struck;

        public void Init(PlayerController player, Vector3 strike, float radius, float damage,
                         GameObject impactVfx, float impactScale, Vector2 shake, float strikeDelay, float autoDestroy,
                         AudioClip impactClip = null, float impactVolume = 1f)
        {
            _player = player; _strike = strike; _radius = radius; _damage = damage;
            _impactVfx = impactVfx; _impactScale = impactScale; _shake = shake;
            _impactClip = impactClip; _impactVolume = impactVolume;
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
            if (_impactClip != null) CombatAudio.PlaySpell(_impactClip, _strike, _impactVolume);
            if (_impactVfx != null)
            {
                GameObject go = Instantiate(_impactVfx, _strike, Quaternion.identity);
                if (!Mathf.Approximately(_impactScale, 1f)) go.transform.localScale *= _impactScale;
                Destroy(go, 3f);
            }
            SpawnLingeringFires();
            PlayerCameraRig.Shake(_shake.x, _shake.y);
        }

        // Scatter a few looping fire patches around the impact that play out and fade (don't loop forever).
        private void SpawnLingeringFires()
        {
            if (lingerVfx == null || lingerVfx.Length == 0) return;
            for (int i = 0; i < lingerCount; i++)
            {
                GameObject prefab = lingerVfx[Random.Range(0, lingerVfx.Length)];
                if (prefab == null) continue;
                Vector2 off = Random.insideUnitCircle * lingerRadius;
                GameObject fire = Instantiate(prefab, _strike + new Vector3(off.x, 0f, off.y), Quaternion.identity);
                if (!Mathf.Approximately(lingerScale, 1f)) fire.transform.localScale *= lingerScale;
                foreach (ParticleSystem ps in fire.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule m = ps.main;
                    m.loop = false;   // play out + fade rather than burning forever
                }
                Destroy(fire, lingerLifetime);
            }
        }
    }
}
