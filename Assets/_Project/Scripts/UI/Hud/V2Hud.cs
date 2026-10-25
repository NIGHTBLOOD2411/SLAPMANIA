using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;

namespace SlapMania.UI
{
    /// The V2 in-match HUD (M10, ADR-0011): routes MatchSignals to the screens built from the
    /// V2 reference — C4 intro, C6 charge, C5 timing ring, D1 impact, D2 opponent turn,
    /// D3 knock-out, E2/E3 result — and owns the shared HudContext. It never drives gameplay:
    /// Rematch is raised as an event for the composition root (UI cannot reference App).
    public sealed class V2Hud : MonoBehaviour
    {
        public event Action RematchRequested;
        /// Results' "Main menu" (M10): the composition root returns to the front end.
        public event Action MenuRequested;
        /// A HUD pause button was pressed (M10): the composition root pauses the bout.
        public event Action PauseRequested;
        /// The K.O.'s CONTINUE, when OfferReplay is set: the composition root shows D4 Instant
        /// Replay (M09) and calls ShowResultScreen when it is dismissed.
        public event Action ReplayRequested;

        /// Set by the composition root at BoutEnd when a final-blow photo exists.
        public bool OfferReplay { get; set; }

        [SerializeField] private HudScorebug _scorebug;
        [SerializeField] private HudIntro _intro;
        [SerializeField] private HudCharge _charge;
        [SerializeField] private HudTiming _timing;
        [SerializeField] private HudImpact _impact;
        [SerializeField] private HudOpponentTurn _opponentTurn;
        [SerializeField] private HudKnockout _knockout;
        [SerializeField] private HudResult _result;
        [Tooltip("Every HUD pause control: the scorebug's and the C6 letterbox's.")]
        [SerializeField] private HudPauseButton[] _pauseButtons = new HudPauseButton[0];

        [Header("Names (placeholders until a fighter roster exists)")]
        [SerializeField] private string _playerName = "ALUCARD";
        [SerializeField] private string _opponentName = "RIVAL";

        private static HudAttentionConfig s_defaultBudget;

        private readonly HudContext _ctx = new HudContext();
        private MatchDirector _director;
        // One headline at a time (POLISH §9): a screen whose show waits for D1's headline to leave.
        private HudView _deferred;
        private double _deferUntil;

        public HudContext Context => _ctx;

        /// `opponentName`: this bout's AI tier name (M07 ladder); null or empty keeps the placeholder.
        /// Views read names at Bind, so it is set here, not later.
        public void Bind(MatchDirector director, SlapStats stats, GameConfig config, Camera cam,
                         Transform playerHead, Transform opponentHead, string opponentName = null)
        {
            Unsubscribe();
            _director = director;
            _ctx.Director = director;
            _ctx.Stats = stats;
            _ctx.Config = config;
            if (config != null && config.hudAttention != null) _ctx.Attention = config.hudAttention;
            else
            {
                if (s_defaultBudget == null) s_defaultBudget = ScriptableObject.CreateInstance<HudAttentionConfig>();
                _ctx.Attention = s_defaultBudget;
            }
            _deferred = null;
            _ctx.Camera = cam;
            _ctx.PlayerHead = playerHead;
            _ctx.OpponentHead = opponentHead;
            _ctx.PlayerName = _playerName;
            _ctx.OpponentName = string.IsNullOrEmpty(opponentName) ? _opponentName : opponentName;
            _ctx.HasResult = false;

            BindView(_scorebug); BindView(_intro); BindView(_charge); BindView(_timing);
            BindView(_impact); BindView(_opponentTurn); BindView(_knockout); BindView(_result);
            Subscribe();
        }

        public void SetPortraits(Texture player, Texture opponent)
        {
            _ctx.PlayerPortrait = player;
            _ctx.OpponentPortrait = opponent;
            if (_scorebug != null) _scorebug.RefreshPortraits();
        }

        private void BindView(HudView v)
        {
            if (v != null) v.Bind(_ctx);
        }

        // ── Signals ──────────────────────────────────────────────────
        private void Subscribe()
        {
            if (_director == null) return;
            var s = _director.Signals;
            s.BoutIntroStart += OnBoutIntroStart;
            s.BoutIntroEnd   += OnBoutIntroEnd;
            s.ChargeStart    += OnChargeStart;
            s.ChargeEnd      += OnChargeEnd;
            s.TimingStart    += OnTimingStart;
            s.TimingEnd      += OnTimingEnd;
            s.ImpactFrame    += OnImpactFrame;
            s.WindupStart    += OnWindupStart;
            s.BraceResolved  += OnBraceResolved;
            s.FinisherStart  += OnFinisherStart;
            s.BoutEnd        += OnBoutEnd;
            if (_knockout != null) _knockout.ContinueRequested += OnKnockoutContinue;
            if (_result != null) _result.RematchRequested += OnRematch;
            if (_result != null) _result.MenuRequested += OnMenu;
            for (int i = 0; i < _pauseButtons.Length; i++)
                if (_pauseButtons[i] != null) _pauseButtons[i].Pressed += OnPause;
        }

        private void Unsubscribe()
        {
            if (_knockout != null) _knockout.ContinueRequested -= OnKnockoutContinue;
            if (_result != null) _result.RematchRequested -= OnRematch;
            if (_result != null) _result.MenuRequested -= OnMenu;
            for (int i = 0; i < _pauseButtons.Length; i++)
                if (_pauseButtons[i] != null) _pauseButtons[i].Pressed -= OnPause;
            if (_director == null) return;
            var s = _director.Signals;
            s.BoutIntroStart -= OnBoutIntroStart;
            s.BoutIntroEnd   -= OnBoutIntroEnd;
            s.ChargeStart    -= OnChargeStart;
            s.ChargeEnd      -= OnChargeEnd;
            s.TimingStart    -= OnTimingStart;
            s.TimingEnd      -= OnTimingEnd;
            s.ImpactFrame    -= OnImpactFrame;
            s.WindupStart    -= OnWindupStart;
            s.BraceResolved  -= OnBraceResolved;
            s.FinisherStart  -= OnFinisherStart;
            s.BoutEnd        -= OnBoutEnd;
        }

        private void OnBoutIntroStart()
        {
            _ctx.PhaseStartedAt = _ctx.Now;
            if (_intro != null)
            {
                _intro.Begin(_ctx.PhaseStartedAt, _ctx.Config.bout.introMs * 0.001f);
                _intro.Show();
            }
            if (_scorebug != null) _scorebug.Show();
        }

        private void OnBoutIntroEnd()
        {
            if (_intro != null) _intro.Hide();
        }

        private void OnChargeStart(TurnInfo t)
        {
            _ctx.PhaseStartedAt = _ctx.Now;
            // The new turn owns the screen: the last slap's headline leaves now, and the next
            // headline (INCOMING!) waits for it rather than stacking on it.
            float wait = _impact != null ? _impact.Yield() : 0f;
            if (t.Attacker == FighterSlot.Player)
            {
                _deferred = null;
                Hide(_opponentTurn);
                Hide(_scorebug);   // C6 is full-screen cinematic: letterbox instead of the bug
                Show(_charge);
            }
            else ShowAfter(_opponentTurn, wait);
        }

        private void ShowAfter(HudView v, float seconds)
        {
            if (seconds <= 0f) { _deferred = null; Show(v); return; }
            _deferred = v;
            _deferUntil = _ctx.Now + seconds;
        }

        // Sim time (MatchDirector.Now) holds on the pause menu, so a deferred show does too.
        private void Update()
        {
            if (_deferred == null || _ctx.IsPaused || _ctx.Now < _deferUntil) return;
            var v = _deferred;
            _deferred = null;
            Show(v);
        }

        private void OnChargeEnd(PowerResult r)
        {
            _ctx.LastPower = r;
            if (_director.CurrentTurn.Attacker == FighterSlot.Player)
            {
                Hide(_charge);
                Show(_scorebug);
            }
        }

        private void OnTimingStart(TurnInfo t)
        {
            _ctx.PhaseStartedAt = _ctx.Now;
            if (t.Attacker == FighterSlot.Player) Show(_timing);
            else Show(_opponentTurn); // hold ring: the ring opens before the rival's press (ChargeStart)
        }

        private void OnTimingEnd(TimingResult r)
        {
            _ctx.LastTiming = r;
            if (_director.CurrentTurn.Attacker == FighterSlot.Player && _timing != null) _timing.Commit(r);
        }

        private void OnImpactFrame(DamageResult d)
        {
            _ctx.LastDamage = d;
            var t = _director.CurrentTurn;
            // The rival's slap: the brace result stays readable a moment (ADR-0019).
            if (t.Attacker == FighterSlot.Opponent && _opponentTurn != null) _opponentTurn.HideSoon();
            else Hide(_opponentTurn);
            Hide(_timing);
            if (_impact != null) _impact.Play(t.Attacker, d, _ctx.LastTiming, _ctx.LastPower);
            if (_scorebug != null) _scorebug.OnHit(t.Defender);
        }

        private void OnWindupStart(double contactAt)
        {
            if (_director.CurrentTurn.Attacker == FighterSlot.Opponent && _director.IsBraceWindowOpen && _opponentTurn != null)
                _opponentTurn.ShowBraceCue();
        }

        private void OnBraceResolved(BraceResult r)
        {
            if (_opponentTurn != null) _opponentTurn.ShowBraceResult(r);
        }

        private void OnFinisherStart(DamageResult d)
        {
            Hide(_scorebug);
            if (_knockout != null) _knockout.Play(_director.CurrentTurn.Attacker);
        }

        private void OnBoutEnd(MatchResult r)
        {
            _ctx.Result = r;
            _ctx.HasResult = true;
            _deferred = null;
            // A forfeit can end the bout from any phase, the intro included.
            Hide(_intro); Hide(_charge); Hide(_timing); Hide(_opponentTurn);
            if (_knockout != null && _knockout.IsShown) _knockout.ShowContinue();
            else ShowResult();
        }

        private void OnKnockoutContinue()
        {
            Hide(_knockout);
            if (OfferReplay && ReplayRequested != null)
            {
                OfferReplay = false;   // once: the replay's dismissal comes back via ShowResultScreen
                Hide(_scorebug);
                Hide(_impact);
                ReplayRequested();
                return;
            }
            ShowResult();
        }

        /// E2/E3 now: after D4 Instant Replay is dismissed (M09).
        public void ShowResultScreen() => ShowResult();

        private void ShowResult()
        {
            Hide(_scorebug);
            Hide(_impact);
            if (_result != null && _ctx.HasResult) _result.Play(_ctx.Result);
        }

        private void OnRematch() => RematchRequested?.Invoke();
        private void OnMenu() => MenuRequested?.Invoke();
        private void OnPause() => PauseRequested?.Invoke();

        /// A press at `screen` (pixels) that hits a HUD control rather than the slap - today
        /// the pause buttons. The player's tap source asks this per tap (M02 Blocker), so it
        /// walks an array: no allocation (R5).
        public bool BlocksTap(Vector2 screen)
        {
            for (int i = 0; i < _pauseButtons.Length; i++)
                if (_pauseButtons[i] != null && _pauseButtons[i].Covers(screen)) return true;
            return false;
        }

        private static void Show(HudView v) { if (v != null) v.Show(); }
        private static void Hide(HudView v) { if (v != null) v.Hide(); }

        private void OnDestroy() => Unsubscribe();
    }
}
