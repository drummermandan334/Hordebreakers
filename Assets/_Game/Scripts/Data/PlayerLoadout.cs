using System.Collections.Generic;

namespace Hordebreakers
{
    /// <summary>
    /// The run-scoped, per-player BUILD model: which class is playing, the weapons they hold, the
    /// abilities they've been granted, and every <see cref="Augment"/> taken this run. Synergy and
    /// evolution effects read it; the draft filters and weights against it. One loadout per character
    /// (co-op = four loadouts, no refactor). Owned by <see cref="PlayerController"/>, created at run
    /// start and discarded at run end. Holds the build only — run bookkeeping (XP/level/waves) stays
    /// in GameManager.
    /// </summary>
    public sealed class PlayerLoadout
    {
        private readonly List<Augment> _taken = new List<Augment>();
        private readonly Dictionary<Augment, int> _stacks = new Dictionary<Augment, int>();
        private readonly List<string> _grantedAbilities = new List<string>();
        private readonly List<OnHitProc> _onHitProcs = new List<OnHitProc>();

        public CharacterClass Class { get; }
        public ThrowWeapon Throw { get; }   // may be null; the weapon set grows over time

        public PlayerLoadout(CharacterClass characterClass, ThrowWeapon throwWeapon)
        {
            Class = characterClass;
            Throw = throwWeapon;
        }

        public IReadOnlyList<Augment> Taken => _taken;
        public IReadOnlyList<string> GrantedAbilities => _grantedAbilities;

        public int StacksOf(Augment augment)
        {
            return augment != null && _stacks.TryGetValue(augment, out int n) ? n : 0;
        }

        public bool Has(Augment augment) => StacksOf(augment) > 0;

        /// <summary>How many taken augments carry any of the given tag(s) — the synergy query.</summary>
        public int CountWithTag(AugmentTag tag)
        {
            int count = 0;
            for (int i = 0; i < _taken.Count; i++)
            {
                if ((_taken[i].tags & tag) != 0) count++;
            }
            return count;
        }

        /// <summary>Record an augment as taken (after its effects have applied).</summary>
        public void Record(Augment augment)
        {
            if (augment == null) return;
            _taken.Add(augment);
            _stacks[augment] = StacksOf(augment) + 1;
        }

        /// <summary>
        /// Consume an augment from the build (used by evolutions). Removes one stack from the build model;
        /// it does NOT reverse already-applied stat deltas (those would need delta tracking — out of scope).
        /// </summary>
        public void Remove(Augment augment)
        {
            if (augment == null) return;
            _taken.Remove(augment);
            int n = StacksOf(augment) - 1;
            if (n > 0) _stacks[augment] = n;
            else _stacks.Remove(augment);
        }

        /// <summary>
        /// Grant an ability by id. Until the ability system (Task A) exists this just records the grant so
        /// nothing is lost and synergy can see it; later it forwards to that system.
        /// </summary>
        public void GrantAbility(string abilityId, int level)
        {
            if (string.IsNullOrEmpty(abilityId)) return;
            _grantedAbilities.Add(abilityId);
        }

        // ---------- On-hit procs ----------
        public int OnHitProcCount => _onHitProcs.Count;

        /// <summary>Register a proc to fire when the player's melee hits land.</summary>
        public void AddOnHitProc(OnHitProc proc)
        {
            if (proc != null) _onHitProcs.Add(proc);
        }

        /// <summary>Fire every registered on-hit proc for a single landed hit. Called from the melee hit loop.</summary>
        public void DispatchOnHit(OnHitContext ctx)
        {
            for (int i = 0; i < _onHitProcs.Count; i++)
            {
                _onHitProcs[i].OnHit(ctx);
            }
        }
    }
}
