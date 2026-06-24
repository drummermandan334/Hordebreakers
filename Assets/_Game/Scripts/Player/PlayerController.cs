using UnityEngine;
using UnityEngine.InputSystem;

namespace Hordebreakers
{
    /// <summary>
    /// Prototype player: camera-relative movement, jump, dodge (i-frames), and an INTERCHANGEABLE melee
    /// combo — every hit reads Light or Heavy, so 3 inputs make routes like L-L-L, L-H-L, L-L-H, H-H-H.
    /// The AnimatorController owns the combo tree (slots 1/2/3, each with a Light and Heavy state) and is
    /// driven by the Light/Heavy triggers; this script tracks the step, drives forward feel, lands hits,
    /// and DETONATES Marks on the heavy finisher (the core mark->detonate loop). Blade orientation comes
    /// from the Synty Prop_R bone (no code blade tricks). Timer-driven, no per-frame alloc.
    /// KBM: WASD move, LMB light, RMB heavy, Space jump, LeftShift dodge. (Throw = ThrowWeapon component.)
    /// Gamepad: left stick move, X light, Y/RT heavy, A jump, B dodge.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("Data")]
        [SerializeField] private PlayerCombatData data;

        [Header("Targeting / FX")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private GameObject detonationVfxPrefab;   // optional one-shot
        [SerializeField] private Transform modelRoot;              // visual to rotate (defaults to self)
        [SerializeField] private Animator animator;               // locomotion + attack anims (defaults to child)
        [SerializeField] private HitFlash hitFlash;               // player flash on taking damage (defaults to child)
        [Tooltip("Front-arc gate for melee: enemies whose direction·facing is below this are ignored.")]
        [SerializeField] private float meleeArcDot = 0.35f;
        [Tooltip("Melee hitbox center is placed this fraction of reach in front of the player.")]
        [SerializeField] private float meleeHitboxCenterFactor = 0.5f;
        [Tooltip("Melee hitbox radius as a fraction of reach.")]
        [SerializeField] private float meleeHitboxRadiusFactor = 0.45f;

        // Attack VFX (slash / stab swipe) is handled by the WeaponVfx component on this object.

        [Header("Forward drive (brief step on a swing, hard stop)")]
        [SerializeField] private float attackStep = 2.5f;         // forward m/s on a slash / heavy
        [SerializeField] private float lungeStep = 4.0f;          // forward m/s on the 3rd hit (the lunge)
        [SerializeField] private float attackStepTime = 0.12f;    // how long the step lasts, then stop
        [Tooltip("How fast the sword-grip override layers blend off during attacks / on in locomotion.")]
        [SerializeField] private float gripBlendSpeed = 10f;
        [Tooltip("Override weight of the SwordArm ready-stance layer in locomotion (~0.7 keeps some base-loco arm).")]
        [SerializeField] private float armLayerWeight = 0.7f;
        [Tooltip("How fast the model turns to face the movement direction (exponential slerp rate).")]
        [SerializeField] private float faceTurnSpeed = 15f;
        [Tooltip("Animator Speed parameter damping time (smooths the locomotion blend).")]
        [SerializeField] private float animSpeedDamp = 0.1f;

        [Header("Jump")]
        [SerializeField] private float jumpSpeed = 7f;            // initial upward velocity
        [SerializeField] private float gravity = 20f;
        [Range(0f, 1f)][SerializeField] private float airControl = 0.7f;  // move authority while airborne

        [Header("Detonation (heavy finisher pops Marks)")]
        [Tooltip("Delay from the heavy-finisher press to the AoE detonation (lines up with the strike).")]
        [SerializeField] private float detonateDelay = 0.14f;
        [Tooltip("Detonation center is placed this fraction of heavyReach in front of the player.")]
        [SerializeField] private float detonateForwardFactor = 0.6f;
        [Tooltip("Seconds the spawned detonation VFX instance lives before being destroyed.")]
        [SerializeField] private float detonateVfxLifetime = 2f;

        [Header("Juice — shake (x=amplitude, y=seconds) + hit-stop (x=seconds, y=timeScale)")]
        [SerializeField] private Vector2 hitShake = new Vector2(0.07f, 0.08f);
        [SerializeField] private Vector2 hitStop = new Vector2(0.03f, 0.3f);
        [SerializeField] private Vector2 finisherShake = new Vector2(0.16f, 0.14f);
        [SerializeField] private Vector2 finisherHitStop = new Vector2(0.07f, 0.12f);
        [Tooltip("Detonation shake: x = base amplitude (grows per mark), y = seconds.")]
        [SerializeField] private Vector2 detonateShake = new Vector2(0.12f, 0.25f);
        [SerializeField] private float detonateShakePerStack = 0.03f;
        [SerializeField] private float detonateHitStop = 0.08f;
        [SerializeField] private Vector2 damageShake = new Vector2(0.18f, 0.18f);
        [SerializeField] private Vector2 damageHitStop = new Vector2(0.05f, 0.25f);

        [Tooltip("DEBUG: player takes no damage (for feel-testing). Turn OFF for real runs.")]
        [SerializeField] private bool invincible = false;

        private CharacterController _cc;
        private Transform _camT;
        private float _hp;
        private AutoWeapon _autoWeapon;

        // timers / state
        private float _dodgeTimer;         // > 0 while dashing
        private float _dodgeCdTimer;
        private bool _invulnerable;
        private Vector3 _dodgeDir;
        private float _detonateTimer;      // counts down to the heavy-finisher detonation
        private bool _detonatePending;
        private int _comboStep;            // 1..3 combo slot (advances while chaining, resets in locomotion)
        private int _bufferedAttack;       // 0 none / 1 light / 2 heavy — press queued mid-swing
        private Vector3 _attackVel;        // brief forward drive on a swing
        private float _attackStepTimer;    // > 0 while the forward step is applied
        private float _verticalVel;
        private bool _grounded;
        private int _gripLayer = -1;       // "RightHandGrip" override layer; held off during attacks
        private int _armLayer = -1;        // "SwordArm" ready-stance layer; held off during attacks

        private readonly Collider[] _hits = new Collider[64];

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimLight = Animator.StringToHash("Light");
        private static readonly int AnimHeavy = Animator.StringToHash("Heavy");
        private static readonly int AnimDodge = Animator.StringToHash("Dodge");
        private static readonly int AnimJump = Animator.StringToHash("Jump");
        private static readonly int AnimGrounded = Animator.StringToHash("Grounded");

        public bool IsAlive => _hp > 0f;
        public float HealthNormalized => data != null ? Mathf.Clamp01(_hp / data.maxHp) : 0f;
        /// <summary>Facing the player rotates toward — used by the throw weapon to aim.</summary>
        public Transform ModelRoot => modelRoot;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
            if (animator != null) { _gripLayer = animator.GetLayerIndex("RightHandGrip"); _armLayer = animator.GetLayerIndex("SwordArm"); }
            if (data == null)
            {
                Debug.LogError("[PlayerController] Assign a PlayerCombatData asset.", this);
                enabled = false;
                return;
            }
            data = Instantiate(data);                 // runtime copy so upgrades don't dirty the source asset
            _autoWeapon = GetComponent<AutoWeapon>();
            _camT = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
            _hp = data.maxHp;
        }

        /// <summary>Apply a level-up upgrade to the runtime data (clones, so the source asset is untouched).</summary>
        public void ApplyUpgrade(UpgradeCard c)
        {
            if (c == null) return;
            switch (c.type)
            {
                case UpgradeType.MoveSpeed: data.moveSpeed += c.magnitude; break;
                case UpgradeType.MaxHealth: data.maxHp += c.magnitude; _hp += c.magnitude; break;
                case UpgradeType.DetonationRadius: data.detonationRadius += c.magnitude; break;
                case UpgradeType.DetonationPower: data.detonationDamagePerStack += c.magnitude; break;
                case UpgradeType.AutoDamage:
                    if (_autoWeapon != null && _autoWeapon.RuntimeData != null) _autoWeapon.RuntimeData.damage += c.magnitude;
                    break;
                case UpgradeType.AutoFireRate:
                    if (_autoWeapon != null && _autoWeapon.RuntimeData != null)
                        _autoWeapon.RuntimeData.fireInterval = Mathf.Max(0.05f, _autoWeapon.RuntimeData.fireInterval - c.magnitude);
                    break;
                case UpgradeType.ExtraMark:
                    if (_autoWeapon != null && _autoWeapon.RuntimeData != null) _autoWeapon.RuntimeData.marksPerHit += Mathf.RoundToInt(c.magnitude);
                    break;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickTimers(dt);
            UpdateGripLayer(dt);

            // Dodge takes over completely while active.
            if (_dodgeTimer > 0f)
            {
                SetAnimSpeed(0f, dt);
                MoveWithVertical(_dodgeDir * data.dodgeSpeed, dt);
                return;
            }

            ResolveDetonate(dt);

            _grounded = _cc.isGrounded;
            if (animator != null) animator.SetBool(AnimGrounded, _grounded);

            Vector3 moveDir = ReadMoveDir();

            bool inAttack = InComboAttack();                                       // base layer is playing an attack clip
            // Chain only once the current swing has played comboChainOpen of its clip, so each swing animates fully.
            bool canAct = !inAttack || (_grounded && AttackPhase() >= data.comboChainOpen);

            if (_bufferedAttack != 0 && canAct) { int b = _bufferedAttack; _bufferedAttack = 0; AttackInput(b == 2); }
            HandleActionInput(moveDir, canAct, inAttack);   // jump/dodge act immediately; attacks chain or buffer

            if (inAttack)
            {
                // Committed to a swing: the attack's forward lunge drives movement and OVERRIDES move input;
                // the player only gets a slight steer (no free gliding mid-attack).
                Vector3 momentum = _attackStepTimer > 0f ? _attackVel : Vector3.zero;
                MoveWithVertical(momentum + moveDir * data.attackSteerSpeed, dt);
                SetAnimSpeed(0f, dt);
            }
            else
            {
                _comboStep = 0;                                                    // back to locomotion: combo resets
                float ctrl = _grounded ? 1f : airControl;
                MoveWithVertical(moveDir * data.moveSpeed * ctrl, dt);
                if (moveDir.sqrMagnitude > 0.01f) FaceDir(moveDir, dt);
                SetAnimSpeed(moveDir.magnitude, dt);
            }
        }

        private void TickTimers(float dt)
        {
            if (_attackStepTimer > 0f) _attackStepTimer -= dt;
            if (_dodgeCdTimer > 0f) _dodgeCdTimer -= dt;
            if (_dodgeTimer > 0f) _dodgeTimer -= dt;
            _invulnerable = _dodgeTimer > 0f && _dodgeTimer > (data.dodgeDuration - data.dodgeIFrames);
        }

        private Vector3 ReadMoveDir()
        {
            Vector2 mv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > mv.sqrMagnitude) mv = ls;   // prefer the stronger source (KBM or pad)
            }
            Vector3 input = new Vector3(mv.x, 0f, mv.y);
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (_camT == null) return input;

            Vector3 fwd = _camT.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = _camT.right; right.y = 0f; right.Normalize();
            return right * input.x + fwd * input.z;
        }

        private void HandleActionInput(Vector3 moveDir, bool canAct, bool inAttack)
        {
            Gamepad pad = Gamepad.current;
            bool jump  = Input.GetKeyDown(KeyCode.Space)     || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool dodge = Input.GetKeyDown(KeyCode.LeftShift) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool light = Input.GetMouseButtonDown(0)         || (pad != null && pad.buttonWest.wasPressedThisFrame);
            bool heavy = Input.GetMouseButtonDown(1)         || (pad != null && (pad.buttonNorth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame));

            if (dodge && _dodgeCdTimer <= 0f) { _bufferedAttack = 0; StartDodge(moveDir); return; }   // dodge cancels a swing
            if (jump && _grounded && !inAttack) DoJump();                     // no jump-canceling a swing

            if (light || heavy)
            {
                if (canAct) AttackInput(heavy);          // idle, or past the chain point: swing now
                else _bufferedAttack = heavy ? 2 : 1;    // mid-swing: buffer for the chain window
            }
        }

        // ---------- Movement ----------
        private void MoveWithVertical(Vector3 horizontalVel, float dt)
        {
            if (_cc.isGrounded && _verticalVel < 0f) _verticalVel = -2f;
            _verticalVel += -gravity * dt;
            Vector3 vel = horizontalVel;
            vel.y = _verticalVel;
            _cc.Move(vel * dt);
        }

        private void DoJump()
        {
            _verticalVel = jumpSpeed;
            _grounded = false;
            if (animator != null) { animator.SetBool(AnimGrounded, false); animator.SetTrigger(AnimJump); }
        }

        private void FaceDir(Vector3 dir, float dt)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, target, 1f - Mathf.Exp(-faceTurnSpeed * dt));
        }

        private void SetAnimSpeed(float target, float dt)
        {
            if (animator != null) animator.SetFloat(AnimSpeed, target, animSpeedDamp, dt);
        }

        /// <summary>
        /// Holds the sword-grip override layers (hand + Prop_R orientation) ON during locomotion and OFF
        /// during attacks/dodges, so the full-body sword-combat clips drive the hand and blade themselves.
        /// </summary>
        private void UpdateGripLayer(float dt)
        {
            if (animator == null) return;
            bool attacking = IsBaseInAttack();
            if (_gripLayer >= 0)
                animator.SetLayerWeight(_gripLayer, attacking ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(_gripLayer), 1f, gripBlendSpeed * dt));
            if (_armLayer >= 0)
                animator.SetLayerWeight(_armLayer, attacking ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(_armLayer), armLayerWeight, gripBlendSpeed * dt));
        }

        /// <summary>True while the base layer is in (or transitioning into) a state tagged "Attack".</summary>
        private bool IsBaseInAttack()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Attack");
        }

        /// <summary>Like IsBaseInAttack but excludes the dodge roll (which has its own movement handling).</summary>
        private bool InComboAttack()
        {
            if (animator == null) return false;
            if (animator.IsInTransition(0))
            {
                var nxt = animator.GetNextAnimatorStateInfo(0);
                return nxt.IsTag("Attack") && !nxt.IsName("Dodge");
            }
            var cur = animator.GetCurrentAnimatorStateInfo(0);
            return cur.IsTag("Attack") && !cur.IsName("Dodge");
        }

        /// <summary>Normalized progress (0..1) of the current base-layer attack clip; 1 when not attacking.</summary>
        private float AttackPhase()
        {
            if (animator == null) return 1f;
            if (animator.IsInTransition(0)) return animator.GetNextAnimatorStateInfo(0).IsTag("Attack") ? 0f : 1f;
            var cur = animator.GetCurrentAnimatorStateInfo(0);
            return cur.IsTag("Attack") ? Mathf.Repeat(cur.normalizedTime, 1f) : 1f;
        }

        private void StepForward(float speed)
        {
            _attackVel = modelRoot.forward * speed;
            _attackStepTimer = attackStepTime;
        }

        // ---------- Dodge ----------
        private void StartDodge(Vector3 moveDir)
        {
            _attackStepTimer = 0f; _attackVel = Vector3.zero;
            _dodgeDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : modelRoot.forward;
            _dodgeTimer = data.dodgeDuration;
            _dodgeCdTimer = data.dodgeCooldown;
            modelRoot.rotation = Quaternion.LookRotation(_dodgeDir);
            if (animator != null) animator.SetTrigger(AnimDodge);
        }

        // ---------- Interchangeable combo ----------
        // Each press advances one slot (1->2->3) within the combo window, firing the Light or Heavy
        // trigger; the AnimatorController routes slot+input to the right clip. The heavy 3rd hit detonates.
        private void AttackInput(bool heavy)
        {
            _comboStep = (InComboAttack() && _comboStep < 3) ? _comboStep + 1 : 1;   // chaining advances the slot; a fresh attack resets it
            SoftTargetFace();
            if (animator != null) animator.SetTrigger(heavy ? AnimHeavy : AnimLight);

            bool finisher = _comboStep >= 3;
            StepForward(finisher ? lungeStep : attackStep);

            float reach = heavy ? data.heavyReach : data.lightReach;
            float dmg = heavy ? data.heavyDamage : data.lightDamage;
            int hits = MeleeHit(reach, dmg);

            if (heavy && finisher) { _detonatePending = true; _detonateTimer = detonateDelay; }   // pop Marks
            if (hits > 0 || (heavy && finisher)) HitJuice(finisher || heavy);
        }

        private void ResolveDetonate(float dt)
        {
            if (!_detonatePending) return;
            _detonateTimer -= dt;
            if (_detonateTimer > 0f) return;

            _detonatePending = false;
            Vector3 center = transform.position + modelRoot.forward * (data.heavyReach * detonateForwardFactor) + Vector3.up;
            int stacks = Detonate(center, data.detonationRadius);    // consume + burst marks
            if (detonationVfxPrefab != null) Destroy(Instantiate(detonationVfxPrefab, center, Quaternion.identity), detonateVfxLifetime);

            // Juice scales with the payoff: bigger detonation -> bigger shake, brief hit-stop on a real pop.
            if (ThirdPersonCamera.Instance != null)
                ThirdPersonCamera.Instance.Shake(detonateShake.x + Mathf.Min(stacks, 20) * detonateShakePerStack, detonateShake.y);
            if (stacks > 0 && GameManager.Instance != null)
                GameManager.Instance.HitStop(detonateHitStop);
        }

        // ---------- FX / feedback ----------
        /// <summary>Impact feedback (weight): camera shake + brief hit-stop, stronger on the finisher.</summary>
        private void HitJuice(bool finisher)
        {
            Vector2 shake = finisher ? finisherShake : hitShake;
            Vector2 stop = finisher ? finisherHitStop : hitStop;
            if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(shake.x, shake.y);
            if (GameManager.Instance != null) GameManager.Instance.HitStop(stop.x, stop.y);
        }

        // ---------- Hit helpers (non-alloc, front-arc) ----------
        private int MeleeHit(float reach, float damage)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 fwd = modelRoot.forward;
            Vector3 c = origin + fwd * (reach * meleeHitboxCenterFactor);
            int n = Physics.OverlapSphereNonAlloc(c, reach * meleeHitboxRadiusFactor, _hits, enemyMask);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                Collider col = _hits[i];
                Vector3 to = col.transform.position - origin; to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Dot(to.normalized, fwd) < meleeArcDot) continue;  // front arc only
                if (col.TryGetComponent(out IDamageable d) && d.IsAlive) { d.TakeDamage(damage); hits++; }
            }
            return hits;
        }

        private int Detonate(Vector3 center, float radius)
        {
            int total = 0;
            int n = Physics.OverlapSphereNonAlloc(center, radius, _hits, enemyMask);
            for (int i = 0; i < n; i++)
            {
                Collider col = _hits[i];
                int stacks = col.TryGetComponent(out Markable m) ? m.ConsumeAll() : 0;
                total += stacks;
                if (col.TryGetComponent(out IDamageable d) && d.IsAlive)
                    d.TakeDamage(data.detonationFlatDamage + stacks * data.detonationDamagePerStack);
            }
            return total;
        }

        private void SoftTargetFace()
        {
            Transform t = FindNearest(data.softTargetRange);
            if (t == null) return;
            Vector3 dir = t.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(dir);
        }

        private Transform FindNearest(float range)
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, range, _hits, enemyMask);
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float sq = (_hits[i].transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = _hits[i].transform; }
            }
            return best;
        }

        // ---------- IDamageable ----------
        public void TakeDamage(float amount)
        {
            if (invincible || _invulnerable || _hp <= 0f) return;
            _hp -= amount;
            if (hitFlash != null) hitFlash.Flash();
            if (ThirdPersonCamera.Instance != null) ThirdPersonCamera.Instance.Shake(damageShake.x, damageShake.y);
            if (GameManager.Instance != null) GameManager.Instance.HitStop(damageHitStop.x, damageHitStop.y);
            if (_hp <= 0f) Die();
        }

        private void Die()
        {
            Debug.Log("[PlayerController] Player down. (Prototype: reload the scene to retry.)");
            gameObject.SetActive(false);
        }
    }
}
