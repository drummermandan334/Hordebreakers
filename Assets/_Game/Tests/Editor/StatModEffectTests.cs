using NUnit.Framework;
using UnityEngine;

namespace Hordebreakers.Tests
{
    public class StatModEffectTests
    {
        private static PlayerCombatData Stats(float moveSpeed = 5f)
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.moveSpeed = moveSpeed;
            return d;
        }

        // Player is null on purpose — only the MaxHp branch dereferences it, and these tests avoid that target.
        private static AugmentContext Ctx(PlayerCombatData stats, PlayerLoadout loadout = null)
            => new AugmentContext(null, stats, loadout);

        [Test]
        public void Add_IncreasesStatByAmount()
        {
            PlayerCombatData d = Stats(5f);
            var effect = new StatModEffect()
                .With("target", StatTarget.MoveSpeed).With("op", StatOp.Add).With("amount", 2f);

            effect.Apply(Ctx(d));

            Assert.AreEqual(7f, d.moveSpeed, 0.0001f);
        }

        [Test]
        public void Multiply_ScalesStat()
        {
            PlayerCombatData d = Stats(4f);
            var effect = new StatModEffect()
                .With("target", StatTarget.MoveSpeed).With("op", StatOp.Multiply).With("amount", 1.5f);

            effect.Apply(Ctx(d));

            Assert.AreEqual(6f, d.moveSpeed, 0.0001f);
        }

        [Test]
        public void Synergy_AddsPerStackForEachTaggedAugment()
        {
            var lo = new PlayerLoadout(CharacterClass.Universal, null);
            Augment t1 = ScriptableObject.CreateInstance<Augment>(); t1.tags = AugmentTag.Offense; lo.Record(t1);
            Augment t2 = ScriptableObject.CreateInstance<Augment>(); t2.tags = AugmentTag.Offense; lo.Record(t2);

            PlayerCombatData d = Stats(5f);
            var effect = new StatModEffect()
                .With("target", StatTarget.MoveSpeed).With("op", StatOp.Add).With("amount", 1f)
                .With("synergyTag", AugmentTag.Offense).With("perStack", 2f);

            effect.Apply(Ctx(d, lo));

            // effective amount = 1 + 2 * (2 owned Offense) = 5  ->  5 + 5 = 10
            Assert.AreEqual(10f, d.moveSpeed, 0.0001f);
        }

        [Test]
        public void DodgeIFrames_ClampedToDodgeDuration()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.dodgeIFrames = 0.1f;
            d.dodgeDuration = 0.3f;
            var effect = new StatModEffect()
                .With("target", StatTarget.DodgeIFrames).With("op", StatOp.Add).With("amount", 5f);

            effect.Apply(Ctx(d));

            Assert.AreEqual(0.3f, d.dodgeIFrames, 0.0001f, "i-frames must never outlast the dodge");
        }

        [Test]
        public void NullStats_IsSafe()
        {
            var effect = new StatModEffect()
                .With("target", StatTarget.MoveSpeed).With("op", StatOp.Add).With("amount", 2f);

            Assert.DoesNotThrow(() => effect.Apply(Ctx(null)));
        }

        // ---- New post-stamina combat dials (the Augment surface that loosens the re-gated defense) ----

        [Test]
        public void AttackSpeed_Multiplies()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.attackSpeedMult = 1f;
            new StatModEffect().With("target", StatTarget.AttackSpeed).With("op", StatOp.Multiply).With("amount", 1.2f)
                .Apply(Ctx(d));
            Assert.AreEqual(1.2f, d.attackSpeedMult, 0.0001f);
        }

        [Test]
        public void BlockMitigation_ClampedTo01()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.blockMitigation = 0.7f;
            new StatModEffect().With("target", StatTarget.BlockMitigation).With("op", StatOp.Add).With("amount", 5f)
                .Apply(Ctx(d));
            Assert.AreEqual(1f, d.blockMitigation, 0.0001f, "mitigation can't exceed 100%");
        }

        [Test]
        public void GuardBreakResist_RaisedFromZero()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.guardBreakResist = 0f;
            new StatModEffect().With("target", StatTarget.GuardBreakResist).With("op", StatOp.Add).With("amount", 1f)
                .Apply(Ctx(d));
            Assert.AreEqual(1f, d.guardBreakResist, 0.0001f);
        }

        [Test]
        public void DodgeMaxCharges_AddsWholeCharge_AndIsSafeWithNullPlayer()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.dodgeMaxCharges = 1;
            var effect = new StatModEffect()
                .With("target", StatTarget.DodgeMaxCharges).With("op", StatOp.Add).With("amount", 1f);
            // Ctx(d) passes a null player; the case must null-guard the RefillDodgeCharges() callback.
            Assert.DoesNotThrow(() => effect.Apply(Ctx(d)));
            Assert.AreEqual(2, d.dodgeMaxCharges);
        }

        [Test]
        public void MusouGainDealt_Multiplies()
        {
            PlayerCombatData d = ScriptableObject.CreateInstance<PlayerCombatData>();
            d.musouGainDealtPerDamage = 0.9f;
            new StatModEffect().With("target", StatTarget.MusouGainDealt).With("op", StatOp.Multiply).With("amount", 2f)
                .Apply(Ctx(d));
            Assert.AreEqual(1.8f, d.musouGainDealtPerDamage, 0.0001f);
        }
    }
}
