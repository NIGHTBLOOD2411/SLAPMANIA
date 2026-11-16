using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;
using SlapMania.Input;
using SlapMania.Presentation;
using SlapMania.UI;
using SlapMania.Logging;

namespace SlapMania.App
{
    /// The bout driver — the "game manager" for one fight. It builds the headless
    /// MatchDirector, binds the player's live input and the AI opponent (the same code path,
    /// ARCHITECTURE §1.3), ticks the FSM every frame, and translates its signals into
    /// fighter-view, HUD and hitstop calls. App owns the wiring; Gameplay owns the rules.
    ///
    /// It resolves config from GameRoot when present, else from a serialized fallback, so you
    /// can press Play straight in the bout scene while iterating. Feel values (hitstop, recoil)
    /// come from GameConfig.feel so Play Mode tuning persists in the asset.
    ///
    /// Each bout gets a fresh seed unless BoutConfig.matchSeed pins one (S-094), and an opponent
    /// rolled from the AI ladder near the player's level in GameRoot (M07 §4b, S-095). Every
    /// landed slap is photographed (SlapSnapshotter, M09); the loser's gallery goes to the HUD
    /// at BoutEnd.
    ///
    /// Pause, forfeit and the UI Toolkit overlay (PauseMenu, RankUp, ...) are in
    /// BoutController.Overlay.cs.
    public sealed partial class BoutController : MonoBehaviour
    {
        [Header("Config (fallback used only when there is no GameRoot)")]
        [SerializeField] private GameConfig _config;

        [Header("Scene references")]
        [SerializeField] private FighterViewBase _playerView;
        [SerializeField] private FighterViewBase _opponentView;
        [SerializeField] private V2Hud _hud;
        [SerializeField] private HitstopController _hitstop;
        [Tooltip("Camera the HUD anchors to (rings over faces). Falls back to Camera.main.")]
        [SerializeField] private Camera _camera;

        [Header("Arena (optional)")]
        [SerializeField] private ArenaDirector _arena;
        [SerializeField] private CameraRig _cameraRig;

        private MatchDirector _director;
        private InputSystemTapSource _playerInput;
        private readonly SlapStats _stats = new SlapStats();
        private GameConfig _cfg;
        private FeelConfig _feel;
        private bool _running;
        private int _portraitFrame = -1;
        private SlapSnapshotter _snapshots;
        private int _ladderLevel;
        // The current slap's halves, for D4's stat tiles (struct copies: nothing allocates, R5).
        private PowerResult _slapPower;
        private TimingResult _slapTiming;
        private double _boutStartedAt;

        public MatchDirector Director => _director;
        public SlapStats Stats => _stats;

        private void Start()
        {
            var cfg = ResolveConfig();
            if (cfg == null)
            {
                SlapLog.Error(LogChannel.Boot, "BoutController: no GameConfig (assign one or add a GameRoot). Disabling.");
                enabled = false;
                return;
            }
            if (!cfg.Validate(out string err))
            {
                SlapLog.Error(LogChannel.Boot, "BoutController: GameConfig invalid: " + err + ". Disabling.");
                enabled = false;
                return;
            }

            _cfg = cfg;
            _feel = cfg.feel;
            var clock = new UnityClock();
            // 0 = a fresh seed every bout (S-094); logged so a bout can be replayed (S-024).
            int seed = cfg.bout.matchSeed != 0 ? cfg.bout.matchSeed : Environment.TickCount;
            var rng = new SeededRandom(seed);
            _director = new MatchDirector(cfg.bout, cfg.powerPhase, cfg.timingPhase,
                                          cfg.timingZoneLayout, cfg.damage, clock, rng, cfg.holdRing);

            // The AI ladder (M07 §4b): this bout's tier is rolled near the player's level.
            _ladderLevel = GameRoot.Instance != null ? GameRoot.Instance.AiLadderLevel : 0;
            int tier = AiLadder.RollTier(_ladderLevel, cfg.aiProfiles.Length, rng);
            var opponent = cfg.ProfileForTier(tier);
            SlapLog.Info(LogChannel.Match, "Bout seed " + seed + ", ladder level " + _ladderLevel +
                         ", AI tier " + tier + " (" + opponent.name + ")");

            string opponentName = opponent.displayName;
            string playerName = null;
            if (SpawnChampions(cfg.roster, rng, out var champion, out var rival))
            {
                opponentName = rival.ShownName;
                playerName = champion.ShownName;
            }

            _playerInput = new InputSystemTapSource(TapEvent.SourcePlayerTouch);
            _director.BindPlayerSource(_playerInput);
            _director.BindOpponent(opponent);
            _stats.Attach(_director, cfg.timingPhase.perfectCentre);

            if (_playerView != null && _opponentView != null)
            {
                _playerView.BindOpponent(_opponentView);
                _opponentView.BindOpponent(_playerView);
            }

            Subscribe();
            var cam = _camera != null ? _camera : Camera.main;
            _snapshots = new SlapSnapshotter(cam);
            if (_hud != null)
            {
                _hud.Bind(_director, _stats, cfg, cam,
                          _playerView != null ? _playerView.HeadAnchor : null,
                          _opponentView != null ? _opponentView.HeadAnchor : null,
                          opponentName, playerName);
                _hud.RematchRequested += OnRematchRequested;
                _hud.MenuRequested += OnMenuRequested;
            }
            BindOverlay(cfg);

            if (_playerView != null) _playerView.PlayIdle();
            if (_opponentView != null) _opponentView.PlayIdle();
            _portraitFrame = Time.frameCount + 2; // after the Animators have posed the fighters

            _director.StartBout();
            _boutStartedAt = _director.Now;
            _running = true;
        }

        /// The champions (M08, ADR-0022): the player's Roster pick (the first when none or an
        /// unknown id) and a seeded opponent that is never the same champion unless the roster
        /// has one. Their prefabs replace the scene's fighters and their stats bind to the FSM.
        /// Rolled after the AI tier so a pinned matchSeed keeps its tier (S-094).
        private bool SpawnChampions(ChampionRoster roster, IRandom rng, out ChampionDefinition player, out ChampionDefinition rival)
        {
            player = rival = null;
            if (roster == null || roster.Count == 0) return false;
            int pi = GameRoot.Instance != null ? roster.IndexOf(GameRoot.Instance.SelectedChampionId) : -1;
            if (pi < 0) pi = 0;
            int oi = ChampionRules.PickOpponent(roster.Count, pi, rng);
            player = roster.Get(pi);
            rival = roster.Get(oi);
            _playerView = ChampionSpawner.Spawn(_playerView, player, tintAsRival: false, roster.fighterHumanScale);
            _opponentView = ChampionSpawner.Spawn(_opponentView, rival, tintAsRival: pi == oi, roster.fighterHumanScale);
            _director.BindChampions(player.stats, rival.stats);
            SlapLog.Info(LogChannel.Match, "Champions: " + player.id + " vs " + rival.id);
            return true;
        }

        private void Update()
        {
            if (_running) _director.Tick();
        }

        private void LateUpdate()
        {
            if (_portraitFrame < 0 || Time.frameCount < _portraitFrame) return;
            _portraitFrame = -1;
            if (_hud == null || _playerView == null || _opponentView == null) return;
            var p = FighterPortrait.Capture(_playerView.HeadAnchor, _opponentView.transform.position - _playerView.transform.position);
            var o = FighterPortrait.Capture(_opponentView.HeadAnchor, _playerView.transform.position - _opponentView.transform.position);
            _hud.SetPortraits(p, o);
        }

        private GameConfig ResolveConfig()
        {
            if (GameRoot.Instance != null && GameRoot.Instance.Config != null) return GameRoot.Instance.Config;
            return _config;
        }

        // ── Signal wiring: discrete moments only; per-frame values are polled by the HUD. ──
        private void Subscribe()
        {
            var s = _director.Signals;
            s.BoutIntroStart += OnBoutIntroStart;
            s.TurnStart     += OnTurnStart;
            s.ChargeStart   += OnChargeStart;
            s.TimingStart   += OnTimingStart;
            s.WindupStart   += OnWindupStart;
            s.ImpactFrame   += OnImpactFrame;
            s.FinisherStart += OnFinisherStart;
            s.ChargeEnd     += OnChargeEnd;
            s.TimingEnd     += OnTimingEnd;
            s.SnapshotRequested += OnSnapshotRequested;
            s.FinisherSnapshotRequested += OnSnapshotRequested;
            s.TurnEnd       += OnTurnEnd;
            s.KO            += OnKO;
            s.BoutEnd       += OnBoutEnd;
        }

        private void Unsubscribe()
        {
            if (_director == null) return;
            var s = _director.Signals;
            s.BoutIntroStart -= OnBoutIntroStart;
            s.TurnStart     -= OnTurnStart;
            s.ChargeStart   -= OnChargeStart;
            s.TimingStart   -= OnTimingStart;
            s.WindupStart   -= OnWindupStart;
            s.ImpactFrame   -= OnImpactFrame;
            s.FinisherStart -= OnFinisherStart;
            s.ChargeEnd     -= OnChargeEnd;
            s.TimingEnd     -= OnTimingEnd;
            s.SnapshotRequested -= OnSnapshotRequested;
            s.FinisherSnapshotRequested -= OnSnapshotRequested;
            s.TurnEnd       -= OnTurnEnd;
            s.KO            -= OnKO;
            s.BoutEnd       -= OnBoutEnd;
        }

        private FighterViewBase ViewOf(FighterSlot slot) =>
            slot == FighterSlot.Player ? _playerView : _opponentView;

        private void OnBoutIntroStart()
        {
            if (_cameraRig != null) _cameraRig.PlayIntro(_cfg.bout.introMs * 0.001f);
            BindSideTargets();
            PlayCameraCue(CameraCue.BoutIntro);
        }

        // Fixed-side names for two-sided camera shots (M16): unlike attacker/defender these never
        // swap, so the player's shoulder cam and the opponent's are authored separately.
        private void BindSideTargets()
        {
            var cam = _cameraRig != null ? _cameraRig.Director : null;
            if (cam == null) return;
            if (_playerView != null) { cam.SetTarget("player", _playerView.transform); cam.SetTarget("playerHead", _playerView.HeadAnchor); }
            if (_opponentView != null) { cam.SetTarget("opponent", _opponentView.transform); cam.SetTarget("opponentHead", _opponentView.HeadAnchor); }
        }

        // M16: a recorded sequence bound to the cue plays over the rig; unbound, nothing changes.
        private void PlayCameraCue(CameraCue cue)
        {
            var d = _cameraRig != null ? _cameraRig.Director : null;
            if (d != null) d.Cue(cue);
        }

        private void OnTurnStart(TurnInfo t)
        {
            var atk = ViewOf(t.Attacker);
            var def = ViewOf(t.Defender);
            if (_arena != null && atk != null && def != null) _arena.FocusDamageCam(def.HeadAnchor, atk.transform.position);
            var cam = _cameraRig != null ? _cameraRig.Director : null;
            if (cam != null && atk != null && def != null)
            {
                // Names camera keys can lookAt / follow (M16 §3).
                cam.Attacker = t.Attacker; // two-sided state shots pick their side from this
                BindSideTargets();
                cam.SetTarget("attacker", atk.transform);
                cam.SetTarget("attackerHead", atk.HeadAnchor);
                cam.SetTarget("defender", def.transform);
                cam.SetTarget("defenderHead", def.HeadAnchor);
            }
            PlayCameraCue(CameraCue.TurnStart);
        }

        private void OnChargeStart(TurnInfo t)
        {
            var atk = ViewOf(t.Attacker); if (atk != null) atk.PlayChargePose();
            var def = ViewOf(t.Defender); if (def != null) def.PlayIdle();
            PlayCameraCue(CameraCue.ChargeStart);
        }

        private void OnTimingStart(TurnInfo t) => PlayCameraCue(CameraCue.TimingStart);

        private void OnWindupStart(double contactDsp)
        {
            var atk = ViewOf(_director.CurrentTurn.Attacker);
            if (atk != null) atk.PlayWindup(_cfg.bout.windupMs * 0.001f);
            PlayCameraCue(CameraCue.Windup);
        }

        private void OnKO(FighterSlot loser) => PlayCameraCue(CameraCue.KO);

        private void OnImpactFrame(DamageResult d)
        {
            var t = _director.CurrentTurn;
            var atk = ViewOf(t.Attacker);
            var def = ViewOf(t.Defender);
            float intensity = Mathf.Clamp01(d.FinalDamage / _feel.recoilDamageForFull);
            Vector3 dir = atk != null && def != null ? def.transform.position - atk.transform.position : Vector3.forward;
            if (def != null)
            {
                if (d.IsLethal) def.PlayKnockout(intensity, dir);
                else def.PlayImpactRecoil(intensity, dir);
            }

            int freezeMs = _feel.hitstopMs
                         + (d.IsCrit ? _feel.critHitstopBonusMs : 0)
                         + (d.IsFinisher ? _feel.finisherHitstopBonusMs : 0);
            if (_hitstop != null) _hitstop.Apply(freezeMs * 0.001f);
            if (_arena != null) _arena.ImpactFlash(d.IsLethal ? 1f : intensity);
            if (_cameraRig != null) _cameraRig.Kick(d.IsLethal ? 1f : intensity, d.IsLethal);
            PlayCameraCue(d.IsLethal ? CameraCue.LethalImpact : CameraCue.Impact);
        }

        private void OnFinisherStart(DamageResult d)
        {
            var atk = ViewOf(_director.CurrentTurn.Attacker);
            if (atk != null) atk.PlayFinisher();
            PlayCameraCue(CameraCue.Finisher);
        }

        private void OnChargeEnd(PowerResult p) { _slapPower = p; PlayCameraCue(CameraCue.ChargeEnd); }
        private void OnTimingEnd(TimingResult t) { _slapTiming = t; PlayCameraCue(CameraCue.TimingEnd); }

        // Every landed slap, the finisher included: photograph the defender with the palm on
        // their cheek (M09), and make it D4 Instant Replay's shot (the pause menu's "Last
        // replay", or the K.O.'s replay). A powerful one is also a slapshot: the Polaroid shows
        // while the director holds the Snapshot state (the K.O. has its own screen instead).
        private void OnSnapshotRequested(DamageResult d)
        {
            var t = _director.CurrentTurn;
            var def = ViewOf(t.Defender);
            var atk = ViewOf(t.Attacker);
            if (_snapshots == null || def == null || atk == null) return;
            bool slapshot = _director.LastWasSlapshot;
            if (!_snapshots.Capture(def.HeadAnchor, atk.transform.position, atk.StrikingHand,
                                    new SlapShot(t.SlapIndex, t.Defender, d.FinalDamage, d.IsLethal, slapshot))) return;
            if (slapshot && !d.IsLethal && _hud != null) _hud.ShowSlapshot(_snapshots.LatestHero, t.Defender, d.FinalDamage);
            if (_screens == null) return;
            var r = _screens.Replay;
            r.Photo = _snapshots.LatestHero;
            r.Round = t.RoundIndex + 1;
            r.Finisher = d.IsLethal;
            r.Damage = d.FinalDamage;
            r.Power = Mathf.RoundToInt(_slapPower.RawPower * 100f);
            r.Zone = _slapTiming.Zone.ToString().ToUpperInvariant();
            r.Multiplier = _slapTiming.Multiplier;
            r.ImpactAt = _director.Now - _boutStartedAt;
        }

        private void OnTurnEnd(TurnInfo t)
        {
            var atk = ViewOf(t.Attacker); if (atk != null) atk.PlayIdle();
            var def = ViewOf(t.Defender); if (def != null) def.PlayIdle();
            PlayCameraCue(CameraCue.TurnEnd);
        }

        private void OnBoutEnd(MatchResult r)
        {
            _running = false;
            RememberResult(r);
            PlayCameraCue(CameraCue.BoutEnd);
            // Before the HUD's own BoutEnd handler (it subscribes after this class), so E2/E3 have it.
            if (_hud != null && _snapshots != null)
            {
                var ctx = _hud.Context;
                ctx.GalleryCount = _snapshots.FillGallery(r.Winner.Other(), ctx.Gallery, ctx.GalleryDamage);
                // A K.O. with its photo: the K.O.'s CONTINUE plays D4 Instant Replay first (M09).
                _hud.OfferReplay = !r.Forfeited && _screens != null && _screens.Hosts(UI.FrontEnd.ScreenId.Replay)
                                   && _snapshots.LatestHero != null && _snapshots.LatestShot.IsLethal;
            }
            if (GameRoot.Instance != null)
            {
                GameRoot.Instance.AiLadderLevel = AiLadder.NextLevel(_ladderLevel, r.Winner == FighterSlot.Player,
                                                                     _cfg.aiProfiles.Length);
                SlapLog.Info(LogChannel.Match, "Ladder level " + _ladderLevel + " -> " + GameRoot.Instance.AiLadderLevel);
            }
            var winner = ViewOf(r.Winner);            if (winner != null) winner.PlayVictoryPose();
            var loser  = ViewOf(r.Winner.Other());    if (loser != null)  loser.PlayDefeatPose();
            SlapLog.Info(LogChannel.Match, "Bout over. Winner: " + r.Winner + ", slaps: " + r.TotalSlaps);
        }

        /// Boot flow: GameRoot unloads and re-adds the bout scene. Standalone (Play pressed in
        /// the bout scene itself): reload this scene.
        private void OnRematchRequested()
        {
            _running = false;
            if (GameRoot.Instance != null) GameRoot.Instance.RequestBoutReload();
            else SceneManager.LoadScene(gameObject.scene.name);
        }

        /// Results' "Main menu": back to the front end's main menu, through E4 RankUp after a
        /// win. Standalone (no GameRoot, so no front end loaded): there is nowhere to go, so it
        /// acts as Rematch.
        private void OnMenuRequested()
        {
            if (TryShowRankUp()) return;
            LeaveToMenu();
        }

        private void LeaveToMenu()
        {
            _running = false;
            if (GameRoot.Instance != null) GameRoot.Instance.RequestFrontEnd(UI.FrontEnd.ScreenId.MainMenu);
            else OnRematchRequested();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _stats.Detach();
            if (_hud != null) _hud.RematchRequested -= OnRematchRequested;
            if (_hud != null) _hud.MenuRequested -= OnMenuRequested;
            UnbindOverlay();
            _playerInput?.Dispose();
            _snapshots?.Dispose();
        }
    }
}
