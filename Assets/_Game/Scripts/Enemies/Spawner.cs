using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Prototype swarm spawner: pools enemies and spawns them on a ring around the player,
    /// ramping the spawn rate over time. Stand-in for the full wave/boss director (GDD section 6).
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemyData enemyData;
        [SerializeField] private Transform player;

        [Header("Spawning")]
        [SerializeField] private float spawnRadius = 18f;
        [SerializeField] private float startInterval = 1.0f;
        [SerializeField] private float minInterval = 0.15f;
        [SerializeField] private float rampSeconds = 120f;   // time to reach minInterval
        [SerializeField] private int maxAlive = 200;
        [SerializeField] private int poolSize = 220;

        private ObjectPool<Enemy> _pool;
        private Action<Enemy> _returnAction;
        private float _timer;
        private float _elapsed;
        private int _alive;

        private void Awake()
        {
            if (enemyPrefab == null || enemyData == null)
            {
                Debug.LogError("[Spawner] Assign enemyPrefab + EnemyData.", this);
                enabled = false;
                return;
            }
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            _pool = new ObjectPool<Enemy>(enemyPrefab, poolSize, transform);
            _returnAction = ReturnEnemy;
            _timer = startInterval;
        }

        private void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            _elapsed += dt;
            _timer -= dt;
            if (_timer > 0f || _alive >= maxAlive) return;

            Spawn();
            float t = Mathf.Clamp01(_elapsed / rampSeconds);
            _timer = Mathf.Lerp(startInterval, minInterval, t);
        }

        private void Spawn()
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 pos = player.position + new Vector3(c.x, 0f, c.y);
            Enemy e = _pool.Get();
            e.transform.position = pos;
            e.Init(enemyData, player, _returnAction);
            _alive++;
        }

        private void ReturnEnemy(Enemy e)
        {
            _alive = Mathf.Max(0, _alive - 1);
            _pool.Return(e);
        }
    }
}
