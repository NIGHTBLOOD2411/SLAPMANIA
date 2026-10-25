using UnityEngine;
using UnityEngine.EventSystems;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// Tactile button feedback: squash on press, spring back with overshoot on release.
    /// Unscaled, so it still works while hitstop or a result screen slows time.
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Range(0.8f, 1f)] private float _pressed = 0.94f;
        private readonly Tweener _tw = new Tweener(2);

        public void OnPointerDown(PointerEventData e) => _tw.Scale(transform, transform.localScale.x, _pressed, 0.06f, Ease.OutQuad);
        public void OnPointerUp(PointerEventData e) => _tw.Scale(transform, transform.localScale.x, 1f, 0.22f, Ease.OutBack);

        private void Update() => _tw.Tick(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            _tw.KillAll();
            transform.localScale = Vector3.one;
        }
    }
}
