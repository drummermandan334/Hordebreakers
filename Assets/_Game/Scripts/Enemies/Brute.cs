using System;
using Unity.Behavior;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled elite. Slow seek; when the player is close it commits to a telegraphed slam (ground decal during the
    /// wind-up = the dodge window), then deals AoE damage at that spot. High HP + poise (never flinches mid-slam), so
    /// the loop is: dodge the slam, then punish the recovery.
    ///
    /// Like <see cref="Enemy"/>, decisions run through one of two BRAINS (<see cref="_brain"/>): the hand-rolled FSM
    /// (default/fallback) or a Unity Behavior graph. Both drive the same <see cref="IEnemyBody"/> verbs below.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class Brute : MonoBehaviour, IDamageable, IEnemyBody
    {
        private enum State { Seek, Windup, Strike, Recover }
        private enum Brain { FSM, BehaviorTree }

        [Tooltip("FSM = the hand-rolled state machine (fallback). BehaviorTree = a Unity Behavior graph on the agent below.")]
        [SerializeField] private Brain _brain = Brain.FSM;
        [Tooltip("Behavior graph agent (only used when brain = BehaviorTree). Auto-found in Awake.")]
        [SerializeField] private BehaviorGraphAgent _agent;

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
        private State _state;
        private float _cdTimer;
        private bool _slamming;        // true through the whole slam (windup→strike→recover) — drives poise + skips separation
        private float _phaseTimer;     // FSM state time + the body's telegraph/recover phase timer
        private Vector3 _slamPoint;    // where the slam will land (locked at wind-up start — dodge out of it)
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
            if (_agent == null) _agent = GetComponent<BehaviorGraphAgent>();
        }

        public void Init(Transform player, Action<Brute> ret, EliteData dataOverride = null)
        {
            if (dataOverride != null) data = dataOverride;   // GarrisonDirector passes regular vs commander EliteData from one pool
            _player = player;
            _playerDmg = player != null ? player.GetComponent<IDamageable>() : null;
            _return = ret;
            _hp = data.maxHp;
            _active = true;
            _dying = false;
            _state = State.Seek;
            _slamming = false;
            _phaseTimer = 0f;
            _cdTimer = initialSlamCooldown;
            _staggerTimer = 0f;
            _knockback = Vector3.zero;
            if (_telegraph != null) _telegraph.SetActive(false);   // never carry a stale telegraph across a pool reuse
            _separationMask = (1 << gameObject.layer) | (player != null ? (1 << player.gameObject.layer) : 0);
            if (_collider != null) _collider.enabled = true;
            if (animator != null) { animator.Rebind(); animator.Update(0f); }
            if (_brain == Brain.BehaviorTree && _agent != null) _agent.Restart();   // reset the graph for a pooled reuse
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_dying) { TickDying(dt); return; }
            if (!_active || _player == null) return;

            if (_brain == Brain.BehaviorTree)
            {
                // The graph drives decisions + movement; just tick timers. Corrections run in LateUpdate.
                if (_cdTimer > 0f) _cdTimer -= dt;
                if (_staggerTimer > 0f) _staggerTimer -= dt;
                return;
            }

            // ---- FSM brain (fallback) ----
            ClampOutOfPlayer();
            if (_state == State.Seek) transform.position += Separation() * (data.separationForce * dt);   // separation only while not mid-slam
            if (_cdTimer > 0f) _cdTimer -= dt;
            if (_staggerTimer > 0f) { _staggerTimer -= dt; AnimStop(dt); return; }

            switch (_state)
            {
                case State.Seek:    FsmSeek(dt);    break;
                case State.Windup:  FsmWindup(dt);  break;
                case State.Strike:  FsmStrike(dt);  break;
                case State.Recover: FsmRecover(dt); break;
            }
        }

        private void LateUpdate()
        {
            if (_brain != Brain.BehaviorTree || _dying || !_active || _player == null) return;
            float dt = Time.deltaTime;
            TickKnockback(dt);
            if (!_slamming) transform.position += Separation() * (data.separationForce * dt);
            ClampOutOfPlayer();
        }

        // ---------------- FSM brain (thin orchestration over the IEnemyBody verbs) ----------------
        private void FsmSeek(float dt)
        {
            if (!InAttackRange) { ApproachStep(dt); return; }
            if (OffCooldown && TryStartTelegraph()) { _state = State.Windup; return; }
            RepositionStep(dt);
        }

        private void FsmWindup(float dt)
        {
            if (TickTelegraph(dt)) { StartAttack(); _state = State.Strike; }
        }

        private void FsmStrike(float dt)
        {
            if (TickAttack(dt)) { StartRecover(); _state = State.Recover; }
        }

        private void FsmRecover(float dt)
        {
            if (TickRecover(dt)) _state = State.Seek;
        }

        // ---------------- IEnemyBody (the body API — driven by both brains) ----------------
        public bool IsStaggered => _staggerTimer > 0f;
        public bool InAttackRange => PlanarDist() <= data.slamRange;
        public bool OffCooldown => _cdTimer <= 0f;

        public void ApproachStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            FaceTowardPlayer(dt);
            if (dist > 0.05f) transform.position += (to / dist) * data.moveSpeed * dt;
            SetAnimSpeed(animSpeedMove, animDampMove, dt);
        }

        public void RepositionStep(float dt)
        {
            // Hold at slam range instead of walking right up to the player — the slam is an AoE at your position, so
            // it doesn't need to hug. Re-approach only if you've slipped outside slam range.
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (PlanarDist() > data.slamRange) { ApproachStep(dt); return; }
            FaceTowardPlayer(dt);
            AnimStop(dt);
        }

        public bool TryStartTelegraph()
        {
            _slamming = true;
            _slamPoint = _player.position;   // locked here — dodge out of this spot during the wind-up
            _phaseTimer = data.slamWindup;
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
            return true;   // the brute (boss) is never capped — it always commits to a slam
        }

        public void CancelTelegraph()
        {
            _slamming = false;
            if (attackTelegraph != null) attackTelegraph.Cancel();
            if (_telegraph != null) _telegraph.SetActive(false);
        }

        public bool TickTelegraph(float dt)
        {
            FaceTowardPlayer(dt);   // slow turn (low turnSpeedDeg) — readable, can be juked
            _phaseTimer -= dt;
            return _phaseTimer <= 0f;
        }

        public bool TelegraphShouldAbort => false;   // the brute commits (hyperarmor) — it never aborts a slam

        public void StartAttack()
        {
            // The slam strike lands at the telegraphed point (instant); recovery is the Recover phase.
            if (_telegraph != null) _telegraph.SetActive(false);   // telegraph clears at the moment of impact
            if (_player != null && _playerDmg != null && _playerDmg.IsAlive)
            {
                Vector3 d = _player.position - _slamPoint; d.y = 0f;
                if (d.magnitude <= data.slamRadius) _playerDmg.TakeDamage(data.slamDamage, transform.position, data.guardBreaks);
            }
            PlayerCameraRig.Shake(slamShake.x, slamShake.y);
        }

        public bool TickAttack(float dt) => true;   // the slam strike is instant

        public void StartRecover() => _phaseTimer = data.slamRecovery;

        public bool TickRecover(float dt)
        {
            AnimStop(dt);
            _phaseTimer -= dt;
            if (_phaseTimer <= 0f) { _cdTimer = data.slamCooldown; _slamming = false; return true; }
            return false;
        }

        // ---------------- helpers ----------------
        private float PlanarDist()
        {
            Vector3 d = _player.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        private void FaceTowardPlayer(float dt)
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(to);
                modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, target, data.turnSpeedDeg * dt);
            }
        }

        private void SetAnimSpeed(float value, float damp, float dt) { if (animator != null) animator.SetFloat(AnimSpeed, value, damp, dt); }
        private void AnimStop(float dt) { if (animator != null) animator.SetFloat(AnimSpeed, 0f, animDampStop, dt); }

        private void TickKnockback(float dt)
        {
            if (_knockback.sqrMagnitude > 0.0001f)
            {
                transform.position += _knockback * dt;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-knockbackDecayRate * dt));
            }
        }

        private void TickDying(float dt)
        {
            if (_knockback.sqrMagnitude > 0.0001f)   // ride the death knockback while the body falls
            {
                transform.position += _knockback * dt;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-knockbackDecayRate * dt));
            }
            _deathTimer -= dt;
            if (_deathTimer <= 0f) { _dying = false; _return?.Invoke(this); }
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
