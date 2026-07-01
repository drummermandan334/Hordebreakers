using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Static facade over a small pool of <see cref="Projectile"/>s for ENEMY arrows (Goblin Archer, Shaman).
    /// Mirrors the CombatVfx / CombatAudio singleton-behind-static pattern: place one in the scene holding the arrow
    /// prefab; Ranged archers call <see cref="Fire"/> — zero per-shot allocation, capped concurrent arrows in flight.
    /// </summary>
    public sealed class EnemyArrows : MonoBehaviour
    {
        [Tooltip("Arrow prefab — a visual (mesh + optional trail) with a Projectile component on the root.")]
        [SerializeField] private Projectile _arrowPrefab;
        [Tooltip("Pool size — max arrows in flight at once across all archers. Excess shots are dropped (cheap backpressure).")]
        [SerializeField] private int _poolSize = 24;

        private static EnemyArrows _instance;
        private readonly List<Projectile> _all = new();
        private readonly Queue<Projectile> _free = new();
        private bool _built;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            if (_built || _arrowPrefab == null) { return; }
            for (int arrowIndex = 0; arrowIndex < _poolSize; arrowIndex++)
            {
                Projectile arrow = Instantiate(_arrowPrefab, transform);
                arrow.gameObject.SetActive(false);
                _all.Add(arrow);
                _free.Enqueue(arrow);
            }
            _built = true;
        }

        /// <summary>Launch an arrow from <paramref name="origin"/> along <paramref name="dir"/>. Safe no-op if no host/prefab exists.</summary>
        public static void Fire(Vector3 origin, Vector3 dir, float damage, float speed, float life, LayerMask hitMask)
        {
            if (_instance == null || !_instance._built) { return; }
            _instance.Launch(origin, dir, damage, speed, life, hitMask);
        }

        private void Launch(Vector3 origin, Vector3 dir, float damage, float speed, float life, LayerMask hitMask)
        {
            if (_free.Count == 0) { return; }   // pool exhausted — drop the shot
            Projectile arrow = _free.Dequeue();
            arrow.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            arrow.gameObject.SetActive(true);
            arrow.Init(dir, damage, speed, life, hitMask, Recycle);
        }

        private void Recycle(Projectile arrow)
        {
            arrow.gameObject.SetActive(false);
            _free.Enqueue(arrow);
        }
    }
}
