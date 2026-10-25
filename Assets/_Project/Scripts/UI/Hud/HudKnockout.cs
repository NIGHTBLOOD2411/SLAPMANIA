using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// V2 D3 Knockout, from the finisher: red tint, the spinning red burst, falling confetti,
    /// the gold "K.O." slam with its white KNOCKOUT tag, the winner line with portrait, the
    /// three-stat glass panel, and CONTINUE once the bout has formally ended.
    ///
    /// Attention budget (POLISH §9): the finisher camera shot reads first. The slam lands
    /// koSlamDelayMs later over a light tint; the tint rises after koTintHoldMs, and the winner
    /// line and stats wait for BoutEnd. Only the HUD is delayed - the camera is untouched (M16).
    public sealed class HudKnockout : HudView
    {
        public event Action ContinueRequested;

        private const int Slam = 1;
        private const int TintRise = 2;

        [SerializeField] private CanvasGroup _tint;
        [Tooltip("Rays, hot spot and confetti behind the K.O.; alpha is the budget's koBurstAlpha.")]
        [SerializeField] private CanvasGroup _burst;
        [SerializeField] private RectTransform _rays;
        [SerializeField] private RectTransform[] _confetti = new RectTransform[0];

        [Header("K.O.")]
        [SerializeField] private RectTransform _koRoot;
        [SerializeField] private RectTransform _koTag;
        [SerializeField] private CanvasGroup _koTagGroup;

        [Header("Winner + stats")]
        [SerializeField] private CanvasGroup _winnerGroup;
        [SerializeField] private TMP_Text _winnerLine;
        [SerializeField] private RawImage _winnerPortrait;
        [SerializeField] private CanvasGroup _statsGroup;
        [SerializeField] private TMP_Text _statFinal;
        [SerializeField] private TMP_Text _statBest;
        [SerializeField] private TMP_Text _statPerfect;

        [Header("Continue")]
        [SerializeField] private CanvasGroup _continueGroup;
        [SerializeField] private Button _continue;

        private Vector2[] _confettiHome = new Vector2[0];

        private void Awake()
        {
            if (_continue != null) _continue.onClick.AddListener(OnContinue);
        }

        private void OnDestroy()
        {
            if (_continue != null) _continue.onClick.RemoveListener(OnContinue);
        }

        public override void Bind(HudContext ctx)
        {
            if (_confettiHome.Length != _confetti.Length)
            {
                _confettiHome = new Vector2[_confetti.Length];
                for (int i = 0; i < _confetti.Length; i++)
                    if (_confetti[i] != null) _confettiHome[i] = _confetti[i].anchoredPosition;
            }
            base.Bind(ctx);
        }

        public void Play(FighterSlot winner)
        {
            Show();
            SetContinue(false, false);
            var b = Budget;
            float d = Sec(b.koSlamDelayMs);

            // A delayed tween shows its start value at once, and a second tween of the same kind on
            // the same target replaces the first (Tweener.Begin). So what waits starts from 0 alpha,
            // and the slam and the tint's two steps are started by timers (OnTween), not by delays.
            if (_tint != null) _tint.alpha = 0f;
            Tw.Delay(d, this, Slam);
            if (_burst != null)
            {
                _burst.alpha = 0f;
                Tw.Fade(_burst, 0f, b.koBurstAlpha, 0.3f, Ease.OutQuad, d);
            }
            if (_koRoot != null) _koRoot.localScale = Vector3.zero;
            if (_koTagGroup != null) _koTagGroup.alpha = 0f;
            if (_winnerGroup != null) _winnerGroup.alpha = 0f;
            if (_statsGroup != null) _statsGroup.alpha = 0f;
            if (_rays != null) Tw.RotateZ(_rays, 0f, 360f, 24f, Ease.Linear).SetLoops(-1);
            for (int i = 0; i < _confetti.Length; i++)
            {
                var c = _confetti[i];
                if (c == null) continue;
                Vector2 home = _confettiHome[i];
                float dur = 2.1f + (i % 4) * 0.35f;
                Tw.AnchorMove(c, home + new Vector2(0f, 60f), home + new Vector2((i % 2 == 0 ? 14f : -14f), -300f), dur, Ease.Linear, d + i * 0.08f).SetLoops(-1);
                Tw.RotateZ(c, i * 37f, i * 37f + (i % 2 == 0 ? 540f : -540f), dur, Ease.Linear, d + i * 0.08f).SetLoops(-1);
            }

            if (_koTag != null) Tw.Scale(_koTag, 0.4f, 1f, 0.3f, Ease.OutBack, d + 0.3f);
            if (_koTagGroup != null) Tw.Fade(_koTagGroup, 0f, 1f, 0.15f, Ease.OutQuad, d + 0.3f);

            if (_winnerLine != null) _winnerLine.text = winner == FighterSlot.Player ? "YOU WIN THE BOUT" : Ctx.NameOf(winner) + " WINS THE BOUT";
            if (_winnerPortrait != null)
            {
                var tex = Ctx.PortraitOf(winner);
                _winnerPortrait.texture = tex;
                _winnerPortrait.enabled = tex != null;
            }
            if (!b.koStatsAtBoutEnd) RevealStats(d + 0.5f);

            var s = Ctx.Stats != null ? Ctx.Stats[winner] : default;
            if (_statFinal != null) _statFinal.SetText("{0}", Ctx.Stats != null ? Ctx.Stats.LastFinalBlow : Ctx.LastDamage.FinalDamage);
            if (_statBest != null) _statBest.SetText("{0}", s.BestSlap);
            if (_statPerfect != null) _statPerfect.SetText("{0}/{1}", s.Perfects, s.Commits);
        }

        protected override void OnTween(int id)
        {
            var b = Budget;
            if (id == Slam)
            {
                if (_tint != null) Tw.Fade(_tint, _tint.alpha, b.koTintAlpha, 0.25f, Ease.OutQuad);
                if (_koRoot != null)
                {
                    Tw.Scale(_koRoot, 2.6f, 1f, 0.45f, Ease.OutBack);
                    Tw.ShakeRotation(_koRoot, 5f, 0.4f, 10, 0.3f);
                }
                Tw.Delay(Sec(b.koTintHoldMs), this, TintRise);
            }
            else if (id == TintRise && _tint != null)
                Tw.Fade(_tint, _tint.alpha, b.koSettledTintAlpha, 0.4f, Ease.InOutQuad);
        }

        private void RevealStats(float delay)
        {
            if (_winnerGroup != null) Tw.Fade(_winnerGroup, _winnerGroup.alpha, 1f, 0.3f, Ease.OutQuad, delay);
            if (_statsGroup != null) Tw.Fade(_statsGroup, _statsGroup.alpha, 1f, 0.3f, Ease.OutQuad, delay + 0.2f);
        }

        /// The bout has formally ended (BoutEnd): offer CONTINUE to the result screen.
        public void ShowContinue()
        {
            if (!IsShown) return;
            SetContinue(true, true);
            float after = 0f;
            if (Budget.koStatsAtBoutEnd)
            {
                RevealStats(0f);
                after = 0.4f;
            }
            if (_continueGroup != null)
            {
                Tw.Fade(_continueGroup, 0f, 1f, 0.2f, Ease.OutQuad, after);
                Tw.Scale(_continueGroup.transform, 0.8f, 1f, 0.3f, Ease.OutBack, after);
            }
        }

        private void SetContinue(bool visible, bool interactive)
        {
            if (_continueGroup == null) return;
            if (!visible) _continueGroup.alpha = 0f;
            _continueGroup.interactable = interactive;
            _continueGroup.blocksRaycasts = interactive;
        }

        private void OnContinue()
        {
            SetContinue(true, false); // one press only
            ContinueRequested?.Invoke();
        }
    }
}
