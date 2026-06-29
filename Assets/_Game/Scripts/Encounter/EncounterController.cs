using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The arena brain (encounter-loop-spec): owns the objective + run flow over a <see cref="GarrisonDirector"/>.
    /// Starts the encounter (commander + escalation + checkpoint + arena XP mode), detects CLEAR (objective complete
    /// → bank the at-risk XP → Augment draft) and DEATH (forfeit at-risk XP → respawn at checkpoint → re-fight).
    /// Banked progress (level + augments on the player's runtime data clone) persists across deaths; in-memory only.
    /// Objectives are pluggable behind <see cref="IArenaObjective"/> (Slay now).
    /// </summary>
    public sealed class EncounterController : MonoBehaviour
    {
        [SerializeField] private GarrisonDirector garrison;
        [SerializeField] private PlayerController player;
        [Tooltip("Player respawn point (the arena-start checkpoint). Defaults to the player's start position.")]
        [SerializeField] private Transform checkpoint;
        [Tooltip("Seconds the YOU DIED prompt shows before the arena resets.")]
        [SerializeField] private float deathPause = 2f;

        public event System.Action<IArenaObjective> OnArenaStarted;
        public event System.Action OnArenaCleared;
        public event System.Action OnPlayerDied;
        public event System.Action OnArenaReset;

        private IArenaObjective _objective;
        private Vector3 _checkpointPos;
        private enum State { Running, Cleared, Dying }
        private State _state;
        private float _deathTimer;

        public IArenaObjective Objective => _objective;
        public GarrisonDirector Garrison => garrison;

        private void Start()
        {
            if (garrison == null) garrison = FindFirstObjectByType<GarrisonDirector>();
            if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.GetComponent<PlayerController>(); }
            _checkpointPos = checkpoint != null ? checkpoint.position : (player != null ? player.transform.position : Vector3.zero);
            _objective = new SlayCommanderObjective(garrison);
            if (player != null) player.Died += OnPlayerDeath;
            BeginArena();
        }

        private void OnDestroy()
        {
            if (player != null) player.Died -= OnPlayerDeath;
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(false);
        }

        private void BeginArena()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(true);   // kills accrue uncommitted now
            _objective.OnArenaStart();
            if (garrison != null) garrison.Begin();
            _state = State.Running;
            OnArenaStarted?.Invoke(_objective);
        }

        private void Update()
        {
            if (_state == State.Running)
            {
                _objective?.Tick(Time.deltaTime);
                if (_objective != null && _objective.IsComplete) Clear();
            }
            else if (_state == State.Dying)
            {
                _deathTimer -= Time.unscaledDeltaTime;   // unscaled so it survives any hit-stop/pause
                if (_deathTimer <= 0f) ResetArena();
            }
        }

        private void Clear()
        {
            _state = State.Cleared;
            if (garrison != null) garrison.Stop();
            OnArenaCleared?.Invoke();
            // Bank the at-risk spoils → levels → the Augment draft (reuses the existing pipeline). Heavy farm = several
            // draws; a rush = one or none.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetArenaXpMode(false);
                GameManager.Instance.BankUncommitted();
                GameManager.Instance.ResolvePendingAtBreather();   // pause + draft; no-op if no levels were gained
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
            if (garrison != null) garrison.Begin();   // despawn all, reset budget/timer, respawn commander
            if (GameManager.Instance != null) GameManager.Instance.SetArenaXpMode(true);
            _state = State.Running;
            OnArenaReset?.Invoke();
        }
    }
}
