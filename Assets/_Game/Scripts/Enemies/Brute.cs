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
    public class Brute : MonoBehaviour, IDamageable, IEnemyBody, ICrowdAgent
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
        [Tooltip("Speed param while circling/prowling around the player (slower than a straight approach).")]
        [SerializeField] private float animSpeedStrafe = 0.5f;
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
        private float _poise;          // depletes on hits outside a slam; a break is a real stagger (inactive when data.maxPoise <= 0)
        private int _agentId = -1;     // CrowdDirector handle; -1 = unmanaged
        private bool _hasToken;        // holds a heavy-tell token while slamming (best-effort; the boss always commits)
        private float _engageDwell;    // > 0 while prowling/circling after arrival, before the first slam is allowed
        private bool _wasInRange;      // last frame's InAttackRange, to detect arrival (arms the dwell)
        private float _strafeDir;      // +1 / -1 — which way it circles the player while prowling
        private bool _aggro;           // engaged? false = PASSIVE (patrols its spawn). Woken by proximity / damage / a nearby ally's alert.
        private float _leashTimer;     // counts down while the player is beyond leashRadius; at 0 the elite de-aggros
        private Vector3 _patrolHome;   // spawn point (or where it last de-aggro'd) — patrol lumbers within patrolRadius of this
        private Vector3 _patrolTarget; // current patrol destination
        private float _patrolPauseTimer; // > 0 while pausing at a patrol point
        private static readonly Collider[] _sepHits = new Collider[16];

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");

        public bool IsAlive => _active && _hp > 0f;

        // ---- ICrowdAgent (the brute reserves a heavy-tell token but never ring-waits, so it holds no slot) ----
        public int AgentId => _agentId;
        public Transform AgentTransform => transform;
        public bool WantsSlot => false;
        public float Aggression => 1f;   // the boss is always top-priority threat
        public void Wake() => Aggro(false);   // alerted by a nearby ally engaging — wake without re-propagating
        public void AssignSlotAngle(float deg) { }
        public void ClearSlot() { }

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
            _poise = data.maxPoise;
            _hasToken = false;
            _engageDwell = 0f;
            _wasInRange = false;
            _strafeDir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _aggro = data.aggroRadius <= 0f;   // legacy (<=0) = aggro from spawn; otherwise spawn PASSIVE until alerted
            _leashTimer = data.leashTime;
            _patrolHome = transform.position;
            _patrolPauseTimer = UnityEngine.Random.Range(0f, data.patrolPauseMax);   // desync the patrol
            PickPatrolTarget();
            if (CrowdDirector.Instance != null && _agentId < 0) _agentId = CrowdDirector.Instance.Register(this);   // pooled brutes re-register each spawn
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
                TickPoise(dt);
                UpdateAggro(dt);   // resolve aggro BEFORE the dwell — the dwell's InAttackRange check depends on _aggro
                TickEngage(dt);
                return;
            }

            // ---- FSM brain (fallback) ----
            ClampOutOfPlayer();
            if (_state == State.Seek) transform.position += Separation() * (data.separationForce * dt);   // separation only while not mid-slam
            if (_cdTimer > 0f) _cdTimer -= dt;
            TickPoise(dt);
            UpdateAggro(dt);   // resolve aggro BEFORE the dwell — the dwell's InAttackRange check depends on _aggro
            TickEngage(dt);
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

        private void OnDisable()
        {
            ReleaseToken();
            IncomingAttackWarning.Cancel(transform);
            if (_agentId >= 0 && CrowdDirector.Instance != null) CrowdDirector.Instance.Unregister(this);   // frees token + slot, drops from registry
            _agentId = -1;
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
        public bool InAttackRange => _aggro && PlanarDist() <= data.slamRange;   // passive elites never engage (idle until alerted)
        public bool OffCooldown => _cdTimer <= 0f && _engageDwell <= 0f && _staggerTimer <= 0f;   // cooled down, done prowling, not flinching

        public void ApproachStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (!_aggro) { PatrolStep(dt); return; }   // passive — amble around the spawn until engaged
            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            FaceTowardPlayer(dt);
            if (dist > 0.05f) transform.position += (to / dist) * data.moveSpeed * dt;
            SetAnimSpeed(animSpeedMove, animDampMove, dt);
            ArmEngageDwell();   // arm the prowl dwell IN the graph tick the instant we reach slam range, BEFORE OffCooldown is read
        }

        public void RepositionStep(float dt)
        {
            // Prowl: CIRCLE the player at a hold ring (just inside slam range) instead of standing still — a deliberate,
            // menacing approach rather than a walk-up-and-pound. The slam is an AoE locked onto your position, so circling
            // at this range still covers you. Re-approach only if you've slipped outside slam range.
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (PlanarDist() > data.slamRange) { ApproachStep(dt); return; }
            FaceTowardPlayer(dt);
            Vector3 fromPlayer = transform.position - _player.position; fromPlayer.y = 0f;
            float dist = fromPlayer.magnitude;
            Vector3 dirFromPlayer = dist > 0.001f ? fromPlayer / dist : modelRoot.forward;
            float hold = data.slamRange * data.holdRingFraction;
            Vector3 radialFix = dirFromPlayer * (hold - dist) * data.ringHoldStrength;   // ease toward the hold ring
            Vector3 tangent = Vector3.Cross(Vector3.up, dirFromPlayer) * _strafeDir;      // orbit the player
            transform.position += (tangent * data.moveSpeed * data.strafeFraction + radialFix) * dt;
            SetAnimSpeed(animSpeedStrafe, animDampMove, dt);
        }

        public bool TryStartTelegraph()
        {
            // Best-effort heavy reservation: a slam costs 2 and counts as a heavy tell, so chaff chargers yield while
            // the boss winds up. The boss is NEVER denied its slam (it commits regardless) — it just reserves budget.
            _hasToken = CrowdDirector.Instance != null && CrowdDirector.Instance.TryBeginAttack(_agentId, 2, true);
            _slamming = true;
            _slamPoint = _player.position;   // locked here — dodge out of this spot during the wind-up
            _phaseTimer = data.slamWindup;
            if (animator != null) { animator.SetFloat(AnimSpeed, 0f); animator.SetTrigger(AnimAttack); }
            if (attackTelegraph != null) attackTelegraph.Begin(data.slamWindup);   // glow only during the dodge window
            IncomingAttackWarning.Begin(transform, data.slamWindup);   // directional "incoming slam!" tell on the HUD
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
            ReleaseToken();
            if (attackTelegraph != null) attackTelegraph.Cancel();
            IncomingAttackWarning.Cancel(transform);
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
            IncomingAttackWarning.Cancel(transform);   // wind-up (dodge window) over — the slam lands now
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
            if (_phaseTimer <= 0f) { _cdTimer = data.slamCooldown; _slamming = false; ReleaseToken(); return true; }
            return false;
        }

        private void ReleaseToken()
        {
            if (!_hasToken) return;
            _hasToken = false;
            if (CrowdDirector.Instance != null) CrowdDirector.Instance.ReleaseToken(_agentId);
        }

        /// <summary>Regenerate poise toward the pool max while not broken (so accumulated chip eventually staggers).</summary>
        private void TickPoise(float dt)
        {
            if (data != null && data.maxPoise > 0f && _poise < data.maxPoise)
                _poise = Mathf.Min(data.maxPoise, _poise + data.poiseRegen * dt);
        }

        /// <summary>
        /// Arm a prowl dwell on the rising edge of InAttackRange (the brute just reached slam range) so it circles the
        /// player for a beat before its first slam. Called from <see cref="ApproachStep"/> (the graph-driven verb) so the
        /// dwell is set in the SAME tick the body reaches range — BEFORE OffCooldown is read. The Behavior graph (exec
        /// order -50) moves the body in and re-checks OffCooldown within one tick, so arming from Update (order 0) would
        /// be a frame too late. Idempotent via <see cref="_wasInRange"/>.
        /// </summary>
        private void ArmEngageDwell()
        {
            if (data == null) return;
            bool inRange = InAttackRange;
            if (inRange && !_wasInRange && data.engageDwell > 0f)
                _engageDwell = data.engageDwell * UnityEngine.Random.Range(0.8f, 1.2f);   // prowl before the first slam
            _wasInRange = inRange;
        }

        /// <summary>Maintain the dwell each frame: catch the arrival edge (covers spawn-in-range) and count it down.</summary>
        private void TickEngage(float dt)
        {
            ArmEngageDwell();
            if (_engageDwell > 0f) _engageDwell -= dt;
        }

        /// <summary>Drive the passive&lt;-&gt;engaged state (both brains). Passive elites idle until the player enters aggroRadius; engaged ones leash back if the player stays beyond leashRadius for leashTime.</summary>
        private void UpdateAggro(float dt)
        {
            if (data == null) return;
            if (data.aggroRadius <= 0f) return;   // legacy: aggro'd from spawn, never leashes / re-evaluates
            float dist = PlanarDist();
            if (!_aggro)
            {
                if (data.aggroRadius > 0f && dist <= data.aggroRadius) Aggro(true);
                return;
            }
            if (data.leashRadius > 0f)
            {
                if (dist > data.leashRadius) { _leashTimer -= dt; if (_leashTimer <= 0f) { _aggro = false; _patrolHome = transform.position; PickPatrolTarget(); } }
                else _leashTimer = data.leashTime;
            }
        }

        /// <summary>Engage the player; <paramref name="propagate"/> also alerts nearby allies (one hop) so a cluster wakes together.</summary>
        private void Aggro(bool propagate)
        {
            if (_aggro || !_active || _dying || data == null) return;
            _aggro = true;
            _leashTimer = data.leashTime;
            if (propagate && data.alertRadius > 0f && CrowdDirector.Instance != null)
                CrowdDirector.Instance.AlertNear(transform.position, data.alertRadius, _agentId);
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

        /// <summary>Passive: lumber around the spawn home instead of standing frozen (same amble as the chaff, elite-paced). patrolRadius &lt;= 0 = stand still.</summary>
        private void PatrolStep(float dt)
        {
            if (data.patrolRadius <= 0f) { AnimStop(dt); return; }
            if (_patrolPauseTimer > 0f) { _patrolPauseTimer -= dt; AnimStop(dt); return; }
            Vector3 to = _patrolTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude <= 0.25f)
            {
                _patrolPauseTimer = UnityEngine.Random.Range(data.patrolPauseMin, data.patrolPauseMax);
                PickPatrolTarget();
                AnimStop(dt);
                return;
            }
            Vector3 dir = to.normalized;
            transform.position += dir * (data.moveSpeed * data.patrolSpeed * dt);
            FaceDir(dir, dt);
            SetAnimSpeed(animSpeedStrafe, animDampMove, dt);
        }

        /// <summary>Pick a fresh random patrol destination within patrolRadius of the spawn home (XZ plane).</summary>
        private void PickPatrolTarget()
        {
            if (data == null) { _patrolTarget = _patrolHome; return; }
            Vector2 r = UnityEngine.Random.insideUnitCircle * data.patrolRadius;
            _patrolTarget = _patrolHome + new Vector3(r.x, 0f, r.y);
        }

        /// <summary>Turn the model toward an arbitrary heading (patrol uses this; FaceTowardPlayer is for the engaged slam).</summary>
        private void FaceDir(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, target, data.turnSpeedDeg * dt);
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
            Aggro(true);   // getting hit always engages the elite (and wakes nearby allies)
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
            // Mid-slam = full hyperarmor (never flinches). Outside a slam, the poise pool still absorbs light taps —
            // only an emptied pool or a heavy (>= bloodMinDamage) breaks the big bruiser into a real stagger.
            if (!_slamming)
            {
                bool staggered = PoiseTracker.Resolve(ref _poise, data.maxPoise, amount, amount >= bloodMinDamage);
                if (staggered && animator != null)
                {
                    int dir = HitReaction.Direction(transform.position, modelRoot.forward, modelRoot.right, sourcePos);
                    animator.SetInteger(AnimHitDir, dir);
                    animator.SetTrigger(AnimHit);
                    _staggerTimer = data.staggerDuration > 0f ? data.staggerDuration : data.hitReactTime;
                }
            }
        }

        private void Die()
        {
            ReleaseToken();
            IncomingAttackWarning.Cancel(transform);
            if (CrowdDirector.Instance != null) CrowdDirector.Instance.Release(_agentId);
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
