using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd
{
    /// Makes the front end blend translucency the way the browser mockups do (M10, ADR-0013).
    ///
    /// The project renders in Linear colour space (required by the photo grade), so UI alpha
    /// blends in linear light: a 6 % white glass fill over near-black comes out at 70/255
    /// instead of the mockup's 21. UI Toolkit can blend in gamma, but only into a render
    /// target (PanelSettings.forceGammaRendering + targetTexture). So: the panel renders into
    /// a screen-sized linear RenderTexture, and a full-screen RawImage presents it through
    /// SlapMania/UI/GammaComposite, which decodes the gamma values once. One full-screen draw,
    /// no per-frame copy. Touches reach the panel through SetScreenToPanelSpaceFunction.
    ///
    /// Runs before ScreenManager so the screens are built on the final panel.
    ///
    /// In the bout (an overlay over the uGUI HUD) it presents only while its stack shows a
    /// screen: otherwise the composite would cost a full-screen blend every frame of Charge and
    /// Timing for nothing. The panel takes input in the order it draws: its sorting order is
    /// copied from the output canvas, so an overlay drawn over the HUD also wins its clicks.
    [DefaultExecutionOrder(-80)]
    public sealed class GammaPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [Tooltip("Full-screen RawImage on a Screen Space - Overlay canvas.")]
        [SerializeField] private RawImage _output;
        [SerializeField] private Material _composite;
        [Tooltip("Present only while this stack shows a screen (the bout overlay). Empty: always (the front end).")]
        [SerializeField] private ScreenManager _presentWhileOpen;
        // The template, remembered at the first OnEnable. OnDisable leaves the document with no
        // panel settings (S-077), so a later OnEnable in the same session - re-enabling, or the
        // domain reload of a mid-Play recompile - rebuilds from this (S-081). Serialized only so
        // a hot reload keeps it; it is set at runtime, never in edit mode.
        [SerializeField, HideInInspector] private PanelSettings _template;

        private PanelSettings _source;
        private PanelSettings _runtime;
        private RenderTexture _rt;
        private Vector2Int _size;
        private System.Func<Vector2, Vector2> _screenToPanel;
        private bool _presenting;

        public RenderTexture Target => _rt;
        public bool IsPresenting => _presenting;

        private void OnEnable()
        {
            var template = _document != null && _document.panelSettings != null ? _document.panelSettings : _template;
            if (_document == null || _output == null || _composite == null || template == null)
            {
                SlapLog.Error(LogChannel.UI, "GammaPresenter is not wired; the front end renders in linear blending.");
                enabled = false;
                return;
            }
            _template = template;
            _source = template;
            _runtime = Instantiate(_source);
            _runtime.name = _source.name + " (gamma)";
            _runtime.forceGammaRendering = true;
            // Only the render target is cleared. The template asset must not clear: a panel
            // made from it would wipe the whole screen every frame (DEBUG S-077).
            _runtime.clearColor = true;
            _screenToPanel = ScreenToPanel;
            _runtime.SetScreenToPanelSpaceFunction(_screenToPanel);
            var canvas = _output.GetComponentInParent<Canvas>(true);
            if (canvas != null) _runtime.sortingOrder = canvas.sortingOrder;
            Allocate();
            _document.panelSettings = _runtime;
            _output.material = _composite;
            _output.raycastTarget = false;
            _output.gameObject.SetActive(true);
            _presenting = true;
            if (_presentWhileOpen != null && _presentWhileOpen.Current == ScreenId.None) SetPresenting(false);
        }

        private void OnDisable()
        {
            if (_runtime == null) return;
            // Never hand the document back to the template here: this runs while the scene
            // unloads, and a panel created from the template outlives the scene (S-077).
            if (_document != null && _document.panelSettings == _runtime) _document.panelSettings = null;
            if (_output != null) _output.texture = null;
            Release();
            Destroy(_runtime);
            _runtime = null;
        }

        private void LateUpdate()
        {
            if (_runtime == null) return;
            if (Screen.width != _size.x || Screen.height != _size.y) Allocate();
            if (_presentWhileOpen != null)
            {
                bool open = _presentWhileOpen.Current != ScreenId.None;
                if (open != _presenting) SetPresenting(open);
            }
        }

        /// Hidden: no composite draw and no per-frame clear of the target. The target is
        /// cleared once on the way out so the next show never flashes the last frame.
        private void SetPresenting(bool on)
        {
            _presenting = on;
            _output.enabled = on;
            _runtime.clearColor = on;
            if (on || _rt == null) return;
            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void Allocate()
        {
            Release();
            _size = new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            // Linear (UNorm) so the gamma-space values are stored as written; 24-bit depth
            // brings the stencil UI Toolkit clips rounded corners and overflow with.
            _rt = new RenderTexture(_size.x, _size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "FrontEnd Panel (gamma)",
                useMipMap = false,
                antiAliasing = 1,
            };
            _rt.Create();
            _runtime.targetTexture = _rt;
            _output.texture = _rt;
        }

        private void Release()
        {
            if (_rt == null) return;
            if (_runtime != null) _runtime.targetTexture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }

        // Screen pixels -> panel texture pixels. UI Toolkit has already flipped the position to
        // a top-left origin before it calls this, and the texture is the screen 1:1, so nothing
        // changes; the panel's scale is applied afterwards. Flipping y here mirrored every
        // touch (DEBUG S-091).
        private Vector2 ScreenToPanel(Vector2 screen) => screen;
    }
}
