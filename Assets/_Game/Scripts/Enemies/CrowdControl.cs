namespace Hordebreakers
{
    /// <summary>
    /// PURE crowd-coordination logic for the <see cref="CrowdDirector"/> — no UnityEngine, no allocation in the hot
    /// path, fully unit-testable. Owns two scarce resources that turn a dogpile into a "deliberate crowd":
    ///
    ///  • An ATTACK-TOKEN budget — only a few enemies may commit at once. A heavy attack (Brute slam) costs more, and
    ///    a separate heavy-tell budget stops two big telegraphs crescendoing at the same time.
    ///  • An APPROACH-SLOT ring — angular sectors around the player; near enemies claim the slot nearest where they
    ///    already are (sticky, anti-thrash) so pressure comes from spread angles, not one clump. More enemies than
    ///    slots is normal — the rest hold the outer ring and menace.
    ///
    /// Everything is keyed by a stable integer agent id; all release calls are idempotent so death/stagger races can't
    /// leak the budget. Negative ids (unmanaged agents) are ignored everywhere, so a director-less agent is a no-op.
    /// <see cref="RankByThreat"/> orders ring-waiters so scarce slots go to the enemies that matter (closest, most
    /// in-front, most aggressive).
    /// </summary>
    public sealed class CrowdControl
    {
        private const int EMPTY = -1;

        // --- attack tokens (parallel arrays; n is tiny, linear scan is free) ---
        private readonly int[] _tokAgent;
        private readonly int[] _tokCost;
        private readonly bool[] _tokHeavy;
        private int _tokCount;
        private int _tokensInUse;
        private int _heavyTellsInUse;
        private int _tokenBudget;
        private int _heavyTellBudget;

        // --- approach-slot ring ---
        private readonly int[] _slotOwner;   // agentId per slot, or EMPTY
        private readonly int _slotCount;

        public CrowdControl(int tokenCapacity, int slotCount, int tokenBudget = 2, int heavyTellBudget = 1)
        {
            if (tokenCapacity < 1)
            {
                tokenCapacity = 1;
            }
            if (slotCount < 1)
            {
                slotCount = 1;
            }
            _tokAgent = new int[tokenCapacity];
            _tokCost = new int[tokenCapacity];
            _tokHeavy = new bool[tokenCapacity];
            _slotOwner = new int[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _slotOwner[i] = EMPTY;
            }
            _slotCount = slotCount;
            _tokenBudget = tokenBudget;
            _heavyTellBudget = heavyTellBudget;
        }

        public int TokenBudget => _tokenBudget;
        public int TokensInUse => _tokensInUse;
        public int HeavyTellsInUse => _heavyTellsInUse;
        public int SlotCount => _slotCount;

        /// <summary>Set the live budgets (e.g. from <see cref="BudgetForWave"/>). Clamped to the token-array capacity.</summary>
        public void SetBudget(int tokenBudget, int heavyTellBudget)
        {
            if (tokenBudget < 0)
            {
                tokenBudget = 0;
            }
            else if (tokenBudget > _tokAgent.Length)
            {
                tokenBudget = _tokAgent.Length;
            }
            _tokenBudget = tokenBudget;
            _heavyTellBudget = heavyTellBudget < 0 ? 0 : heavyTellBudget;
        }

        // ---------- attack tokens ----------

        public bool HoldsToken(int agentId) => IndexOfToken(agentId) >= 0;

        private int IndexOfToken(int agentId)
        {
            for (int i = 0; i < _tokCount; i++)
            {
                if (_tokAgent[i] == agentId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Grant an attack token to <paramref name="agentId"/> if the budget allows (and a heavy-tell slot, for heavy
        /// attacks). Re-acquiring while already holding one is a no-op that returns true (idempotent). Negative ids
        /// (unmanaged agents) are always denied so they can never collide on a shared sentinel key.
        /// </summary>
        public bool TryAcquireToken(int agentId, int cost, bool heavyTell)
        {
            if (agentId < 0)
            {
                return false;
            }
            if (cost < 1)
            {
                cost = 1;
            }
            if (IndexOfToken(agentId) >= 0)
            {
                return true;                                          // already holding — idempotent
            }
            if (_tokensInUse + cost > _tokenBudget)
            {
                return false;                                         // budget full
            }
            if (heavyTell && _heavyTellsInUse >= _heavyTellBudget)
            {
                return false;                                         // a heavy tell is already on
            }
            if (_tokCount >= _tokAgent.Length)
            {
                return false;                                         // capacity guard (shouldn't hit; budget <= capacity)
            }

            _tokAgent[_tokCount] = agentId;
            _tokCost[_tokCount] = cost;
            _tokHeavy[_tokCount] = heavyTell;
            _tokCount++;
            _tokensInUse += cost;
            if (heavyTell)
            {
                _heavyTellsInUse++;
            }
            return true;
        }

        /// <summary>Release the agent's token if it holds one. Idempotent — safe to call on recover, stagger, AND death.</summary>
        public void ReleaseToken(int agentId)
        {
            if (agentId < 0)
            {
                return;
            }
            int idx = IndexOfToken(agentId);
            if (idx < 0)
            {
                return;
            }
            _tokensInUse -= _tokCost[idx];
            if (_tokHeavy[idx])
            {
                _heavyTellsInUse--;
            }
            int last = _tokCount - 1;            // swap-remove
            _tokAgent[idx] = _tokAgent[last];
            _tokCost[idx] = _tokCost[last];
            _tokHeavy[idx] = _tokHeavy[last];
            _tokCount--;
            if (_tokensInUse < 0)
            {
                _tokensInUse = 0;
            }
            if (_heavyTellsInUse < 0)
            {
                _heavyTellsInUse = 0;
            }
        }

        // ---------- approach-slot ring ----------

        public float SlotAngle(int index) => index * (360f / _slotCount);

        public int SlotOf(int agentId)
        {
            if (agentId < 0)
            {
                return -1;
            }
            for (int i = 0; i < _slotCount; i++)
            {
                if (_slotOwner[i] == agentId)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Claim the FREE slot whose centre angle is nearest <paramref name="desiredAngleDeg"/> (the agent's current
        /// bearing from the player), so it barely has to move. Sticky: if the agent already owns a slot, that same slot
        /// is returned (no thrash). Returns the slot index, or -1 if the ring is full (or the id is unmanaged).
        /// </summary>
        public int ClaimNearestFreeSlot(int agentId, float desiredAngleDeg)
        {
            if (agentId < 0)
            {
                return -1;
            }
            int owned = SlotOf(agentId);
            if (owned >= 0)
            {
                return owned;
            }

            int best = -1;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < _slotCount; i++)
            {
                if (_slotOwner[i] != EMPTY)
                {
                    continue;
                }
                float delta = AngularDistance(desiredAngleDeg, SlotAngle(i));
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }
            if (best >= 0)
            {
                _slotOwner[best] = agentId;
            }
            return best;
        }

        public void ReleaseSlot(int agentId)
        {
            if (agentId < 0)
            {
                return;
            }
            for (int i = 0; i < _slotCount; i++)
            {
                if (_slotOwner[i] == agentId)
                {
                    _slotOwner[i] = EMPTY;
                    return;
                }
            }
        }

        /// <summary>Watchdog: free everything an agent holds (called when it unregisters / dies). Idempotent.</summary>
        public void ReleaseAll(int agentId)
        {
            ReleaseToken(agentId);
            ReleaseSlot(agentId);
        }

        /// <summary>Smallest absolute angle between two bearings in degrees, in [0,180].</summary>
        public static float AngularDistance(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d < 0f)
            {
                d += 360f;
            }
            if (d > 180f)
            {
                d = 360f - d;
            }
            return d;
        }

        // ---------- threat ranking (pure) ----------

        /// <summary>A candidate snapshot for ranking; the director fills a pre-sized array of these (no alloc).</summary>
        public struct AgentThreat
        {
            public int AgentId;
            public float DistSq;       // squared distance to the player (closer = more threatening)
            public float FacingDot;    // dot of (player->agent) with player's facing (in front = more threatening), -1..1
            public float Aggression;   // 0..1 per-instance aggression
        }

        /// <summary>
        /// Fill <paramref name="order"/>[0..count) with indices into <paramref name="items"/> sorted most- to
        /// least-threatening. Distance is normalised by <paramref name="refRadiusSq"/> so a near-far span doesn't swamp
        /// the facing/aggression weights (without it, raw squared distance dominates and those knobs go dead).
        /// Allocation-free insertion sort (count is small); deterministic for a fixed input.
        /// </summary>
        public static void RankByThreat(AgentThreat[] items, int count, int[] order,
                                        float refRadiusSq = 64f, float wDist = 3f, float wFacing = 6f, float wAggro = 3f)
        {
            if (refRadiusSq < 0.0001f)
            {
                refRadiusSq = 0.0001f;
            }
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < count; i++)
            {
                int cur = order[i];
                float curScore = Score(items[cur], refRadiusSq, wDist, wFacing, wAggro);
                int j = i - 1;
                while (j >= 0 && Score(items[order[j]], refRadiusSq, wDist, wFacing, wAggro) < curScore)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }
        }

        private static float Score(in AgentThreat t, float refRadiusSq, float wDist, float wFacing, float wAggro)
            => t.FacingDot * wFacing + t.Aggression * wAggro - (t.DistSq / refRadiusSq) * wDist;

        // ---------- wave scaling ----------

        /// <summary>
        /// Token budget for a wave: starts at <paramref name="baseBudget"/>, gains one every
        /// <paramref name="wavesPerExtra"/> waves and one per extra co-op player, clamped to [1, maxBudget]. Wave
        /// escalation makes the crowd MORE AGGRESSIVE (more simultaneous attackers), not bigger.
        /// </summary>
        public static int BudgetForWave(int wave, int players, int baseBudget = 2, int wavesPerExtra = 3, int maxBudget = 5)
        {
            if (wave < 1)
            {
                wave = 1;
            }
            if (players < 1)
            {
                players = 1;
            }
            if (wavesPerExtra < 1)
            {
                wavesPerExtra = 1;
            }
            if (maxBudget < 1)
            {
                maxBudget = 1;
            }
            int b = baseBudget + (wave - 1) / wavesPerExtra + (players - 1);
            if (b < 1)
            {
                b = 1;
            }
            if (b > maxBudget)
            {
                b = maxBudget;
            }
            return b;
        }
    }
}
