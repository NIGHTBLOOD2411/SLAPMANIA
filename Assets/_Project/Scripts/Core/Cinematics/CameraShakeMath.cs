using System;
using UnityEngine;

namespace SlapMania.Core.Cinematics
{
    /// A named shake character (GLOSSARY: **Trauma**). Strengths are at trauma 1; the actual
    /// shake scales with trauma², so small hits stay subtle and big ones read as violent (M16 §10).
    [Serializable]
    public struct ShakeProfile
    {
        public string name;
        /// Metres of positional jitter at full trauma.
        public float maxOffset;
        /// Pitch, yaw, roll in degrees at full trauma.
        public Vector3 maxAngles;
        /// Noise frequency in Hz: ~25 is a violent hit, ~6 a heavy rumble.
        public float frequency;
        /// Trauma lost per second.
        public float decayPerSecond;

        public ShakeProfile(string name, float maxOffset, Vector3 maxAngles, float frequency, float decayPerSecond)
        {
            this.name = name;
            this.maxOffset = maxOffset;
            this.maxAngles = maxAngles;
            this.frequency = frequency;
            this.decayPerSecond = decayPerSecond;
        }
    }

    /// Trauma shake as pure functions of (trauma, time) (R2): Perlin noise, never Random, so a
    /// shake is smooth and the same time gives the same offset.
    public static class CameraShakeMath
    {
        // Wrap the noise coordinate: Perlin input loses precision far from the origin.
        private const double Wrap = 4096.0;

        public static float Add(float trauma, float amount)
        {
            if (float.IsNaN(trauma)) trauma = 0f;
            if (float.IsNaN(amount) || amount <= 0f) return Clamp01(trauma);
            return Clamp01(trauma + amount);
        }

        /// Trauma for a slap of `intensity` (0..1, damage-scaled). Any landed hit gets at least
        /// `minTrauma`, so a weak slap still reads as contact (shake goes with trauma², so raw
        /// intensity 0.3 alone would be ~1 cm). `scale` above 1 reaches full trauma sooner.
        /// No hit (0, negative, NaN) means no shake.
        public static float ImpactTrauma(float intensity, float minTrauma, float scale)
        {
            if (float.IsNaN(intensity) || intensity <= 0f) return 0f;
            intensity = Clamp01(intensity);
            minTrauma = float.IsNaN(minTrauma) ? 0f : Clamp01(minTrauma);
            if (float.IsNaN(scale) || scale < 0f) scale = 1f;
            return Clamp01(minTrauma + (1f - minTrauma) * intensity * scale);
        }

        /// Linear decay, never below `floor` (a sustained rumble) and never below 0.
        public static float Decay(float trauma, float decayPerSecond, float dt, float floor = 0f)
        {
            if (float.IsNaN(trauma)) trauma = 0f;
            floor = float.IsNaN(floor) ? 0f : Clamp01(floor);
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            if (float.IsNaN(decayPerSecond) || decayPerSecond < 0f) decayPerSecond = 0f;
            float t = trauma - decayPerSecond * dt;
            return t < floor ? floor : Clamp01(t);
        }

        /// Local-space position offset and Euler offset (degrees) for this trauma and time.
        public static void Sample(in ShakeProfile p, float trauma, double timeSec, out Vector3 offset, out Vector3 angles)
        {
            float s = Clamp01(float.IsNaN(trauma) ? 0f : trauma);
            s *= s;
            if (s <= 0f || double.IsNaN(timeSec)) { offset = Vector3.zero; angles = Vector3.zero; return; }

            double x = timeSec * p.frequency % Wrap;
            if (x < 0) x += Wrap;
            float fx = (float)x;
            offset = new Vector3(Noise(fx, 0), Noise(fx, 1), Noise(fx, 2) * 0.5f) * (p.maxOffset * s);
            angles = new Vector3(Noise(fx, 3) * p.maxAngles.x,
                                 Noise(fx, 4) * p.maxAngles.y,
                                 Noise(fx, 5) * p.maxAngles.z) * s;
        }

        private static float Noise(float x, int axis)
        {
            float n = Mathf.PerlinNoise(x, 0.37f + axis * 17.13f) * 2f - 1f;
            return n < -1f ? -1f : n > 1f ? 1f : n;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
