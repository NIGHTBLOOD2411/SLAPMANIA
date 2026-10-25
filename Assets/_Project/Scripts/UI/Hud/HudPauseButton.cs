using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// A HUD pause control (M10): the scorebug's, top-left on C4/C5/D2, and the charge
    /// screen's, in the top letterbox of C6. It never pauses anything itself - V2Hud forwards
    /// Pressed to the composition root - and it says whether a press lands on it, so that press
    /// is a click, not a slap tap (V2Hud.BlocksTap, the player tap source's Blocker, M02).
    [RequireComponent(typeof(Button))]
    public sealed class HudPauseButton : MonoBehaviour
    {
        public event Action Pressed;

        [SerializeField] private Button _button;
        [Tooltip("The screen it sits on: the button only counts while that screen is shown.")]
        [SerializeField] private HudView _owner;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        private void OnClick() => Pressed?.Invoke();

        /// True when a press at `screen` (pixels; Screen Space - Overlay, so no camera) lands
        /// on this button while its screen is shown.
        public bool Covers(Vector2 screen)
        {
            if (_owner == null || !_owner.IsShown || !isActiveAndEnabled) return false;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, screen, null);
        }
    }
}
