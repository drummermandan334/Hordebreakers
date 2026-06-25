using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Prototype wave director (replaces the raw Spawner). Runs waves of rising density with a
    /// short breather between them, ring-spawns pooled Husks, and injects pooled Brutes from a
    /// set wave onward. Pushes the current wave to the GameManager for the HUD.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Enemy huskPrefab;
        [SerializeField] private EnemyData huskData;
        [Tooltip("Charger variant data (archetype = Charger). Spawns from the same Husk pool, just Init'd with this.")]
        [SerializeField] private EnemyData chargerData;
        [SerializeField] private Brute brutePrefab;
        [SerializeField] private Transform player;

        [Header("Training mode (feel testing: ONE passive dummy, no waves/attacks)")]
        [SerializeField] private bool trainingMode = false;
        [SerializeField] private EnemyData dummyData;
        [Tooltip("Fixed, in-arena spawn for the dummy. If set, the dummy always respawns here (so it never ends up outside a wall). Falls back to in-front-of-player if unset.")]
        [SerializeField] private Transform dummySpawnPoint;
        [SerializeField] private float dummySpawnDistance = 4.5f;
        [SerializeField] private float dummyRespawnDelay = 1.5f;

        [Header("Spawn")]
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private int huskPoolSize = 250;
        [SerializeField] private int brutePoolSize = 12;
        [SerializeField] private int maxAlive = 180;

        [Header("Waves")]
        [SerializeField] private float waveDuration = 25f;
        [SerializeField] private float breatherDuration = 8f;
        [SerializeField] private float baseSpawnInterval = 1.2f;
        [SerializeField] private float spawnIntervalWaveMult = 0.85f;   // each wave spawns faster
        [SerializeField] private float minSpawnInterval = 0.18f;
        [Tooltip("From this wave on, a fraction of crowd spawns become Chargers (telegraphed dashers).")]
        [SerializeField] private int firstChargerWave = 2;
        [Range(0f, 1f)] [SerializeField] private float chargerChance = 0.25f;
        [SerializeField] private int firstBruteWave = 2;
        [Tooltip("Delay after a wave starts before the first Brute is injected (waves at/after firstBruteWave).")]
        [SerializeField] private float bruteFirstSpawnDelay = 3f;
        [Tooltip("Interval between Brute injections = max(floor, baseInterval - wave). Floor it never drops below.")]
        [SerializeField] private float bruteIntervalFloor = 8f;
        [SerializeField] private float bruteIntervalBase = 18f;

        private ObjectPool<Enemy> _huskPool;
        private Action<Enemy> _huskReturn;
        private int _huskAlive;
        private ObjectPool<Brute> _brutePool;
        private Action<Brute> _bruteReturn;
        private int _bruteAlive;

        private int _wave;
        private bool _inBreather;
        private Enemy _dummy;
        private float _dummyRespawnTimer;
        private float _phaseTimer;
        private float _spawnTimer;
        private float _bruteTimer;

        private void Awake()
        {
            if (huskPrefab == null || huskData == null)
            {
                Debug.LogError("[WaveDirector] Assign huskPrefab + huskData.", this);
                enabled = false;
                return;
            }
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            _huskPool = new ObjectPool<Enemy>(huskPrefab, huskPoolSize, transform);
            _huskReturn = ReturnHusk;
            if (brutePrefab != null)
            {
                _brutePool = new ObjectPool<Brute>(brutePrefab, brutePoolSize, transform);
                _bruteReturn = ReturnBrute;
            }
        }

        private void Start()
        {
            if (trainingMode) SpawnDummy();
            else StartWave(1);
        }

        private void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;

            if (trainingMode) { TickTraining(dt); return; }

            _phaseTimer -= dt;

            if (_inBreather)
            {
                if (_phaseTimer <= 0f) StartWave(_wave + 1);
                return;
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f && _huskAlive < maxAlive)
            {
                SpawnHusk();
                _spawnTimer = CurrentInterval();
            }

            if (_brutePool != null && _wave >= firstBruteWave)
            {
                _bruteTimer -= dt;
                if (_bruteTimer <= 0f)
                {
                    SpawnBrute();
                    _bruteTimer = Mathf.Max(bruteIntervalFloor, bruteIntervalBase - _wave);   // more frequent as waves climb
                }
            }

            if (_phaseTimer <= 0f) StartBreather();
        }

        private float CurrentInterval()
        {
            float iv = baseSpawnInterval * Mathf.Pow(spawnIntervalWaveMult, _wave - 1);
            return Mathf.Max(minSpawnInterval, iv);
        }

        private void StartWave(int w)
        {
            _wave = w;
            _inBreather = false;
            _phaseTimer = waveDuration;
            _spawnTimer = 0f;
            _bruteTimer = (_wave >= firstBruteWave) ? bruteFirstSpawnDelay : 999f;
            if (GameManager.Instance != null) GameManager.Instance.SetWave(_wave);
        }

        private void StartBreather()
        {
            _inBreather = true;
            _phaseTimer = breatherDuration;
            // Wave cleared → resolve any banked level-up augment picks (between encounters, not mid-fight).
            if (GameManager.Instance != null) GameManager.Instance.ResolvePendingAtBreather();
        }

        private Vector3 RingPoint()
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
            return player.position + new Vector3(c.x, 0f, c.y);
        }

        // ---------- Training mode (feel testing) ----------
        private void TickTraining(float dt)
        {
            if (_dummy != null && _dummy.IsAlive) return;   // keep exactly one dummy alive
            _dummyRespawnTimer -= dt;
            if (_dummyRespawnTimer <= 0f) SpawnDummy();
        }

        private void SpawnDummy()
        {
            if (dummyData == null) return;
            Enemy e = _huskPool.Get();
            Vector3 p = dummySpawnPoint != null
                ? dummySpawnPoint.position                                   // fixed, in-arena spawn (never ends up outside a wall)
                : player.position + player.forward * dummySpawnDistance;     // fallback: in front of the player
            e.transform.position = p;
            e.Init(dummyData, player, _huskReturn);
            _dummy = e;
            _dummyRespawnTimer = dummyRespawnDelay;
        }

        private void SpawnHusk()
        {
            Enemy e = _huskPool.Get();
            e.transform.position = RingPoint();
            // Same pool/prefab; a fraction roll as Chargers, reconfigured purely by their EnemyData archetype.
            bool charger = chargerData != null && _wave >= firstChargerWave && UnityEngine.Random.value < chargerChance;
            e.Init(charger ? chargerData : huskData, player, _huskReturn);
            _huskAlive++;
        }

        private void SpawnBrute()
        {
            Brute b = _brutePool.Get();
            b.transform.position = RingPoint();
            b.Init(player, _bruteReturn);
            _bruteAlive++;
        }

        private void ReturnHusk(Enemy e) { _huskAlive = Mathf.Max(0, _huskAlive - 1); _huskPool.Return(e); }
        private void ReturnBrute(Brute b) { _bruteAlive = Mathf.Max(0, _bruteAlive - 1); _brutePool.Return(b); }
    }
}
