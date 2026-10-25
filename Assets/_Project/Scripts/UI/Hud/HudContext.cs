using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;

namespace SlapMania.UI
{
    /// What every HUD screen can read about the bout (M10). Filled once by V2Hud.Bind and by
    /// the signal handlers there; views read it, they never write to gameplay (M10 §2).
    public sealed class HudContext
    {
        public MatchDirector Director;
        public SlapStats Stats;
        public GameConfig Config;
        /// The attention budget (POLISH §9): never null once bound - GameConfig.hudAttention, or
        /// the built-in defaults when the config has none.
        public HudAttentionConfig Attention;
        public Camera Camera;
        public Transform PlayerHead;
        public Transform OpponentHead;
        public string PlayerName = "YOU";
        public string OpponentName = "RIVAL";
        public Texture PlayerPortrait;
        public Texture OpponentPortrait;

        /// Latest results, for screens that report them after the fact (C5 power meter, D1 tag).
        public PowerResult LastPower;
        public TimingResult LastTiming;
        public DamageResult LastDamage;
        public MatchResult Result;
        public bool HasResult;

        /// The loser's slap gallery (M09): up to three photos in the order they happened, with
        /// each slap's damage. Filled by the composition root at BoutEnd, before E2/E3 show.
        public readonly Texture[] Gallery = new Texture[3];
        public readonly int[] GalleryDamage = new int[3];
        public int GalleryCount;

        /// Simulation time (Now) at which the current phase started.
        public double PhaseStartedAt;

        /// The bout's simulation time: realtime minus every pause (MatchDirector.Now), so the
        /// shot clock and the intro countdown hold still on the pause menu. Realtime before a
        /// director is bound.
        public double Now => Director != null ? Director.Now : Time.realtimeSinceStartupAsDouble;

        public bool IsPaused => Director != null && Director.IsPaused;

        public Transform HeadOf(FighterSlot slot) => slot == FighterSlot.Player ? PlayerHead : OpponentHead;
        public string NameOf(FighterSlot slot) => slot == FighterSlot.Player ? PlayerName : OpponentName;
        public Texture PortraitOf(FighterSlot slot) => slot == FighterSlot.Player ? PlayerPortrait : OpponentPortrait;

        /// World position to a local point in `space` (a Screen Space - Overlay rect).
        /// False when the point is behind the camera or there is no camera.
        public bool TryWorldToLocal(Vector3 world, RectTransform space, out Vector2 local)
        {
            local = Vector2.zero;
            if (Camera == null || space == null) return false;
            Vector3 sp = Camera.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, sp, null, out local);
        }
    }
}
