using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// The elven "advisor" in the throne room — a non-combatant who watches the King's fight, detached, and SLIPS AWAY
    /// the moment the King (the GarrisonDirector's boss/commander) falls: the dramatic-irony reveal that the elves were
    /// the hand behind the goblins all along. No combat, no health — pure staging. It walks to an exit and vanishes.
    /// Drive it either by polling the boss's death (default) or by calling <see cref="BeginExit"/> from an encounter event.
    /// </summary>
    public sealed class AdvisorReveal : MonoBehaviour
    {
        [Tooltip("Where the advisor slips away to (a doorway off the arena). If unset, he simply stands, then vanishes.")]
        [SerializeField] private Transform exitPoint;
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float turnSpeedDeg = 360f;
        [Tooltip("Optional — drives a 'Speed' float param so a walk clip plays as he leaves.")]
        [SerializeField] private Animator animator;
        [Tooltip("Seconds after reaching the exit before he's deactivated (slips out of sight).")]
        [SerializeField] private float vanishDelay = 0.5f;

        private bool _bossWasAlive;
        private bool _leaving;
        private float _vanishTimer = -1f;
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (!_leaving)
            {
                // Watch the boss: once he's been alive and then isn't, the advisor makes his exit.
                GarrisonDirector gd = GarrisonDirector.Instance;
                if (gd != null)
                {
                    if (gd.CommanderAlive) _bossWasAlive = true;
                    else if (_bossWasAlive) BeginExit();
                }
                return;
            }
            TickExit(Time.deltaTime);
        }

        /// <summary>Trigger the slip-away directly (e.g. from an EncounterController death event) instead of polling.</summary>
        public void BeginExit() => _leaving = true;

        private void TickExit(float dt)
        {
            if (exitPoint != null && _vanishTimer < 0f)
            {
                Vector3 to = exitPoint.position - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 0.16f)   // still walking to the exit
                {
                    Vector3 dir = to.normalized;
                    transform.position += dir * walkSpeed * dt;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeedDeg * dt);
                    if (animator != null) animator.SetFloat(AnimSpeed, 1f);
                    return;
                }
            }
            // arrived (or no exit set) → stop, wait a beat, slip out of sight
            if (animator != null) animator.SetFloat(AnimSpeed, 0f);
            if (_vanishTimer < 0f) _vanishTimer = vanishDelay;
            _vanishTimer -= dt;
            if (_vanishTimer <= 0f) gameObject.SetActive(false);
        }
    }
}
