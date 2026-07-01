using System.Collections.Generic;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The combat "brain" for the crowd (à la God of War / Shadow of Mordor). It does NOT drive enemy locomotion —
    /// each enemy still moves itself. It only ADVISES: enemies pull an attack token at the moment they commit (so the
    /// budget can never be reserved-but-unused), and every frame it threat-ranks the ring-waiters and hands the closest
    /// / most in-front / most aggressive ones a spread approach slot. Everyone denied a token keeps menacing.
    ///
    /// Absent from the scene, enemies fall back to their solo behaviour (every call site is null-guarded), so this is
    /// purely additive. Single authoritative instance (mirrors GameManager / CombatVfx); co-op ready (holds a list of
    /// player targets) though this pass fills and uses player 0 only. Works for both the FSM and Behavior-graph brains
    /// because the gates it feeds (TryStartTelegraph, RepositionStep) are the shared <see cref="IEnemyBody"/> verbs.
    /// </summary>
    // Runs before GarrisonDirector (default order 0) so a SCENE-placed CrowdDirector reliably claims Instance first —
    // otherwise GarrisonDirector.Awake might add a runtime one and the scene object (with the designer's tuned budget/
    // slot values) would self-destroy on its later Awake. Negative order guarantees "scene-placed one wins".
    [DefaultExecutionOrder(-100)]
    public sealed class CrowdDirector : MonoBehaviour
    {
        public static CrowdDirector Instance { get; private set; }

        [Header("Coordination")]
        [Tooltip("Angular approach slots around the player — ring-waiters claim the nearest, so the crowd spreads out.")]
        [SerializeField] private int slotCount = 8;
        [Tooltip("Base simultaneous-attacker budget at wave 1, solo. Grows with wave / player count (see CrowdControl.BudgetForWave).")]
        [SerializeField] private int baseAttackBudget = 2;
        [Tooltip("Max attack budget the wave scaling can reach.")]
        [SerializeField] private int maxAttackBudget = 5;
        [Tooltip("How many heavy telegraphs (Charger / Brute slam) may crescendo at once. 1 keeps the read clean.")]
        [SerializeField] private int heavyTellBudget = 1;
        [Tooltip("Waves between each +1 to the attack budget.")]
        [SerializeField] private int wavesPerBudgetStep = 3;
        [Tooltip("Hard ceiling on tracked agents (scratch-buffer size). Keep >= max enemies alive + brutes.")]
        [SerializeField] private int capacity = 64;
        [Tooltip("Reference engagement radius (m) used to normalise threat ranking so distance doesn't swamp facing/aggression.")]
        [SerializeField] private float threatReferenceRadius = 8f;

        private CrowdControl _control;
        private readonly List<ICrowdAgent> _agents = new List<ICrowdAgent>(64);
        private readonly List<Transform> _players = new List<Transform>(4);
        private int _nextId = 1;
        private int _wave = 1;   // pushed by the spawner (GarrisonDirector); scales the attack budget

        // pre-sized scratch buffers (no per-frame allocation)
        private CrowdControl.AgentThreat[] _snap;
        private ICrowdAgent[] _wanters;
        private int[] _order;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _control = new CrowdControl(Mathf.Max(maxAttackBudget, 8), Mathf.Max(1, slotCount), baseAttackBudget, heavyTellBudget);
            _snap = new CrowdControl.AgentThreat[capacity];
            _wanters = new ICrowdAgent[capacity];
            _order = new int[capacity];
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsurePlayer()
        {
            if (_players.Count > 0 && _players[0] != null)
            {
                return;
            }
            _players.Clear();
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                _players.Add(p.transform);
            }
        }

        /// <summary>Register a spawned agent; returns its stable id. (Pooled agents re-register on each spawn.)</summary>
        public int Register(ICrowdAgent a)
        {
            if (a == null)
            {
                return -1;
            }
            _agents.Add(a);
            return _nextId++;
        }

        /// <summary>Unregister on despawn/death — frees any token/slot the agent still held (watchdog against leaks).</summary>
        public void Unregister(ICrowdAgent a)
        {
            if (a == null)
            {
                return;
            }
            if (_control != null)
            {
                _control.ReleaseAll(a.AgentId);
            }
            int i = _agents.IndexOf(a);
            if (i >= 0)
            {
                _agents[i] = _agents[_agents.Count - 1];   // swap-remove
                _agents.RemoveAt(_agents.Count - 1);
            }
        }

        /// <summary>The spawner pushes the current wave so the attack budget escalates (more attackers, not more bodies).</summary>
        public void SetWave(int wave) => _wave = Mathf.Max(1, wave);

        /// <summary>
        /// Alert propagation: wake every managed agent within <paramref name="radius"/> of <paramref name="pos"/> (one
        /// hop — the woken agents don't re-propagate, so a single engage can't cascade across the whole arena). Skips the
        /// caller (<paramref name="exceptId"/>). Routed through the director (not a physics query) so it works across
        /// husks/brutes regardless of layers, with no allocation.
        /// </summary>
        public void AlertNear(Vector3 pos, float radius, int exceptId)
        {
            if (radius <= 0f)
            {
                return;
            }
            float rSq = radius * radius;
            for (int i = 0; i < _agents.Count; i++)
            {
                ICrowdAgent a = _agents[i];
                if (a == null || !a.IsAlive || a.AgentId == exceptId || a.AgentTransform == null)
                {
                    continue;
                }
                Vector3 to = a.AgentTransform.position - pos; to.y = 0f;
                if (to.sqrMagnitude <= rSq)
                {
                    a.Wake();
                }
            }
        }

        /// <summary>
        /// Count managed agents alive within <paramref name="radius"/> of <paramref name="pos"/> (excluding
        /// <paramref name="exceptId"/>). Powers the goblin Morale check ("am I still in a pack?") — the same
        /// allocation-free scan as <see cref="AlertNear"/>, so it counts husks / archers / brutes alike regardless
        /// of physics layers. Callers throttle it (a few times a second), so the O(n) scan is cheap.
        /// </summary>
        public int CountAllyNear(Vector3 pos, float radius, int exceptId)
        {
            if (radius <= 0f)
            {
                return 0;
            }
            float rSq = radius * radius;
            int count = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                ICrowdAgent a = _agents[i];
                if (a == null || !a.IsAlive || a.AgentId == exceptId || a.AgentTransform == null)
                {
                    continue;
                }
                Vector3 to = a.AgentTransform.position - pos; to.y = 0f;
                if (to.sqrMagnitude <= rSq)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Pull-model token request: an agent calls this at the instant it commits an attack. True = granted.</summary>
        public bool TryBeginAttack(int agentId, int cost, bool heavyTell) => _control != null && _control.TryAcquireToken(agentId, cost, heavyTell);

        /// <summary>Release only the agent's attack TOKEN (on attack-end / stagger) — it keeps its ring slot. Idempotent.</summary>
        public void ReleaseToken(int agentId)
        {
            if (_control != null)
            {
                _control.ReleaseToken(agentId);
            }
        }

        /// <summary>Release an agent's attack token AND slot (on death / despawn). Idempotent.</summary>
        public void Release(int agentId)
        {
            if (_control != null)
            {
                _control.ReleaseAll(agentId);
            }
        }

        // The director does NOT allocate attack tokens here (that's pull, at commit). Its per-frame job is to keep the
        // approach-slot ring assigned to the most threatening ring-waiters and to scale the budget with the wave.
        private void Update()
        {
            if (_control == null)
            {
                return;
            }
            EnsurePlayer();
            if (_players.Count == 0 || _players[0] == null)
            {
                return;
            }
            Transform player = _players[0];

            _control.SetBudget(CrowdControl.BudgetForWave(_wave, _players.Count, baseAttackBudget, wavesPerBudgetStep, maxAttackBudget), heavyTellBudget);

            Vector3 pPos = player.position;
            Vector3 pFwd = player.forward; pFwd.y = 0f;
            pFwd = pFwd.sqrMagnitude > 0.0001f ? pFwd.normalized : Vector3.forward;
            float refSq = threatReferenceRadius * threatReferenceRadius;

            // Gather ring-waiters; everyone else frees any slot they hold.
            int wn = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                ICrowdAgent a = _agents[i];
                if (a == null || !a.IsAlive)
                {
                    continue;
                }
                if (a.WantsSlot && wn < _snap.Length)
                {
                    Vector3 to = a.AgentTransform.position - pPos; to.y = 0f;
                    float distSq = to.sqrMagnitude;
                    float facingDot = distSq > 0.0001f ? Vector3.Dot(to.normalized, pFwd) : 1f;
                    _snap[wn] = new CrowdControl.AgentThreat { AgentId = a.AgentId, DistSq = distSq, FacingDot = facingDot, Aggression = a.Aggression };
                    _wanters[wn] = a;
                    wn++;
                }
                else
                {
                    _control.ReleaseSlot(a.AgentId);
                    a.ClearSlot();
                }
            }

            if (wn == 0)
            {
                return;
            }

            // Assign the scarce slots to the most threatening ring-waiters first; the rest hold the outer ring and menace.
            CrowdControl.RankByThreat(_snap, wn, _order, refSq);
            for (int k = 0; k < wn; k++)
            {
                ICrowdAgent a = _wanters[_order[k]];
                Vector3 to = a.AgentTransform.position - pPos; to.y = 0f;
                float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;   // 0 = +Z; matches CrowdControl.SlotAngle
                if (bearing < 0f)
                {
                    bearing += 360f;
                }
                int slot = _control.ClaimNearestFreeSlot(a.AgentId, bearing);
                if (slot >= 0)
                {
                    a.AssignSlotAngle(_control.SlotAngle(slot));
                }
                else
                {
                    a.ClearSlot();
                }
            }
        }
    }
}
