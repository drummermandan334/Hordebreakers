using NUnit.Framework;
using UnityEngine;

namespace Hordebreakers.Tests
{
    public class PlayerLoadoutTests
    {
        private static Augment MakeAugment(AugmentTag tags = AugmentTag.None)
        {
            Augment a = ScriptableObject.CreateInstance<Augment>();
            a.tags = tags;
            return a;
        }

        private static PlayerLoadout NewLoadout() => new PlayerLoadout(CharacterClass.Universal, null);

        [Test]
        public void Record_IncrementsStacks_AndHasBecomesTrue()
        {
            PlayerLoadout lo = NewLoadout();
            Augment a = MakeAugment();
            Assert.AreEqual(0, lo.StacksOf(a));
            Assert.IsFalse(lo.Has(a));

            lo.Record(a);
            Assert.AreEqual(1, lo.StacksOf(a));
            Assert.IsTrue(lo.Has(a));

            lo.Record(a);
            Assert.AreEqual(2, lo.StacksOf(a));
        }

        [Test]
        public void StacksOf_UnknownAugment_IsZero()
        {
            PlayerLoadout lo = NewLoadout();
            Assert.AreEqual(0, lo.StacksOf(MakeAugment()));
        }

        [Test]
        public void CountWithTag_CountsOnlyMatchingAugments()
        {
            PlayerLoadout lo = NewLoadout();
            lo.Record(MakeAugment(AugmentTag.Offense));
            lo.Record(MakeAugment(AugmentTag.Offense | AugmentTag.Mobility));
            lo.Record(MakeAugment(AugmentTag.Defense));

            Assert.AreEqual(2, lo.CountWithTag(AugmentTag.Offense));
            Assert.AreEqual(1, lo.CountWithTag(AugmentTag.Mobility));
            Assert.AreEqual(1, lo.CountWithTag(AugmentTag.Defense));
            Assert.AreEqual(0, lo.CountWithTag(AugmentTag.Throw));
        }

        [Test]
        public void Remove_DecrementsStack_ThenClears()
        {
            PlayerLoadout lo = NewLoadout();
            Augment a = MakeAugment();
            lo.Record(a);
            lo.Record(a);

            lo.Remove(a);
            Assert.AreEqual(1, lo.StacksOf(a));
            Assert.IsTrue(lo.Has(a));

            lo.Remove(a);
            Assert.AreEqual(0, lo.StacksOf(a));
            Assert.IsFalse(lo.Has(a));
        }

        [Test]
        public void GrantAbility_Dedupes_AndIgnoresNull()
        {
            PlayerLoadout lo = NewLoadout();
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();

            lo.GrantAbility(ability);
            lo.GrantAbility(ability);   // same ability again — should not duplicate
            lo.GrantAbility(null);      // ignored

            Assert.AreEqual(1, lo.Abilities.Count);
            Assert.AreSame(ability, lo.Abilities[0]);
        }

        [Test]
        public void DispatchOnHit_FiresEveryRegisteredProc()
        {
            PlayerLoadout lo = NewLoadout();
            var p1 = new CountingProc();
            var p2 = new CountingProc();
            lo.AddOnHitProc(p1);
            lo.AddOnHitProc(p2);
            lo.AddOnHitProc(null);   // ignored
            Assert.AreEqual(2, lo.OnHitProcCount);

            var ctx = new OnHitContext(null, new MockDamageable(), Vector3.zero, 10f, false, false);
            lo.DispatchOnHit(ctx);
            lo.DispatchOnHit(ctx);

            Assert.AreEqual(2, p1.Fired);
            Assert.AreEqual(2, p2.Fired);
        }
    }
}
