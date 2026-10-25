using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class PowerMeterTests
    {
        [Test]
        public void ZeroTaps_YieldsMinPower_NotZero()
        {
            var cfg = TestFactories.PowerCfg();
            var src = new ManualTapSource();
            var pm = new PowerMeter();
            pm.Begin(cfg, src);
            double t = 0;
            while (!pm.IsComplete)
            {
                pm.Tick(0.016);
                t += 0.016;
                if (t > 10) Assert.Fail("Power phase never completed");
            }
            var r = pm.End();
            Assert.AreEqual(0f, r.Fill, 1e-4);
            Assert.AreEqual(cfg.minPower, r.RawPower, 1e-3);
            Assert.Greater(r.RawPower, 0f, "minPower must be strictly positive so matches terminate");
        }

        [Test]
        public void SustainedTapRate_FillsBar()
        {
            var cfg = TestFactories.PowerCfg();
            var src = new ManualTapSource();
            var pm = new PowerMeter();
            pm.Begin(cfg, src);
            double t = 0;
            double nextTap = 0.05;
            while (!pm.IsComplete)
            {
                if (t >= nextTap) { src.Push(t); nextTap += 0.10; } // 10 tps
                pm.Tick(0.016);
                t += 0.016;
                if (t > 10) break;
            }
            var r = pm.End();
            Assert.Greater(r.RawPower, 0.9f, "10 tps should saturate the bar");
        }

        [Test]
        public void TooCloseTogether_TapsRejected()
        {
            var cfg = TestFactories.PowerCfg();
            var src = new ManualTapSource();
            var pm = new PowerMeter();
            pm.Begin(cfg, src);
            src.Push(0.10);
            src.Push(0.11); // within minTapInterval (0.045)
            pm.Tick(0.016);
            var r = pm.End();
            Assert.AreEqual(1, r.TapsAccepted);
            Assert.AreEqual(1, r.TapsRejected);
        }

        [Test]
        public void NegativeDt_Rejected_NoNaN()
        {
            var cfg = TestFactories.PowerCfg();
            var src = new ManualTapSource();
            var pm = new PowerMeter();
            pm.Begin(cfg, src);
            pm.Tick(-1.0);
            pm.Tick(double.NaN);
            Assert.IsFalse(float.IsNaN(pm.Fill));
        }

        [Test]
        public void HugeDt_ClampsFillToZeroNotNegative()
        {
            var cfg = TestFactories.PowerCfg();
            var src = new ManualTapSource();
            var pm = new PowerMeter();
            pm.Begin(cfg, src);
            src.Push(0.0); // gets some fill
            pm.Tick(0.016);
            pm.Tick(1000.0); // massive decay
            Assert.GreaterOrEqual(pm.Fill, 0f);
        }
    }
}
