using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled swarm enemy with a real attack pattern (not a VS-style follower): it seeks to a standoff
    /// RING around the player (so the crowd spaces out instead of piling on), then commits to a
    /// telegraphed LUNGE — windup (the dodge window) → forward dash strike → recovery → cooldown.
    /// Kinematic (no NavMesh/Rigidbody for the prototype). Flashes on hit; returns to its pool on death.
    /// </summary>
    [RequireComponent(typeof(Markable), typeof(CapsuleCollider))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        private enum State { Seek, Windup, Lunge, Recover }

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private HitFlash hitFlash;

        private EnemyData _data;
        private Markable _mark;
        private CapsuleCollider _collider;
        private Transform _player;
        private IDamageable _playerDmg;
        private Action<Enemy> _returnToPool;

        private float _hp;
        private bool _active;
        private bool _dying;
        private float _deathTimer;

        private State _state;
        private float _stateTimer;
        private float _cooldownTimer;
        private float _slotOffset;     // per-instance ring angle so they don't all stack on one point
        private float _strafeDir;      // +1 / -1 — which way it circles the player while waiting
        private float _aggression;     // 0 = cautious (hangs back, circles), 1 = aggressive (rushes, attacks often)
        private float _standoff;       // per-instance ring distance (from aggression)
        private float _attackCd;       // per-instance attack cooldown (from aggression)
        private Vector3 _lungeDir;
        private Vector3 _knockback;    // hit-recoil impulse
        private float _staggerTimer;   // > 0 while flinching from a hit (movement paused)
        private int _separationMask;   // enemy + player layers, for crowd separation
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

        public void Init(EnemyData data, Transform player, Action<Enemy> returnToPool)
        {
            _data = data;
            _player = player;
            _returnToPool = returnToPool;
            _playerDmg = player != null ? player.GetComponent<IDamageable>() : null;
            _hp = data.maxHp;
            _active = true;
            _dying = false;
            _state = State.Seek;
            _stateTimer = 0f;
            _aggression = UnityEngine.Random.value;
            _standoff = data.attackRange * (1.35f - 0.6f * _aggression);    // aggressive enemies crowd in close
            _attackCd = data.attackCooldown * (1.4f - 0.8f * _aggression);   // aggressive enemies strike more often
            _cooldownTimer = UnityEngine.Random.Range(0.3f, _attackCd);      // desync the crowd
            _slotOffset = UnityEngine.Random.Range(-35f, 35f);
            _strafeDir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
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
                if (_deathTimer <= 0f) { _dying = false; _returnToPool?.Invoke(this); }
                return;
            }
            if (!_active || _player == null) return;

            if (_knockback.sqrMagnitude > 0.0001f)   // hit recoil (weight)
            {
                transform.position += _knockback * dt;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-12f * dt));
            }

            transform.position += Separation() * (_data.separationForce * dt);   // crowd separation: don't pile on / clip through

            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            if (_staggerTimer > 0f) { _staggerTimer -= dt; if (animator != null) animator.SetFloat(AnimSpeed, 0f, 0.1f, dt); return; }

            switch (_state)
            {
                case State.Seek:    TickSeek(dt);    break;
                case State.Windup:  TickWindup(dt);  break;
                case State.Lunge:   TickLunge(dt);   break;
                case State.Recover: TickRecover(dt); break;
            }
        }

        private void TickSeek(float dt)
        {
            Vector3 fromPlayer = transform.position - _player.position; fromPlayer.y = 0f;
            float dist = fromPlayer.magnitude;
            Vector3 dirFromPlayer = dist > 0.001f ? fromPlayer / dist : modelRoot.forward;

            FacePlayer();

            if (dist > _standoff + 0.4f)
            {
                // approach the standoff ring, nudged to this enemy's slot so the crowd fans out
                Vector3 ringDir = Quaternion.Euler(0f, _slotOffset, 0f) * dirFromPlayer;
                Vector3 ringTarget = _player.position + ringDir * _standoff;
                Vector3 toTarget = ringTarget - transform.position; toTarget.y = 0f;
                Vector3 step = toTarget.normalized * _data.moveSpeed * dt;
                if (step.sqrMagnitude > toTarget.sqrMagnitude) step = toTarget;
                transform.position += step;
                if (animator != null) animator.SetFloat(AnimSpeed, 1f, 0.12f, dt);
                return;
            }

            // in the ring: strike when ready, else circle the player (pack behavior, not a blind follow)
            if (_cooldownTimer <= 0f)
            {
                _state = State.Windup;
                _stateTimer = _data.attackWindup;
                if (animator != null) animator.SetTrigger(AnimAttack);
                return;
            }

            Vector3 tangent = Vector3.Cross(Vector3.up, dirFromPlayer) * _strafeDir;
            Vector3 radialFix = dirFromPlayer * (_standoff - dist) * 0.6f;   // hold the ring distance
            float strafe = 0.3f + 0.35f * (1f - _aggression);                // cautious enemies circle more
            transform.position += (tangent * _data.moveSpeed * strafe + radialFix) * dt;
            if (animator != null) animator.SetFloat(AnimSpeed, 0.45f, 0.15f, dt);
        }

        private void TickWindup(float dt)
        {
            FacePlayer();   // track the player during the telegraph; the Attack anim plays the wind-up
            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                Vector3 to = _player.position - transform.position; to.y = 0f;
                _lungeDir = to.sqrMagnitude > 0.001f ? to.normalized : modelRoot.forward;
                modelRoot.rotation = Quaternion.LookRotation(_lungeDir);
                _state = State.Lunge;
                _stateTimer = _data.lungeTime;
            }
        }

        private void TickLunge(float dt)
        {
            transform.position += _lungeDir * _data.lungeSpeed * dt;
            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                // connect if the player is still in front and within reach
                if (_playerDmg != null && _playerDmg.IsAlive)
                {
                    Vector3 to = _player.position - transform.position; to.y = 0f;
                    if (to.magnitude <= _standoff * 1.25f)
                        _playerDmg.TakeDamage(_data.contactDamage);
                }
                _state = State.Recover;
                _stateTimer = _data.attackRecovery;
            }
        }

        private void TickRecover(float dt)
        {
            if (animator != null) animator.SetFloat(AnimSpeed, 0f, 0.1f, dt);
            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                _cooldownTimer = _attackCd;
                _state = State.Seek;
            }
        }

        private void FacePlayer()
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(to);
        }

        /// <summary>Push-away from nearby enemies + the player so the crowd spaces out (kinematic, no physics).</summary>
        private Vector3 Separation()
        {
            float r = _data.separationRadius;
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
            if (_player != null && _data != null)
            {
                Vector3 kb = transform.position - _player.position; kb.y = 0f;
                if (kb.sqrMagnitude > 0.001f) _knockback = kb.normalized * _data.knockback;
            }
            if (_hp <= 0f) { Die(); return; }
            if (animator != null)   // every hit flinches (action/wushu hitstun)
            {
                animator.SetTrigger(AnimHit);
                _staggerTimer = _data.hitReactTime;
                _state = State.Seek;                      // a hit cancels a wind-up / lunge
            }
        }

        private void Die()
        {
            _active = false;
            _dying = true;
            _deathTimer = _data != null ? _data.deathDuration : 1.1f;
            if (_collider != null) _collider.enabled = false;
            if (animator != null) animator.SetTrigger(AnimDead);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKill();
                GameManager.Instance.DropGem(transform.position, _data != null ? _data.xpValue : 1);
            }
        }
    }
}
