namespace Hordebreakers
{
    /// <summary>Anything that can be hit by melee, projectiles, or a detonation.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount);
    }
}
