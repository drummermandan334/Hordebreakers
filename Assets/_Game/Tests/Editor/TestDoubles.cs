using System;
using System.Reflection;
using UnityEngine;

namespace Hordebreakers.Tests
{
    /// <summary>Test IDamageable that just records the damage dealt to it.</summary>
    internal sealed class MockDamageable : IDamageable
    {
        public bool Alive = true;
        public int HitCount;
        public float TotalDamage;
        public float LastDamage;
        public Vector3 LastSource;

        public bool IsAlive => Alive;
        public void TakeDamage(float amount) => TakeDamage(amount, Vector3.zero);
        public void TakeDamage(float amount, Vector3 sourcePos)
        {
            HitCount++;
            TotalDamage += amount;
            LastDamage = amount;
            LastSource = sourcePos;
        }
    }

    /// <summary>On-hit proc that just counts how many times it fired.</summary>
    internal sealed class CountingProc : OnHitProc
    {
        public int Fired;
        public override void OnHit(OnHitContext ctx) => Fired++;
    }

    internal static class Reflect
    {
        /// <summary>Fluent reflection setter so tests can configure an effect/proc's private [SerializeField]s.</summary>
        public static T With<T>(this T obj, string field, object value)
        {
            FieldInfo f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) throw new ArgumentException($"No field '{field}' on {obj.GetType().Name}");
            f.SetValue(obj, value);
            return obj;
        }
    }
}
