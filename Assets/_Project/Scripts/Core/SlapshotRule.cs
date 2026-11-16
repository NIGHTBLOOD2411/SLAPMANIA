namespace SlapMania.Core
{
    /// Which landed slaps earn a slapshot (M09, GLOSSARY: Slapshot): the Polaroid of the
    /// slapped fighter, the attacker's palm on their cheek, taken at contact. Pure (R2): the
    /// threshold arrives from BoutConfig.slapshotMinDamage; nothing here reads Time or config.
    ///
    /// Every landed slap is still photographed for the gallery (SnapshotPicker); this rule only
    /// decides which ones are *celebrated* - shown as a Polaroid mid-bout, held on screen by the
    /// director for BoutConfig.slapshotHoldMs, and flagged for the game-over gallery.
    public static class SlapshotRule
    {
        /// True when the slap described by `damage`, `timing` and `power` is a slapshot.
        /// `minDamage` is BoutConfig.slapshotMinDamage (final damage after every multiplier).
        public static bool Qualifies(in DamageResult damage, in TimingResult timing, in PowerResult power, int minDamage)
        {
            // TODO(human): decide what makes a slap "powerful" enough for a slapshot.
            // Placeholder (what Play was verified with): any slap at or over the damage bar.
            return damage.FinalDamage >= minDamage;
        }
    }
}
