using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Central run controller for the prototype: physics layer setup, hit-stop, pooled XP gems,
    /// and run state (XP / level / wave / kills) with events the HUD and wave director listen to.
    /// Single instance; lives in the Arena scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private string playerLayerName = "Player";
        [SerializeField] private string enemyLayerName = "Enemy";
        [SerializeField] private int baseXpToNext = 5;

        [Header("XP curve (next-level cost grows each level)")]
        [Tooltip("XpToNext is multiplied by this each level-up.")]
        [SerializeField] private float xpToNextGrowth = 1.25f;
        [Tooltip("Flat amount added to XpToNext each level-up (on top of the growth multiplier).")]
        [SerializeField] private int xpToNextFlatAdd = 2;

        [Header("XP gems")]
        [SerializeField] private XpGem gemPrefab;
        [SerializeField] private int gemPoolSize = 128;
        [Tooltip("Height above the drop point a gem spawns at.")]
        [SerializeField] private float gemSpawnHeight = 0.5f;

        [Header("Hit-stop")]
        [Tooltip("Default time-scale used during hit-stop when a caller doesn't specify one.")]
        [Range(0f, 1f)][SerializeField] private float defaultHitStopScale = 0.05f;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext { get; private set; }
        /// <summary>Arena spoils accrued but NOT yet banked — at risk until the arena is cleared (forfeited on death).</summary>
        public int UncommittedXp { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public bool LevelUpPending { get; private set; }
        public int PendingLevelUps => _pendingLevelUps;

        public event Action OnStateChanged;     // HUD refresh
        public event Action<int> OnLevelUp;      // arg = new level (fires per level, for HUD feedback)
        public event Action OnDraftRequested;    // fires at a breather when banked picks are ready to resolve

        private int _pendingLevelUps;
        private bool _arenaXpMode;   // arena: AddXp accrues to UncommittedXp (banked on clear, dropped on death) instead of leveling immediately
        private float _hitStopTimer;
        private ObjectPool<XpGem> _gemPool;
        private Action<XpGem> _gemReturn;
        private Transform _player;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            XpToNext = baseXpToNext;

            int pl = LayerMask.NameToLayer(playerLayerName);
            int en = LayerMask.NameToLayer(enemyLayerName);
            if (pl >= 0 && en >= 0) Physics.IgnoreLayerCollision(pl, en, true);   // player walks through the crowd

            GameObject pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null) _player = pgo.transform;

            if (gemPrefab != null)
            {
                _gemPool = new ObjectPool<XpGem>(gemPrefab, gemPoolSize, transform);
                _gemReturn = _gemPool.Return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (_hitStopTimer > 0f)
            {
                _hitStopTimer -= Time.unscaledDeltaTime;
                if (_hitStopTimer <= 0f && !LevelUpPending) Time.timeScale = 1f;
            }
        }

        /// <summary>Brief slow-mo on impact (juice). Uses unscaled time to recover.
        /// Pass a negative scale (the default) to use <see cref="defaultHitStopScale"/>.</summary>
        public void HitStop(float seconds, float scale = -1f)
        {
            if (LevelUpPending) return;
            Time.timeScale = scale < 0f ? defaultHitStopScale : scale;
            _hitStopTimer = seconds;
        }

        public void DropGem(Vector3 pos, int value)
        {
            if (_gemPool == null) return;
            XpGem g = _gemPool.Get();
            g.transform.position = pos + Vector3.up * gemSpawnHeight;
            g.Init(value, _player, _gemReturn);
        }

        public void AddKill()
        {
            Kills++;
            OnStateChanged?.Invoke();
        }

        /// <summary>Award XP. In arena mode it accrues as UNCOMMITTED (the at-risk spoils — no mid-fight leveling),
        /// banked on clear and forfeited on death. Outside arena mode it commits immediately (the wave sandbox).</summary>
        public void AddXp(int amount)
        {
            if (_arenaXpMode) { UncommittedXp += amount; OnStateChanged?.Invoke(); return; }
            CommitXp(amount);
            OnStateChanged?.Invoke();
        }

        // The level-up loop, shared by immediate AddXp (sandbox) and BankUncommitted (arena clear). Level-ups bank
        // silently as _pendingLevelUps; the augment pick resolves at a breather / on clear (see ResolvePendingAtBreather).
        private void CommitXp(int amount)
        {
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                XpToNext = Mathf.RoundToInt(XpToNext * xpToNextGrowth) + xpToNextFlatAdd;
                _pendingLevelUps++;
                OnLevelUp?.Invoke(Level);
            }
        }

        /// <summary>Arena XP mode: while on, AddXp accrues to UncommittedXp instead of leveling immediately.</summary>
        public void SetArenaXpMode(bool on) => _arenaXpMode = on;

        /// <summary>Commit the at-risk arena XP into levels (call on arena clear, then ResolvePendingAtBreather to draft).</summary>
        public void BankUncommitted()
        {
            int amt = UncommittedXp;
            UncommittedXp = 0;
            if (amt > 0) CommitXp(amt);
            OnStateChanged?.Invoke();
        }

        /// <summary>Forfeit the at-risk arena XP (call on death — the greed spoils lost).</summary>
        public void DiscardUncommitted()
        {
            UncommittedXp = 0;
            OnStateChanged?.Invoke();
        }

        /// <summary>Called by the wave director when a wave/arena is cleared (the breather). If level-ups are
        /// banked, pause the run and request the augment draft to resolve them.</summary>
        public void ResolvePendingAtBreather()
        {
            if (_pendingLevelUps <= 0) return;
            LevelUpPending = true;
            Time.timeScale = 0f;
            OnDraftRequested?.Invoke();
        }

        /// <summary>Called by the draft UI after each pick. Restores time once the last banked pick is taken.</summary>
        public void ConsumeOnePendingLevelUp()
        {
            if (_pendingLevelUps > 0) _pendingLevelUps--;
            if (_pendingLevelUps == 0)
            {
                LevelUpPending = false;
                Time.timeScale = 1f;
            }
        }

        public void SetWave(int wave)
        {
            Wave = wave;
            OnStateChanged?.Invoke();
        }
    }
}
