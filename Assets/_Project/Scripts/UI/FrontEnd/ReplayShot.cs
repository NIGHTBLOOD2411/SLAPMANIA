using UnityEngine;

namespace SlapMania.UI.FrontEnd
{
    /// What D4 Instant Replay shows (M09/M10): the latest landed slap's photo and its numbers.
    /// The bout's composition root fills it on every snapshot; ReplayView reads it on show.
    /// Plain data, reused for the whole bout.
    public sealed class ReplayShot
    {
        /// The hero-size photo (SlapSnapshotter.LatestHero). Null: the mockup still image stays.
        public Texture Photo;
        /// 1-based round of the slap.
        public int Round;
        /// The lethal slap (caption "FINAL BLOW"); otherwise "LAST SLAP".
        public bool Finisher;
        public int Damage;
        /// Power 0..100 (PowerResult.RawPower x 100).
        public int Power;
        /// The zone name in caps (PERFECT, GREAT...) and its multiplier.
        public string Zone = "";
        public float Multiplier;
        /// Seconds from the bout's start to the impact (the timeline's "IMPACT · 00:02.4").
        public double ImpactAt;
        /// Shown between the K.O. and the results: Close and Next both raise
        /// ScreenManager.ReplayFinished instead of navigating.
        public bool EndOfBout;
    }
}
