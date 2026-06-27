using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Damages every enemy within a radius (a slam / nova / whirlwind). The center can be offset forward from the
    /// player. Enemies recoil outward from the center via their own knockback (scaled by the big ability damage).
    /// Camera shake on impact. The flashy VFX is a later juice pass — this is the mechanical effect.
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class AoeDamageEffect : AbilityEffect
    {
        [SerializeField] private float radius = 5f;
        [SerializeField] private float damage = 40f;
        [Tooltip("Center offset forward from the player (0 = centered on the player; >0 = a forward slam).")]
        [SerializeField] private float forwardOffset = 0f;
        [Tooltip("Camera shake on cast — x = amplitude, y = seconds.")]
        [SerializeField] private Vector2 shake = new Vector2(0.2f, 0.25f);

        [Header("Impact VFX")]
        [Tooltip("Fire/explosion VFX spawned at the blast center.")]
        [SerializeField] private GameObject impactVfx;
        [Tooltip("Uniform scale applied to the spawned VFX (size it to the radius).")]
        [SerializeField] private float impactScale = 1f;
        [SerializeField] private float impactLifetime = 3f;

        [Header("Impact audio")]
        [Tooltip("Explosion / boom played at the blast center on impact.")]
        [SerializeField] private AudioClip impactClip;
        [Range(0f, 1f)]
        [SerializeField] private float impactVolume = 1f;

        public override void Activate(PlayerController player)
        {
            Vector3 center = player.transform.position + player.ModelRoot.forward * forwardOffset;
            player.DealAreaDamage(center, radius, damage);
            PlayerCameraRig.Shake(shake.x, shake.y);
            if (impactClip != null) CombatAudio.PlaySpell(impactClip, center, impactVolume);
            if (impactVfx != null)
            {
                GameObject go = UnityEngine.Object.Instantiate(impactVfx, center, Quaternion.identity);
                if (!Mathf.Approximately(impactScale, 1f)) go.transform.localScale *= impactScale;
                UnityEngine.Object.Destroy(go, impactLifetime);
            }
        }
    }
}
