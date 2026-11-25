namespace RacingGame
{
    /// <summary>
    /// Pure geometric line-segment intersection math for directional checkpoint gates (zero UnityEngine dependency).
    /// </summary>
    public static class CheckpointGateMath
    {
        public static bool IsSegmentCrossingGateForward(
            float fromX, float fromY,
            float toX, float toY,
            float gatePosX, float gatePosY,
            float gateForwardX, float gateForwardY,
            float gateHalfWidth)
        {
            float moveX = toX - fromX;
            float moveY = toY - fromY;
            if (moveX * moveX + moveY * moveY < 0.000001f) return false;

            float dotForward = moveX * gateForwardX + moveY * gateForwardY;
            if (dotForward <= 0f) return false;

            float rightX = gateForwardY;
            float rightY = -gateForwardX;
            float safeHalfWidth = CoreMath.Max(0.5f, gateHalfWidth);

            float ax = gatePosX - rightX * safeHalfWidth;
            float ay = gatePosY - rightY * safeHalfWidth;
            float bx = gatePosX + rightX * safeHalfWidth;
            float by = gatePosY + rightY * safeHalfWidth;

            float gateX = bx - ax;
            float gateY = by - ay;

            float denom = Cross(moveX, moveY, gateX, gateY);
            if (CoreMath.Abs(denom) < 0.000001f) return false;

            float diffX = ax - fromX;
            float diffY = ay - fromY;
            float t = Cross(diffX, diffY, gateX, gateY) / denom;
            float u = Cross(diffX, diffY, moveX, moveY) / denom;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static float Cross(float ax, float ay, float bx, float by)
        {
            return ax * by - ay * bx;
        }
    }
}
