using System;

namespace SlapMania.Core
{
    public interface IRandom
    {
        float NextFloat01();
        int NextInt(int minInclusive, int maxExclusive);
        float NextGaussian(float mean, float sigma);
    }

    public sealed class SeededRandom : IRandom
    {
        private readonly Random _rng;
        private double _spare;
        private bool _hasSpare;

        public SeededRandom(int seed) { _rng = new Random(seed); }

        public float NextFloat01() => (float)_rng.NextDouble();

        public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        public float NextGaussian(float mean, float sigma)
        {
            if (_hasSpare)
            {
                _hasSpare = false;
                return mean + sigma * (float)_spare;
            }
            double u, v, s;
            do
            {
                u = _rng.NextDouble() * 2.0 - 1.0;
                v = _rng.NextDouble() * 2.0 - 1.0;
                s = u * u + v * v;
            } while (s >= 1.0 || s <= 0.0);
            double mul = System.Math.Sqrt(-2.0 * System.Math.Log(s) / s);
            _spare = v * mul;
            _hasSpare = true;
            return mean + sigma * (float)(u * mul);
        }
    }
}
