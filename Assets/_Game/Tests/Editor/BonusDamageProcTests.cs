using NUnit.Framework;
using UnityEngine;

namespace Hordebreakers.Tests
{
    public class BonusDamageProcTests
    {
        private static OnHitContext Hit(MockDamageable victim, float damage = 10f, bool heavy = false)
            => new OnHitContext(null, victim, Vector3.zero, damage, heavy, false);

        [Test]
        public void Flat_AppliesFlatBonus()
        {
            var proc = new BonusDamageProc().With("flat", 5f).With("fractionOfHit", 0f).With("heavyOnly", false);
            var victim = new MockDamageable();

            proc.OnHit(Hit(victim, 10f));

            Assert.AreEqual(1, victim.HitCount);
            Assert.AreEqual(5f, victim.TotalDamage, 0.0001f);
        }

        [Test]
        public void Fraction_AppliesShareOfHit()
        {
            var proc = new BonusDamageProc().With("flat", 0f).With("fractionOfHit", 0.5f).With("heavyOnly", false);
            var victim = new MockDamageable();

            proc.OnHit(Hit(victim, 20f));

            Assert.AreEqual(10f, victim.TotalDamage, 0.0001f);   // 0.5 * 20
        }

        [Test]
        public void FlatPlusFraction_Stack()
        {
            var proc = new BonusDamageProc().With("flat", 3f).With("fractionOfHit", 0.25f).With("heavyOnly", false);
            var victim = new MockDamageable();

            proc.OnHit(Hit(victim, 40f));

            Assert.AreEqual(13f, victim.TotalDamage, 0.0001f);   // 3 + 0.25 * 40
        }

        [Test]
        public void HeavyOnly_SkipsLightHits()
        {
            var proc = new BonusDamageProc().With("flat", 5f).With("fractionOfHit", 0f).With("heavyOnly", true);
            var victim = new MockDamageable();

            proc.OnHit(Hit(victim, 10f, heavy: false));

            Assert.AreEqual(0, victim.HitCount);
        }

        [Test]
        public void HeavyOnly_AppliesOnHeavyHits()
        {
            var proc = new BonusDamageProc().With("flat", 5f).With("fractionOfHit", 0f).With("heavyOnly", true);
            var victim = new MockDamageable();

            proc.OnHit(Hit(victim, 10f, heavy: true));

            Assert.AreEqual(5f, victim.TotalDamage, 0.0001f);
        }

        [Test]
        public void DeadVictim_IsSkipped()
        {
            var proc = new BonusDamageProc().With("flat", 5f).With("fractionOfHit", 0f).With("heavyOnly", false);
            var victim = new MockDamageable { Alive = false };

            proc.OnHit(Hit(victim, 10f));

            Assert.AreEqual(0, victim.HitCount);
        }

        [Test]
        public void NullVictim_IsSafe()
        {
            var proc = new BonusDamageProc().With("flat", 5f).With("fractionOfHit", 0f).With("heavyOnly", false);
            Assert.DoesNotThrow(() => proc.OnHit(new OnHitContext(null, null, Vector3.zero, 10f, false, false)));
        }
    }
}
