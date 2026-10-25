using UnityEngine;

namespace SlapMania.UI
{
    /// Constrains a RectTransform to Screen.safeArea so HUD content clears notches, punch-holes
    /// and the home indicator (M10 §4, DEBUG S-063). Full-bleed layers — shades, vignettes,
    /// letterbox bars — live outside it on purpose so they still reach the screen edge.
    ///
    /// Re-applies whenever the safe area or screen size changes (orientation lock, split
    /// screen, the Device Simulator). The per-frame check is a struct compare: no allocation.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _lastSafe;
        private Vector2Int _lastScreen;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _lastSafe || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y) Apply();
        }

        private void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            _lastSafe = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0 || safe.width <= 0f || safe.height <= 0f)
            {
                _rt.anchorMin = Vector2.zero; // M10 §8: zero rect -> fall back to full screen
                _rt.anchorMax = Vector2.one;
            }
            else
            {
                _rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                _rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            }
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
