using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Calls a meteor down from the sky onto the locked target (or in front of the player), exploding for AoE
    /// damage on impact — the signature Cataclysm for the fire battle-sorcerer. Spawns a falling fireball FX above
    /// the strike point aimed straight down; a <see cref="Meteor"/> on it handles the impact (damage + explosion).
    /// </summary>
    [Serializable]
    public sealed class MeteorEffect : AbilityEffect
    {
        [Tooltip("The falling fireball FX prefab (has a Meteor + the self-driving particle systems).")]
        [SerializeField] private GameObject meteorPrefab;
        [SerializeField] private float radius = 6f;
        [SerializeField] private float damage = 90f;
        [Tooltip("Where the meteor lands if there's no locked target — this far in front of the player.")]
        [SerializeField] private float forwardRange = 8f;
        [Tooltip("Height above the strike point the meteor spawns at.")]
        [SerializeField] private float spawnHeight = 18f;
        [Tooltip("Fall speed (drives the fireball's particle Start Speed + the impact timing).")]
        [SerializeField] private float fallSpeed = 28f;

        [Header("Impact")]
        [SerializeField] private GameObject impactVfx;
        [SerializeField] private float impactScale = 3.5f;
        [SerializeField] private Vector2 shake = new Vector2(0.4f, 0.45f);
        [SerializeField] private float autoDestroy = 4f;

        public override void Activate(PlayerController player)
        {
            if (meteorPrefab == null) return;

            Vector3 strike = (TargetLock.Instance != null && TargetLock.Instance.HasTarget)
                ? TargetLock.Instance.Target.position
                : player.transform.position + player.ModelRoot.forward * forwardRange;
            strike.y = player.transform.position.y;   // ground level

            Vector3 spawn = strike + Vector3.up * spawnHeight;
            GameObject go = UnityEngine.Object.Instantiate(meteorPrefab, spawn, Quaternion.LookRotation(Vector3.down));
            if (go.TryGetComponent(out ParticleSystem ps))   // the FX self-drives the fall; sync its speed
            {
                ParticleSystem.MainModule m = ps.main;
                m.startSpeed = fallSpeed;
                ps.Clear(true);
                ps.Play(true);
            }
            float strikeDelay = fallSpeed > 0.1f ? spawnHeight / fallSpeed : 1f;
            if (go.TryGetComponent(out Meteor meteor))
                meteor.Init(player, strike, radius, damage, impactVfx, impactScale, shake, strikeDelay, autoDestroy);
        }
    }
}
