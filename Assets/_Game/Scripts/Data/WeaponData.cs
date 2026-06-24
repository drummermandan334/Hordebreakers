using UnityEngine;

namespace Hordebreakers
{
    /// <summary>Auto-weapon tuning. One asset per weapon (Throwing Knives, etc.).</summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Hordebreakers/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public float damage = 5f;
        public float fireInterval = 0.8f;
        public float range = 10f;
        public int marksPerHit = 1;
        public float projectileSpeed = 22f;
        public float projectileLifetime = 2f;
        public Projectile projectilePrefab;
    }
}
