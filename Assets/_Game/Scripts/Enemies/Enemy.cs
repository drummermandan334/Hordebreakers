using System;
using Unity.Behavior;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled swarm enemy with a real attack pattern (not a VS-style follower): it seeks to a standoff RING around the
    /// player, then commits to a telegraphed strike — windup (the dodge window) → dash → recovery → cooldown.
    /// Kinematic (no NavMesh/Rigidbody for the prototype). Flashes on hit; returns to its pool on death.
    ///
    /// The decision logic runs through one of two BRAINS (<see cref="_brain"/>): the hand-rolled FSM (default/fallback)
    /// or a Unity Behavior graph on a <see cref="BehaviorGraphAgent"/>. BOTH drive the same <see cref="IEnemyBody"/> verb
    /// set below — the FSM is a thin orchestrator over it, so it validates the API the graph nodes also use. Mechanics
    /// (movement application, separation, clamp, knockback, hit-feel, death) live here regardless of brain.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class Enemy : MonoBehaviour, IDamageable, IEnemyBody, ICrowdAgent
    {
        private enum State { Seek, Windup, Lunge, Recover }
        private enum Brain { FSM, BehaviorTree }

        [Tooltip("FSM = the hand-rolled state machine (fallback). BehaviorTree = a Unity Behavior graph on the agent below.")]
        [SerializeField] private Brain _brain = Brain.FSM;
        [Tooltip("Behavior graph agent (only used when brain = BehaviorTree). Auto-found in Awake.")]
        [SerializeField] private BehaviorGraphAgent _agent;

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private HitFlash hitFlash;
        [SerializeField] private AttackTelegraph attackTelegraph;   // wind-up glow tell (auto-found)
        [Tooltip("Hits at/above this damage also splash blood (heavies). Every hit sparks; kills always splash.")]
        [SerializeField] private float bloodMinDamage = 20f;
        [Tooltip("Height above the enemy's feet where the hit spark/blood spawns (~chest).")]
        [SerializeField] private float hitFxHeight = 1f;

        [Header("Aggression (per-instance variety, derived from a 0..1 roll)")]
        [Tooltip("Standoff ring = attackRange * (base - slope * aggression). Aggressive enemies crowd in closer.")]
        [SerializeField] private float standoffRangeBase = 1.35f;
        [SerializeField] private float standoffRangeAggressionSlope = 0.6f;
        [Tooltip("Attack cooldown = attackCooldown * (base - slope * aggression). Aggressive enemies strike more often.")]
        [SerializeField] private float attackCooldownBase = 1.4f;
        [SerializeField] private float attackCooldownAggressionSlope = 0.8f;
        [Tooltip("Initial cooldown is randomized between this and the per-instance attack cooldown (desyncs the crowd).")]
        [SerializeField] private float initialCooldownMin = 0.3f;
        [Tooltip("Per-instance ring slot angle is randomized within +/- this (fans the crowd out).")]
        [SerializeField] private float slotOffsetRange = 35f;

        [Header("Movement / approach")]
        [Tooltip("Rate the hit-recoil knockback impulse decays toward zero.")]
        [SerializeField] private float knockbackDecayRate = 12f;
        [Tooltip("Extra distance past the standoff ring before the enemy stops approaching.")]
        [SerializeField] private float ringApproachBuffer = 0.4f;
        [Tooltip("How hard the enemy corrects back toward the ring distance while circling.")]
        [SerializeField] private float ringHoldStrength = 0.6f;
        [Tooltip("Strafe (circle) speed fraction = base + aggressionScale * (1 - aggression). Cautious enemies circle more.")]
        [SerializeField] private float strafeSpeedBase = 0.3f;
        [SerializeField] private float strafeSpeedAggressionScale = 0.35f;
        [Tooltip("Wind-up aborts (rusher) if the player escapes past standoff * this during the telegraph.")]
        [SerializeField] private float telegraphAbortReachMult = 1.5f;
        [Tooltip("StandoffLunge: during the wind-up the rusher STALKS IN to this distance (m) so the strike commits from melee range — close enough that the player can hit it to interrupt (you can't reach a strike thrown from the wide orbit ring). Keep within the player's melee reach (~2m).")]
        [SerializeField] private float telegraphStrikeRange = 1.7f;
        [Tooltip("StandoffLunge: stalk-in speed (m/s) during the wind-up as it closes to telegraphStrikeRange. Higher = reaches melee range sooner, so the player gets a longer window to interrupt.")]
        [SerializeField] private float telegraphStepSpeed = 4f;
        [Tooltip("Charger: between charges it backs off to chargeStartRange * this, so the next charge is a real run-up instead of a point-blank tap.")]
        [SerializeField] private float chargerHoldFraction = 0.8f;

        [Header("Animator blend (Speed param value + damping time)")]
        [Tooltip("Speed param while approaching the ring, and its blend damping time.")]
        [SerializeField] private float animSpeedApproach = 1f;
        [SerializeField] private float animDampApproach = 0.12f;
        [Tooltip("Speed param while circling/strafing in the ring, and its blend damping time.")]
        [SerializeField] private float animSpeedStrafe = 0.45f;
        [SerializeField] private float animDampStrafe = 0.15f;
        [Tooltip("Animator Speed damping time when stopping (stagger / recover).")]
        [SerializeField] private float animDampStop = 0.1f;

        [Header("Fallbacks")]
        [Tooltip("Death animation duration used only if EnemyData is missing (normally data.deathDuration).")]
        [SerializeField] private float fallbackDeathDuration = 1.1f;

        private EnemyData _data;
        private CapsuleCollider _collider;
        private Transform _player;
        private IDamageable _playerDmg;
        private Action<Enemy> _returnToPool;

        private float _hp;
        private bool _active;
        private bool _dying;
        private float _deathTimer;

        private State _state;
        private float _stateTimer;     // FSM state time + the body's telegraph/attack/recover phase timer (one phase at a time)
        private float _cooldownTimer;
        private float _slotOffset;     // per-instance ring angle so they don't all stack on one point
        private float _strafeDir;      // +1 / -1 — which way it circles the player while waiting
        private float _aggression;     // 0 = cautious (hangs back, circles), 1 = aggressive (rushes, attacks often)
        private float _standoff;       // per-instance ring distance (from aggression)
        private float _attackCd;       // per-instance attack cooldown (from aggression)
        private Vector3 _lungeDir;
        private Vector3 _knockback;    // hit-recoil impulse
        private float _staggerTimer;   // > 0 while flinching from a hit (movement paused)
        private bool _hasAttackToken;  // true while this rusher holds an attack token (director, or static fallback)
        private int _separationMask;   // enemy + player layers, for crowd separation
        private float _poise;          // depletes on hits; a break is a real stagger (inactive when data.maxPoise <= 0)
        private int _agentId = -1;     // CrowdDirector handle; -1 = unmanaged (solo fallback)
        private bool _hasSlot;         // director assigned us an approach-ring slot
        private float _slotAngleDeg;   // absolute bearing (deg, 0 = +Z) of the assigned slot around the player
        private float _engageDwell;    // > 0 while circling the player after arrival, before the first telegraph is allowed
        private bool _wasInRange;      // last frame's InAttackRange, to detect arrival (the rising edge that arms the dwell)
        private bool _aggro;           // engaged? false = PASSIVE (idles at spawn, no homing). Woken by proximity / damage / a nearby ally's alert.
        private float _leashTimer;     // counts down while the player is beyond leashRadius; at 0 the enemy de-aggros (back to passive)
        private static readonly Collider[] _sepHits = new Collider[16];
        private static int _activeAttackers;   // shared budget fallback when no CrowdDirector is present (StandoffLunge attacker-cap)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAttackerBudget() => _activeAttackers = 0;   // statics survive fast-enter-playmode; reset each run

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimDead = Animator.StringToHash("Dead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");

        public bool IsAlive => _active && _hp > 0f;

        // ---- ICrowdAgent (crowd-coordination hooks the CrowdDirector reads) ----
        public int AgentId => _agentId;
        public Transform AgentTransform => transform;
        // Ring-waiters are the standoff rushers; chargers rush in and dummies don't fight, so they don't hold a slot.
        // Passive (un-engaged) rushers don't claim a slot either — they're idling at the perimeter, not circling the player.
        public bool WantsSlot => _active && !_dying && _aggro && _data != null && _data.archetype == AttackArchetype.StandoffLunge;
        public float Aggression => _aggression;
        public void AssignSlotAngle(float deg) { _slotAngleDeg = deg; _hasSlot = true; }
        public void ClearSlot() => _hasSlot = false;
        // Alerted by a nearby ally engaging — wake but DON'T re-propagate (one hop, so it can't cascade arena-wide).
        public void Wake() => Aggro(false);

        private void Awake()
        {
            _collider = GetComponent<CapsuleCollider>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
            if (attackTelegraph == null) attackTelegraph = GetComponentInChildren<AttackTelegraph>();
            if (_agent == null) _agent = GetComponent<BehaviorGraphAgent>();
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
            _standoff = data.attackRange * (standoffRangeBase - standoffRangeAggressionSlope * _aggression);    // aggressive enemies crowd in close
            _attackCd = data.attackCooldown * (attackCooldownBase - attackCooldownAggressionSlope * _aggression);   // aggressive enemies strike more often
            _cooldownTimer = UnityEngine.Random.Range(initialCooldownMin, _attackCd);      // desync the crowd
            _slotOffset = UnityEngine.Random.Range(-slotOffsetRange, slotOffsetRange);
            _strafeDir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _staggerTimer = 0f;
            _knockback = Vector3.zero;
            _separationMask = (1 << gameObject.layer) | (player != null ? (1 << player.gameObject.layer) : 0);
            _poise = data.maxPoise;
            _hasSlot = false;
            _hasAttackToken = false;
            _engageDwell = 0f;
            _wasInRange = false;
            _aggro = data.aggroRadius <= 0f;   // legacy (<=0) = aggro from spawn; otherwise spawn PASSIVE until alerted
            _leashTimer = data.leashTime;
            if (CrowdDirector.Instance != null && _agentId < 0) _agentId = CrowdDirector.Instance.Register(this);   // pooled agents re-register each spawn
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
                // The BehaviorGraphAgent drives decisions + movement (via the IEnemyBody verbs); just tick timers here.
                // Position corrections (knockback / separation / clamp) run in LateUpdate, after the tree has moved.
                if (_cooldownTimer > 0f) _cooldownTimer -= dt;
                if (_staggerTimer > 0f) _staggerTimer -= dt;
                TickPoise(dt);
                UpdateAggro(dt);   // resolve aggro BEFORE the dwell — the dwell's InAttackRange check depends on _aggro
                TickEngage(dt);
                return;
            }

            // ---- FSM brain (fallback) — corrections, then the state machine, all over the same body API ----
            TickKnockback(dt);
            ClampOutOfPlayer();   // never stand inside the player (a CharacterController can't push a kinematic body out)
            if (_data.archetype == AttackArchetype.Dummy) { DummyTick(dt); return; }
            transform.position += Separation() * (_data.separationForce * dt);   // crowd separation: don't pile on / clip through

            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            TickPoise(dt);
            UpdateAggro(dt);   // resolve aggro BEFORE the dwell — the dwell's InAttackRange check depends on _aggro
            TickEngage(dt);
            if (_staggerTimer > 0f) { _staggerTimer -= dt; AnimStop(dt); return; }

            switch (_state)
            {
                case State.Seek:    FsmSeek(dt);    break;
                case State.Windup:  FsmWindup(dt);  break;
                case State.Lunge:   FsmLunge(dt);   break;
                case State.Recover: FsmRecover(dt); break;
            }
        }

        private void LateUpdate()
        {
            if (_brain != Brain.BehaviorTree || _dying || !_active || _player == null) return;
            float dt = Time.deltaTime;
            TickKnockback(dt);
            if (_data.archetype != AttackArchetype.Dummy) transform.position += Separation() * (_data.separationForce * dt);
            ClampOutOfPlayer();
        }

        private void OnDisable()
        {
            ReleaseAttackToken();   // pooled/despawned mid-attack → free the token (static fallback too)
            IncomingAttackWarning.Cancel(transform);
            if (_agentId >= 0 && CrowdDirector.Instance != null) CrowdDirector.Instance.Unregister(this);   // frees token + slot, drops from registry
            _agentId = -1;
            _hasSlot = false;
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
            if (TickTelegraph(dt)) { StartAttack(); _state = State.Lunge; }
        }

        private void FsmLunge(float dt)
        {
            if (TickAttack(dt)) { StartRecover(); _state = State.Recover; }
        }

        private void FsmRecover(float dt)
        {
            if (TickRecover(dt)) _state = State.Seek;
        }

        // ---------------- IEnemyBody (the body API — driven by both brains) ----------------
        public bool IsStaggered => _staggerTimer > 0f;

        public bool InAttackRange
        {
            get
            {
                if (!_aggro) return false;   // passive (un-engaged): never "in range" → the brain idles via ApproachStep, never attacks
                if (_data.archetype == AttackArchetype.Dummy) return false;   // dummies never attack
                float dist = PlanarDist();
                if (_data.archetype == AttackArchetype.Charger) return dist <= _data.chargeStartRange;
                return dist <= HoldRing + ringApproachBuffer;
            }
        }

        public bool OffCooldown
        {
            get
            {
                if (_cooldownTimer > 0f) return false;
                if (_staggerTimer > 0f) return false;   // flinching → don't open a new wind-up (the BT brain has no global stagger gate)
                if (_engageDwell > 0f) return false;   // freshly arrived — circle a beat before the first strike
                if (CrowdDirector.Instance != null) return true;   // budget is enforced when the token is pulled in TryStartTelegraph
                // No director: keep the legacy static attacker-cap pre-check (rusher needs a free slot).
                return _data.archetype != AttackArchetype.StandoffLunge || _activeAttackers < _data.maxSimultaneousAttackers;
            }
        }

        public void ApproachStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (_data.archetype == AttackArchetype.Dummy) { FaceTowardPlayer(dt); AnimStop(dt); return; }
            if (!_aggro) { PassiveStep(dt); return; }   // not engaged yet — idle at the perimeter instead of homing in
            if (_data.archetype == AttackArchetype.Charger) { RushStep(dt); return; }

            Vector3 fromPlayer = transform.position - _player.position; fromPlayer.y = 0f;
            float dist = fromPlayer.magnitude;
            Vector3 dirFromPlayer = dist > 0.001f ? fromPlayer / dist : modelRoot.forward;
            FaceTowardPlayer(dt);

            // approach the standoff ring at the director-assigned slot bearing (the crowd spreads to distinct angles);
            // with no director, fall back to this enemy's random slot offset off its current bearing.
            Vector3 ringDir = _hasSlot ? SlotDir() : Quaternion.Euler(0f, _slotOffset, 0f) * dirFromPlayer;
            Vector3 ringTarget = _player.position + ringDir * HoldRing;
            Vector3 toTarget = ringTarget - transform.position; toTarget.y = 0f;
            Vector3 step = toTarget.normalized * _data.moveSpeed * dt;
            if (step.sqrMagnitude > toTarget.sqrMagnitude) step = toTarget;
            transform.position += step;
            SetAnimSpeed(animSpeedApproach, animDampApproach, dt);
            ArmEngageDwell();   // arm the dwell IN the graph tick the instant we reach the ring, BEFORE OffCooldown is read
        }

        public void RepositionStep(float dt)
        {
            if (_staggerTimer > 0f) { AnimStop(dt); return; }
            if (_data.archetype == AttackArchetype.Charger) { ChargerReposition(dt); return; }   // back off to charge range, not point-blank
            if (_data.archetype != AttackArchetype.StandoffLunge) { ApproachStep(dt); return; }   // dummy idles

            Vector3 fromPlayer = transform.position - _player.position; fromPlayer.y = 0f;
            float dist = fromPlayer.magnitude;
            Vector3 dirFromPlayer = dist > 0.001f ? fromPlayer / dist : modelRoot.forward;
            FaceTowardPlayer(dt);
            float strafe = strafeSpeedBase + strafeSpeedAggressionScale * (1f - _aggression);   // cautious enemies circle more
            Vector3 radialFix = dirFromPlayer * (HoldRing - dist) * ringHoldStrength;   // hold the (wider) ring distance

            // With a director slot, circle toward the assigned bearing and settle there (the deliberate encirclement);
            // without one, free-circle in this enemy's strafe direction (legacy).
            float strafeDir = _strafeDir;
            if (_hasSlot)
            {
                float bearing = Mathf.Atan2(fromPlayer.x, fromPlayer.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(bearing, _slotAngleDeg);   // signed, toward the slot
                strafeDir = Mathf.Abs(delta) > 3f ? Mathf.Sign(delta) : 0f;   // settle at the slot, don't jitter
            }
            Vector3 tangent = Vector3.Cross(Vector3.up, dirFromPlayer) * strafeDir;
            transform.position += (tangent * _data.moveSpeed * strafe + radialFix) * dt;
            SetAnimSpeed(animSpeedStrafe, animDampStrafe, dt);
        }

        public bool TryStartTelegraph()
        {
            // Claim an attack token from the crowd director (denied -> keep orbiting/menacing). The Charger's long tell
            // counts as a "heavy" so two big telegraphs don't crescendo. With no director, fall back to the static
            // rusher cap so solo scenes still behave.
            bool heavy = _data.archetype == AttackArchetype.Charger;
            if (CrowdDirector.Instance != null)
            {
                if (!CrowdDirector.Instance.TryBeginAttack(_agentId, 1, heavy)) return false;
                _hasAttackToken = true;
            }
            else if (_data.archetype == AttackArchetype.StandoffLunge)
            {
                if (_activeAttackers >= _data.maxSimultaneousAttackers) return false;
                _activeAttackers++;
                _hasAttackToken = true;
            }
            float windup = _data.archetype == AttackArchetype.Charger ? _data.chargeWindup : _data.attackWindup;
            _stateTimer = windup;
            if (animator != null) animator.SetTrigger(AnimAttack);
            if (attackTelegraph != null) attackTelegraph.Begin(windup);
            IncomingAttackWarning.Begin(transform, windup);   // directional "incoming!" tell on the player's HUD
            return true;
        }

        private void ReleaseAttackToken()
        {
            if (!_hasAttackToken) return;
            _hasAttackToken = false;
            if (CrowdDirector.Instance != null) CrowdDirector.Instance.ReleaseToken(_agentId);   // keep the ring slot
            else _activeAttackers = Mathf.Max(0, _activeAttackers - 1);
        }

        public void CancelTelegraph()
        {
            if (attackTelegraph != null) attackTelegraph.Cancel();
            IncomingAttackWarning.Cancel(transform);
            ReleaseAttackToken();   // aborted wind-up frees the slot
        }

        public bool TickTelegraph(float dt)
        {
            FaceTowardPlayer(dt);   // track during the telegraph; the Attack anim plays the wind-up
            // StandoffLunge: STALK IN during the wind-up so the strike commits from melee range (where the player can
            // hit back to interrupt it) instead of from the wide orbit ring. Eases to telegraphStrikeRange, then holds.
            if (_data.archetype == AttackArchetype.StandoffLunge)
            {
                float dist = PlanarDist();
                if (dist > telegraphStrikeRange)
                {
                    Vector3 to = _player.position - transform.position; to.y = 0f;
                    if (to.sqrMagnitude > 0.0001f)
                    {
                        float step = Mathf.Min(telegraphStepSpeed * dt, dist - telegraphStrikeRange);
                        transform.position += to.normalized * step;
                    }
                }
            }
            _stateTimer -= dt;
            return _stateTimer <= 0f;
        }

        public bool TelegraphShouldAbort
        {
            get
            {
                if (_data.archetype != AttackArchetype.StandoffLunge) return false;   // chargers/brutes commit
                return PlanarDist() > (_standoff + ringApproachBuffer) * telegraphAbortReachMult;   // rusher: player escaped
            }
        }

        public void StartAttack()
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            _lungeDir = to.sqrMagnitude > 0.001f ? to.normalized : modelRoot.forward;
            modelRoot.rotation = Quaternion.LookRotation(_lungeDir);   // lock the dash direction (committed)
            IncomingAttackWarning.Cancel(transform);   // wind-up (dodge window) over — the strike is committed now
            _stateTimer = _data.archetype == AttackArchetype.Charger ? _data.chargeTime : _data.lungeTime;
        }

        public bool TickAttack(float dt)
        {
            bool charger = _data.archetype == AttackArchetype.Charger;
            float speed = charger ? _data.chargeSpeed : _data.lungeSpeed;
            transform.position += _lungeDir * speed * dt;

            // Connect on ACTUAL contact: the dash darts toward the locked direction and hits only when it reaches the
            // player (a readable, dodgeable strike — NOT a hit from a couple metres away). It whiffs if the player got
            // out of the way. lungeTime/chargeTime is the max travel window before the strike gives up.
            if (_playerDmg != null && _playerDmg.IsAlive)
            {
                Vector3 toP = _player.position - transform.position; toP.y = 0f;
                float hitRadius = charger ? _data.chargeHitRadius : _data.contactRange;
                if (toP.magnitude <= hitRadius)
                {
                    float dmg = charger ? _data.chargeDamage : _data.contactDamage;
                    _playerDmg.TakeDamage(dmg, transform.position, _data.guardBreaks);
                    return true;   // connected → done
                }
            }

            _stateTimer -= dt;
            return _stateTimer <= 0f;   // travel window over → done (whiffed if it never reached the player)
        }

        public void StartRecover()
        {
            _stateTimer = _data.archetype == AttackArchetype.Charger ? _data.chargeRecovery : _data.attackRecovery;
        }

        public bool TickRecover(float dt)
        {
            AnimStop(dt);
            _stateTimer -= dt;
            if (_stateTimer <= 0f) { _cooldownTimer = _attackCd; ReleaseAttackToken(); return true; }
            return false;
        }

        // ---------------- helpers ----------------
        private float PlanarDist()
        {
            Vector3 d = _player.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>The engagement/strafe ring (StandoffLunge): the tight standoff distance widened by holdRingMult so the crowd circles in a bigger ring. The lunge-connect stays based on the tight _standoff.</summary>
        private float HoldRing => _standoff * (_data != null ? _data.holdRingMult : 1f);

        private void FaceTowardPlayer(float dt)
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(to);
                modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, target, _data.turnSpeedDeg * dt);
            }
        }

        private void SetAnimSpeed(float value, float damp, float dt) { if (animator != null) animator.SetFloat(AnimSpeed, value, damp, dt); }
        private void AnimStop(float dt) { if (animator != null) animator.SetFloat(AnimSpeed, 0f, animDampStop, dt); }

        /// <summary>World-space outward direction (from the player) at the director-assigned slot bearing (0 = +Z).</summary>
        private Vector3 SlotDir()
        {
            float r = _slotAngleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        /// <summary>Regenerate poise toward the pool max while not broken (so accumulated chip eventually staggers).</summary>
        private void TickPoise(float dt)
        {
            if (_data != null && _data.maxPoise > 0f && _poise < _data.maxPoise)
                _poise = Mathf.Min(_data.maxPoise, _poise + _data.poiseRegen * dt);
        }

        /// <summary>
        /// Arm an engagement dwell on the rising edge of InAttackRange (a rusher just ARRIVED in the ring) so it circles
        /// the player for a beat before its first telegraph. Called from <see cref="ApproachStep"/> (the graph-driven
        /// approach verb) so the dwell is set in the SAME tick the body reaches the ring — BEFORE OffCooldown is read.
        /// The Behavior graph (exec order -50) moves the body in and re-checks OffCooldown all within one tick, so arming
        /// from Update (order 0) would be a frame too late and the first strike would skip the dwell. Idempotent via
        /// <see cref="_wasInRange"/>, so the per-frame <see cref="TickEngage"/> call can't double-arm.
        /// </summary>
        private void ArmEngageDwell()
        {
            if (_data == null) return;
            bool inRange = _data.archetype == AttackArchetype.StandoffLunge && InAttackRange;
            if (inRange && !_wasInRange && _data.engageDwell > 0f)
                _engageDwell = _data.engageDwell * UnityEngine.Random.Range(0.8f, 1.2f);   // circle before the first strike
            _wasInRange = inRange;
        }

        /// <summary>Maintain the dwell each frame: catch the arrival edge (covers spawn-in-range) and count it down. Gating
        /// is via OffCooldown, so both brains keep circling (BT Reposition / FSM RepositionStep) while the dwell holds.</summary>
        private void TickEngage(float dt)
        {
            ArmEngageDwell();
            if (_engageDwell > 0f) _engageDwell -= dt;
        }

        /// <summary>Passive (not yet engaged): hold position and idle — no homing, no player tracking. Waits to be alerted.</summary>
        private void PassiveStep(float dt) => AnimStop(dt);

        /// <summary>
        /// Drive the passive&lt;-&gt;engaged state (runs for both brains). PASSIVE: wake when the player enters aggroRadius.
        /// ENGAGED: leash back to passive if the player stays beyond leashRadius for leashTime. Movement obeys this via the
        /// ApproachStep verb (idles while passive), so neither the FSM nor the graph needs to know about aggro.
        /// </summary>
        private void UpdateAggro(float dt)
        {
            if (_data == null || _data.archetype == AttackArchetype.Dummy) return;   // dummies never fight
            if (_data.aggroRadius <= 0f) return;   // legacy: aggro'd from spawn, never leashes / re-evaluates
            float dist = PlanarDist();
            if (!_aggro)
            {
                if (_data.aggroRadius > 0f && dist <= _data.aggroRadius) Aggro(true);   // player got close → engage + alert allies
                return;
            }
            if (_data.leashRadius > 0f)
            {
                if (dist > _data.leashRadius)
                {
                    _leashTimer -= dt;
                    if (_leashTimer <= 0f) _aggro = false;   // lost interest → back to passive (idles where it stands)
                }
                else _leashTimer = _data.leashTime;
            }
        }

        /// <summary>Engage the player. <paramref name="propagate"/> also raises the alarm to nearby allies (one hop) so a cluster wakes together.</summary>
        private void Aggro(bool propagate)
        {
            if (_aggro || !_active || _dying || _data == null || _data.archetype == AttackArchetype.Dummy) return;
            _aggro = true;
            _leashTimer = _data.leashTime;
            if (propagate && _data.alertRadius > 0f && CrowdDirector.Instance != null)
                CrowdDirector.Instance.AlertNear(transform.position, _data.alertRadius, _agentId);
        }

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
            if (_knockback.sqrMagnitude > 0.0001f)   // ride the big death knockback while the body falls
            {
                transform.position += _knockback * dt;
                _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-knockbackDecayRate * dt));
            }
            _deathTimer -= dt;
            if (_deathTimer <= 0f) { _dying = false; _returnToPool?.Invoke(this); }
        }

        private void RushStep(float dt)
        {
            Vector3 to = _player.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            FaceTowardPlayer(dt);
            if (dist > 0.05f) transform.position += (to / dist) * _data.moveSpeed * dt;
            SetAnimSpeed(animSpeedApproach, animDampApproach, dt);
        }

        // Charger between charges: back off to charge range (eyeing the player) so the next charge is a real run-up.
        private void ChargerReposition(float dt)
        {
            FaceTowardPlayer(dt);
            float hold = _data.chargeStartRange * chargerHoldFraction;
            if (PlanarDist() < hold)
            {
                Vector3 away = transform.position - _player.position; away.y = 0f;
                if (away.sqrMagnitude > 0.01f) transform.position += away.normalized * _data.moveSpeed * dt;
                SetAnimSpeed(animSpeedApproach, animDampApproach, dt);
            }
            else AnimStop(dt);   // at range → hold until off cooldown, then charge
        }

        /// <summary>Hard depenetration from the player's body — kinematic enemies can't be pushed by the player's CharacterController, so we keep ourselves out of it.</summary>
        private void ClampOutOfPlayer()
        {
            Vector3 toEnemy = transform.position - _player.position; toEnemy.y = 0f;
            float min = _data != null ? _data.playerSpacing : 0.6f;
            float d = toEnemy.magnitude;
            if (d > 0.0001f && d < min) transform.position += toEnemy / d * (min - d);
        }

        /// <summary>Passive training-dummy tick (FSM brain): face the player, never move/attack. Stands where knocked so the recoil stays readable.</summary>
        private void DummyTick(float dt)
        {
            FaceTowardPlayer(dt);
            if (_staggerTimer > 0f) _staggerTimer -= dt;
            AnimStop(dt);
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

        // Damage from an unknown source recoils away from the player (the usual melee attacker).
        public void TakeDamage(float amount) => TakeDamage(amount, _player != null ? _player.position : transform.position - transform.forward);

        // Enemies don't block — guardBreak is irrelevant, so delegate to the 2-arg path.
        public void TakeDamage(float amount, Vector3 sourcePos, bool guardBreak) => TakeDamage(amount, sourcePos);

        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            if (!_active) return;
            Aggro(true);   // getting hit always engages it (and wakes nearby allies) — you can't poke a passive enemy for free
            _hp -= amount;
            if (hitFlash != null) hitFlash.Flash();
            CombatAudio.PlayHit(transform.position);   // impact thunk (clips live on CombatAudio)

            // Hit VFX: spark every hit, blood on heavies/kills, at the impact point facing away from the hitter.
            Vector3 hitDir = transform.position - sourcePos; hitDir.y = 0f;
            if (hitDir.sqrMagnitude < 0.0001f) hitDir = modelRoot.forward;
            CombatVfx.Hit(transform.position + Vector3.up * hitFxHeight, hitDir.normalized, _hp <= 0f || amount >= bloodMinDamage);
            if (_data != null)
            {
                Vector3 kb = transform.position - sourcePos; kb.y = 0f;   // recoil away from the hit source
                if (kb.sqrMagnitude > 0.001f) _knockback = kb.normalized * (_data.knockback + amount * _data.knockbackPerDamage);
            }
            if (_hp <= 0f) { Die(); return; }

            // Poise (weight): a light tap chips the pool but doesn't interrupt — only an emptied pool or a heavy
            // (>= bloodMinDamage) breaks into a real stagger. maxPoise <= 0 = legacy: every hit staggers. The recoil
            // knockback above still lands on an absorbed tap, so a light hit reads as impact without an interrupt.
            // EXCEPTION: an enemy hit while COMMITTED to a strike (it holds an attack token — Husk telegraph->lunge OR
            // Charger wind-up->dash) is ALWAYS interrupted: you can STUFF its attack by hitting it first. This is the
            // core melee counterplay. Orbiting taps (no token) still get absorbed by poise. (Dummies never attack.)
            bool committing = _hasAttackToken && _data.archetype != AttackArchetype.Dummy;
            bool staggered = PoiseTracker.Resolve(ref _poise, _data.maxPoise, amount, amount >= bloodMinDamage || committing);
            if (!staggered) return;

            if (attackTelegraph != null) attackTelegraph.Cancel();   // a real stagger interrupts the wind-up tell
            IncomingAttackWarning.Cancel(transform);
            if (animator != null)   // flinch toward the hit
            {
                int dir = HitReaction.Direction(transform.position, modelRoot.forward, modelRoot.right, sourcePos);
                animator.SetInteger(AnimHitDir, dir);
                animator.SetTrigger(AnimHit);
            }
            ReleaseAttackToken();                 // a real stagger interrupts the attack → free the token
            _staggerTimer = _data.staggerDuration > 0f ? _data.staggerDuration : _data.hitReactTime;   // pauses both brains
            _state = State.Seek;                  // a stagger cancels a wind-up / lunge (FSM)
        }

        private void Die()
        {
            ReleaseAttackToken();   // free the token if killed mid-attack
            IncomingAttackWarning.Cancel(transform);   // clear any incoming-attack arrow if killed mid-wind-up
            if (CrowdDirector.Instance != null) CrowdDirector.Instance.Release(_agentId);   // free the ring slot NOW, not after the ~1s death slide
            _hasSlot = false;
            _active = false;
            _dying = true;
            _deathTimer = _data != null ? _data.deathDuration : fallbackDeathDuration;
            if (_data != null && _knockback.sqrMagnitude > 0.0001f) _knockback = _knockback.normalized * _data.deathKnockback;   // BIG knockback on death
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
