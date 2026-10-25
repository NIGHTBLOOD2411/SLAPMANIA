using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// Pure C#. No MonoBehaviour, no Time.* read internally. dt arrives via Tick.
    public sealed class PowerMeter
    {
        public float Fill { get; private set; }
        public float Overcharge { get; private set; }
        public bool IsComplete => _elapsed >= _cfg.phaseDuration;
        public int TapsAccepted => _accepted;
        public int TapsRejected => _rejected;
        /// Event time of the last accepted tap (-inf before the first). The timing meter's spam
        /// gap starts from it (M04, DEBUG S-093).
        public double LastTapTime => _lastTapTime;

        private PowerPhaseConfig _cfg;
        private ITapSource _source;
        private double _elapsed;
        private double _lastTapTime;
        private int _accepted;
        private int _rejected;
        private float _peakTps;
        private bool _running;

        // Fixed-size ring buffer of recent tap timestamps for the sliding-window count.
        private readonly double[] _tapBuf = new double[64];
        private int _tapHead;
        private int _tapCount;

        public void Begin(PowerPhaseConfig cfg, ITapSource source)
        {
            SlapAssert.NotNull(cfg, nameof(cfg));
            SlapAssert.NotNull(source, nameof(source));
            _cfg = cfg;
            _source = source;
            Fill = 0f;
            Overcharge = 0f;
            _elapsed = 0;
            _lastTapTime = double.NegativeInfinity;
            _accepted = 0;
            _rejected = 0;
            _peakTps = 0f;
            _tapHead = 0;
            _tapCount = 0;
            _running = true;
            _source.OnTap += HandleTap;
            _source.Enable();
        }

        public void Tick(double dt)
        {
            if (!_running) return;
            if (double.IsNaN(dt) || dt < 0) { SlapLog.Warn(LogChannel.Power, "bad dt"); return; }
            float d = _cfg.decayPerSecond * (float)dt;
            Fill = Fill - d;
            if (Fill < 0f) Fill = 0f;
            _elapsed += dt;
        }

        public bool RegisterTap(in TapEvent e)
        {
            if (!_running) return false;
            double t = e.EventTime;
            if (t - _lastTapTime < _cfg.minTapInterval) { _rejected++; return false; }

            _tapBuf[_tapHead] = t;
            _tapHead = (_tapHead + 1) % _tapBuf.Length;
            if (_tapCount < _tapBuf.Length) _tapCount++;

            int windowCount = CountInWindow(t, _cfg.diminishWindow);
            float instTps = windowCount / _cfg.diminishWindow;
            if (instTps > _peakTps) _peakTps = instTps;

            float d = Mathf.Lerp(1.0f, _cfg.diminishFloor,
                InverseLerp(_cfg.diminishSoftTaps, _cfg.diminishHardTaps, windowCount));
            float tNorm = _cfg.phaseDuration > 0 ? (float)(_elapsed / _cfg.phaseDuration) : 0f;
            float s = _cfg.phaseShape != null ? _cfg.phaseShape.Evaluate(tNorm) : 1f;
            float gain = _cfg.impulsePerTap * d * s;

            if (Fill < _cfg.maxFill)
            {
                Fill = Mathf.Min(_cfg.maxFill, Fill + gain);
            }
            else
            {
                Overcharge = Mathf.Min(1f, Overcharge + _cfg.overchargeRatePerTap * d);
            }

            _lastTapTime = t;
            _accepted++;
            return true;
        }

        public PowerResult End()
        {
            if (!_running) return default;
            _running = false;
            _source.OnTap -= HandleTap;
            _source.Disable();

            float fillNorm = _cfg.maxFill > 0 ? Fill / _cfg.maxFill : 0f;
            float shaped = _cfg.feedbackCurve != null ? _cfg.feedbackCurve.Evaluate(Mathf.Clamp01(fillNorm)) : Mathf.Clamp01(fillNorm);
            float raw = Mathf.Lerp(_cfg.minPower, _cfg.maxPower, shaped) + Overcharge * _cfg.overchargeGain;
            return new PowerResult(Fill, Overcharge, raw, _accepted, _rejected, _peakTps);
        }

        private void HandleTap(TapEvent e) => RegisterTap(in e);

        private int CountInWindow(double now, float windowSec)
        {
            double cutoff = now - windowSec;
            int count = 0;
            for (int i = 0; i < _tapCount; i++)
            {
                double v = _tapBuf[i];
                if (v > cutoff && v <= now) count++;
            }
            return count;
        }

        private static float InverseLerp(int a, int b, int v)
        {
            if (b == a) return 0f;
            float t = (float)(v - a) / (b - a);
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return t;
        }
    }
}
