namespace SlapMania.Core
{
    /// How the player's brace landed against the rival's slap (M05, ADR-0019).
    /// None: no tap in the window. Flinch: a tap, but too early or too late to help.
    public enum BraceGrade : byte { None = 0, Flinch = 1, Good = 2, Perfect = 3 }

    public readonly struct BraceResult
    {
        public readonly BraceGrade Grade;
        /// 0..1, fed to DamageInput.Reduction.
        public readonly float Reduction;
        /// Seen-tap time minus impact time, in ms: negative = early. NaN when Grade is None.
        public readonly float OffsetMs;

        public BraceResult(BraceGrade grade, float reduction, float offsetMs)
        {
            Grade = grade;
            Reduction = reduction;
            OffsetMs = offsetMs;
        }

        public static BraceResult NoBrace => new BraceResult(BraceGrade.None, 0f, float.NaN);
    }
}
