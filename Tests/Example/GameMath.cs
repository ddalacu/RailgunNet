using System;

namespace Tests.Example
{
    public static class GameMath
    {
        public const float COORDINATE_PRECISION = 0.001f;
        public static bool CoordinatesEqual(float a, float b)
        {
            return Math.Abs(a - b) < COORDINATE_PRECISION;
        }
    }
}
