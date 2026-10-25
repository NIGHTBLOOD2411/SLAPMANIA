using UnityEngine;
using TMPro;
using SlapMania.Core;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// V2 C6 Wind-up · Power Charge, on the player's charge phase: cinematic letterbox bars,
    /// the red lock-on ring tracking the opponent's face, the gold -> red power arc with the big
    /// number, and the chroma "TAP FAST!" that kicks on every accepted tap.
    ///
    /// Charge is an R5 window: everything here is polled from PowerMeter in OnTick, numbers
    /// are written only when they change, and no tween is created per frame — only per tap.
    ///
    /// HoldRing (ADR-0014): shown at the press, it is the wind-up. The arc fills from
    /// HoldRingMeter.WindUp01, the lock-on reticle is hidden (C5's closing ring stays on the
    /// face instead), and the prompt reads HOLD! until the wind-up peaks - on the gold band -
    /// then RELEASE!, as in the C6 frame where MAX POWER and "Release!" appear together.
    ///
    /// Attention budget (POLISH §9): the fighters stay the picture. Thin letterbox, a light
    /// vignette, a smaller gauge in the bottom safe area, speed lines that only kick on a tap,
    /// a prompt that dims once the player has the idea, and a reticle with no filled dot.
    public sealed class HudCharge : HudView
    {
        private const string TapPrompt = "TAP FAST!";
        private const string TapHint = "EVERY TAP ADDS POWER · 2.5 SECONDS";
        private const string HoldPrompt = "HOLD!";
        private const string ReleasePrompt = "RELEASE!";
        private const string HoldHint = "LET GO WHEN THE WHITE RING HITS GOLD";

        [SerializeField] private RectTransform _world;
        [SerializeField] private RectTransform _letterTop;
        [SerializeField] private RectTransform _letterBottom;
        [SerializeField] private CanvasGroup _vignette;

        [Header("Lock-on (follows the opponent's head)")]
        [SerializeField] private RectTransform _lockRoot;
        [SerializeField] private RectTransform _lockSpin;
        [SerializeField] private GameObject _lockDot;
        [SerializeField] private CanvasGroup _rays;

        [Header("Power")]
        [SerializeField] private SlapArc _gauge;
        [Tooltip("The gauge's root, scaled by the budget's chargeGaugeScale.")]
        [SerializeField] private RectTransform _gaugeRoot;
        [SerializeField] private RectTransform _valueRoot;
        [SerializeField] private TMP_Text _value;
        [SerializeField] private TMP_Text _caption;
        [SerializeField] private RectTransform _tapPulse;
        [SerializeField] private RectTransform _tapRoot;
        [SerializeField] private ChromaText _prompt;
        [SerializeField] private CanvasGroup _promptGroup;
        [SerializeField] private TMP_Text _hint;

        [Header("Mini HP (top right)")]
        [SerializeField] private HpBar _miniHp;
        [SerializeField] private TMP_Text _miniHpLabel;

        [Header("Colours")]
        [SerializeField] private Color _captionColor = new Color(0.635f, 0.659f, 0.702f);
        [SerializeField] private Color _maxColor = new Color(1f, 0.294f, 0.325f);

        [SerializeField, Range(0.5f, 1f)] private float _maxAt = 0.95f;

        private int _lastTaps;
        private int _shownValue = -1;
        private bool _shownMax;
        private float _barHeight = 70f;
        private bool _holdMode;

        public override void Bind(HudContext ctx)
        {
            base.Bind(ctx);
            var b = ctx.Attention;
            _barHeight = b.letterboxPx;
            SetBarHeight(_letterTop);
            SetBarHeight(_letterBottom);
            if (_gaugeRoot != null) _gaugeRoot.localScale = Vector3.one * b.chargeGaugeScale;
            if (_miniHpLabel != null) _miniHpLabel.text = ctx.OpponentName + " HP";
        }

        private void SetBarHeight(RectTransform bar)
        {
            if (bar != null) bar.sizeDelta = new Vector2(bar.sizeDelta.x, _barHeight);
        }

        protected override void OnShow()
        {
            var b = Budget;
            _lastTaps = 0;
            _shownValue = -1;
            _shownMax = true; // force the first caption write
            _holdMode = Ctx.Director != null && Ctx.Director.CurrentAttackMode == AttackMode.HoldRing;
            if (_lockRoot != null) _lockRoot.gameObject.SetActive(!_holdMode);
            if (_lockDot != null) _lockDot.SetActive(b.chargeLockDot);
            if (_vignette != null) _vignette.alpha = b.chargeVignetteAlpha;
            if (_rays != null) _rays.alpha = b.chargeRaysRestAlpha;
            if (_promptGroup != null) _promptGroup.alpha = 1f;
            if (_prompt != null) _prompt.SetText(_holdMode ? HoldPrompt : TapPrompt);
            if (_hint != null) _hint.text = _holdMode ? HoldHint : TapHint;
            if (_letterTop != null) Tw.AnchorMove(_letterTop, new Vector2(0f, _barHeight), Vector2.zero, 0.28f, Ease.OutCubic);
            if (_letterBottom != null) Tw.AnchorMove(_letterBottom, new Vector2(0f, -_barHeight), Vector2.zero, 0.28f, Ease.OutCubic);
            if (_lockRoot != null) Tw.Scale(_lockRoot, 1.8f, 1f, 0.35f, Ease.OutBack);
            if (_lockSpin != null) Tw.RotateZ(_lockSpin, 0f, -360f, 6f, Ease.Linear).SetLoops(-1);
            float ps = b.chargePromptScale;
            if (_tapPulse != null) Tw.Scale(_tapPulse, ps, ps * 1.07f, 0.28f, Ease.InOutQuad).SetLoops(-1, LoopType.Yoyo);
            if (_gauge != null) _gauge.Fill = 0f;
            if (_miniHp != null && Ctx.Director != null) _miniHp.SetImmediate(Ctx.Director.OpponentHealth.NormalizedHp);
            Follow();
        }

        protected override void OnHide()
        {
            if (_letterTop != null) Tw.AnchorMove(_letterTop, Vector2.zero, new Vector2(0f, _barHeight), 0.22f, Ease.InQuad);
            if (_letterBottom != null) Tw.AnchorMove(_letterBottom, Vector2.zero, new Vector2(0f, -_barHeight), 0.22f, Ease.InQuad);
        }

        protected override void OnTick(float dt)
        {
            float fill;
            if (_holdMode) fill = Mathf.Clamp01(Ctx.Director.HoldRingMeter.WindUp01);
            else
            {
                float max = Ctx.Config.powerPhase.maxFill > 0f ? Ctx.Config.powerPhase.maxFill : 1f;
                fill = Mathf.Clamp01(Ctx.Director.PowerMeter.Fill / max);
            }
            if (_gauge != null) _gauge.Fill = Mathf.Lerp(_gauge.Fill, fill, 1f - Mathf.Exp(-18f * dt));

            int v = Mathf.RoundToInt(fill * 100f);
            if (v != _shownValue && _value != null) { _shownValue = v; _value.SetText("{0}", v); }

            bool isMax = fill >= _maxAt;
            if (isMax != _shownMax)
            {
                _shownMax = isMax;
                if (_caption != null)
                {
                    _caption.text = isMax ? "MAX POWER" : "POWER";
                    _caption.color = isMax ? _maxColor : _captionColor;
                }
                if (_holdMode && _prompt != null)
                {
                    _prompt.SetText(isMax ? ReleasePrompt : HoldPrompt);
                    if (isMax && _tapRoot != null) Tw.Punch(_tapRoot, 0.18f, 0.2f, 3);
                    if (isMax) KickRays();
                }
            }

            if (!_holdMode)
            {
                int taps = Ctx.Director.PowerMeter.TapsAccepted;
                if (taps > _lastTaps)
                {
                    int before = _lastTaps;
                    _lastTaps = taps;
                    if (_valueRoot != null) Tw.Punch(_valueRoot, 0.12f, 0.16f, 3);
                    if (_tapRoot != null) Tw.Punch(_tapRoot, 0.14f, 0.16f, 3);
                    KickRays();
                    int fadeAfter = Budget.chargePromptFadeAfterTaps;
                    if (fadeAfter > 0 && before < fadeAfter && taps >= fadeAfter && _promptGroup != null)
                        Tw.Fade(_promptGroup, _promptGroup.alpha, Budget.chargePromptFadedAlpha, 0.3f, Ease.OutQuad);
                }
            }
            Follow();
        }

        // Motion that answers the finger: the lines flare on the tap and settle back to rest.
        private void KickRays()
        {
            if (_rays == null) return;
            var b = Budget;
            Tw.Kill(_rays);
            Tw.Fade(_rays, b.chargeRaysKickAlpha, b.chargeRaysRestAlpha, Sec(b.chargeRaysKickMs), Ease.OutQuad);
        }

        private void Follow()
        {
            if (_lockRoot == null || Ctx == null) return;
            var head = Ctx.HeadOf(FighterSlot.Opponent);
            if (head != null && Ctx.TryWorldToLocal(head.position, _world, out var local))
                _lockRoot.anchoredPosition = local;
        }
    }
}
