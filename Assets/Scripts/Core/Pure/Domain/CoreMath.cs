using System;

namespace RacingGame
{
    /// <summary>
    /// Engine-independent scalar math helpers for the pure C# domain layer (zero UnityEngine dependency).
    /// </summary>
    public static class CoreMath
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        public static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        public static int Max(int a, int b)
        {
            return a > b ? a : b;
        }

        public static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        public static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }

        public static float Sqrt(float value)
        {
            return value <= 0f ? 0f : (float)Math.Sqrt(value);
        }

        public static float Pow(float value, float power)
        {
            return (float)Math.Pow(value, power);
        }

        public static int RoundToInt(float value)
        {
            return (int)Math.Round(value);
        }
    }
}
