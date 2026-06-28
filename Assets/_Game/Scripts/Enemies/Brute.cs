using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled elite. Slow seek; when the player is close it commits to a telegraphed slam
    /// (ground decal during the windup = the dodge window), then deals AoE damage at that spot.
    /// High HP, so the reward loop is: dodge the slam, then punish the recovery.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class Brute : MonoBehaviour, IDamageable
    {
        [SerializeField] private EliteData data;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject telegraphPrefab;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private AttackTelegraph attackTelegraph;   // wind-up glow tell (auto-found)

        [Header("Tuning")]
        [Tooltip("Grace period before the elite can slam after spawning.")]
        [SerializeField] private float initialSlamCooldown = 1.5f;
        [Tooltip("Camera shake when the slam lands (x = amplitude, y = seconds).")]
        [SerializeField] private Vector2 slamShake = new Vector2(0.25f, 0.3f);
        [Tooltip("Death-knockback decay (higher = shorter slide).")]
        [SerializeField] private float knockbackDecayRate = 12f;
        [Tooltip("Height above the feet where the hit spark/blood spawns (~chest of the big elite).")]
        [SerializeField] private float hitFxHeight = 1.5f;
        [Tooltip("Hits at/above this damage also splash blood; every hit sparks, kills always splash.")]
        [SerializeField] private float bloodMinDamage = 20f;
        [Tooltip("Hard minimum distance the brute keeps from the player's center (no standing inside the player). Just under player CC radius + brute radius (~0.8).")]
        [SerializeField] private float playerSpacing = 0.7f;

        [Header("Animator (Speed param value + damping time)")]
        [Tooltip("Speed param while moving toward the player, and its blend damping time.")]
        [SerializeField] private float animSpeedMove = 1f;
        [SerializeField] private float animDampMove = 0.15f;
        [Tooltip("Animator Speed damping time when stopping (stagger).")]
        [SerializeField] private float animDampStop = 0.1f;

        [Header("Fallbacks")]
        [Tooltip("Death animation duration used only if EliteData is missing (normally data.deathDuration).")]
        [SerializeField] private float fallbackDeathDuration = 1.6f;

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
        private Vector3 _knockback;
        private int _separationMask;
        private static readonly Collider[] _sepHits = new Collider[16];

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");

        public bool IsAlive => _active && _hp > 0f;

        private void Awake()
        {
            _collider = GetComponent<CapsuleCollider>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
            if (attackTelegraph == null) attackTelegraph = GetComponentInChildren<AttackTelegraph>();
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
            _cdTimer = initialSlamCooldown;
            _staggerTimer = 0f;
            _knockback = Vector3.zero;
            if (_telegraph != null) _telegraph.SetActive(false);   // never carry a stale telegraph across a pool reuse
            _separationMask = (1 << gameObject.layer) | (player != null ? (1 << player.gameObject.layer) : 0);
            if (_collider != null) _collider.enabled = true;
            if (animator != null) { animator.Rebind(); animator.Update(0f); }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_dying)
            {
                if (_knockback.sqrMagnitude > 0.0001f)   // ride the death knockback while the body falls
                {
                    transform.position += _knockback * dt;
                    _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-knockbackDecayRate * dt));
                }
                _deathTimer -= dt;
                if (_deathTimer <= 0f) { _dying = false; _return?.Invoke(this); }
                return;
            }
            if (!_active || _player == null) return;

            ClampOutOfPlayer();   // never stand inside the player

            if (_slamming) { TickSlam(dt); return; }

            transform.position += Separation() * (data.separationForce * dt);   // crowd separation
            if (_cdTimer > 0f) _cdTimer -= dt;
            if (_staggerTimer > 0f) { _staggerTimer -= dt; if (animator != null) animator.SetFloat(AnimSpeed, 0f, animDampStop, dt); return; }

            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;

            if (dist <= data.slamRange && _cdTimer <= 0f) { StartSlam(); return; }

            if (dist > 0.05f)
            {
                Vector3 dir = to / dist;
                transform.position += dir * data.moveSpeed * dt;
                modelRoot.rotation = Quaternion.LookRotation(dir);
            }
            if (animator != null) animator.SetFloat(AnimSpeed, animSpeedMove, animDampMove, dt);
        }

        private void StartSlam()
        {
            _slamming = true;
            _slamTimer = data.slamWindup + data.slamRecovery;
            _slamHitPending = true;
            _slamPoint = _player.position;

            if (animator != null) { animator.SetFloat(AnimSpeed, 0f); animator.SetTrigger(AnimAttack); }
            if (attackTelegraph != null) attackTelegraph.Begin(data.slamWindup);   // glow only during the dodge window
            Vector3 d = _slamPoint - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(d);

            if (telegraphPrefab != null)
            {
                // Reuse one telegraph instance per brute instead of Instantiate/Destroy each slam (no per-slam GC).
                if (_telegraph == null) _telegraph = Instantiate(telegraphPrefab);
                _telegraph.transform.position = _slamPoint + Vector3.up * 0.02f;
                _telegraph.transform.rotation = Quaternion.identity;
                _telegraph.transform.localScale = new Vector3(data.slamRadius * 2f, 0.05f, data.slamRadius * 2f);
                _telegraph.SetActive(true);
            }
        }

        private void TickSlam(float dt)
        {
            _slamTimer -= dt;
            float since = (data.slamWindup + data.slamRecovery) - _slamTimer;

            if (_slamHitPending && since >= data.slamWindup)
            {
                _slamHitPending = false;
                if (_telegraph != null) _telegraph.SetActive(false);   // telegraph clears at the moment of impact

                if (_player != null && _playerDmg != null && _playerDmg.IsAlive)
                {
                    Vector3 d = _player.position - _slamPoint; d.y = 0f;
                    if (d.magnitude <= data.slamRadius) _playerDmg.TakeDamage(data.slamDamage, transform.position, data.guardBreaks);
                }
                PlayerCameraRig.Shake(slamShake.x, slamShake.y);
            }

            if (_slamTimer <= 0f)
            {
                _slamming = false;
                _cdTimer = data.slamCooldown;
                if (_telegraph != null) _telegraph.SetActive(false);
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

        /// <summary>Hard depenetration from the player's body (kinematic — the player's CharacterController can't push us out).</summary>
        private void ClampOutOfPlayer()
        {
            Vector3 toEnemy = transform.position - _player.position; toEnemy.y = 0f;
            float d = toEnemy.magnitude;
            if (d > 0.0001f && d < playerSpacing) transform.position += toEnemy / d * (playerSpacing - d);
        }

        public void TakeDamage(float amount) => TakeDamage(amount, _player != null ? _player.position : transform.position - transform.forward);

        // The brute doesn't block — guardBreak is irrelevant, so delegate to the 2-arg path.
        public void TakeDamage(float amount, Vector3 sourcePos, bool guardBreak) => TakeDamage(amount, sourcePos);

        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            if (!_active) return;
            _hp -= amount;
            if (hitFlash != null) hitFlash.Flash();
            CombatAudio.PlayHit(transform.position);

            // Hit VFX: spark every hit, blood on heavies/kills, at the impact point facing away from the hitter.
            Vector3 hitDir = transform.position - sourcePos; hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = modelRoot.forward;
            hitDir = hitDir.normalized;
            CombatVfx.Hit(transform.position + Vector3.up * hitFxHeight, hitDir, _hp <= 0f || amount >= bloodMinDamage);

            if (_hp <= 0f)
            {
                _knockback = hitDir * (data != null ? data.deathKnockback : 10f);   // big slide-back on death
                Die();
                return;
            }
            if (!_slamming && animator != null)   // reacts to every hit, but has poise mid-slam
            {
                int dir = HitReaction.Direction(transform.position, modelRoot.forward, modelRoot.right, sourcePos);
                animator.SetInteger(AnimHitDir, dir);
                animator.SetTrigger(AnimHit);
                _staggerTimer = data.hitReactTime;
            }
        }

        private void Die()
        {
            _active = false;
            _dying = true;
            _deathTimer = data != null ? data.deathDuration : fallbackDeathDuration;
            if (_collider != null) _collider.enabled = false;
            if (_telegraph != null) _telegraph.SetActive(false);   // keep the instance; reused when this brute respawns from the pool
            if (animator != null) animator.SetTrigger(AnimDead);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKill();
                GameManager.Instance.DropGem(transform.position, data != null ? data.xpValue : 5);
            }
        }
    }
}
