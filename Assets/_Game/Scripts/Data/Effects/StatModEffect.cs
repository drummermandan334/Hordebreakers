using System;
using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Which <see cref="PlayerCombatData"/> stat a <see cref="StatModEffect"/> changes.</summary>
    public enum StatTarget
    {
        // NOTE: these are serialized by integer index in the card .assets — only ever APPEND new targets at the end,
        // never reorder/insert, or existing cards silently retarget. (0..11 are the original set.)
        MaxHp,
        MoveSpeed,
        DodgeSpeed,
        DodgeDuration,
        DodgeIFrames,
        DodgeCooldown,
        LightDamage,
        LightReach,
        HeavyDamage,
        HeavyReach,
        ComboChainOpen,
        AttackSteerSpeed,
        // --- appended post-stamina (v0.4); keep appending below this line only ---
        DodgeMaxCharges,
        DodgeCancelPhase,
        AttackSpeed,
        BlockMitigation,
        GuardBreakResist,
        Hyperarmor,
        MusouGainDealt,
        MusouGainTaken
    }

    /// <summary>How a buff combines with the current value.</summary>
    public enum StatOp
    {
        Add,        // amount is a flat delta
        Multiply    // amount is a multiplier (1.1 = +10%)
    }

    /// <summary>
    /// Buffs a single <see cref="PlayerCombatData"/> stat on the runtime clone. Optional synergy scaling
    /// grows the effect by how many owned augments carry <see cref="synergyTag"/> — so a stat can scale
    /// with the build instead of a flat trickle. Raising MaxHp also heals by the gain; DodgeIFrames is
    /// clamped to DodgeDuration (the i-frame window can't outlast the dodge).
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hordebreakers", "Assembly-CSharp", null)]
    public sealed class StatModEffect : AugmentEffect
    {
        [SerializeField] private StatTarget target;
        [SerializeField] private StatOp op = StatOp.Add;
        [SerializeField] private float amount = 1f;

        [Header("Synergy (optional)")]
        [Tooltip("If set, the effect scales with how many owned augments carry this tag.")]
        [SerializeField] private AugmentTag synergyTag = AugmentTag.None;
        [Tooltip("Extra 'amount' added per owned augment with the synergy tag.")]
        [SerializeField] private float perStack = 0f;

        public override void Apply(in AugmentContext ctx)
        {
            PlayerCombatData d = ctx.Stats;
            if (d == null) return;

            float effective = amount;
            if (synergyTag != AugmentTag.None && perStack != 0f && ctx.Loadout != null)
            {
                effective += perStack * ctx.Loadout.CountWithTag(synergyTag);
            }

            switch (target)
            {
                case StatTarget.MaxHp:
                {
                    float gain = Combine(d.maxHp, effective) - d.maxHp;
                    d.maxHp += gain;
                    ctx.Player.Heal(gain);   // keep current HP in step, as the original upgrade did
                    break;
                }
                case StatTarget.MoveSpeed: d.moveSpeed = Combine(d.moveSpeed, effective); break;
                case StatTarget.DodgeSpeed: d.dodgeSpeed = Combine(d.dodgeSpeed, effective); break;
                case StatTarget.DodgeDuration: d.dodgeDuration = Combine(d.dodgeDuration, effective); break;
                case StatTarget.DodgeIFrames: d.dodgeIFrames = Mathf.Min(Combine(d.dodgeIFrames, effective), d.dodgeDuration); break;
                case StatTarget.DodgeCooldown: d.dodgeCooldown = Mathf.Max(0f, Combine(d.dodgeCooldown, effective)); break;
                case StatTarget.DodgeMaxCharges:
                {
                    d.dodgeMaxCharges = Mathf.Max(1, Mathf.RoundToInt(Combine(d.dodgeMaxCharges, effective)));
                    ctx.Player?.RefillDodgeCharges();   // make the extra dodge available immediately on pick-up
                    break;
                }
                case StatTarget.DodgeCancelPhase: d.dodgeCancelPhase = Mathf.Clamp01(Combine(d.dodgeCancelPhase, effective)); break;
                case StatTarget.LightDamage: d.lightDamage = Combine(d.lightDamage, effective); break;
                case StatTarget.LightReach: d.lightReach = Combine(d.lightReach, effective); break;
                case StatTarget.HeavyDamage: d.heavyDamage = Combine(d.heavyDamage, effective); break;
                case StatTarget.HeavyReach: d.heavyReach = Combine(d.heavyReach, effective); break;
                case StatTarget.ComboChainOpen: d.comboChainOpen = Mathf.Clamp01(Combine(d.comboChainOpen, effective)); break;
                case StatTarget.AttackSteerSpeed: d.attackSteerSpeed = Combine(d.attackSteerSpeed, effective); break;
                case StatTarget.AttackSpeed: d.attackSpeedMult = Mathf.Max(0.01f, Combine(d.attackSpeedMult, effective)); break;
                case StatTarget.BlockMitigation: d.blockMitigation = Mathf.Clamp01(Combine(d.blockMitigation, effective)); break;
                case StatTarget.GuardBreakResist: d.guardBreakResist = Mathf.Max(0f, Combine(d.guardBreakResist, effective)); break;
                case StatTarget.Hyperarmor: d.heavyHyperarmor = Mathf.Max(0f, Combine(d.heavyHyperarmor, effective)); break;
                case StatTarget.MusouGainDealt: d.musouGainDealtPerDamage = Mathf.Max(0f, Combine(d.musouGainDealtPerDamage, effective)); break;
                case StatTarget.MusouGainTaken: d.musouGainTakenPerDamage = Mathf.Max(0f, Combine(d.musouGainTakenPerDamage, effective)); break;
            }
        }

        private float Combine(float current, float value)
        {
            return op == StatOp.Add ? current + value : current * value;
        }
    }
}
