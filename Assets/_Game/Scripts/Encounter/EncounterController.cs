using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The arena brain (encounter-loop-spec): owns the objective + run flow over a <see cref="GarrisonDirector"/>.
    /// Runs the demo as <see cref="roundCount"/> ROUNDS: clear a round (objective complete → bank the at-risk XP →
    /// Augment draft) → a short breather → the next round's garrison mobilizes; after the LAST round → DEMO COMPLETE.
    /// DEATH forfeits the at-risk XP and re-fights the CURRENT round from the checkpoint. Banked progress (level +
    /// augments on the player's runtime data clone) persists across deaths AND rounds; in-memory only. Objectives are
    /// pluggable behind <see cref="IArenaObjective"/> (Slay now).
    /// </summary>
    public sealed class EncounterController : MonoBehaviour
    {
        [SerializeField] private GarrisonDirector garrison;
        [SerializeField] private PlayerController player;
        [Tooltip("Player respawn point (the arena-start checkpoint). Defaults to the player's start position.")]
        [SerializeField] private Transform checkpoint;
        [Tooltip("Seconds the YOU DIED prompt shows before the arena resets.")]
        [SerializeField] private float deathPause = 2f;
        [Tooltip("How many rounds the demo runs. Clear a round → draft upgrades → next round; after the last → DEMO COMPLETE. The point of >1 is to feel the upgrades carry into a fresh fight.")]
        [SerializeField] private int roundCount = 2;
        [Tooltip("Breather (seconds, real play-time) after a round's draft is finished before the next round's garrison mobilizes.")]
        [SerializeField] private float interRoundDelay = 6f;

        public event System.Action<IArenaObjective> OnArenaStarted;
        public event System.Action OnArenaCleared;      // a (non-final) round was cleared
        public event System.Action OnDemoComplete;      // the LAST round was cleared — the demo is won
        public event System.Action<int> OnRoundStarted; // a round began (1-based round number)
        public event System.Action OnPlayerDied;
        public event System.Action OnArenaReset;

        private IArenaObjective _objective;
        private Vector3 _checkpointPos;
        private enum State { Running, BetweenRounds, Dying, Complete }
        private State _state;
        private float _deathTimer;
        private int _round = 1;          // 1-based current round
        private float _interRoundTimer;  // counts down (scaled time) during the breather between rounds

        public IArenaObjective Objective => _objective;
        public GarrisonDirector Garrison => garrison;
        public int Round => _round;
        public int RoundCount => Mathf.Max(1, roundCount);
        public bool IsBetweenRounds => _state == State.BetweenRounds;
        public bool IsComplete => _state == State.Complete;
        /// <summary>Seconds until the next round mobilizes (0 unless in the between-rounds breather). Frozen while the draft is up.</summary>
        public float SecondsToNextRound => _state == State.BetweenRounds ? Mathf.Max(0f, _interRoundTimer) : 0f;

        private void Start()
        {
            if (garrison == null) garrison = FindFirstObjectByType<GarrisonDirector>();
            if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.GetComponent<PlayerController>(); }
            _checkpointPos = checkpoint != null ? checkpoint.position : (player != null ? player.transform.position : Vector3.zero);
            _objective = new SlayCommanderObjective(garrison);
            if (player != null) player.Died += OnPlayerDeath;
            _round = 1;
            BeginRound();
        }

        private void OnDestroy()
        {
            if (player != null) player.Died -= OnPlayerDeath;
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(false);
        }

        private void BeginRound()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(true);   // kills accrue uncommitted now
            _objective.OnArenaStart();
            if (garrison != null) garrison.Begin();   // despawns everything, resets the wave clock + commander, runs
            _state = State.Running;
            OnArenaStarted?.Invoke(_objective);
            OnRoundStarted?.Invoke(_round);
        }

        private void Update()
        {
            if (_state == State.Running)
            {
                _objective?.Tick(Time.deltaTime);
                if (_objective != null && _objective.IsComplete) Clear();
            }
            else if (_state == State.BetweenRounds)
            {
                // SCALED time: the draft pauses the game (timeScale 0), so this only counts down AFTER the last pick is
                // taken — the next round won't start until the player has finished drafting.
                _interRoundTimer -= Time.deltaTime;
                if (_interRoundTimer <= 0f) { _round++; BeginRound(); }
            }
            else if (_state == State.Dying)
            {
                _deathTimer -= Time.unscaledDeltaTime;   // unscaled so it survives any hit-stop/pause
                if (_deathTimer <= 0f) ResetArena();
            }
        }

        private void Clear()
        {
            if (garrison != null) garrison.Stop();
            // Bank the at-risk spoils → levels → the Augment draft (reuses the existing pipeline). The draft pauses the
            // run; the between-rounds timer below is scaled, so the next round waits until the player finishes picking.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetArenaXpMode(false);
                GameManager.Instance.BankUncommitted();
                GameManager.Instance.ResolvePendingAtBreather();   // pause + draft; no-op if no levels were gained
            }

            if (_round < RoundCount)
            {
                _state = State.BetweenRounds;
                _interRoundTimer = interRoundDelay;
                OnArenaCleared?.Invoke();
            }
            else
            {
                _state = State.Complete;
                OnDemoComplete?.Invoke();
            }
        }

        private void OnPlayerDeath()
        {
            if (_state != State.Running) return;
            _state = State.Dying;
            _deathTimer = deathPause;
            if (GameManager.Instance != null) GameManager.Instance.DiscardUncommitted();   // forfeit the greed spoils
            OnPlayerDied?.Invoke();
        }

        private void ResetArena()
        {
            if (player != null) player.Respawn(_checkpointPos);
            if (garrison != null) garrison.Begin();   // re-fight the CURRENT round: despawn all, reset budget/timer, fresh commander
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(true);
            _state = State.Running;
            OnArenaReset?.Invoke();
            OnRoundStarted?.Invoke(_round);
        }
    }
}
