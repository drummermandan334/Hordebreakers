using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The encounter-loop core spawner (encounter-loop-spec §2): a finite garrison that mobilizes in discrete WAVES.
    /// Each wave drops a staggered BATCH whose size + composition escalate (Husk → Charger → Brute); between waves a
    /// COUNTDOWN tells the player how long until the next one arrives — pace you can read and gamble against, not a
    /// punishment timer. Capped at maxAlive on screen (dozens, not hundreds). A commander (a beefed Brute) is the win
    /// target, present from the start. Reuses the pools + Enemy/Brute.Init + return callbacks. EncounterController
    /// drives Begin()/Stop(). Everything is tunable.
    /// </summary>
    public sealed class GarrisonDirector : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Enemy huskPrefab;
        [SerializeField] private Brute brutePrefab;
        [SerializeField] private EnemyData huskData;
        [SerializeField] private EnemyData chargerData;
        [SerializeField] private EliteData bruteData;       // regular Brute reinforcement
        [SerializeField] private EliteData commanderData;   // beefed Brute = the win target
        [SerializeField] private Transform player;
        [Tooltip("Perimeter spawn points. If empty, falls back to a ring around arenaCenter at spawnRadius.")]
        [SerializeField] private Transform[] spawnPoints;
        [Tooltip("Arena center for the ring fallback + commander placement. Defaults to this object's position.")]
        [SerializeField] private Transform arenaCenter;
        [Tooltip("Where the commander spawns. Defaults to arenaCenter.")]
        [SerializeField] private Transform commanderSpawnPoint;
        [SerializeField] private float spawnRadius = 18f;   // ring fallback when no spawnPoints assigned

        [Header("Pools")]
        [SerializeField] private int huskPoolSize = 64;
        [SerializeField] private int brutePoolSize = 12;

        [Header("Waves (the finite garrison)")]
        [Tooltip("Total reinforcement waves. After the last wave, no more reinforcements — grind down what's left + the commander.")]
        [SerializeField] private int waveCount = 8;
        [Tooltip("The commander (boss) joins when THIS wave starts — not at arena start. Set > waveCount to never spawn it (pure wave/melee testing; the arena isn't clearable then).")]
        [SerializeField] private int commanderWave = 6;
        [Tooltip("Countdown before the FIRST wave arrives.")]
        [SerializeField] private float firstWaveDelay = 4f;
        [Tooltip("Countdown between waves — the 'next wave in Xs' the player sees.")]
        [SerializeField] private float interWaveTime = 14f;
        [Tooltip("Max reinforcements alive at once (on-screen scale — dozens). A wave's batch waits if we're at the cap. Commander not counted.")]
        [SerializeField] private int maxAlive = 28;
        [Tooltip("Wave 1 batch size; each later wave adds waveSizeGrowth.")]
        [SerializeField] private int baseWaveSize = 4;
        [SerializeField] private float waveSizeGrowth = 1.5f;
        [Tooltip("Seconds between individual spawns within a wave's batch (so they don't all pop at once).")]
        [SerializeField] private float batchSpawnStagger = 0.25f;

        [Header("Composition escalation (per wave)")]
        [Tooltip("Chargers start appearing in the wave batches from this wave on.")]
        [SerializeField] private int chargerStartWave = 3;
        [Range(0f, 1f)] [SerializeField] private float chargerChance = 0.3f;
        [Tooltip("Brute reinforcements start from this wave on.")]
        [SerializeField] private int bruteStartWave = 5;
        [Range(0f, 1f)] [SerializeField] private float bruteChance = 0.2f;

        private ObjectPool<Enemy> _huskPool;
        private ObjectPool<Brute> _brutePool;
        private Action<Enemy> _huskReturn;
        private Action<Brute> _bruteReturn;
        private readonly List<Enemy> _activeHusks = new List<Enemy>(64);
        private readonly List<Brute> _activeBrutes = new List<Brute>(12);
        private Brute _commander;

        private bool _running;
        private int _alive;             // reinforcements alive (commander excluded)
        private int _waveNumber;        // 0 before wave 1; 1..waveCount
        private float _waveTimer;       // countdown to the next wave
        private int _batchRemaining;    // reinforcements still queued to spawn from launched wave(s)
        private float _batchTimer;      // stagger between spawns within a batch

        // ---- read by the objective + HUD ----
        public bool CommanderSpawned { get; private set; }
        public bool CommanderAlive => _commander != null && _commander.IsAlive;
        public int AliveCount => _alive;
        public int WaveNumber => _waveNumber;
        public int WaveCount => waveCount;
        public float SecondsToNextWave => _waveNumber < waveCount ? Mathf.Max(0f, _waveTimer) : 0f;
        public bool AllWavesDone => _waveNumber >= waveCount;

        private void Awake()
        {
            if (arenaCenter == null) arenaCenter = transform;
            if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
            if (huskPrefab != null) { _huskPool = new ObjectPool<Enemy>(huskPrefab, huskPoolSize, transform); _huskReturn = ReturnHusk; }
            if (brutePrefab != null) { _brutePool = new ObjectPool<Brute>(brutePrefab, brutePoolSize, transform); _bruteReturn = ReturnBrute; }
        }

        /// <summary>(Re)start the encounter: clear any active enemies, reset the wave clock, spawn the commander, run.</summary>
        public void Begin()
        {
            DespawnAll();
            _alive = 0;
            _waveNumber = 0;
            _waveTimer = firstWaveDelay;
            _batchRemaining = 0;
            _batchTimer = 0f;
            _running = true;
            // The commander does NOT spawn at the start — it joins at commanderWave (see StartWave).
        }

        /// <summary>Stop mobilizing reinforcements (e.g. on clear). Leaves existing enemies in place.</summary>
        public void Stop() => _running = false;

        private void Update()
        {
            if (!_running || player == null) return;
            float dt = Time.deltaTime;

            // Drain the current batch, staggered + capped at maxAlive.
            if (_batchRemaining > 0)
            {
                _batchTimer -= dt;
                if (_batchTimer <= 0f && _alive < maxAlive)
                {
                    SpawnOneOfWave();
                    _batchRemaining--;
                    _batchTimer = batchSpawnStagger;
                }
            }

            // Count down to the next wave (this is the "next wave in Xs" the HUD shows).
            if (_waveNumber < waveCount)
            {
                _waveTimer -= dt;
                if (_waveTimer <= 0f) StartWave(_waveNumber + 1);
            }
        }

        private void StartWave(int n)
        {
            _waveNumber = n;
            _batchRemaining += baseWaveSize + Mathf.RoundToInt((n - 1) * waveSizeGrowth);   // accumulate (a prior batch may still be draining at the cap)
            _waveTimer = interWaveTime;
            if (n == commanderWave && !CommanderSpawned) SpawnCommander();   // the boss joins now
        }

        private void SpawnOneOfWave()
        {
            float cc = _waveNumber >= chargerStartWave ? chargerChance : 0f;
            float bc = (_waveNumber >= bruteStartWave && _brutePool != null) ? bruteChance : 0f;
            float r = UnityEngine.Random.value;
            if (r < bc) SpawnBruteReinforcement();
            else if (r < bc + cc) SpawnEnemy(chargerData != null ? chargerData : huskData);
            else SpawnEnemy(huskData);
            _alive++;
        }

        private void SpawnEnemy(EnemyData data)
        {
            Enemy e = _huskPool.Get();
            e.transform.position = SpawnPos();
            e.Init(data, player, _huskReturn);
            _activeHusks.Add(e);
        }

        private void SpawnBruteReinforcement()
        {
            Brute b = _brutePool.Get();
            b.transform.position = SpawnPos();
            b.Init(player, _bruteReturn, bruteData);
            _activeBrutes.Add(b);
        }

        private void SpawnCommander()
        {
            if (_brutePool == null) { CommanderSpawned = false; return; }
            _commander = _brutePool.Get();
            _commander.transform.position = commanderSpawnPoint != null ? commanderSpawnPoint.position : arenaCenter.position;
            _commander.Init(player, _bruteReturn, commanderData != null ? commanderData : bruteData);
            CommanderSpawned = true;
        }

        private Vector3 SpawnPos()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform t = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (t != null) return t.position;
            }
            Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
            return arenaCenter.position + new Vector3(c.x, 0f, c.y);
        }

        private void ReturnHusk(Enemy e)
        {
            if (_activeHusks.Remove(e)) _alive = Mathf.Max(0, _alive - 1);
            _huskPool.Return(e);
        }

        private void ReturnBrute(Brute b)
        {
            if (b == _commander) _commander = null;                       // commander wasn't counted in _alive
            else if (_activeBrutes.Remove(b)) _alive = Mathf.Max(0, _alive - 1);
            _brutePool.Return(b);
        }

        // Hard-clear every active enemy (force back to pool, no death/XP) — used on arena reset.
        private void DespawnAll()
        {
            for (int i = 0; i < _activeHusks.Count; i++) if (_activeHusks[i] != null) _huskPool.Return(_activeHusks[i]);
            _activeHusks.Clear();
            for (int i = 0; i < _activeBrutes.Count; i++) if (_activeBrutes[i] != null) _brutePool.Return(_activeBrutes[i]);
            _activeBrutes.Clear();
            if (_commander != null && _brutePool != null) { _brutePool.Return(_commander); _commander = null; }
            CommanderSpawned = false;
            _alive = 0;
        }
    }
}
