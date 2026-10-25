using UnityEngine;
using UnityEngine.InputSystem;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;
using SlapMania.UI.FrontEnd;

namespace SlapMania.App
{
    /// The bout's UI Toolkit overlay (M10, ADR-0013) and the pause it fronts (M01).
    ///
    /// Pause: the HUD's pause button, device back / Escape, or the app going to the background
    /// -> MatchDirector.RequestPause (the simulation clock stops), HitstopController.SetPaused
    /// (scaled time stops; a freeze in progress keeps its remaining time) and D5 PauseMenu.
    ///
    /// The overlay screens are the mockups, so their links name mockup screens. Here they mean:
    ///   PauseMenu  Resume (-> Aim)          resume the bout
    ///              Settings                 B6 on top; its back arrow (-> Profile) comes back
    ///              Last replay (-> Replay)  D4 placeholder: Close (-> Knockout) comes back,
    ///                                       Next (-> RoundEnd) shows E1, whose "Round 2"
    ///                                       (-> Ready) or Win link resumes
    ///              Forfeit (-> Loss)        MatchDirector.Forfeit(Player): the E3 Defeat card
    ///   Results    MAIN MENU after a win    E4 RankUp first (a placeholder until divisions,
    ///                                       M12); its "Collect all" (-> MainMenu) leaves
    public sealed partial class BoutController
    {
        [Header("Overlay (M10: pause menu, rank-up; optional)")]
        [SerializeField] private ScreenManager _screens;

        private InputAction _back;
        private bool _rankUpShown;
        private bool _hasResult;
        private MatchResult _result;

        public bool IsPaused => _director != null && _director.IsPaused;
        public ScreenManager Screens => _screens;

        private void BindOverlay(GameConfig cfg)
        {
            if (_hud != null)
            {
                _hud.PauseRequested += OnPauseRequested;
                _hud.ReplayRequested += OnReplayRequested;
                // A press on the pause button is a click, not a slap tap (it would commit timing).
                _playerInput.Blocker = _hud.BlocksTap;
            }
            if (_screens != null)
            {
                if (cfg.uiMotion != null) _screens.Motion = cfg.uiMotion;
                _screens.MatchRequested += OnOverlayMatchLink;
                _screens.LeaveRequested += OnOverlayLeave;
                _screens.ReplayFinished += OnReplayFinished;
            }
            // Android's back button arrives as Escape in the Input System.
            _back = new InputAction("BoutBack", InputActionType.Button);
            _back.AddBinding("<Keyboard>/escape");
            _back.AddBinding("<Gamepad>/start");
            _back.performed += OnBack;
            _back.Enable();
        }

        private void UnbindOverlay()
        {
            if (_hud != null) _hud.PauseRequested -= OnPauseRequested;
            if (_hud != null) _hud.ReplayRequested -= OnReplayRequested;
            if (_screens != null)
            {
                _screens.MatchRequested -= OnOverlayMatchLink;
                _screens.LeaveRequested -= OnOverlayLeave;
                _screens.ReplayFinished -= OnReplayFinished;
            }
            if (_back == null) return;
            _back.performed -= OnBack;
            _back.Dispose();
            _back = null;
        }

        // ── Pause ────────────────────────────────────────────────────
        /// Freeze the bout and open D5 PauseMenu. False when there is nothing to pause: before
        /// the bout runs, once it is over, or when already paused.
        public bool Pause()
        {
            if (!_running || _director == null || !_director.RequestPause()) return false;
            if (_hitstop != null) _hitstop.SetPaused(true);
            if (_screens != null) _screens.ResetTo(ScreenId.PauseMenu);
            SlapLog.Info(LogChannel.Match, "Paused in " + _director.CurrentState);
            return true;
        }

        /// Close the overlay at once and carry on from the exact paused instant.
        public bool Resume()
        {
            if (_director == null || !_director.IsPaused) return false;
            if (_screens != null) _screens.Close(animate: false);
            if (_hitstop != null) _hitstop.SetPaused(false);
            _director.RequestResume();
            SlapLog.Info(LogChannel.Match, "Resumed in " + _director.CurrentState);
            return true;
        }

        /// PauseMenu's "Forfeit match": the opponent wins now (BoutEnd -> the Defeat card). Once a
        /// lethal slap has landed the result stands, so the forfeit is refused and play resumes.
        public bool Forfeit()
        {
            if (_director == null) return false;
            if (_screens != null) _screens.Close(animate: false);
            if (_hitstop != null) _hitstop.SetPaused(false);
            if (_director.Forfeit(FighterSlot.Player)) return true;
            _director.RequestResume();
            return false;
        }

        private void OnPauseRequested() => Pause();

        // The app going to the background (home button, a call) pauses a live bout.
        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnBack(InputAction.CallbackContext ctx)
        {
            if (_screens != null && _screens.Current == ScreenId.Replay && _screens.Replay.EndOfBout)
            {
                OnReplayFinished();
                return;
            }
            if (_screens != null && _screens.Current != ScreenId.None)
            {
                if (!_screens.Back()) CloseOverlay();
            }
            else Pause();
        }

        // ── Overlay links ────────────────────────────────────────────
        // A link that points into the match (ScreenIds.IsMatch).
        private void OnOverlayMatchLink(ScreenId to)
        {
            switch (to)
            {
                case ScreenId.Loss:     Forfeit(); break;                              // PauseMenu: Forfeit match
                case ScreenId.Knockout: if (!_screens.Back()) CloseOverlay(); break;   // Replay: Close
                default:                CloseOverlay(); break;                         // Resume, Round 2, ...
            }
        }

        // A link to a screen only the front end hosts (NavOp.Leave).
        private void OnOverlayLeave(ScreenId to)
        {
            if (to == ScreenId.MainMenu) { LeaveToMenu(); return; }  // RankUp: Collect all
            if (!_screens.Back()) CloseOverlay();                    // Settings' back arrow (-> Profile)
        }

        private void CloseOverlay()
        {
            if (IsPaused) Resume();
            else if (_screens != null) _screens.Close();
        }

        // ── D4 Instant Replay between the K.O. and the results (M09) ──
        private void OnReplayRequested()
        {
            if (_screens == null) { _hud.ShowResultScreen(); return; }
            _screens.Replay.EndOfBout = true;
            _screens.ResetTo(ScreenId.Replay);
        }

        private void OnReplayFinished()
        {
            if (_screens != null)
            {
                _screens.Replay.EndOfBout = false;
                _screens.Close();
            }
            if (_hud != null) _hud.ShowResultScreen();
        }

        // ── Results ──────────────────────────────────────────────────
        private void RememberResult(in MatchResult r)
        {
            _result = r;
            _hasResult = true;
        }

        /// Results' MAIN MENU after a win: E4 RankUp plays first, once. The division, points and
        /// rewards on it are the mockup's until M12 exists. False when the menu should open now.
        private bool TryShowRankUp()
        {
            if (_screens == null || _rankUpShown || !_hasResult || _result.Winner != FighterSlot.Player) return false;
            _rankUpShown = true;
            _screens.ResetTo(ScreenId.RankUp);
            return true;
        }
    }
}
