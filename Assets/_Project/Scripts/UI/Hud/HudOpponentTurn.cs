using UnityEngine;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// V2 D2 Opponent Turn: the pulsing red edge vignette and the red "INCOMING!" tag with the
    /// rival's power — live while they charge, locked once they aim.
    ///
    /// Brace (ADR-0019): during the rival's wind-up a BRACE! cue pulses; the result (PERFECT
    /// BRACE −50%, BRACED −25%, FLINCHED, NO BRACE) replaces it, and the screen stays up a
    /// moment past the impact so it can be read. The reference's hold-and-release "gold ring"
    /// block and the Lean dodge are not built: the brace is one tap.
    ///
    /// Attention budget (POLISH §9): a looping edge pulse drags the eye to the periphery for the
    /// whole turn, so by default the vignette is still and pulses once, on the BRACE! cue - the
    /// one moment the player must act. INCOMING! says its piece and leaves the caption behind.
    public sealed class HudOpponentTurn : HudView
    {
        private const int HideAfterBrace = 1;

        [Header("Brace (ADR-0019)")]
        [SerializeField] private CanvasGroup _brace;
        [SerializeField] private RectTransform _braceRoot;
        [SerializeField] private TMP_Text _bracePrompt;
        [SerializeField] private TMP_Text _braceSub;
        [SerializeField] private Color _braceCueColor = Color.white;
        [SerializeField] private Color _bracePerfectColor = new Color(0.914f, 0.725f, 0.286f);
        [SerializeField] private Color _braceGoodColor = new Color(0.247f, 0.824f, 1f);
        [SerializeField] private Color _braceFailColor = new Color(1f, 0.294f, 0.325f);

        [SerializeField] private CanvasGroup _vignette;
        [SerializeField] private RectTransform _tag;
        [SerializeField] private CanvasGroup _tagGroup;
        [SerializeField] private CanvasGroup _caption;
        [SerializeField] private TMP_Text _lead;
        [SerializeField] private TMP_Text _power;

        private int _shown = -1;
        private Vector2 _tagHome;

        public override void Bind(HudContext ctx)
        {
            if (_tag != null) _tagHome = _tag.anchoredPosition;
            base.Bind(ctx);
            if (_lead != null) _lead.text = ctx.OpponentName + " WINDS UP ·";
        }

        protected override void OnShow()
        {
            _shown = -1;
            var b = Budget;
            if (_vignette != null)
            {
                float a = b.opponentVignetteAlpha;
                if (b.opponentVignettePulse) Tw.Fade(_vignette, 0.55f * a, a, 0.45f, Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
                else _vignette.alpha = a;
            }
            if (_tagGroup != null)
            {
                _tagGroup.alpha = 1f;
                Tw.Fade(_tagGroup, 1f, 0f, Sec(b.incomingFadeMs), Ease.InQuad, Sec(b.incomingHoldMs));
            }
            if (_tag != null)
            {
                Tw.AnchorMove(_tag, _tagHome + new Vector2(-36f, 0f), _tagHome, 0.3f, Ease.OutBack);
                Tw.Punch(_tag, 0.12f, 0.3f, 3);
            }
            if (_caption != null) Tw.Fade(_caption, 0f, 1f, 0.25f, Ease.OutQuad, 0.1f);
            if (_brace != null) _brace.alpha = 0f;
        }

        /// The rival's wind-up started and the player can brace.
        public void ShowBraceCue()
        {
            if (!IsShown) Show();
            SetBrace("BRACE!", "TAP AS THE HAND LANDS", _braceCueColor);
            if (_brace != null) Tw.Fade(_brace, 0f, 1f, 0.12f, Ease.OutQuad);
            if (_braceRoot != null) Tw.Scale(_braceRoot, 1.25f, 1f, 0.22f, Ease.OutBack);
            // The one peripheral pulse of the turn: now is when the player has to act.
            if (_vignette != null && !Budget.opponentVignettePulse)
            {
                Tw.Kill(_vignette);
                Tw.Fade(_vignette, 1f, Budget.opponentVignetteAlpha, Sec(Budget.braceCuePulseMs), Ease.OutQuad);
            }
        }

        /// The brace was judged (just before the impact frame).
        public void ShowBraceResult(in BraceResult r)
        {
            switch (r.Grade)
            {
                case BraceGrade.Perfect:
                    SetBrace("PERFECT BRACE", BlockedText(r.Reduction), _bracePerfectColor); break;
                case BraceGrade.Good:
                    SetBrace("BRACED", BlockedText(r.Reduction), _braceGoodColor); break;
                case BraceGrade.Flinch:
                    SetBrace("FLINCHED", r.OffsetMs < 0f ? "TOO EARLY" : "TOO LATE", _braceFailColor); break;
                default:
                    SetBrace("NO BRACE", "TAP AS THE HAND LANDS", _braceFailColor); break;
            }
            if (_brace != null) _brace.alpha = 1f;
            if (_braceRoot != null)
            {
                Tw.Kill(_braceRoot);
                _braceRoot.localScale = Vector3.one;
                Tw.Punch(_braceRoot, r.Grade == BraceGrade.Perfect ? 0.25f : 0.12f, 0.3f, 3);
            }
        }

        /// The impact landed: keep the brace result readable a moment, then leave.
        public void HideSoon()
        {
            if (!IsShown) return;
            if (_vignette != null) { Tw.Kill(_vignette); Tw.Fade(_vignette, _vignette.alpha, 0f, 0.2f, Ease.OutQuad); }
            Tw.Delay(0.8f, this, HideAfterBrace);
        }

        protected override void OnTween(int id)
        {
            if (id == HideAfterBrace) Hide();
        }

        private static string BlockedText(float reduction) => "BLOCKED " + Mathf.RoundToInt(reduction * 100f) + "%";

        private void SetBrace(string prompt, string sub, Color color)
        {
            if (_bracePrompt != null) { _bracePrompt.text = prompt; _bracePrompt.color = color; }
            if (_braceSub != null) _braceSub.text = sub;
        }

        protected override void OnTick(float dt)
        {
            var d = Ctx.Director;
            bool opponentAttacking = d.CurrentTurn.Attacker == FighterSlot.Opponent;
            float power01 = opponentAttacking && d.CurrentState == MatchState.Charge ? d.PowerMeter.Fill
                          : opponentAttacking && d.CurrentState == MatchState.HoldRing ? d.HoldRingMeter.Power
                          : Ctx.LastPower.RawPower;
            int v = Mathf.RoundToInt(Mathf.Clamp(power01, 0f, 2f) * 100f);
            if (v == _shown || _power == null) return;
            _shown = v;
            _power.SetText("{0} POWER", v);
        }
    }
}
