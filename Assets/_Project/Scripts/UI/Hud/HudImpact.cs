using UnityEngine;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// V2 D1 SMASH Impact, on every impact frame: white flash, speed lines and shock rings at
    /// the defender's face, the gold damage number with its CRITICAL / zone caption, and —
    /// for the player's non-lethal slaps — the rotated chroma "SMASH!" with the red
    /// "PERFECT · 96 POWER" tag. When the player is the one hit, the number turns red and the
    /// headline is skipped. Lethal hits leave the headline to the K.O. screen.
    ///
    /// Attention budget (POLISH §9): the impact is the reward, so it peaks hard - and then gets
    /// out of the way. Headline and number are smaller than the mockup, gone by ~700 ms, and
    /// Yield() hurries them off when the next turn starts so two headlines never share the screen.
    public sealed class HudImpact : HudView
    {
        private const int AutoHide = 1;
        private const int FadeHeadline = 2;
        private const int FadeDamage = 3;

        private static readonly string[] ZoneNames = { "MISS", "WEAK", "GOOD", "GREAT", "PERFECT" };

        [SerializeField] private RectTransform _world;
        [SerializeField] private CanvasGroup _vignette;

        [Header("FX at the face")]
        [SerializeField] private RectTransform _fxRoot;
        [SerializeField] private RectTransform _flash;
        [SerializeField] private CanvasGroup _flashGroup;
        [SerializeField] private RectTransform _speed;
        [SerializeField] private CanvasGroup _speedGroup;
        [SerializeField] private RectTransform _ring1;
        [SerializeField] private CanvasGroup _ring1Group;
        [SerializeField] private RectTransform _ring2;
        [SerializeField] private CanvasGroup _ring2Group;

        [Header("Damage number")]
        [SerializeField] private RectTransform _damageRoot;
        [SerializeField] private CanvasGroup _damageGroup;
        [SerializeField] private TMP_Text _damage;
        [SerializeField] private TMP_Text _damageCaption;
        [SerializeField] private Vector2 _damageOffset = new Vector2(70f, 150f);

        [Header("Headline")]
        [SerializeField] private RectTransform _headlineRoot;
        [SerializeField] private CanvasGroup _headlineGroup;
        [SerializeField] private ChromaText _headline;
        [SerializeField] private TMP_Text _tag;

        [Header("Next-turn caption")]
        [SerializeField] private TMP_Text _next;

        [Header("Number gradients (V2 .gt gold / .rt red)")]
        [SerializeField] private Color _goldTop = new Color(1f, 0.953f, 0.788f);
        [SerializeField] private Color _goldBottom = new Color(0.722f, 0.510f, 0.059f);
        [SerializeField] private Color _redTop = new Color(1f, 0.478f, 0.502f);
        [SerializeField] private Color _redBottom = new Color(0.639f, 0.059f, 0.094f);

        private readonly string[] _captionFormats = new string[5];
        private readonly string[] _tagFormats = new string[5];
        private string _nextPlayer = "YOUR TURN NEXT";
        private string _nextOpponent = "RIVAL'S TURN NEXT";
        private Vector2 _damageHome;
        private double _headlineGoneAt;   // sim time (Ctx.Now) the headline is off the screen

        public override void Bind(HudContext ctx)
        {
            base.Bind(ctx);
            for (int z = 0; z < ZoneNames.Length; z++)
            {
                _captionFormats[z] = ZoneNames[z] + "  x{0:1}";
                _tagFormats[z] = ZoneNames[z] + " · {0} POWER";
            }
            _nextOpponent = ctx.OpponentName + "'S TURN NEXT";
        }

        public void Play(FighterSlot attacker, in DamageResult d, in TimingResult t, in PowerResult p)
        {
            Show();
            Tw.KillAll(); // a new hit restarts everything, including a pending auto-hide
            Group.alpha = 1f;
            var b = Budget;

            var head = Ctx.HeadOf(attacker.Other());
            Vector2 at = Vector2.zero;
            if (head != null) Ctx.TryWorldToLocal(head.position, _world, out at);
            if (_fxRoot != null) _fxRoot.anchoredPosition = at;

            if (_flash != null) Tw.Scale(_flash, 0.6f, 1.35f, 0.35f, Ease.OutQuad);
            if (_flashGroup != null) Tw.Fade(_flashGroup, 1f, 0f, 0.35f, Ease.OutQuad);
            float rays = Sec(b.impactRaysMs);
            if (_speed != null) Tw.RotateZ(_speed, 0f, 12f * rays / 0.6f, rays, Ease.OutQuad);
            if (_speedGroup != null) Tw.Fade(_speedGroup, b.impactRaysAlpha, 0f, rays, Ease.InQuad);
            // Held at the budget alpha through the headline's hold (a delayed tween shows its start value at once).
            if (_vignette != null)
                Tw.Fade(_vignette, b.impactVignetteAlpha, 0f, Sec(b.impactHeadlineFadeMs), Ease.InQuad, Sec(b.impactHeadlineHoldMs));
            if (_ring1 != null) Tw.Scale(_ring1, 0.3f, 1.3f, 0.35f, Ease.OutCubic);
            if (_ring1Group != null) Tw.Fade(_ring1Group, 1f, 0f, 0.35f, Ease.InQuad);
            if (_ring2 != null) Tw.Scale(_ring2, 0.5f, 1.8f, 0.5f, Ease.OutCubic);
            if (_ring2Group != null) Tw.Fade(_ring2Group, 0.6f, 0f, 0.5f, Ease.InQuad);

            // Damage number, gold when you land it, red when you take it.
            bool youHit = attacker == FighterSlot.Player;
            if (_damage != null)
            {
                _damage.SetText("-{0}", d.FinalDamage);
                _damage.colorGradient = youHit
                    ? new VertexGradient(_goldTop, _goldTop, _goldBottom, _goldBottom)
                    : new VertexGradient(_redTop, _redTop, _redBottom, _redBottom);
            }
            int z = Mathf.Clamp((int)t.Zone, 0, ZoneNames.Length - 1);
            if (_damageCaption != null)
            {
                if (d.IsCrit) _damageCaption.SetText("CRITICAL  x{0:1}", t.Multiplier);
                else _damageCaption.SetText(_captionFormats[z], t.Multiplier);
            }
            if (_damageRoot != null)
            {
                _damageHome = at + _damageOffset;
                float ds = b.impactDamageScale;
                Tw.Scale(_damageRoot, 1.9f * ds, ds, 0.35f, Ease.OutBack);
                Tw.AnchorMove(_damageRoot, _damageHome, _damageHome + new Vector2(0f, 22f), 0.4f, Ease.InQuad, 0.75f);
            }
            if (_damageGroup != null) Tw.Fade(_damageGroup, 0f, 1f, 0.08f);
            Tw.Delay(Sec(b.impactDamageHoldMs), this, FadeDamage);

            // Headline: only for the player's non-lethal slaps.
            bool headline = youHit && !d.IsLethal;
            if (_headlineRoot != null) _headlineRoot.gameObject.SetActive(headline);
            if (headline)
            {
                if (_headline != null) _headline.SetText(d.IsCrit ? "SMASH!" : t.Zone >= TimingZone.Good ? "SLAP!" : "WEAK...");
                if (_tag != null) _tag.SetText(_tagFormats[z], Mathf.RoundToInt(p.RawPower * 100f));
                float hs = b.impactHeadlineScale;
                if (_headlineRoot != null) Tw.Scale(_headlineRoot, 2.2f * hs, hs, 0.4f, Ease.OutBack);
                if (_headlineGroup != null) Tw.Fade(_headlineGroup, 0f, 1f, 0.06f);
                Tw.Delay(Sec(b.impactHeadlineHoldMs), this, FadeHeadline);
                _headlineGoneAt = Ctx.Now + Sec(b.impactHeadlineHoldMs + b.impactHeadlineFadeMs);
            }
            else _headlineGoneAt = 0d;

            if (_next != null)
            {
                _next.gameObject.SetActive(!d.IsLethal);
                _next.text = youHit ? _nextOpponent : _nextPlayer;
            }
            Tw.Delay(Sec(b.impactAutoHideMs), this, AutoHide);
        }

        /// The next turn has started: headline and number leave now. Returns how long until the
        /// headline is off the screen (0 when none is up), for the next screen to wait.
        public float Yield()
        {
            if (!IsShown) return 0f;
            float fade = Sec(Budget.impactYieldMs);
            FadeOut(_damageGroup, fade);
            FadeOut(_vignette, fade);
            double now = Ctx.Now;
            if (_headlineGoneAt <= now) return 0f;
            FadeOut(_headlineGroup, fade);
            double gone = now + fade;
            if (gone < _headlineGoneAt) _headlineGoneAt = gone;
            return (float)(_headlineGoneAt - now);
        }

        // From wherever it is: a pending fade timer firing after a Yield must not pop it back to 1.
        private void FadeOut(CanvasGroup g, float seconds)
        {
            if (g == null || g.alpha <= 0f) return;
            Tw.Kill(g);
            Tw.Fade(g, g.alpha, 0f, seconds, Ease.InQuad);
        }

        protected override void OnTween(int id)
        {
            if (id == FadeHeadline) FadeOut(_headlineGroup, Sec(Budget.impactHeadlineFadeMs));
            else if (id == FadeDamage) FadeOut(_damageGroup, Sec(Budget.impactDamageFadeMs));
            else if (id == AutoHide) Hide();
        }
    }
}
