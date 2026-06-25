using UnityEngine;

namespace Hordebreakers
{
    /// <summary>
    /// Shared helper that classifies which way a hit came from, relative to the victim's facing,
    /// into the directional hit-react index used by the animator controllers (the <c>HitDir</c> int):
    /// 0 = Front, 1 = Back, 2 = Left, 3 = Right. Picks the dominant axis (forward vs. right).
    /// </summary>
    public static class HitReaction
    {
        public const int Front = 0;
        public const int Back = 1;
        public const int Left = 2;
        public const int Right = 3;

        public static int Direction(Vector3 victimPos, Vector3 forward, Vector3 right, Vector3 sourcePos)
        {
            Vector3 to = sourcePos - victimPos; to.y = 0f;
            if (to.sqrMagnitude < 1e-6f) return Front;   // no usable direction → treat as frontal
            to.Normalize();
            float f = Vector3.Dot(to, forward);
            float r = Vector3.Dot(to, right);
            if (Mathf.Abs(f) >= Mathf.Abs(r)) return f >= 0f ? Front : Back;
            return r >= 0f ? Right : Left;
        }
    }
}
