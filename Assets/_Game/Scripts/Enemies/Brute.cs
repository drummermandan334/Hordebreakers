using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled elite. Slow seek; when the player is close it commits to a telegraphed slam
    /// (ground decal during the windup = the dodge window), then deals AoE damage at that spot.
    /// High HP and markable, so the reward loop is: dodge the slam, then detonate it.
    /// </summary>
    [RequireComponent(typeof(Markable), typeof(CapsuleCollider))]
    public class Brute : MonoBehaviour, IDamageable
    {
        [SerializeField] private EliteData data;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject telegraphPrefab;
        [SerializeField] private HitFlash hitFlash;

        private Markable _mark;
        private CapsuleCollider _collider;
        private Transform _player;
        private IDamageable _playerDmg;
        private Action<Brute> _return;

        private float _hp;
        private bool _active;
        private bool _dying;
        private float _deathTimer;
        private float _cdTimer;
        private bool _slamming;
        private float _slamTimer;
        private bool _slamHitPending;
        private Vector3 _slamPoint;
        private GameObject _telegraph;
        private float _staggerTimer;
        private int _separationMask;
        private static readonly Collider[] _sepHits = new Collider[16];

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");

        public bool IsAlive => _active && _hp > 0f;

        private void Awake()
        {
            _mark = GetComponent<Markable>();
            _collider = GetComponent<CapsuleCollider>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
        }

        public void Init(Transform player, Action<Brute> ret)
        {
            _player = player;
            _playerDmg = player != null ? player.GetComponent<IDamageable>() : null;
            _return = ret;
            _hp = data.maxHp;
            _active = true;
            _dying = false;
            _slamming = false;
            _slamHitPending = false;
            _cdTimer = 1.5f;
            _staggerTimer = 0f;
            _separationMask = (1 << gameObject.layer) | (player != null ? (1 << player.gameObject.layer) : 0);
            if (_collider != null) _collider.enabled = true;
            _mark.Configure(data.markMaxStacks, data.markDecayTime);
            _mark.ClearMarks();
            if (animator != null) { animator.Rebind(); animator.Update(0f); }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_dying)
            {
                _deathTimer -= dt;
                if (_deathTimer <= 0f) { _dying = false; _return?.Invoke(this); }
                return;
            }
            if (!_active || _player == null) return;

            if (_slamming) { TickSlam(dt); return; }

            transform.position += Separation() * (data.separationForce * dt);   // crowd separation
            if (_cdTimer > 0f) _cdTimer -= dt;
            if (_staggerTimer > 0f) { _staggerTimer -= dt; if (animator != null) animator.SetFloat(AnimSpeed, 0f, 0.1f, dt); return; }

            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;

            if (dist <= data.slamRange && _cdTimer <= 0f) { StartSlam(); return; }

            if (dist > 0.05f)
            {
                Vector3 dir = to / dist;
                transform.position += dir * data.moveSpeed * dt;
                modelRoot.rotation = Quaternion.LookRotation(dir);
            }
            if (animator != null) animator.SetFloat(AnimSpeed, 1f, 0.15f, dt);
        }

        private void StartSlam()
        {
            _slamming = true;
            _slamTimer = data.slamWindup + data.slamRecovery;
            _slamHitPending = true;
            _slamPoint = _player.position;

            if (animator != null) { animator.SetFloat(AnimSpeed, 0f); animator.SetTrigger(AnimAttack); }
            Vector3 d = _slamPoint - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(d);

            if (telegraphPrefab != null)
            {
                _telegraph = Instantiate(telegraphPrefab, _slamPoint + Vector3.up * 0.02f, Quaternion.identity);
                _telegraph.transform.localScale = new Vector3(data.slamRadius * 2f, 0.05f, data.slamRadius * 2f);
            }
        }

        private void TickSlam(float dt)
        {
            _slamTimer -= dt;
            float since = (data.slamWindup + data.slamRecovery) - _slamTimer;

            if (_slamHitPending && since >= data.slamWindup)
            {
                _slamHitPending = false;
                if (_telegraph != null) Destroy(_telegraph);

                if (_player != null && _playerDmg != null && _playerDmg.IsAlive)
                {
                    Vector3 d = _player.position - _slamPoint; d.y = 0f;
                    if (d.magnitude <= data.slamRadius) _playerDmg.TakeDamage(data.slamDamage);
                }
                if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(0.25f, 0.3f);
            }

            if (_slamTimer <= 0f)
            {
                _slamming = false;
                _cdTimer = data.slamCooldown;
                if (_telegraph != null) Destroy(_telegraph);
            }
        }

        /// <summary>Push-away from nearby enemies + the player so the crowd spaces out (kinematic).</summary>
        private Vector3 Separation()
        {
            float r = data.separationRadius;
            if (r <= 0f) return Vector3.zero;
            Vector3 push = Vector3.zero;
            int n = Physics.OverlapSphereNonAlloc(transform.position, r, _sepHits, _separationMask);
            for (int i = 0; i < n; i++)
            {
                Transform o = _sepHits[i].transform;
                if (o == transform) continue;
                Vector3 away = transform.position - o.position; away.y = 0f;
                float d = away.magnitude;
                if (d > 0.0001f) push += away / d * (1f - Mathf.Min(d, r) / r);
                else push += new Vector3(UnityEngine.Random.value - 0.5f, 0f, UnityEngine.Random.value - 0.5f);
            }
            return push;
        }

        public void TakeDamage(float amount)
        {
            if (!_active) return;
            _hp -= amount;
            if (hitFlash != null) hitFlash.Flash();
            if (_hp <= 0f) { Die(); return; }
            if (!_slamming && animator != null)   // reacts to every hit, but has poise mid-slam
            {
                animator.SetTrigger(AnimHit);
                _staggerTimer = data.hitReactTime;
            }
        }

        private void Die()
        {
            _active = false;
            _dying = true;
            _deathTimer = data != null ? data.deathDuration : 1.6f;
            if (_collider != null) _collider.enabled = false;
            if (_telegraph != null) Destroy(_telegraph);
            if (animator != null) animator.SetTrigger(AnimDead);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKill();
                GameManager.Instance.DropGem(transform.position, data != null ? data.xpValue : 5);
            }
        }
    }
}
