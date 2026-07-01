using System;
using Unity.Behavior;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The Boss/Commander archetype: a multi-attack elite duel. Unlike the single-slam <see cref="Brute"/>, it PICKS an
    /// attack per commit by range — a telegraphed AoE slam or wide frontal cleave up close, a committed charge across the
    /// gap when the player is far — and ENRAGES below a health threshold (faster wind-ups/cooldowns, harder hits). Reuses
    /// the same <see cref="IEnemyBody"/> flow, telegraph glow, HUD warning, camera shake, poise and pooling as the Brute.
    /// Configured by <see cref="BossData"/>. The Ork Champion is its first instance; the Goblin King layers phases on top.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class BossEnemy : MonoBehaviour, IDamageable, IEnemyBody, ICrowdAgent, ITargetInfo
    {
        private enum State { Seek, Windup, Strike, Recover }
        private enum Move { Slam, Charge, Cleave }
        private enum Brain { FSM, BehaviorTree }

        [Tooltip("FSM = the hand-rolled state machine (fallback). BehaviorTree = a Unity Behavior graph on the agent below.")]
        [SerializeField] private Brain _brain = Brain.FSM;
        [SerializeField] private BehaviorGraphAgent _agent;

        [SerializeField] private BossData data;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [Tooltip("Ground decal shown during a SLAM wind-up (the dodge-out zone). Optional; the glow/HUD tell still fire without it.")]
        [SerializeField] private GameObject telegraphPrefab;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private AttackTelegraph attackTelegraph;

        [Header("Tuning")]
        [Tooltip("Camera shake when a slam/charge lands (x = amplitude, y = seconds). Cleave uses a softer share of this.")]
        [SerializeField] private Vector2 slamShake = new Vector2(0.28f, 0.32f);
        [SerializeField] private float knockbackDecayRate = 12f;
        [Tooltip("Height above the feet where the hit spark/blood spawns (~chest of the big boss).")]
        [SerializeField] private float hitFxHeight = 1.6f;
        [Tooltip("Hits at/above this damage also splash blood; every hit sparks, kills always splash.")]
        [SerializeField] private float bloodMinDamage = 20f;

        [Header("Animator (Speed param value + damping time)")]
        [SerializeField] private float animSpeedMove = 1f;
        [SerializeField] private float animDampMove = 0.15f;
        [SerializeField] private float animSpeedStrafe = 0.5f;
        [SerializeField] private float animDampStop = 0.1f;

        [Header("Fallbacks")]
        [SerializeField] private float fallbackDeathDuration = 2f;

        private CapsuleCollider _collider;
        private Transform _player;
        private IDamageable _playerDmg;
        private Action<BossEnemy> _return;

        private float _hp;
        private bool _active;
        private bool _dying;
        private float _deathTimer;
        private State _state;
        private Move _move;             // the attack chosen for the current commit
        private bool _committing;       // true through a whole attack (windup→strike→recover) — drives hyperarmor + skips separation
        private bool _enraged;
        private bool _commanderPhase;   // Goblin King P1: hold + summon; exits to the personal melee fight below commanderUntilHpFraction
        private float _summonTimer;
        private int _playerLayerMask;   // player layer — the commander hazard bolt's hit mask
        private float _cdTimer;
        private float _phaseTimer;
        private Vector3 _slamPoint;     // slam impact point (locked at wind-up start — dodge out of it)
        private Vector3 _chargeDir;     // locked charge direction (at strike start)
        private GameObject _telegraph;
        private float _staggerTimer;
        private Vector3 _knockback;
        private int _separationMask;
        private float _poise;
        private int _agentId = -1;
        private bool _hasToken;         // holds a heavy-tell token while committing (best-effort; the boss always commits)
        private float _engageDwell;     // > 0 while prowling after arrival, before the first attack
        private bool _wasInRange;
        private float _strafeDir;
        private bool _aggro;
        private float _leashTimer;
        private Vector3 _patrolHome;
        private Vector3 _patrolTarget;
        private float _patrolPauseTimer;
        private static readonly Collider[] _sepHits = new Collider[16];
        private static readonly Collider[] _wallHits = new Collider[8];
        private int _obstacleMask;      // walls/props to depenetrate from: all layers except this enemy's + the player's

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");

        public bool IsAlive => _active && _hp > 0f;
        public bool Enraged => _enraged;

        // ---- ITargetInfo (HUD nameplate) ----
        public string TargetName => data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : "Boss";
        public float TargetHp => _hp;
        public float TargetHpMax => data != null ? data.maxHp : 1f;

        // ---- ICrowdAgent (the boss reserves a heavy-tell token but never ring-waits) ----
        public int AgentId => _agentId;
        public Transform AgentTransform => transform;
        public bool WantsSlot => false;
        public float Aggression => 1f;
        public void Wake() => Aggro(false);
        public void AssignSlotAngle(float deg) { }
        public void ClearSlot() { }

        // Widest hold ring: hover near charge distance so it MIXES charges (from range) with close slams/cleaves.
        private float ProwlRing => Mathf.Max(data.slamRange, data.chargeMinRange);
        private float EnrageSpeed => _enraged ? data.enrageSpeedMult : 1f;   // multiplies wind-ups + cooldowns (<1 = faster)
        private float EnrageDmg => _enraged ? data.enrageDamageMult : 1f;

        private void Awake()
        {
            _collider = GetComponent<CapsuleCollider>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
            if (attackTelegraph == null) attackTelegraph = GetComponentInChildren<AttackTelegraph>();
            if (_agent == null) _agent = GetComponent<BehaviorGraphAgent>();
            if (_brain != Brain.BehaviorTree && _agent != null) _agent.enabled = false;
            _obstacleMask = ~(1 << gameObject.layer);
            int pLayer = LayerMask.NameToLayer("Player");
            if (pLayer >= 0) _obstacleMask &= ~(1 << pLayer);
        }

        public void Init(Transform player, Action<BossEnemy> ret, BossData dataOverride = null)
        {
            if (dataOverride != null) data = dataOverride;
            _player = player;
            _playerDmg = player != null ? player.GetComponent<IDamageable>() : null;
            _return = ret;
            _hp = data.maxHp;
            _active = true;
            _dying = false;
            _state = State.Seek;
            _committing = false;
            _enraged = false;
            _commanderPhase = data.hasCommanderPhase;
            _summonTimer = data.commanderCastInterval;
            _playerLayerMask = player != null ? (1 << player.gameObject.layer) : 0;
            _phaseTimer = 0f;
            _cdTimer = data.initialCooldown;
            _staggerTimer = 0f;
            _knockback = Vector3.zero;
            if (_telegraph != null) _telegraph.SetActive(false);
            _separationMask = (1 << gameObject.layer) | (player != null ? (1 << player.gameObject.layer) : 0);
            _poise = data.maxPoise;
            _hasToken = false;
            _engageDwell = 0f;
            _wasInRange = false;
            _strafeDir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _aggro = data.aggroRadius <= 0f;
            _leashTimer = data.leashTime;
            _patrolHome = transform.position;
            _patrolPauseTimer = UnityEngine.Random.Range(0f, data.patrolPauseMax);
            PickPatrolTarget();
            if (CrowdDirector.Instance != null && _agentId < 0) _agentId = CrowdDirector.Instance.Register(this);
            if (_collider != null) _collider.enabled = true;
            if (animator != null) { animator.Rebind(); animator.Update(0f); }
            if (_brain == Brain.BehaviorTree && _agent != null) _agent.Restart();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_dying) { TickDying(dt); return; }
            if (!_active || _player == null) return;

            if (_brain == Brain.BehaviorTree)
            {
                if (_cdTimer > 0f) _cdTimer -= dt;
                if (_staggerTimer > 0f) _staggerTimer -= dt;
                TickPoise(dt);
                TickCommanderCast(dt);
                UpdateAggro(dt);
                TickEngage(dt);
                return;
            }

            ClampOutOfPlayer();
            if (_state == State.Seek) transform.position += Separation() * (data.separationForce * dt);
            if (_cdTimer > 0f) _cdTimer -= dt;
            TickPoise(dt);
            TickCommanderCast(dt);
            UpdateAggro(dt);
            TickEngage(dt);
            if (_staggerTimer > 0f) { _staggerTimer -= dt; AnimStop(dt); return; }

            switch (_state)
            {
                case State.Seek:    FsmSeek(dt);    break;
                case State.Windup:  FsmWindup(dt);  break;
                case State.Strike:  FsmStrike(dt);  break;
                case State.Recover: FsmRecover(dt); break;
            }
            BlockAgainstWalls();   // kinematic bodies ignore wall colliders — push out of anything walked into
        }

        private void LateUpdate()
        {
            if (_brain != Brain.BehaviorTree || _dying || !_active || _player == null) return;
            float dt = Time.deltaTime;
            TickKnockback(dt);
            if (!_committing) transform.position += Separation() * (data.separationForce * dt);
            ClampOutOfPlayer();
            BlockAgainstWalls();
        }

        private void OnDisable()
        {
            ReleaseToken();
            IncomingAttackWarning.Cancel(transform);
            if (_agentId >= 0 && CrowdDirector.Instance != null) CrowdDirector.Instance.Unregister(this);
            _agentId = -1;
        }

        // ---------------- FSM brain ----------------
        private void FsmSeek(float dt)
        {
            if (!InAttackRange) { ApproachStep(dt); return; }
            if (OffCooldown && TryStartTelegraph()) { _state = State.Windup; return; }
            RepositionStep(dt);
        }
        private void FsmWindup(float dt) { if (TickTelegraph(dt)) { StartAttack(); _state = State.Strike; } }
        private void FsmStrike(float dt) { if (TickAttack(dt)) { StartRecover(); _state = State.Recover; } }
        private void FsmRecover(float dt) { if (TickRecover(dt)) _state = State.Seek; }

        // ---------------- IEnemyBody ----------------
        public bool IsStaggered => _staggerTimer > 0f;
        public bool InAttackRange => _aggro && !_commanderPhase && PlanarDist() <= ProwlRing + 1.5f;   // commander phase never melees
        public bool OffCooldown => _cdTimer <= 0f && _engageDwell <= 0f && _staggerTimer <= 0f;

        public void ApproachStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (!_aggro) { PatrolStep(dt); return; }
            if (_commanderPhase) { CommanderHold(dt); return; }   // P1: hold at range from the perch, don't close in
            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            FaceTowardPlayer(dt);
            if (dist > 0.05f) transform.position += (to / dist) * data.moveSpeed * dt;
            SetAnimSpeed(animSpeedMove, animDampMove, dt);
            ArmEngageDwell();
        }

        public void RepositionStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (_commanderPhase) { CommanderHold(dt); return; }
            if (PlanarDist() > ProwlRing + 1.5f) { ApproachStep(dt); return; }
            FaceTowardPlayer(dt);
            Vector3 fromPlayer = transform.position - _player.position; fromPlayer.y = 0f;
            float dist = fromPlayer.magnitude;
            Vector3 dirFromPlayer = dist > 0.001f ? fromPlayer / dist : modelRoot.forward;
            float hold = ProwlRing * data.holdRingFraction;
            Vector3 radialFix = dirFromPlayer * (hold - dist) * data.ringHoldStrength;
            Vector3 tangent = Vector3.Cross(Vector3.up, dirFromPlayer) * _strafeDir;
            transform.position += (tangent * data.moveSpeed * data.strafeFraction + radialFix) * dt;
            SetAnimSpeed(animSpeedStrafe, animDampMove, dt);
        }

        private Move ChooseMove()
        {
            if (PlanarDist() >= data.chargeMinRange) return Move.Charge;                       // far → close the gap
            return UnityEngine.Random.value < data.cleaveChance ? Move.Cleave : Move.Slam;     // close → cleave or slam
        }

        private float MoveWindup(Move m) => m == Move.Slam ? data.slamWindup : m == Move.Charge ? data.chargeWindup : data.cleaveWindup;
        private float MoveRecovery(Move m) => m == Move.Slam ? data.slamRecovery : m == Move.Charge ? data.chargeRecovery : data.cleaveRecovery;

        public bool TryStartTelegraph()
        {
            _move = ChooseMove();
            _hasToken = CrowdDirector.Instance != null && CrowdDirector.Instance.TryBeginAttack(_agentId, 2, true);
            _committing = true;
            _phaseTimer = MoveWindup(_move) * EnrageSpeed;

            if (animator != null) { animator.SetFloat(AnimSpeed, 0f); animator.SetTrigger(AnimAttack); }
            if (attackTelegraph != null) attackTelegraph.Begin(_phaseTimer);
            IncomingAttackWarning.Begin(transform, _phaseTimer);
            Vector3 face = _player.position - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(face);

            // Only the slam paints a ground zone (the AoE you dodge out of); charge/cleave read off the glow + HUD arrow.
            if (_move == Move.Slam)
            {
                _slamPoint = _player.position;
                if (telegraphPrefab != null)
                {
                    if (_telegraph == null) _telegraph = Instantiate(telegraphPrefab);
                    _telegraph.transform.position = _slamPoint + Vector3.up * 0.02f;
                    _telegraph.transform.rotation = Quaternion.identity;
                    _telegraph.transform.localScale = new Vector3(data.slamRadius * 2f, 0.05f, data.slamRadius * 2f);
                    _telegraph.SetActive(true);
                }
            }
            else if (_telegraph != null) _telegraph.SetActive(false);
            return true;   // the boss always commits
        }

        public void CancelTelegraph()
        {
            _committing = false;
            ReleaseToken();
            if (attackTelegraph != null) attackTelegraph.Cancel();
            IncomingAttackWarning.Cancel(transform);
            if (_telegraph != null) _telegraph.SetActive(false);
        }

        public bool TickTelegraph(float dt)
        {
            // Slam/cleave track the player during the wind-up (readable, jukeable). The charge locks its line at strike.
            if (_move != Move.Charge) FaceTowardPlayer(dt);
            _phaseTimer -= dt;
            return _phaseTimer <= 0f;
        }

        public bool TelegraphShouldAbort => false;   // the boss commits (hyperarmor)

        public void StartAttack()
        {
            IncomingAttackWarning.Cancel(transform);
            if (_telegraph != null) _telegraph.SetActive(false);
            switch (_move)
            {
                case Move.Slam:   DoSlam();   _phaseTimer = 0f; break;   // instant → TickAttack ends immediately
                case Move.Cleave: DoCleave(); _phaseTimer = 0f; break;
                case Move.Charge: StartCharge();                break;   // dash resolves over _phaseTimer in TickAttack
            }
        }

        public bool TickAttack(float dt)
        {
            if (_move != Move.Charge) return true;   // slam/cleave already resolved in StartAttack
            transform.position += _chargeDir * data.chargeSpeed * dt;
            if (_playerDmg != null && _playerDmg.IsAlive)
            {
                Vector3 toP = _player.position - transform.position; toP.y = 0f;
                if (toP.magnitude <= data.chargeHitRadius)
                {
                    _playerDmg.TakeDamage(data.chargeDamage * EnrageDmg, transform.position, data.guardBreaks);
                    PlayerCameraRig.Shake(slamShake.x, slamShake.y);
                    return true;
                }
            }
            _phaseTimer -= dt;
            return _phaseTimer <= 0f;   // travel window over (whiffed if it never reached the player)
        }

        public void StartRecover() => _phaseTimer = MoveRecovery(_move);

        public bool TickRecover(float dt)
        {
            AnimStop(dt);
            _phaseTimer -= dt;
            if (_phaseTimer <= 0f) { _cdTimer = data.globalCooldown * EnrageSpeed; _committing = false; ReleaseToken(); return true; }
            return false;
        }

        // ---- concrete attacks ----
        private void DoSlam()
        {
            if (_playerDmg != null && _playerDmg.IsAlive)
            {
                Vector3 d = _player.position - _slamPoint; d.y = 0f;
                if (d.magnitude <= data.slamRadius) _playerDmg.TakeDamage(data.slamDamage * EnrageDmg, transform.position, data.guardBreaks);
            }
            PlayerCameraRig.Shake(slamShake.x, slamShake.y);
        }

        private void DoCleave()
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(to.normalized);   // snap-face the sweep
            if (_playerDmg != null && _playerDmg.IsAlive && to.magnitude <= data.cleaveRange)
            {
                float ang = Vector3.Angle(modelRoot.forward, to.sqrMagnitude > 0.0001f ? to.normalized : modelRoot.forward);
                if (ang <= data.cleaveHalfAngleDeg) _playerDmg.TakeDamage(data.cleaveDamage * EnrageDmg, transform.position, data.guardBreaks);
            }
            PlayerCameraRig.Shake(slamShake.x * 0.7f, slamShake.y * 0.8f);
        }

        private void StartCharge()
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            _chargeDir = to.sqrMagnitude > 0.001f ? to.normalized : modelRoot.forward;
            modelRoot.rotation = Quaternion.LookRotation(_chargeDir);
            _phaseTimer = data.chargeTime;
        }

        private void ReleaseToken()
        {
            if (!_hasToken) return;
            _hasToken = false;
            if (CrowdDirector.Instance != null) CrowdDirector.Instance.ReleaseToken(_agentId);
        }

        // ---------------- commander phase (Goblin King P1) ----------------
        /// <summary>P1 movement: hold on the perch — back off if the player closes inside commanderHoldRange, else stand and command.</summary>
        private void CommanderHold(float dt)
        {
            FaceTowardPlayer(dt);
            if (PlanarDist() < data.commanderHoldRange)
            {
                Vector3 away = transform.position - _player.position; away.y = 0f;
                if (away.sqrMagnitude > 0.01f) transform.position += away.normalized * data.moveSpeed * dt;
                SetAnimSpeed(animSpeedMove, animDampMove, dt);
            }
            else AnimStop(dt);
        }

        /// <summary>P1 cadence: summon a batch of goblins from the stronghold and lob a hazard bolt at the player.</summary>
        private void TickCommanderCast(float dt)
        {
            if (!_commanderPhase || !_aggro) return;
            _summonTimer -= dt;
            if (_summonTimer > 0f) return;
            _summonTimer = data.commanderCastInterval;
            DoCommanderCast();
        }

        private void DoCommanderCast()
        {
            if (animator != null) animator.SetTrigger(AnimAttack);   // cast tell
            GarrisonDirector gd = GarrisonDirector.Instance;
            if (gd != null)
            {
                for (int i = 0; i < data.commanderSummonCount; i++)
                {
                    Vector2 o = UnityEngine.Random.insideUnitCircle.normalized * data.commanderSummonRadius;
                    gd.SpawnGoblinAt(transform.position + new Vector3(o.x, 0f, o.y));   // goblins pour in around the King
                }
            }
            if (_playerLayerMask != 0 && _player != null)   // lob a level hazard bolt at the player (reuses the enemy-arrow pool)
            {
                Vector3 origin = transform.position + Vector3.up * hitFxHeight;
                Vector3 dir = _player.position - origin; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) EnemyArrows.Fire(origin, dir.normalized, data.commanderBoltDamage, data.commanderBoltSpeed, 3f, _playerLayerMask);
            }
        }

        // ---------------- shared helpers (mirror Brute) ----------------
        private void TickPoise(float dt)
        {
            if (data != null && data.maxPoise > 0f && _poise < data.maxPoise)
                _poise = Mathf.Min(data.maxPoise, _poise + data.poiseRegen * dt);
        }

        private void ArmEngageDwell()
        {
            if (data == null) return;
            bool inRange = InAttackRange;
            if (inRange && !_wasInRange && data.engageDwell > 0f)
                _engageDwell = data.engageDwell * UnityEngine.Random.Range(0.8f, 1.2f);
            _wasInRange = inRange;
        }

        private void TickEngage(float dt)
        {
            ArmEngageDwell();
            if (_engageDwell > 0f) _engageDwell -= dt;
        }

        private void UpdateAggro(float dt)
        {
            if (data == null || data.aggroRadius <= 0f) return;
            float dist = PlanarDist();
            if (!_aggro) { if (dist <= data.aggroRadius) Aggro(true); return; }
            if (data.leashRadius > 0f)
            {
                if (dist > data.leashRadius) { _leashTimer -= dt; if (_leashTimer <= 0f) { _aggro = false; _patrolHome = transform.position; PickPatrolTarget(); } }
                else _leashTimer = data.leashTime;
            }
        }

        private void Aggro(bool propagate)
        {
            if (_aggro || !_active || _dying || data == null) return;
            _aggro = true;
            _leashTimer = data.leashTime;
            if (propagate && data.alertRadius > 0f && CrowdDirector.Instance != null)
                CrowdDirector.Instance.AlertNear(transform.position, data.alertRadius, _agentId);
        }

        private float PlanarDist()
        {
            Vector3 d = _player.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        private void FaceTowardPlayer(float dt)
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, Quaternion.LookRotation(to), data.turnSpeedDeg * dt);
        }

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

        private void PickPatrolTarget()
        {
            if (data == null) { _patrolTarget = _patrolHome; return; }
            Vector2 r = UnityEngine.Random.insideUnitCircle * data.patrolRadius;
            _patrolTarget = _patrolHome + new Vector3(r.x, 0f, r.y);
        }

        private void FaceDir(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, Quaternion.LookRotation(dir), data.turnSpeedDeg * dt);
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
            if (_knockback.sqrMagnitude > 0.0001f)
            {
                transform.position += _knockback * dt;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-knockbackDecayRate * dt));
            }
            _deathTimer -= dt;
            if (_deathTimer <= 0f) { _dying = false; _return?.Invoke(this); }
        }

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

        private void ClampOutOfPlayer()
        {
            Vector3 toEnemy = transform.position - _player.position; toEnemy.y = 0f;
            float d = toEnemy.magnitude;
            if (d > 0.0001f && d < data.playerSpacing) transform.position += toEnemy / d * (data.playerSpacing - d);
        }

        /// <summary>Depenetrate the body out of any wall/prop it walked into — kinematic enemies pass through colliders otherwise. Horizontal only.</summary>
        private void BlockAgainstWalls()
        {
            if (_collider == null) return;
            float r = _collider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float halfH = Mathf.Max(_collider.height * 0.5f * transform.lossyScale.y, r);
            Vector3 center = transform.TransformPoint(_collider.center);
            Vector3 up = transform.up;
            Vector3 p0 = center + up * (halfH - r);
            Vector3 p1 = center - up * (halfH - r);
            int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r, _wallHits, _obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider w = _wallHits[i];
                if (w == _collider) continue;
                if (Physics.ComputePenetration(_collider, transform.position, transform.rotation,
                                               w, w.transform.position, w.transform.rotation, out Vector3 dir, out float dist))
                {
                    dir.y = 0f;
                    transform.position += dir * dist;
                }
            }
        }

        public void TakeDamage(float amount) => TakeDamage(amount, _player != null ? _player.position : transform.position - transform.forward);
        public void TakeDamage(float amount, Vector3 sourcePos, bool guardBreak) => TakeDamage(amount, sourcePos);

        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            if (!_active) return;
            TargetNameplate.ReportHit(this);   // player struck this unit → HUD nameplate focuses it
            Aggro(true);
            _hp -= amount;
            if (hitFlash != null) hitFlash.Flash();
            CombatAudio.PlayHit(transform.position);

            Vector3 hitDir = transform.position - sourcePos; hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = modelRoot.forward;
            hitDir = hitDir.normalized;
            CombatVfx.Hit(transform.position + Vector3.up * hitFxHeight, hitDir, _hp <= 0f || amount >= bloodMinDamage);

            // Cross into enrage the first time HP drops past the threshold (a phase flip — jolt + a fresh Attack-trigger tell).
            if (!_enraged && data.enrageHpFraction > 0f && _hp > 0f && _hp <= data.maxHp * data.enrageHpFraction)
            {
                _enraged = true;
                PlayerCameraRig.Shake(slamShake.x * 1.5f, slamShake.y * 1.5f);
            }

            // Goblin King P1 → P2: chopped below the threshold, the coward is cornered and fights personally now.
            if (_commanderPhase && _hp > 0f && _hp <= data.maxHp * data.commanderUntilHpFraction)
            {
                _commanderPhase = false;
                PlayerCameraRig.Shake(slamShake.x * 1.5f, slamShake.y * 1.5f);
            }

            if (_hp <= 0f) { _knockback = hitDir * (data != null ? data.deathKnockback : 6f); Die(); return; }

            // Mid-attack = full hyperarmor. Otherwise the poise pool absorbs chip; a break (empty pool or a heavy) staggers.
            if (!_committing)
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
            if (_telegraph != null) _telegraph.SetActive(false);
            if (animator != null) animator.SetTrigger(AnimDead);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKill();
                GameManager.Instance.DropGem(transform.position, data != null ? data.xpValue : 20);
            }
        }
    }
}
