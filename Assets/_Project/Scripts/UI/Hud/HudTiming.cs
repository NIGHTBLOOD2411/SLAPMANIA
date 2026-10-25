using UnityEngine;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;
using SlapMania.Data;

namespace SlapMania.UI
{
    /// V2 C5 Your Turn · Aim & Timing. The white ring's radius is a pure function of the active
    /// meter and the gold band sits exactly where the Perfect zone is: its radius and thickness
    /// come from the TimingZoneLayout and the round's zone scale, so the UI can never disagree
    /// with the rules about where Perfect is (M04 §10).
    ///
    ///   TapMarker (M04): radius follows TimingMeter.NormalizedPosition - it closes as the marker
    ///     runs 0 -> 1 and reopens on the way back. Tap to commit. It opens on the AIM cue
    ///     (TimingMeter.InCue, DEBUG S-093): the ring holds still under "stop mashing" copy, and
    ///     the tap copy and a ring punch arrive when the marker starts.
    ///   HoldRing (ADR-0014): radius follows HoldRingMeter.Closure - open until the press, then
    ///     closing with the hold. At the press the C5 chrome (banner, hints, meter, vignette) fades
    ///     and C6 (HudCharge) takes over around the ring; the release freezes it via Commit.
    ///
    /// Attention budget (POLISH §9): the ring on the face is the one focal point. YOUR TURN
    /// leaves after a beat, the side meter is off by default (C6 just showed the power), the
    /// shades are lighter, and the PERFECT / x3 tags swap sides rather than leave the screen.
    public sealed class HudTiming : HudView, ITweenSink
    {
        private const int HideAfterCommit = 1;
        private const int ChPower = 0;
        private const int ChBonus = 1;

        // Copy per attack mode (V2 C5 / E3 coaching). Constants: written once per show, never per frame.
        private const string TapCaption  = "TAP WHEN THE WHITE RING HITS GOLD";
        private const string TapHint     = "TAP ON THE GOLD RING";
        private const string TapHintSub  = "Perfect lands x3.0 damage · the ring stops where you tap";
        private const string HoldCaption = "HOLD TO WIND UP · RELEASE ON GOLD";
        private const string HoldHint    = "HOLD ON TARGET TO WIND UP";
        private const string HoldHintSub = "Perfect lands x3.0 damage · let go on gold";
        private const string CueCaption  = "AIM · STOP MASHING";
        private const string CueHint     = "GET READY...";
        private const string CueHintSub  = "One clean tap when the ring hits gold · mashing never counts";

        [SerializeField] private RectTransform _world;

        [Header("Banner")]
        [SerializeField] private RectTransform _banner;
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private CanvasGroup _bannerCaption;
        [SerializeField] private TMP_Text _captionText;

        [Header("Hold ring: faded at the press, when C6 takes over")]
        [SerializeField] private CanvasGroup _chrome;
        [SerializeField] private CanvasGroup _shade;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private TMP_Text _hintSub;

        [Header("Ring (follows the opponent's head)")]
        [SerializeField] private RectTransform _ringRoot;
        [SerializeField] private SlapArc _closingRing;
        [SerializeField] private SlapArc _goldBand;
        [SerializeField] private RectTransform _goldPulse;
        [SerializeField] private TMP_Text _multTag;
        [Tooltip("The PERFECT ZONE and x3 tags beside the ring: they move to the other side when they would leave the screen.")]
        [SerializeField] private RectTransform _perfectTag;
        [SerializeField] private RectTransform _multTagRoot;
        [Tooltip("Ring radius at marker 0 and at marker 1 (px). The Perfect centre lands in between.")]
        [SerializeField] private float _ringOuter = 80f;
        [SerializeField] private float _ringInner = 4f;

        [Header("Locked power meter (right)")]
        [SerializeField] private CanvasGroup _meterGroup;
        [SerializeField] private RectTransform _meterPower;
        [SerializeField] private RectTransform _meterBonus;
        [SerializeField] private TMP_Text _meterValue;

        [Header("Commit feedback")]
        [SerializeField] private Color _ringColor = Color.white;
        [SerializeField] private Color _perfectColor = new Color(0.914f, 0.725f, 0.286f);
        [SerializeField] private Color _missColor = new Color(1f, 0.294f, 0.325f);

        private float _perfectStart = 0.47f;
        private float _perfectEnd = 0.53f;
        private float _perfectMult = 3f;
        private float _powerShown;
        private Vector2 _bannerHome;
        private bool _holdMode;
        private bool _pressSeen;
        private bool _inCue;
        private bool _tagsRight;
        private Vector2 _perfectTagHome;
        private Vector2 _multTagHome;

        public override void Bind(HudContext ctx)
        {
            if (_banner != null) _bannerHome = _banner.anchoredPosition;
            if (_perfectTag != null) _perfectTagHome = _perfectTag.anchoredPosition;
            if (_multTagRoot != null) _multTagHome = _multTagRoot.anchoredPosition;
            base.Bind(ctx);
            var layout = ctx.Config != null ? ctx.Config.timingZoneLayout : null;
            if (layout != null && layout.bands != null)
            {
                for (int i = 0; i < layout.bands.Length; i++)
                {
                    if (layout.bands[i].zone != TimingZone.Perfect) continue;
                    _perfectStart = layout.bands[i].start;
                    _perfectEnd = layout.bands[i].end;
                    _perfectMult = layout.bands[i].multiplier;
                    break;
                }
            }
            if (_multTag != null) _multTag.SetText("x{0:1} DAMAGE", _perfectMult);
        }

        private float RadiusAt(float pos01) => Mathf.Lerp(_ringOuter, _ringInner, Mathf.Clamp01(pos01));

        protected override void OnShow()
        {
            if (_closingRing != null) _closingRing.color = _ringColor;
            _holdMode = Ctx.Director.CurrentAttackMode == AttackMode.HoldRing;
            _pressSeen = false;
            if (_chrome != null) _chrome.alpha = 1f;
            if (_shade != null) _shade.alpha = Budget.timingShadeAlpha;
            if (_meterGroup != null) _meterGroup.gameObject.SetActive(Budget.showPowerMeter);
            if (_captionText != null) _captionText.text = _holdMode ? HoldCaption : TapCaption;
            if (_hint != null) _hint.text = _holdMode ? HoldHint : TapHint;
            if (_hintSub != null) _hintSub.text = _holdMode ? HoldHintSub : TapHintSub;
            _inCue = !_holdMode && Ctx.Director.TimingMeter.InCue;
            if (_inCue) SetCopy(CueCaption, CueHint, CueHintSub);

            // Gold band = the Perfect window, scaled for this round exactly as the active meter scales it.
            float scale = _holdMode ? Ctx.Director.HoldRingMeter.ZoneScale : Ctx.Director.TimingMeter.ZoneScale;
            float ra = RadiusAt(TimingZoneLayout.ScaleAboutCentre(_perfectStart, scale));
            float rb = RadiusAt(TimingZoneLayout.ScaleAboutCentre(_perfectEnd, scale));
            if (_goldBand != null)
            {
                _goldBand.Radius = Mathf.Max(ra, rb);
                _goldBand.Thickness = Mathf.Max(2f, Mathf.Abs(ra - rb));
            }

            if (_banner != null) Tw.AnchorMove(_banner, _bannerHome + new Vector2(-40f, 0f), _bannerHome, 0.32f, Ease.OutBack);
            if (_bannerCaption != null) Tw.Fade(_bannerCaption, 0f, 1f, 0.25f, Ease.OutQuad, 0.1f);
            if (_bannerGroup != null)
            {
                _bannerGroup.alpha = 1f;
                Tw.Fade(_bannerGroup, 1f, 0f, Sec(Budget.bannerFadeMs), Ease.InQuad, Sec(Budget.bannerHoldMs));
            }
            if (_ringRoot != null) Tw.Scale(_ringRoot, 0.5f, 1f, 0.3f, Ease.OutBack);
            if (_goldPulse != null) Tw.Scale(_goldPulse, 1f, 1.08f, 0.35f, Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);

            float max = Ctx.Config.powerPhase.maxPower > 0f ? Ctx.Config.powerPhase.maxPower : 1f;
            if (_holdMode)
            {
                // Nothing is wound yet: the meter shows the floor a release would carry. The live
                // wind-up is C6's arc, which replaces this chrome at the press.
                float floor = Ctx.Director.HoldRingMeter.Power;
                Tw.Float(this, ChPower, 0f, Mathf.Clamp01(floor / max), 0.4f, Ease.OutCubic);
                SetMeter(_meterBonus, 0f, 0f);
                if (_meterValue != null) _meterValue.SetText("{0}", Mathf.RoundToInt(floor * 100f));
            }
            else
            {
                var p = Ctx.LastPower;
                float power01 = Mathf.Clamp01(p.RawPower / max);
                float bonus01 = Mathf.Clamp01(p.Overcharge * Ctx.Config.powerPhase.overchargeGain / max);
                Tw.Float(this, ChPower, 0f, power01, 0.4f, Ease.OutCubic);
                Tw.Float(this, ChBonus, 0f, bonus01, 0.4f, Ease.OutCubic, 0.15f);
                if (_meterValue != null) _meterValue.SetText("{0}", Mathf.RoundToInt(p.RawPower * 100f));
            }
            Follow();
            UpdateRing();
        }

        public void OnTweenValue(int channel, float value)
        {
            if (channel == ChPower)
            {
                _powerShown = value;
                SetMeter(_meterPower, 0f, value);
            }
            else SetMeter(_meterBonus, _powerShown, _powerShown + value);
        }

        private static void SetMeter(RectTransform rt, float from, float to)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, Mathf.Clamp01(from));
            rt.anchorMax = new Vector2(1f, Mathf.Clamp01(to));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        protected override void OnTick(float dt)
        {
            Follow();
            UpdateRing();
        }

        private void UpdateRing()
        {
            if (_holdMode)
            {
                var ring = Ctx.Director.HoldRingMeter;
                if (!_pressSeen && ring.HasPressed) OnPress();
                if (_closingRing == null || ring.CurrentState == Gameplay.HoldRingMeter.State.Resolved) return;
                _closingRing.Radius = RadiusAt(ring.Closure);
                return;
            }
            if (_closingRing == null) return;
            var meter = Ctx.Director.TimingMeter;
            if (meter.CurrentState == Gameplay.TimingMeter.State.Resolved) return; // frozen where it stopped
            if (_inCue && !meter.InCue) EndCue();
            _closingRing.Radius = RadiusAt(meter.NormalizedPosition);
        }

        /// The AIM cue is over and the marker moves: the tap copy, and a punch to say "now".
        private void EndCue()
        {
            _inCue = false;
            SetCopy(TapCaption, TapHint, TapHintSub);
            if (_ringRoot != null)
            {
                Tw.Kill(_ringRoot);
                _ringRoot.localScale = Vector3.one;
                Tw.Punch(_ringRoot, 0.12f, 0.2f, 2);
            }
        }

        // Edges only, never per frame: assigning TMP text re-lays it out.
        private void SetCopy(string caption, string hint, string hintSub)
        {
            if (_captionText != null) _captionText.text = caption;
            if (_hint != null) _hint.text = hint;
            if (_hintSub != null) _hintSub.text = hintSub;
        }

        /// Hold ring: the finger went down. C6 now owns the screen; keep only the ring.
        private void OnPress()
        {
            _pressSeen = true;
            if (_chrome != null) Tw.Fade(_chrome, _chrome.alpha, 0f, 0.18f, Ease.OutQuad);
            if (_shade != null) Tw.Fade(_shade, _shade.alpha, 0f, 0.18f, Ease.OutQuad);
            if (_ringRoot != null)
            {
                Tw.Kill(_ringRoot);
                _ringRoot.localScale = Vector3.one;
                Tw.Punch(_ringRoot, 0.1f, 0.2f, 2);
            }
        }

        /// The tap landed: freeze the ring where it stopped, colour it by the result, then leave.
        public void Commit(in TimingResult r)
        {
            if (!IsShown) return;
            if (_closingRing != null)
            {
                _closingRing.Radius = RadiusAt(r.Position);
                _closingRing.color = r.Zone == TimingZone.Perfect ? _perfectColor
                                   : r.Zone == TimingZone.Miss ? _missColor : _ringColor;
            }
            if (_ringRoot != null)
            {
                Tw.Kill(_ringRoot);                 // a fast tap can land during the pop-in
                _ringRoot.localScale = Vector3.one;
                Tw.Punch(_ringRoot, 0.18f, 0.25f, 3);
            }
            Tw.Delay(0.28f, this, HideAfterCommit);
        }

        protected override void OnTween(int id)
        {
            if (id == HideAfterCommit) Hide();
        }

        private void Follow()
        {
            if (_ringRoot == null || Ctx == null) return;
            var head = Ctx.HeadOf(FighterSlot.Opponent);
            if (head == null || !Ctx.TryWorldToLocal(head.position, _world, out var local)) return;
            _ringRoot.anchoredPosition = local;
            PlaceTags(local.x);
        }

        // The tags hang off the ring's left (V2 C5). A face on the left half would push them off
        // the screen (DEBUG S-099), so they mirror to the right; written only when the side changes.
        private void PlaceTags(float ringX)
        {
            if (_perfectTag == null) return;
            float reach = Mathf.Abs(_perfectTagHome.x) + _perfectTag.rect.width;
            if (_multTagRoot != null) reach = Mathf.Max(reach, Mathf.Abs(_multTagHome.x) + _multTagRoot.rect.width);
            bool right = ringX - reach < -_world.rect.width * 0.5f;
            if (right == _tagsRight) return;
            _tagsRight = right;
            Mirror(_perfectTag, _perfectTagHome, right);
            Mirror(_multTagRoot, _multTagHome, right);
        }

        private static void Mirror(RectTransform rt, Vector2 home, bool right)
        {
            if (rt == null) return;
            rt.pivot = new Vector2(right ? 0f : 1f, rt.pivot.y);
            rt.anchoredPosition = right ? new Vector2(-home.x, home.y) : home;
        }
    }
}
