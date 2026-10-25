using UnityEngine;
using TMPro;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// V2 C4 Round Countdown: "GET READY" / "ROUND 1", the red conic ring draining with the
    /// intro, and the chroma digit slamming in on every whole second. Driven by the real
    /// BoutConfig.introMs, so the count is honest — a 500 ms intro shows a single "1".
    ///
    /// Attention budget (POLISH §9): the intro camera is the show, so the dim is light and the
    /// ring sits in the lower third, under the fighters' faces, at the budget's scale.
    public sealed class HudIntro : HudView
    {
        [Tooltip("The arena wash (baked black at alpha 1); its alpha is the budget's introDimAlpha.")]
        [SerializeField] private CanvasGroup _dim;
        [SerializeField] private RectTransform _titleRoot;
        [SerializeField] private TMP_Text _roundTitle;
        [SerializeField] private RectTransform _ringRoot;
        [SerializeField] private SlapArc _progress;
        [SerializeField] private RectTransform _countRoot;
        [SerializeField] private ChromaText _count;
        [SerializeField] private CanvasGroup _hints;
        [Tooltip("The two how-to-play lines; their copy follows the bout's attack mode (ADR-0014).")]
        [SerializeField] private TMP_Text _hintCharge;
        [SerializeField] private TMP_Text _hintTiming;
        [SerializeField] private CanvasGroup _footer;

        private double _endsAt;
        private float _duration = 1f;
        private int _shown = -1;
        private Vector2 _titleHome;

        public override void Bind(HudContext ctx)
        {
            if (_titleRoot != null) _titleHome = _titleRoot.anchoredPosition;
            base.Bind(ctx);
        }

        public void Begin(double startedAt, float durationSeconds)
        {
            _duration = Mathf.Max(0.001f, durationSeconds);
            _endsAt = startedAt + _duration;
            _shown = -1;
        }

        protected override void OnShow()
        {
            if (_roundTitle != null) _roundTitle.SetText("ROUND {0}", 1);
            bool hold = Ctx.Director != null && Ctx.Director.CurrentAttackMode == Core.AttackMode.HoldRing;
            if (_hintCharge != null) _hintCharge.text = hold ? "HOLD TO WIND UP" : "TAP FAST TO CHARGE";
            if (_hintTiming != null) _hintTiming.text = hold ? "RELEASE ON THE GOLD RING" : "TAP ON THE GOLD RING";
            if (_titleRoot != null) Tw.AnchorMove(_titleRoot, _titleHome + new Vector2(0f, 26f), _titleHome, 0.4f, Ease.OutCubic);
            if (_dim != null) _dim.alpha = Budget.introDimAlpha;
            float s = Budget.introRingScale;
            if (_ringRoot != null) Tw.Scale(_ringRoot, 0.55f * s, s, 0.45f, Ease.OutBack);
            if (_hints != null) Tw.Fade(_hints, 0f, 1f, 0.3f, Ease.OutQuad, 0.2f);
            if (_footer != null) Tw.Fade(_footer, 0f, 1f, 0.3f, Ease.OutQuad, 0.3f);
        }

        protected override void OnTick(float dt)
        {
            float remaining = Mathf.Max(0f, (float)(_endsAt - Ctx.Now));
            if (_progress != null) _progress.Fill = remaining / _duration;
            int n = Mathf.Max(1, Mathf.CeilToInt(remaining));
            if (n == _shown) return;
            _shown = n;
            if (_count != null) _count.SetText("{0}", n);
            if (_countRoot != null) Tw.Scale(_countRoot, 1.7f, 1f, 0.35f, Ease.OutBack);
        }
    }
}
