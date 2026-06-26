using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hordebreakers
{
    /// <summary>
    /// Prototype player: camera-relative movement, jump, dodge (i-frames), and an INTERCHANGEABLE melee
    /// combo — every hit reads Light or Heavy, so 3 inputs make routes like L-L-L, L-H-L, L-L-H, H-H-H.
    /// The AnimatorController owns the combo tree (slots 1/2/3, each with a Light and Heavy state) and is
    /// driven by the Light/Heavy triggers; this script tracks the step, drives forward feel, and lands hits
    /// (the heavy finisher is a lunging strike). Blade orientation comes from the Synty Prop_R bone (no code
    /// blade tricks). Timer-driven, no per-frame alloc.
    /// KBM: WASD move, LMB light, RMB heavy, Space jump, LeftShift dodge. (Throw = ThrowWeapon component.)
    /// Gamepad: left stick move, X light, Y/RT heavy, A jump, B dodge.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        [Header("Data")]
        [SerializeField] private PlayerCombatData data;
        [Tooltip("Which class this character is — gates which augments the level-up draft can roll.")]
        [SerializeField] private CharacterClass characterClass = CharacterClass.BattleSorcerer;

        [Header("Targeting / FX")]
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private Transform modelRoot;              // visual to rotate (defaults to self)
        [SerializeField] private Animator animator;               // locomotion + attack anims (defaults to child)
        [SerializeField] private HitFlash hitFlash;               // player flash on taking damage (defaults to child)
        [Tooltip("Front-arc gate for melee: enemies whose direction·facing is below this are ignored.")]
        [SerializeField] private float meleeArcDot = 0.35f;

        [Header("Aim assist (subtle, rotation-only; 0 = fully manual)")]
        [Range(0f, 1f)]
        [Tooltip("Per-swing nudge toward the nearest enemy in the aim cone so attacks connect in a crowd. 0 = no assist (fully manual). Rotation only — never a pull, lock-on, or snap. Default low; tune by feel.")]
        [SerializeField] private float aimAssistStrength = 0.25f;
        [Tooltip("Half-angle (degrees) of the narrow front cone the assist will turn toward. Enemies outside it are ignored.")]
        [SerializeField] private float aimAssistConeDeg = 25f;
        [Tooltip("Max distance the aim assist looks for a target.")]
        [SerializeField] private float aimAssistRange = 4f;
        [Tooltip("Melee hitbox center is placed this fraction of reach in front of the player.")]
        [SerializeField] private float meleeHitboxCenterFactor = 0.5f;
        [Tooltip("Melee hitbox radius as a fraction of reach.")]
        [SerializeField] private float meleeHitboxRadiusFactor = 0.45f;
        [Range(0f, 1f)]
        [Tooltip("Fraction of a LIGHT swing clip at which the hit lands — resolved AFTER the forward lunge, so the hitbox is where the blade visibly is (not where you stood when you pressed).")]
        [SerializeField] private float meleeContactPhase = 0.35f;
        [Tooltip("Phase of the cast (mirrored L3) clip at which the magical bolt fires from the left hand.")]
        [SerializeField] private float castContactPhase = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Contact phase for HEAVY swings — later, to match the heavier wind-up. KEEP IN SYNC with WeaponVfx.heavyStrikeFraction so the hit and the slash VFX land together.")]
        [SerializeField] private float heavyMeleeContactPhase = 0.5f;

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

        [Header("Juice — shake (x=amplitude, y=seconds) + hit-stop (x=seconds, y=timeScale)")]
        [SerializeField] private Vector2 hitShake = new Vector2(0.07f, 0.08f);
        [SerializeField] private Vector2 hitStop = new Vector2(0.03f, 0.3f);
        [SerializeField] private Vector2 finisherShake = new Vector2(0.16f, 0.14f);
        [SerializeField] private Vector2 finisherHitStop = new Vector2(0.07f, 0.12f);
        [SerializeField] private Vector2 damageShake = new Vector2(0.18f, 0.18f);
        [SerializeField] private Vector2 damageHitStop = new Vector2(0.05f, 0.25f);
        [Tooltip("Min seconds between player hit-react flinches, so a dense horde can't stunlock the flinch animation.")]
        [SerializeField] private float hitReactCooldown = 0.5f;

        [Tooltip("DEBUG: player takes no damage (for feel-testing). Turn OFF for real runs.")]
        [SerializeField] private bool invincible = false;

        [Header("Musou VFX")]
        [Tooltip("Fire/explosion VFX spawned at the player when Musou fires.")]
        [SerializeField] private GameObject musouVfx;
        [SerializeField] private float musouVfxScale = 1f;

        [Header("Starting abilities")]
        [Tooltip("Abilities the player starts the run with — slot order = list order, cast with 1-4 / d-pad. Augments grant more on top.")]
        [SerializeField] private List<AbilityDefinition> startingAbilities = new List<AbilityDefinition>();

        private CharacterController _cc;
        private Transform _camT;
        private ThrowWeapon _throw;
        private PlayerLoadout _loadout;
        private float _hp;
        private float _stamina;
        private float _staminaRegenDelayTimer;   // > 0 while regen is held off after a spend
        private bool _dodgeStumble;              // the current dodge was a too-tired 'stumble' (weaker)
        private bool _blocking;
        private float _musou;
        private bool _musouActive;
        private float _musouTimer;
        private bool _hasBlockParam, _hasMusouParam;   // does the Animator define these (else skip to avoid warnings)

        // timers / state
        private float _dodgeTimer;         // > 0 while dashing
        private float _dodgeCdTimer;
        private float _hitReactCdTimer;    // > 0 while the player hit-react flinch is on cooldown
        private bool _invulnerable;
        private Vector3 _dodgeDir;
        private int _comboStep;            // 1..3 combo slot (advances while chaining, resets in locomotion)
        private int _bufferedAttack;       // 0 none / 1 light / 2 heavy — press queued mid-swing
        private Vector3 _attackVel;        // brief forward drive on a swing
        private float _attackStepTimer;    // > 0 while the forward step is applied
        private float _pendingReach, _pendingDamage;   // the armed swing's hit, resolved at its contact phase
        private bool _pendingHeavy, _pendingFinisher;
        private bool _swingHitResolved;    // true once the current swing has landed its hit (one hit per swing)
        private int _lastAttackStateHash;  // fullPathHash of the swing's attack state — arms one hit per swing (input-independent)
        private bool _lockCycleArmed = true;   // re-armed when the right stick re-centers, so one flick = one target switch
        private bool _castSwing;               // the current swing is a magic-bolt cast (fires a projectile, not a melee hit)
        private float _castCdTimer;
        private Transform _leftHand;           // Hand_L bone — the bolt's spawn point
        private float _verticalVel;
        private bool _grounded;
        private int _gripLayer = -1;       // "RightHandGrip" override layer; held off during attacks
        private int _armLayer = -1;        // "SwordArm" ready-stance layer; held off during attacks

        private readonly Collider[] _hits = new Collider[64];
        private const int MAX_ABILITY_SLOTS = 4;
        private readonly float[] _abilityCooldowns = new float[MAX_ABILITY_SLOTS];   // per granted-ability cooldown timers

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimLight = Animator.StringToHash("Light");
        private static readonly int AnimHeavy = Animator.StringToHash("Heavy");
        private static readonly int AnimDodge = Animator.StringToHash("Dodge");
        private static readonly int AnimJump = Animator.StringToHash("Jump");
        private static readonly int AnimGrounded = Animator.StringToHash("Grounded");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");
        private static readonly int AnimBlock = Animator.StringToHash("Block");
        private static readonly int AnimMusou = Animator.StringToHash("Musou");
        private static readonly int AnimThrow = Animator.StringToHash("Throw");

        public bool IsAlive => _hp > 0f;
        public float HealthNormalized => data != null ? Mathf.Clamp01(_hp / data.maxHp) : 0f;
        public float StaminaNormalized => data != null && data.maxStamina > 0f ? Mathf.Clamp01(_stamina / data.maxStamina) : 0f;
        public float MusouNormalized => data != null && data.maxMusou > 0f ? Mathf.Clamp01(_musou / data.maxMusou) : 0f;
        public bool MusouReady => data != null && _musou >= data.maxMusou;
        /// <summary>Facing the player rotates toward — used by the throw weapon to aim.</summary>
        public Transform ModelRoot => modelRoot;
        /// <summary>The player's run-scoped build (taken augments, weapons, abilities). Wrapped by the augment context.</summary>
        public PlayerLoadout Loadout => _loadout;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (modelRoot == null) modelRoot = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
            _throw = GetComponentInChildren<ThrowWeapon>();
            if (animator != null) { _gripLayer = animator.GetLayerIndex("RightHandGrip"); _armLayer = animator.GetLayerIndex("SwordArm"); _hasBlockParam = HasParam("Block"); _hasMusouParam = HasParam("Musou"); }
            if (animator != null && animator.isHuman) _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (data == null)
            {
                Debug.LogError("[PlayerController] Assign a PlayerCombatData asset.", this);
                enabled = false;
                return;
            }
            data = Instantiate(data);                 // runtime copy so upgrades don't dirty the source asset
            _camT = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
            _hp = data.maxHp;
            _stamina = data.maxStamina;
            _loadout = new PlayerLoadout(characterClass, _throw);   // run-scoped build model
            for (int i = 0; i < startingAbilities.Count; i++) _loadout.GrantAbility(startingAbilities[i]);   // sorcerer's base spell kit
        }

        /// <summary>Apply a level-up augment: runs every effect against the runtime stats + build loadout, then records it.</summary>
        public void ApplyAugment(Augment augment)
        {
            if (augment == null || _loadout == null) return;
            AugmentContext ctx = new AugmentContext(this, data, _loadout);
            augment.Apply(in ctx);
            _loadout.Record(augment);
        }

        /// <summary>Restore health, capped at current max. Used by level-up upgrade effects.</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(_hp + amount, data.maxHp);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickTimers(dt);
            UpdateGripLayer(dt);

            // Musou finisher owns the player completely (rooted, i-frames) while active.
            if (_musouActive) { TickMusou(dt); return; }

            // Dodge takes over completely while active.
            if (_dodgeTimer > 0f)
            {
                SetAnimSpeed(0f, dt);
                float dodgeSpeed = data.dodgeSpeed * (_dodgeStumble ? data.stumbleDodgeSpeedMult : 1f);
                MoveWithVertical(_dodgeDir * dodgeSpeed, dt);
                return;
            }

            _grounded = _cc.isGrounded;
            if (animator != null) animator.SetBool(AnimGrounded, _grounded);

            HandleLockInput();   // acquire / drop / switch the focus target before movement reads it
            bool locked = TargetLock.Instance != null && TargetLock.Instance.HasTarget;
            Vector3 lockDir = Vector3.zero;
            if (locked)
            {
                Vector3 toT = TargetLock.Instance.Target.position - transform.position; toT.y = 0f;
                if (toT.sqrMagnitude > 0.0004f) lockDir = toT.normalized; else locked = false;
            }
            Vector3 moveDir = locked ? LockedMoveDir(lockDir) : ReadMoveDir();

            bool inAttack = InComboAttack();                                       // base layer is playing an attack clip
            // Chain only once the current swing has played comboChainOpen of its clip, so each swing animates fully.
            bool canAct = !inAttack || (_grounded && AttackPhase() >= data.comboChainOpen);

            bool firedBuffered = false;
            if (_bufferedAttack != 0 && canAct) { int b = _bufferedAttack; _bufferedAttack = 0; AttackInput(b == 2); firedBuffered = true; }
            // If a buffered attack already fired this frame, a fresh press BUFFERS for the next chain instead of also
            // firing now — otherwise buffer + same-frame press double-fires (two combo advances + two whooshes).
            HandleActionInput(moveDir, canAct && !firedBuffered, inAttack);   // jump/dodge act immediately; attacks chain or buffer

            // Musou: full meter + grounded + not mid-swing → unleash the screen-clear finisher (it owns the next frames).
            if (TryMusou(inAttack)) return;

            // Block is a grounded defensive stance: held, and not while attacking.
            _blocking = ReadBlockHeld() && _grounded && !inAttack;

            // Grand abilities (Task A): instant-cast when not mid-swing (dodge/musou already returned above).
            if (!inAttack) HandleAbilityInput();

            // Arm exactly one hit per swing the instant a NEW attack state begins (its fullPathHash changes). This ties
            // the hit to the actual swing animation — without it, chaining re-armed the hit while the animator was still
            // in the PREVIOUS swing at a late phase, so it resolved instantly on the button press with the hitbox in the
            // wrong place (the early/inconsistent hits). Mirrors WeaponVfx's per-swing detection.
            int atkHash = CurrentAttackStateHash();
            if (atkHash != _lastAttackStateHash) { _lastAttackStateHash = atkHash; if (atkHash != 0) _swingHitResolved = false; }

            if (inAttack)
            {
                // Land the swing's hit once it reaches its contact phase (after the lunge has carried us in).
                // Heavies land later so the impact stays synced with the heavier swing's slash VFX.
                float contactPhase = _castSwing ? castContactPhase : (_pendingHeavy ? heavyMeleeContactPhase : meleeContactPhase);
                if (!_swingHitResolved && AttackPhase() >= contactPhase)
                {
                    if (_castSwing) ResolveCast(); else ResolveSwingHit();
                }

                // Committed to a swing: the attack's forward lunge drives movement and OVERRIDES move input;
                // the player only gets a slight steer (no free gliding mid-attack).
                Vector3 momentum = _attackStepTimer > 0f ? _attackVel : Vector3.zero;
                MoveWithVertical(momentum + moveDir * data.attackSteerSpeed, dt);
                SetAnimSpeed(0f, dt);
            }
            else if (_blocking)
            {
                _comboStep = 0;
                float bctrl = data.blockMoveSpeedMult;                             // blocking slows you — not a free turtle
                MoveWithVertical(moveDir * data.moveSpeed * bctrl, dt);
                if (locked) FaceDir(lockDir, dt); else if (moveDir.sqrMagnitude > 0.01f) FaceDir(moveDir, dt);
                SetAnimSpeed(moveDir.magnitude * bctrl, dt);
            }
            else
            {
                _comboStep = 0;                                                    // back to locomotion: combo resets
                float ctrl = _grounded ? 1f : airControl;
                MoveWithVertical(moveDir * data.moveSpeed * ctrl, dt);
                if (locked) FaceDir(lockDir, dt); else if (moveDir.sqrMagnitude > 0.01f) FaceDir(moveDir, dt);
                SetAnimSpeed(moveDir.magnitude, dt);
            }

            if (_hasBlockParam) animator.SetBool(AnimBlock, _blocking);
        }

        private void TickTimers(float dt)
        {
            if (_attackStepTimer > 0f) _attackStepTimer -= dt;
            if (_dodgeCdTimer > 0f) _dodgeCdTimer -= dt;
            if (_hitReactCdTimer > 0f) _hitReactCdTimer -= dt;
            if (_dodgeTimer > 0f) _dodgeTimer -= dt;
            for (int i = 0; i < _abilityCooldowns.Length; i++) { if (_abilityCooldowns[i] > 0f) _abilityCooldowns[i] -= dt; }
            if (_castCdTimer > 0f) _castCdTimer -= dt;
            float iFrames = data.dodgeIFrames * (_dodgeStumble ? data.stumbleDodgeIFrameMult : 1f);
            _invulnerable = _dodgeTimer > 0f && _dodgeTimer > (data.dodgeDuration - iFrames);

            // Stamina regen: kicks in after a short delay once you stop spending (movement never costs stamina).
            if (_staminaRegenDelayTimer > 0f) _staminaRegenDelayTimer -= dt;
            else if (_stamina < data.maxStamina) _stamina = Mathf.Min(data.maxStamina, _stamina + data.staminaRegen * dt);
        }

        /// <summary>Spend stamina if affordable; returns false (no spend) when too tired. Resets the regen delay on a spend.</summary>
        private bool SpendStamina(float cost)
        {
            if (_stamina < cost) return false;
            _stamina -= cost;
            _staminaRegenDelayTimer = data.staminaRegenDelay;
            return true;
        }

        // ---------- Grand abilities (Task A) ----------
        private void HandleAbilityInput()
        {
            if (_loadout == null) return;
            IReadOnlyList<AbilityDefinition> abilities = _loadout.Abilities;
            if (abilities.Count == 0) return;

            int slot = -1;
            Gamepad pad = Gamepad.current;
            if (Input.GetKeyDown(KeyCode.Alpha1) || (pad != null && pad.dpad.up.wasPressedThisFrame)) slot = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || (pad != null && pad.dpad.left.wasPressedThisFrame)) slot = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || (pad != null && pad.dpad.right.wasPressedThisFrame)) slot = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha4) || (pad != null && pad.dpad.down.wasPressedThisFrame)) slot = 3;
            if (slot >= 0) TryActivateAbility(slot);
        }

        /// <summary>
        /// Authoritative ability activation (the single entry point a host can drive for co-op): validates slot,
        /// cooldown, and stamina, then casts. Returns true if it fired.
        /// </summary>
        public bool TryActivateAbility(int slot)
        {
            if (_loadout == null || slot < 0 || slot >= MAX_ABILITY_SLOTS) return false;
            IReadOnlyList<AbilityDefinition> abilities = _loadout.Abilities;
            if (slot >= abilities.Count) return false;
            AbilityDefinition ability = abilities[slot];
            if (ability == null || _abilityCooldowns[slot] > 0f) return false;
            if (!SpendStamina(ability.staminaCost)) return false;
            ability.Activate(this);
            _abilityCooldowns[slot] = ability.cooldown;
            return true;
        }

        /// <summary>Ability hook: damage every enemy within <paramref name="radius"/> of a point. Returns enemies hit.</summary>
        public int DealAreaDamage(Vector3 center, float radius, float damage)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, _hits, enemyMask);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].TryGetComponent(out IDamageable d) && d.IsAlive) { d.TakeDamage(damage, center); hits++; }
            }
            return hits;
        }

        private bool HasParam(string name)
        {
            for (int i = 0; i < animator.parameters.Length; i++)
            {
                if (animator.parameters[i].name == name) return true;
            }
            return false;
        }

        // ---------- Block ----------
        private bool ReadBlockHeld()
        {
            Gamepad pad = Gamepad.current;
            return Input.GetKey(KeyCode.F) || (pad != null && pad.leftTrigger.ReadValue() > 0.5f);
        }

        /// <summary>True if the hit came from within the frontal block cone (you can't block your back).</summary>
        private bool IsFrontalHit(Vector3 sourcePos)
        {
            Vector3 toSource = sourcePos - transform.position; toSource.y = 0f;
            if (toSource.sqrMagnitude < 0.0001f) return true;
            return Vector3.Dot(toSource.normalized, modelRoot.forward) >= data.blockFrontDot;
        }

        // ---------- Musou ----------
        private bool TryMusou(bool inAttack)
        {
            Gamepad pad = Gamepad.current;
            bool pressed = Input.GetKeyDown(KeyCode.R) || (pad != null && pad.leftShoulder.wasPressedThisFrame);   // R3 freed for lock-on; Musou now R / LB
            if (!pressed || inAttack || !_grounded || _musou < data.maxMusou) return false;
            StartMusou();
            return true;
        }

        private void StartMusou()
        {
            _musouActive = true;
            _musouTimer = data.musouDuration;
            _musou = 0f;
            _attackStepTimer = 0f; _attackVel = Vector3.zero;
            _bufferedAttack = 0;
            if (_hasMusouParam && animator != null) animator.SetTrigger(AnimMusou);

            // Screen-clear pulse: damage everything in radius; the player position is the source so enemies recoil outward.
            Vector3 origin = transform.position;
            int n = Physics.OverlapSphereNonAlloc(origin, data.musouRadius, _hits, enemyMask);
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].TryGetComponent(out IDamageable d) && d.IsAlive) d.TakeDamage(data.musouDamage, origin);
            }
            PlayerCameraRig.Shake(finisherShake.x * 5f, finisherShake.y * 2.5f);   // big ultimate jolt
            if (GameManager.Instance != null) GameManager.Instance.HitStop(finisherHitStop.x, finisherHitStop.y);
            if (musouVfx != null)
            {
                GameObject go = Instantiate(musouVfx, origin, Quaternion.identity);
                if (!Mathf.Approximately(musouVfxScale, 1f)) go.transform.localScale *= musouVfxScale;
                Destroy(go, 4f);
            }
        }

        private void TickMusou(float dt)
        {
            SetAnimSpeed(0f, dt);
            MoveWithVertical(Vector3.zero, dt);   // rooted + committed; gravity still resolves
            _musouTimer -= dt;
            if (_musouTimer <= 0f) _musouActive = false;
        }

        private void GainMusou(float amount)
        {
            if (amount <= 0f || _musouActive) return;
            _musou = Mathf.Min(data.maxMusou, _musou + amount);
        }

        private Vector2 ReadStick()
        {
            Vector2 mv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (ls.sqrMagnitude > mv.sqrMagnitude) mv = ls;   // prefer the stronger source (KBM or pad)
            }
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            return mv;
        }

        private Vector3 ReadMoveDir()
        {
            Vector2 mv = ReadStick();
            Vector3 input = new Vector3(mv.x, 0f, mv.y);
            if (_camT == null) return input;
            Vector3 fwd = _camT.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = _camT.right; right.y = 0f; right.Normalize();
            return right * input.x + fwd * input.z;
        }

        /// <summary>Strafe-lock movement: the stick is target-relative — up = toward the target, sideways = circle it.</summary>
        private Vector3 LockedMoveDir(Vector3 toTargetDir)
        {
            Vector2 mv = ReadStick();
            Vector3 right = Vector3.Cross(Vector3.up, toTargetDir);
            return toTargetDir * mv.y + right * mv.x;
        }

        private void HandleActionInput(Vector3 moveDir, bool canAct, bool inAttack)
        {
            Gamepad pad = Gamepad.current;
            bool jump  = Input.GetKeyDown(KeyCode.Space)     || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool dodge = Input.GetKeyDown(KeyCode.LeftShift) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            bool light = Input.GetMouseButtonDown(0)         || (pad != null && pad.buttonWest.wasPressedThisFrame);
            bool heavy = Input.GetMouseButtonDown(1)         || (pad != null && (pad.buttonNorth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame));
            bool cast  = Input.GetKeyDown(KeyCode.Q)         || (pad != null && pad.rightShoulder.wasPressedThisFrame);   // magical bolt (Q / RB)

            if (dodge && _dodgeCdTimer <= 0f) { _bufferedAttack = 0; StartDodge(moveDir); return; }   // dodge cancels a swing
            if (jump && _grounded && !inAttack) DoJump();                     // no jump-canceling a swing

            if (light || heavy)
            {
                if (canAct) AttackInput(heavy);          // idle, or past the chain point: swing now
                else _bufferedAttack = heavy ? 2 : 1;    // mid-swing: buffer for the chain window
            }
            else if (cast && canAct && _castCdTimer <= 0f) CastInput();   // bolt only when free to act (not mid-swing)
        }

        /// <summary>Begin a magic-bolt cast: the mirrored-L3 thrust; the bolt fires from the left hand at the contact phase.</summary>
        private void CastInput()
        {
            _castSwing = true;
            _castCdTimer = _throw != null ? _throw.Cooldown : 0.55f;
            AimFace();
            if (animator != null) animator.SetTrigger(AnimThrow);
        }

        /// <summary>Lock-on input: Tab / R3 toggles the focus; Q-E or a right-stick flick switches targets while locked.</summary>
        private void HandleLockInput()
        {
            TargetLock tl = TargetLock.Instance;
            if (tl == null) return;
            Gamepad pad = Gamepad.current;
            if (Input.GetKeyDown(KeyCode.Tab) || (pad != null && pad.rightStickButton.wasPressedThisFrame)) tl.Toggle();
            if (!tl.HasTarget) { _lockCycleArmed = true; return; }

            float scroll = Input.mouseScrollDelta.y;   // mouse wheel switches targets (KBM); Q is the bolt now
            if (scroll > 0.1f) tl.Cycle(1); else if (scroll < -0.1f) tl.Cycle(-1);
            float flick = pad != null ? pad.rightStick.ReadValue().x : 0f;   // right stick is free while locked (no free-look)
            if (Mathf.Abs(flick) < 0.4f) _lockCycleArmed = true;
            else if (_lockCycleArmed && Mathf.Abs(flick) > 0.7f) { tl.Cycle(flick > 0f ? 1 : -1); _lockCycleArmed = false; }
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
            bool suppress = IsBaseInAttack() || IsBaseInHitReact();   // let full-body attack/flinch clips own the arm
            if (_gripLayer >= 0)
                animator.SetLayerWeight(_gripLayer, suppress ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(_gripLayer), 1f, gripBlendSpeed * dt));
            if (_armLayer >= 0)
                animator.SetLayerWeight(_armLayer, suppress ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(_armLayer), armLayerWeight, gripBlendSpeed * dt));
        }

        /// <summary>True while the base layer is in (or transitioning into) a directional hit-react state.</summary>
        private bool IsBaseInHitReact()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("HitReact")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("HitReact");
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

        /// <summary>fullPathHash of the current (or transitioning-into) attack state; 0 when not attacking. Used to arm one hit per swing.</summary>
        private int CurrentAttackStateHash()
        {
            if (animator == null) return 0;
            if (animator.IsInTransition(0))
            {
                var nxt = animator.GetNextAnimatorStateInfo(0);
                return nxt.IsTag("Attack") ? nxt.fullPathHash : 0;
            }
            var cur = animator.GetCurrentAnimatorStateInfo(0);
            return cur.IsTag("Attack") ? cur.fullPathHash : 0;
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
            // Empty != defenseless: too tired to pay full cost still gets a weaker 'stumble' dodge, never a lockout.
            _dodgeStumble = !SpendStamina(data.dodgeStaminaCost);
            if (_dodgeStumble) _staminaRegenDelayTimer = data.staminaRegenDelay;
            _dodgeDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : modelRoot.forward;
            _dodgeTimer = data.dodgeDuration;
            _dodgeCdTimer = data.dodgeCooldown;
            modelRoot.rotation = Quaternion.LookRotation(_dodgeDir);
            if (animator != null) animator.SetTrigger(AnimDodge);
        }

        // ---------- Interchangeable combo ----------
        // Each press advances one slot (1->2->3) within the combo window, firing the Light or Heavy
        // trigger; the AnimatorController routes slot+input to the right clip. The 3rd hit is the lunge.
        private void AttackInput(bool heavy)
        {
            float cost = heavy ? data.heavyStaminaCost : data.lightStaminaCost;
            if (!SpendStamina(cost)) return;   // too tired to swing — movement is free, so reposition while stamina regens

            _castSwing = false;   // a melee swing clears any pending cast
            CombatAudio.PlaySwing(transform.position);   // swing whoosh (clips live on CombatAudio)
            _comboStep = (InComboAttack() && _comboStep < 3) ? _comboStep + 1 : 1;   // chaining advances the slot; a fresh attack resets it
            AimFace();
            if (animator != null) animator.SetTrigger(heavy ? AnimHeavy : AnimLight);

            bool finisher = _comboStep >= 3;
            StepForward(finisher ? lungeStep : attackStep);

            // Stage this swing's hit params; the hit is ARMED + resolved in Update off the animator state change
            // (CurrentAttackStateHash), so it lands at this swing's real contact phase — never on the press frame.
            _pendingHeavy = heavy;
            _pendingFinisher = finisher;
            _pendingReach = heavy ? data.heavyReach : data.lightReach;
            _pendingDamage = heavy ? data.heavyDamage : data.lightDamage;
        }

        /// <summary>Applies the armed swing's melee hit once, at its contact phase.</summary>
        private void ResolveSwingHit()
        {
            _swingHitResolved = true;
            int hits = MeleeHit(_pendingReach, _pendingDamage);
            if (hits > 0 || (_pendingHeavy && _pendingFinisher)) HitJuice(_pendingFinisher);
        }

        /// <summary>Fires the magical bolt from the left hand at the cast's contact phase (the mirrored L3 thrust).</summary>
        private void ResolveCast()
        {
            _swingHitResolved = true;
            if (_throw == null) return;
            Vector3 origin = _leftHand != null ? _leftHand.position : transform.position + Vector3.up;
            Vector3 dir = (TargetLock.Instance != null && TargetLock.Instance.HasTarget)
                ? TargetLock.Instance.TargetPosition - origin
                : modelRoot.forward;
            _throw.Fire(origin, dir);
        }

        // ---------- FX / feedback ----------
        /// <summary>Impact feedback on EVERY connect: camera shake + hit-stop (a small "connect" stop on lights, a big
        /// weighty one on the finisher). Tune hitStop / finisherHitStop to taste — set hitStop to (0, 1) for no light stop.</summary>
        private void HitJuice(bool finisher)
        {
            Vector2 shake = finisher ? finisherShake : hitShake;
            Vector2 stop = finisher ? finisherHitStop : hitStop;
            PlayerCameraRig.Shake(shake.x, shake.y);
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
                if (col.TryGetComponent(out IDamageable d) && d.IsAlive)
                {
                    d.TakeDamage(damage, origin);
                    hits++;
                    GainMusou(damage * data.musouGainDealtPerDamage);
                    if (_loadout != null)
                    {
                        _loadout.DispatchOnHit(new OnHitContext(this, d, col.transform.position, damage, _pendingHeavy, _pendingFinisher));
                    }
                }
            }
            return hits;
        }

        /// <summary>
        /// Facing on a swing: face the player's aim (the camera-relative movement direction), then apply a
        /// SUBTLE, rotation-only assist that nudges a few degrees toward the nearest enemy in a narrow front
        /// cone so swings connect in a crowd. Never a positional pull, lock-on, or snap; aimAssistStrength 0 =
        /// fully manual. When there's no movement input, the current facing is kept (then nudged).
        /// </summary>
        private void AimFace()
        {
            // Locked: aim straight at the focus target (the lock supersedes the aim-assist nudge).
            if (TargetLock.Instance != null && TargetLock.Instance.HasTarget)
            {
                Vector3 lt = TargetLock.Instance.Target.position - transform.position; lt.y = 0f;
                if (lt.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(lt.normalized);
                return;
            }

            Vector3 dir = ReadMoveDir();
            if (dir.sqrMagnitude > 0.01f) modelRoot.rotation = Quaternion.LookRotation(dir.normalized);

            if (aimAssistStrength <= 0f) return;
            Transform target = NearestEnemyInCone(aimAssistConeDeg, aimAssistRange);
            if (target == null) return;
            Vector3 to = target.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(to.normalized);
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, want, aimAssistStrength);   // rotation-only nudge, scaled by strength
        }

        /// <summary>Nearest enemy whose direction is within <paramref name="coneDeg"/> of the player's facing, inside range. Non-alloc.</summary>
        private Transform NearestEnemyInCone(float coneDeg, float range)
        {
            Vector3 origin = transform.position + Vector3.up;
            Vector3 fwd = modelRoot.forward;
            float cosCone = Mathf.Cos(coneDeg * Mathf.Deg2Rad);
            int n = Physics.OverlapSphereNonAlloc(origin, range, _hits, enemyMask);
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 to = _hits[i].transform.position - origin; to.y = 0f;
                float sq = to.sqrMagnitude;
                if (sq < 0.0001f) continue;
                if (Vector3.Dot(to.normalized, fwd) < cosCone) continue;   // outside the narrow cone
                if (sq < bestSq) { bestSq = sq; best = _hits[i].transform; }
            }
            return best;
        }

        // ---------- IDamageable ----------
        public void TakeDamage(float amount) => TakeDamage(amount, transform.position + modelRoot.forward);

        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            if (invincible || _invulnerable || _musouActive || _hp <= 0f) return;   // dodge i-frames AND the musou finisher fully negate

            // Block: a frontal hit is mitigated (chips through) at a stamina cost; mitigates partially even when empty.
            bool blocked = false;
            if (_blocking && IsFrontalHit(sourcePos))
            {
                float mit = _stamina >= data.blockStaminaPerHit ? data.blockMitigation : data.emptyBlockMitigation;
                SpendStamina(data.blockStaminaPerHit);
                amount *= (1f - mit);
                blocked = true;
            }

            _hp -= amount;
            GainMusou(amount * data.musouGainTakenPerDamage);
            if (hitFlash != null) hitFlash.Flash();
            PlayerCameraRig.Shake(damageShake.x, damageShake.y);
            if (GameManager.Instance != null) GameManager.Instance.HitStop(damageHitStop.x, damageHitStop.y);
            if (_hp <= 0f) { Die(); return; }

            // Flinch only when caught out — blocking absorbs the flinch; never mid-swing/dodge, off cooldown.
            if (!blocked && animator != null && _hitReactCdTimer <= 0f && !IsBaseInAttack() && _dodgeTimer <= 0f)
            {
                int dir = HitReaction.Direction(transform.position, modelRoot.forward, modelRoot.right, sourcePos);
                animator.SetInteger(AnimHitDir, dir);
                animator.SetTrigger(AnimHit);
                _hitReactCdTimer = hitReactCooldown;
            }
        }

        private void Die()
        {
            Debug.Log("[PlayerController] Player down. (Prototype: reload the scene to retry.)");
            gameObject.SetActive(false);
        }
    }
}
