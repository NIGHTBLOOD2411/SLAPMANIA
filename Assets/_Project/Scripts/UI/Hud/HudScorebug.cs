using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// The broadcast scorebug (V2 C4/C5/D1/D2): top shade, arena label, LIVE badge, and the
    /// glass bar with both portraits, names, ticked HP bars and the red shot-clock box.
    /// The clock counts down the current interactive phase (charge window / timing timeout);
    /// on a hit the bar kicks (D1's rotate -1.2°), the defender's portrait flashes red and
    /// their sub-line reads STUNNED for a second.
    ///
    /// Top-left is a pause button (C4/C5/D2 `aria-label="Pause"`): a HudPauseButton that V2Hud
    /// routes. C6 has its own, in the letterbox.
    public sealed class HudScorebug : HudView
    {
        private const int RestoreOpponentSub = 1;
        private const int RestorePlayerSub = 2;
        private const int RestoreRestAlpha = 3;

        [SerializeField] private RectTransform _slideRoot;
        [SerializeField] private RectTransform _bug;
        [SerializeField] private CanvasGroup _liveDot;
        [Tooltip("The top shade behind the bar; its alpha is the budget's topShadeAlpha.")]
        [SerializeField] private CanvasGroup _topShade;

        [Header("Opponent (left)")]
        [SerializeField] private RawImage _oppPortrait;
        [SerializeField] private CanvasGroup _oppFlash;
        [SerializeField] private TMP_Text _oppName;
        [SerializeField] private TMP_Text _oppSub;
        [SerializeField] private HpBar _oppHp;

        [Header("Player (right)")]
        [SerializeField] private RawImage _playerPortrait;
        [SerializeField] private CanvasGroup _playerFlash;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_Text _playerSub;
        [SerializeField] private HpBar _playerHp;

        [Header("Clock")]
        [SerializeField] private TMP_Text _clock;
        [SerializeField] private TMP_Text _round;

        [Header("Colours")]
        [SerializeField] private Color _subColor = new Color(0.451f, 0.475f, 0.522f);   // .di #737985
        [SerializeField] private Color _stunColor = new Color(1f, 0.294f, 0.325f);      // #FF4B53

        private int _shownTenths = -1;
        private int _shownRound = -1;
        private Vector2 _home;
        private Vector3 _bugHome;

        public override void Bind(HudContext ctx)
        {
            if (_slideRoot != null) _home = _slideRoot.anchoredPosition;
            if (_bug != null) _bugHome = _bug.localPosition;
            base.Bind(ctx);
            if (_oppName != null) _oppName.text = ctx.OpponentName;
            if (_playerName != null) _playerName.text = ctx.PlayerName;
            ResetSubs();
            RefreshPortraits();
            if (ctx.Director != null)
            {
                _oppHp?.SetImmediate(ctx.Director.OpponentHealth.NormalizedHp);
                _playerHp?.SetImmediate(ctx.Director.PlayerHealth.NormalizedHp);
            }
            if (_oppFlash != null) _oppFlash.alpha = 0f;
            if (_playerFlash != null) _playerFlash.alpha = 0f;
            _shownTenths = _shownRound = -1;
        }

        public void RefreshPortraits()
        {
            if (Ctx == null) return;
            SetPortrait(_oppPortrait, Ctx.OpponentPortrait);
            SetPortrait(_playerPortrait, Ctx.PlayerPortrait);
        }

        private static void SetPortrait(RawImage img, Texture tex)
        {
            if (img == null) return;
            img.texture = tex;
            img.enabled = tex != null; // the tile behind it stays visible as a placeholder
        }

        // Rest state (POLISH §9): the bar is there to glance at, below the fighters in the hierarchy.
        protected override float ShownAlpha => Ctx != null ? Budget.scorebugRestAlpha : 1f;

        protected override void OnShow()
        {
            if (_slideRoot != null)
                Tw.AnchorMove(_slideRoot, _home + new Vector2(0f, 90f), _home, 0.45f, Ease.OutCubic);
            if (_topShade != null) _topShade.alpha = Budget.topShadeAlpha;
            if (_liveDot != null)
            {
                if (Budget.liveDotPulse) Tw.Fade(_liveDot, 1f, 0.2f, 0.55f, Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
                else _liveDot.alpha = 1f;
            }
        }

        public void OnHit(FighterSlot defender)
        {
            if (_bug != null)
            {
                // Back to rest first: a shake started mid-shake would otherwise adopt the
                // current offset as its rest pose and the bar would drift over a bout.
                Tw.Kill(_bug);
                _bug.localPosition = _bugHome;
                _bug.localEulerAngles = Vector3.zero;
                Tw.ShakeRotation(_bug, 2.4f, 0.35f, 10);
                Tw.Shake(_bug, new Vector3(7f, 3f, 0f), 0.3f, 14);
            }
            // Full alpha while the hit is news, then back to rest.
            if (IsShown && Budget.scorebugRestAlpha < 1f)
            {
                Tw.Kill(Group);
                Tw.Fade(Group, Group.alpha, 1f, 0.06f, Ease.OutQuad);
                Tw.Delay(Sec(Budget.scorebugHitHoldMs), this, RestoreRestAlpha);
            }
            var flash = defender == FighterSlot.Player ? _playerFlash : _oppFlash;
            if (flash != null) Tw.Fade(flash, 0.75f, 0f, 0.6f, Ease.OutQuad);

            var sub = defender == FighterSlot.Player ? _playerSub : _oppSub;
            if (sub != null)
            {
                sub.text = "STUNNED";
                sub.color = _stunColor;
                Tw.Delay(1.0f, this, defender == FighterSlot.Player ? RestorePlayerSub : RestoreOpponentSub);
            }
        }

        protected override void OnTween(int id)
        {
            if (id == RestoreOpponentSub || id == RestorePlayerSub) ResetSubs();
            else if (id == RestoreRestAlpha && IsShown)
                Tw.Fade(Group, Group.alpha, Budget.scorebugRestAlpha, 0.3f, Ease.InOutQuad);
        }

        private void ResetSubs()
        {
            if (_oppSub != null) { _oppSub.text = "CPU"; _oppSub.color = _subColor; }
            if (_playerSub != null) { _playerSub.text = "YOU"; _playerSub.color = _subColor; }
        }

        protected override void OnTick(float dt)
        {
            var d = Ctx.Director;
            _oppHp?.SetValue(d.OpponentHealth.NormalizedHp);
            _playerHp?.SetValue(d.PlayerHealth.NormalizedHp);

            float window = 0f;
            if (d.CurrentState == MatchState.Charge) window = Ctx.Config.powerPhase.phaseDuration;
            else if (d.CurrentState == MatchState.Timing) window = Ctx.Config.timingPhase.timeout;
            // Hold ring: until the press, the press timeout; while held, the time to full close
            // (PhaseStartedAt restarts at the press: V2Hud sets it on ChargeStart).
            else if (d.CurrentState == MatchState.HoldRing)
                window = (float)(d.HoldRingMeter.HasPressed ? d.HoldRingMeter.CloseSeconds : d.HoldRingMeter.PressTimeoutSeconds);
            float remaining = window > 0f ? Mathf.Max(0f, window - (float)(Ctx.Now - Ctx.PhaseStartedAt)) : 0f;
            int tenths = Mathf.CeilToInt(remaining * 10f);
            if (_clock != null && tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _clock.SetText("{0:1}", tenths / 10f);
            }

            int round = d.CurrentTurn.RoundIndex + 1;
            if (_round != null && round != _shownRound)
            {
                _shownRound = round;
                _round.SetText("RD {0}", round);
            }
        }
    }
}
