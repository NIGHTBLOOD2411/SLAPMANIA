using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SlapMania.Data;
using SlapMania.Logging;
using SlapMania.UI.FrontEnd;

namespace SlapMania.App
{
    /// Composition root of the FrontEnd scene (M10, ADR-0013): gives the ScreenManager its
    /// motion config and start screen from GameRoot (on GameConfig.frontEndPage's page: V1 opens
    /// at its splash and a bout returns to V1 Home), turns "start a bout" into a GameRoot
    /// scene swap (UI cannot reference App), and maps the device back button / Escape to Back.
    ///
    /// Runs before ScreenManager (execution order) so the start screen is set before the
    /// stack opens.
    [DefaultExecutionOrder(-100)]
    public sealed class FrontEndBridge : MonoBehaviour
    {
        [SerializeField] private ScreenManager _screens;
        [Tooltip("Used only when Play starts in this scene with no GameRoot.")]
        [SerializeField] private string _standaloneBoutScene = "Proto_Bout";

        private InputAction _back;

        private void Awake()
        {
            var root = GameRoot.Instance;
            if (_screens == null || root == null) return;
            if (root.Config != null && root.Config.uiMotion != null) _screens.Motion = root.Config.uiMotion;
            var page = root.Config != null ? root.Config.frontEndPage : FrontEndPage.V2;
            if (root.PendingFrontEndScreen != ScreenId.None)
            {
                _screens.StartScreen = ScreenIds.ForPage(root.PendingFrontEndScreen, page);
                root.PendingFrontEndScreen = ScreenId.None;
            }
            else
            {
                _screens.StartScreen = ScreenIds.ForPage(_screens.StartScreen, page);
            }
        }

        private void OnEnable()
        {
            if (_screens != null) _screens.MatchRequested += OnMatchRequested;
            // Android's back button arrives as Escape in the Input System.
            _back = new InputAction("FrontEndBack", InputActionType.Button);
            _back.AddBinding("<Keyboard>/escape");
            _back.AddBinding("<Gamepad>/buttonEast");
            _back.performed += OnBack;
            _back.Enable();
        }

        private void OnDisable()
        {
            if (_screens != null) _screens.MatchRequested -= OnMatchRequested;
            if (_back == null) return;
            _back.performed -= OnBack;
            _back.Dispose();
            _back = null;
        }

        private void OnBack(InputAction.CallbackContext ctx)
        {
            if (_screens != null) _screens.Back();
        }

        private void OnMatchRequested(ScreenId entry)
        {
            SlapLog.Info(LogChannel.UI, "Front end -> bout (via " + entry + ")");
            if (GameRoot.Instance != null) GameRoot.Instance.RequestMatch();
            else if (!string.IsNullOrEmpty(_standaloneBoutScene)) SceneManager.LoadScene(_standaloneBoutScene);
        }
    }
}
