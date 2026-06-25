using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Deals extra damage to the victim on hit — a flat amount plus a fraction of the hit, optionally heavy-only.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class BonusDamageProc : OnHitProc
    {
        [SerializeField] private float flat = 0f;
        [Range(0f, 2f)]
        [SerializeField] private float fractionOfHit = 0f;
        [SerializeField] private bool heavyOnly = false;

        public override void OnHit(OnHitContext ctx)
        {
            if (ctx.Victim == null || !ctx.Victim.IsAlive) return;
            if (heavyOnly && !ctx.IsHeavy) return;
            float bonus = flat + fractionOfHit * ctx.Damage;
            if (bonus > 0f) ctx.Victim.TakeDamage(bonus, ctx.HitPoint);
        }
    }
}
