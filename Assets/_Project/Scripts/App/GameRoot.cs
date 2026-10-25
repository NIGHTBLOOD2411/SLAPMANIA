using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using SlapMania.Data;
using SlapMania.Logging;
using SlapMania.UI.FrontEnd;

namespace SlapMania.App
{
    /// The one project singleton (M00). It loads the aggregate GameConfig once, applies the
    /// runtime player settings the whole game depends on, and swaps the content scenes
    /// additively (ARCHITECTURE §3: Boot is persistent, content scenes come and go):
    ///
    ///     Boot ─┬─ FrontEnd (menus, M10)  ── RequestMatch ──▶  bout
    ///           └─ bout ── RequestFrontEnd(screen) ──▶ FrontEnd
    ///
    /// With no front-end scene configured it boots straight into the bout, as before M10.
    /// Scene loads go through the Awaitable LoadScene/UnloadScene wrappers in M00 §3; the
    /// content scene becomes the active scene once loaded, so its lighting applies.
    ///
    /// This pass omits DeviceTierDetector, the BootComplete signal and the TransitionOverlay
    /// (see the M00 changelog); it is the minimum boot that makes the scenes run on device.
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public GameConfig Config { get; private set; }

        [SerializeField] private string _gameConfigResourcePath = "GameConfig";
        [SerializeField] private string _boutSceneName = "Bout_Arena01";
        [Tooltip("The menus. Empty boots straight into the bout (the pre-M10 prototype).")]
        [SerializeField] private string _frontEndSceneName = "";
        [SerializeField] private bool _loadBoutSceneOnBoot = true;
        [SerializeField, Min(15)] private int _targetFrameRate = 60;

        private bool _loadInFlight;

        /// The screen the front end opens on next. Title on first boot; the bout sets it
        /// when it hands back (results -> main menu). Read once by FrontEndBridge.
        public ScreenId PendingFrontEndScreen { get; set; } = ScreenId.None;

        public bool HasFrontEnd => !string.IsNullOrEmpty(_frontEndSceneName);

        /// The player's AI ladder level (M07 §4b): a tier index that a win moves up and a loss
        /// moves down. Session memory only until M12 saves it.
        public int AiLadderLevel { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Config = Resources.Load<GameConfig>(_gameConfigResourcePath);
            if (Config == null)
                SlapLog.Error(LogChannel.Boot, "GameConfig not found at Resources/" + _gameConfigResourcePath);
            else if (!Config.Validate(out string err))
                SlapLog.Error(LogChannel.Boot, "GameConfig invalid: " + err);

            ApplySettings();
        }

        private void Start()
        {
            if (!_loadBoutSceneOnBoot) return;
            if (HasFrontEnd) RunSwap(null, _frontEndSceneName);
            else if (!string.IsNullOrEmpty(_boutSceneName)) RunSwap(null, _boutSceneName);
        }

        /// Unload and re-add the bout scene (Rematch). Ignored while a load is already running.
        public void RequestBoutReload() => RunSwap(_boutSceneName, _boutSceneName);

        /// Leave the menus for a bout.
        public void RequestMatch()
        {
            if (string.IsNullOrEmpty(_boutSceneName)) return;
            RunSwap(HasFrontEnd ? _frontEndSceneName : null, _boutSceneName);
        }

        /// Leave the bout for the menus, opening on `screen`.
        public void RequestFrontEnd(ScreenId screen)
        {
            if (!HasFrontEnd) { RequestBoutReload(); return; }
            PendingFrontEndScreen = screen;
            RunSwap(_boutSceneName, _frontEndSceneName);
        }

        public async Awaitable LoadScene(string sceneName, LoadSceneMode mode)
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                SlapLog.Info(LogChannel.Boot, "Scene '" + sceneName + "' already loaded; skipping.");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SlapLog.Error(LogChannel.Boot, "Scene '" + sceneName + "' is not in Build Settings.");
                return;
            }
            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            if (op == null) { SlapLog.Error(LogChannel.Boot, "LoadSceneAsync returned null for '" + sceneName + "'."); return; }
            await Awaitable.FromAsyncOperation(op, destroyCancellationToken);
        }

        public async Awaitable UnloadScene(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) return;
            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null) return;
            await Awaitable.FromAsyncOperation(op, destroyCancellationToken);
        }

        // Fire-and-forget entry point: async void is confined to this one method, which owns the
        // exception handling so nothing is silently dropped.
        private async void RunSwap(string unload, string load)
        {
            if (_loadInFlight) return;
            _loadInFlight = true;
            try
            {
                if (!string.IsNullOrEmpty(unload)) await UnloadScene(unload);
                await LoadScene(load, LoadSceneMode.Additive);
                var scene = SceneManager.GetSceneByName(load);
                if (scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { SlapLog.Error(LogChannel.Boot, "Scene swap " + unload + " -> " + load + " failed: " + e); }
            finally { _loadInFlight = false; }
        }

        private void ApplySettings()
        {
            Application.targetFrameRate = _targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            InputSystem.pollingFrequency = 120; // M02: halves worst-case tap loss vs 60 Hz
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
