using UnityEngine;

namespace Hordebreakers
{
    /// <summary>The transient buff kinds a Shaman lays on nearby goblins (consumed by <see cref="Enemy.ApplyStatus"/>).</summary>
    public enum EnemyStatusKind { Haste, Enrage, Ward }

    /// <summary>Implemented by the player so a Shaman can hex it (slow/weaken). OPTIONAL — the Shaman no-ops if the
    /// player doesn't implement it, so this compiles/runs before the player-side hook exists.</summary>
    public interface IHexable { void ApplyHex(float moveSpeedMult, float duration); }

    /// <summary>
    /// Goblin Shaman support brain — a companion to a Ranged <see cref="Enemy"/> on the same prefab (the Enemy handles
    /// movement / kiting / bolts; this layers the caster on top). On a cadence it picks one of: BUFF nearby goblins
    /// (haste / enrage / ward, and rallies any that have broken), SUMMON a goblin or two via the GarrisonDirector, or
    /// HEX the player (slow). Squishy priority target — kill it and the crowd stops getting worse (and stops re-rallying).
    /// Only casts while the player is within activationRange, so a far-off passive shaman stays quiet.
    /// </summary>
    public sealed class ShamanAbilities : MonoBehaviour
    {
        [Header("Cadence")]
        [Tooltip("Seconds between support casts (a fresh ability is chosen each time).")]
        [SerializeField] private float castInterval = 4f;
        [Tooltip("Grace before the first cast after a (pooled) spawn.")]
        [SerializeField] private float firstCastDelay = 2f;
        [Tooltip("Only casts while the player is within this range — a distant/idle shaman does nothing.")]
        [SerializeField] private float activationRange = 20f;

        [Header("Buff nearby goblins")]
        [SerializeField] private float buffRadius = 8f;
        [SerializeField] private float buffDuration = 6f;
        [Tooltip("Haste: move-speed multiplier.")]      [SerializeField] private float hasteMult = 1.4f;
        [Tooltip("Enrage: outgoing-damage multiplier.")] [SerializeField] private float enrageMult = 1.5f;
        [Tooltip("Ward: incoming-damage multiplier (<1 soaks damage).")] [SerializeField] private float wardMult = 0.6f;
        [Tooltip("Max allies buffed per cast.")]        [SerializeField] private int buffMaxTargets = 6;

        [Header("Hex the player")]
        [SerializeField] private float hexSlowMult = 0.6f;
        [SerializeField] private float hexDuration = 3f;

        [Header("Summon")]
        [SerializeField] private int summonCount = 2;
        [Tooltip("Ring radius (m) around the shaman the summoned goblins appear on.")]
        [SerializeField] private float summonRadius = 3f;

        [Header("Refs")]
        [Tooltip("The enemy layer — nearby goblins to buff. Match the enemy layer used elsewhere.")]
        [SerializeField] private LayerMask allyMask;

        private Transform _player;
        private float _timer;
        private static readonly Collider[] _hits = new Collider[24];

        private void OnEnable()
        {
            _timer = firstCastDelay;   // reset each (pooled) spawn
            if (_player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) _player = p.transform; }
        }

        private void Update()
        {
            if (_player == null) return;
            if ((_player.position - transform.position).sqrMagnitude > activationRange * activationRange) return;   // player far → stay quiet
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = castInterval;
            Cast();
        }

        /// <summary>Pick a support move (buff most often — it's the signature; then summon; hex least).</summary>
        private void Cast()
        {
            float r = Random.value;
            if (r < 0.5f) BuffAllies();
            else if (r < 0.8f) Summon();
            else Hex();
        }

        private void BuffAllies()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, buffRadius, _hits, allyMask, QueryTriggerInteraction.Ignore);
            EnemyStatusKind kind = (EnemyStatusKind)Random.Range(0, 3);
            float mult = kind == EnemyStatusKind.Haste ? hasteMult : kind == EnemyStatusKind.Enrage ? enrageMult : wardMult;
            int applied = 0;
            for (int i = 0; i < n && applied < buffMaxTargets; i++)
            {
                if (_hits[i].TryGetComponent(out Enemy e) && e.IsAlive && e.gameObject != gameObject)
                {
                    e.ApplyStatus(kind, buffDuration, mult);
                    e.RallyMorale();   // a buff also re-emboldens a routed goblin
                    applied++;
                }
            }
        }

        private void Summon()
        {
            GarrisonDirector gd = GarrisonDirector.Instance;
            if (gd == null) { BuffAllies(); return; }   // no spawner → do something useful instead
            for (int i = 0; i < summonCount; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * summonRadius;
                gd.SpawnGoblinAt(transform.position + new Vector3(off.x, 0f, off.y));
            }
        }

        private void Hex()
        {
            if (_player != null && _player.TryGetComponent(out IHexable hexable)) hexable.ApplyHex(hexSlowMult, hexDuration);
            else BuffAllies();   // no hex hook on the player yet → buff instead (never a wasted cast)
        }
    }
}
