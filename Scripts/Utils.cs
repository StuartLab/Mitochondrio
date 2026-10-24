using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace Mitochondrio_OpenGL.Scripts
{
    // Various static helper methods
    internal static class Utils
    {
        private static Random? _random;

        public static Random Random
        {
            get
            {
                if (_random == null)
                    _random = new Random();
                return _random;
            }
        }

        public static float RandomRange(float min, float max)
        {
            return min + Random.NextSingle() * (max - min);
        }

        public static int RandomRange(int min, int max)
        {
            return Random.Next(min, max);
        }


        public static float RandomGaussian(float stddev, float mean)
        {
            float theta = 2f * MathF.PI * RandomRange(0f, 1f);
            float rho = MathF.Sqrt(-2 * MathF.Log(1 - RandomRange(0f, 1f)));
            float scale = stddev * rho;
            float x = mean + scale * MathF.Cos(theta);
            float y = mean + scale * MathF.Sin(theta);
            return x;
        }

        public static float BellCurve(float minValue = 0f, float maxValue = 1f)
        {
            float u, v, S;

            do
            {
                u = 2.0f * RandomRange(0f, 1f) - 1.0f;
                v = 2.0f * RandomRange(0f, 1f) - 1.0f;
                S = u * u + v * v;
            }
            while (S >= 1.0f);

            // Standard Normal Distribution
            float stddev = u * MathF.Sqrt(-2.0f * MathF.Log(S) / S);

            // Normal Distribution centered between the min and max value
            // and clamped following the "three-sigma rule"
            float mean = (minValue + maxValue) / 2.0f;
            float sigma = (maxValue - mean) / 3.0f;
            return Math.Clamp(stddev * sigma + mean, minValue, maxValue);
        }

        // Used to get random points distributed around a sphere (the nucleus)
        public static float RandomGaussian()
        {
            return RandomGaussian(1, 0);
        }

        // Moves towards but never past a point, up to a maximum distance at once
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
        {
            Vector3 delta = target - current;
            float distance = delta.Length;

            if (distance <= maxDistanceDelta || distance == 0f)
                return target;

            return current + delta / distance * maxDistanceDelta;
        }

        public static Vector3 Scaled(this Vector3 toScale, Vector3 scaleVector)
        {
            return new Vector3(
                toScale.X * scaleVector.X,
                toScale.Y * scaleVector.Y,
                toScale.Z * scaleVector.Z
            );
        }


        public static Vector3 Scaled(this Vector3 toScale, float scaler)
        {
            return new Vector3(
                toScale.X * scaler,
                toScale.Y * scaler,
                toScale.Z * scaler
                );
        }
    }
}
