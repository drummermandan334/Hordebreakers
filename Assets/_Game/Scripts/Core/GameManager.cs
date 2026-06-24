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

        [Header("XP gems")]
        [SerializeField] private XpGem gemPrefab;
        [SerializeField] private int gemPoolSize = 128;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext { get; private set; }
        public int Wave { get; private set; }
        public int Kills { get; private set; }
        public bool LevelUpPending { get; private set; }

        public event Action OnStateChanged;     // HUD refresh
        public event Action<int> OnLevelUp;      // arg = new level

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

        /// <summary>Brief slow-mo on impact (juice). Uses unscaled time to recover.</summary>
        public void HitStop(float seconds, float scale = 0.05f)
        {
            if (LevelUpPending) return;
            Time.timeScale = scale;
            _hitStopTimer = seconds;
        }

        public void DropGem(Vector3 pos, int value)
        {
            if (_gemPool == null) return;
            XpGem g = _gemPool.Get();
            g.transform.position = pos + Vector3.up * 0.5f;
            g.Init(value, _player, _gemReturn);
        }

        public void AddKill()
        {
            Kills++;
            OnStateChanged?.Invoke();
        }

        public void AddXp(int amount)
        {
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                XpToNext = Mathf.RoundToInt(XpToNext * 1.25f) + 2;
                // Cards disabled: level-ups bank silently mid-arena. The pick happens AFTER an arena is
                // cleared (D&D-style) — wire that to the arena/wave-complete flow when it exists.
                OnLevelUp?.Invoke(Level);
            }
            OnStateChanged?.Invoke();
        }

        /// <summary>Called by the card UI once a level-up choice is resolved.</summary>
        public void ClearLevelUpPending()
        {
            LevelUpPending = false;
            Time.timeScale = 1f;
        }

        public void SetWave(int wave)
        {
            Wave = wave;
            OnStateChanged?.Invoke();
        }
    }
}
