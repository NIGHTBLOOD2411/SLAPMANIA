using System;
using UnityEngine;
using SlapMania.Core;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "TimingZoneLayout", menuName = "SlapMania/Balance/Timing Zone Layout")]
    public sealed class TimingZoneLayout : ScriptableObject
    {
        [Serializable]
        public struct ZoneBand
        {
            public TimingZone zone;
            [Range(0f, 1f)] public float start;
            [Range(0f, 1f)] public float end;
            public float multiplier;
            public Color colour;
        }

        public ZoneBand[] bands = new ZoneBand[]
        {
            new ZoneBand { zone = TimingZone.Miss,    start = 0.00f, end = 0.12f, multiplier = 0.50f, colour = new Color(0.30f, 0.50f, 0.70f) },
            new ZoneBand { zone = TimingZone.Weak,    start = 0.12f, end = 0.30f, multiplier = 0.85f, colour = new Color(0.90f, 0.85f, 0.20f) },
            new ZoneBand { zone = TimingZone.Good,    start = 0.30f, end = 0.42f, multiplier = 1.40f, colour = new Color(1.00f, 0.60f, 0.15f) },
            new ZoneBand { zone = TimingZone.Great,   start = 0.42f, end = 0.47f, multiplier = 2.00f, colour = new Color(1.00f, 0.35f, 0.15f) },
            new ZoneBand { zone = TimingZone.Perfect, start = 0.47f, end = 0.53f, multiplier = 3.00f, colour = new Color(1.00f, 0.10f, 0.10f) },
            new ZoneBand { zone = TimingZone.Great,   start = 0.53f, end = 0.58f, multiplier = 2.00f, colour = new Color(1.00f, 0.35f, 0.15f) },
            new ZoneBand { zone = TimingZone.Good,    start = 0.58f, end = 0.70f, multiplier = 1.40f, colour = new Color(1.00f, 0.60f, 0.15f) },
            new ZoneBand { zone = TimingZone.Weak,    start = 0.70f, end = 0.88f, multiplier = 0.85f, colour = new Color(0.90f, 0.85f, 0.20f) },
            new ZoneBand { zone = TimingZone.Miss,    start = 0.88f, end = 1.00f, multiplier = 0.50f, colour = new Color(0.30f, 0.50f, 0.70f) },
        };

        public bool Evaluate(float pos01, float zoneScale, out TimingZone zone, out float multiplier)
        {
            float p = Mathf.Clamp01(pos01);
            for (int i = 0; i < bands.Length; i++)
            {
                float s = ScaleAboutCentre(bands[i].start, zoneScale);
                float e = ScaleAboutCentre(bands[i].end, zoneScale);
                if (p >= s && p < e)
                {
                    zone = bands[i].zone;
                    multiplier = bands[i].multiplier;
                    return true;
                }
            }
            var last = bands[bands.Length - 1];
            zone = last.zone;
            multiplier = last.multiplier;
            return false;
        }

        public static float ScaleAboutCentre(float p, float scale) => 0.5f + (p - 0.5f) * scale;

        public bool TilesUnitInterval(out string error)
        {
            error = null;
            if (bands == null || bands.Length == 0) { error = "no bands"; return false; }
            if (bands[0].start > 1e-5f) { error = "first band does not start at 0"; return false; }
            if (Mathf.Abs(bands[bands.Length - 1].end - 1f) > 1e-5f) { error = "last band does not end at 1"; return false; }
            for (int i = 1; i < bands.Length; i++)
            {
                if (Mathf.Abs(bands[i].start - bands[i - 1].end) > 1e-5f)
                {
                    error = "gap or overlap between band " + (i - 1) + " and " + i;
                    return false;
                }
            }
            return true;
        }
    }
}
