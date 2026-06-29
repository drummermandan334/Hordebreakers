namespace Hordebreakers
{
    /// <summary>
    /// Pure poise resolution shared by every enemy. Keeps the "weight" rule in one testable place: light taps chip the
    /// poise pool but don't interrupt; a heavy/finisher (forceBreak) or an emptied pool is a real stagger that resets
    /// the pool. A non-positive pool means legacy behaviour — every hit staggers.
    /// </summary>
    public static class PoiseTracker
    {
        /// <summary>
        /// Apply <paramref name="damage"/> to <paramref name="poise"/> and report whether poise BREAKS (-> real
        /// stagger). On a break the pool is reset to <paramref name="maxPoise"/>. <paramref name="forceBreak"/> (a
        /// heavy hit) always breaks. <paramref name="maxPoise"/> &lt;= 0 is legacy: always breaks.
        /// </summary>
        public static bool Resolve(ref float poise, float maxPoise, float damage, bool forceBreak)
        {
            if (maxPoise <= 0f)
            {
                return true;            // legacy: no poise -> every hit staggers
            }
            if (forceBreak)
            {
                poise = maxPoise;
                return true;
            }
            poise -= damage;
            if (poise <= 0f)
            {
                poise = maxPoise;
                return true;
            }
            return false;               // poise absorbed it — no interrupt
        }
    }
}
