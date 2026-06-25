using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Pooled hit-impact VFX with a static <see cref="Hit"/> facade (mirrors <see cref="CombatAudio"/>). A gold
    /// spark fires on every hit; a light blood splat fires on heavies/kills. Pre-instantiates a small pool of each
    /// and round-robins them, replaying the particles in place — no Instantiate/Destroy per hit (no GC). Prefabs +
    /// scale are SerializeFields so the look is tuned in one spot.
    /// </summary>
    public class CombatVfx : MonoBehaviour
    {
        public static CombatVfx Instance { get; private set; }

        [Header("Prefabs")]
        [Tooltip("Spark on EVERY hit (e.g. FX_Impact_Metal_01).")]
        [SerializeField] private GameObject sparkPrefab;
        [Tooltip("Blood on heavies/kills (e.g. FX_BloodSplat_Small_01).")]
        [SerializeField] private GameObject bloodPrefab;

        [Header("Pool / size")]
        [SerializeField] private int poolEach = 8;
        [Range(0.05f, 3f)][SerializeField] private float sparkScale = 0.5f;
        [Range(0.05f, 3f)][SerializeField] private float bloodScale = 0.5f;

        private GameObject[] _sparkGo, _bloodGo;
        private ParticleSystem[][] _sparkPs, _bloodPs;
        private int _sparkNext, _bloodNext;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build(sparkPrefab, sparkScale, "Spark", out _sparkGo, out _sparkPs);
            Build(bloodPrefab, bloodScale, "Blood", out _bloodGo, out _bloodPs);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build(GameObject prefab, float scale, string label, out GameObject[] gos, out ParticleSystem[][] systems)
        {
            if (prefab == null) { gos = new GameObject[0]; systems = new ParticleSystem[0][]; return; }
            int n = Mathf.Max(1, poolEach);
            gos = new GameObject[n];
            systems = new ParticleSystem[n][];
            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(prefab, transform);
                go.name = label + i;
                go.transform.localScale = Vector3.one * scale;
                var arr = go.GetComponentsInChildren<ParticleSystem>(true);
                for (int j = 0; j < arr.Length; j++) arr[j].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                gos[i] = go;
                systems[i] = arr;
            }
        }

        /// <summary>Spark at the hit point (oriented along <paramref name="normal"/>); blood too when <paramref name="splash"/>.</summary>
        public static void Hit(Vector3 position, Vector3 normal, bool splash)
        {
            if (Instance == null) return;
            Instance._sparkNext = Instance.Emit(Instance._sparkGo, Instance._sparkPs, Instance._sparkNext, position, normal);
            if (splash) Instance._bloodNext = Instance.Emit(Instance._bloodGo, Instance._bloodPs, Instance._bloodNext, position, normal);
        }

        private int Emit(GameObject[] gos, ParticleSystem[][] systems, int next, Vector3 position, Vector3 normal)
        {
            if (gos == null || gos.Length == 0) return next;
            int idx = next % gos.Length;
            Transform t = gos[idx].transform;
            t.position = position;
            if (normal.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(normal);
            ParticleSystem[] arr = systems[idx];
            for (int j = 0; j < arr.Length; j++) { arr[j].Clear(false); arr[j].Play(false); }
            return (next + 1) % gos.Length;
        }
    }
}
