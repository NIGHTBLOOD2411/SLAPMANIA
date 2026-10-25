using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd
{
    /// Core.Motion's Tweener drives Transforms; this is its sibling for UI Toolkit elements.
    /// Same contract: fixed slot array, Tick(unscaled dt) from the owner, no closures, Ease
    /// curves from Core.Motion, completion through ITweenCallback ids.
    ///
    /// Every property animates *relative to the value the element had when the tween
    /// started*, so an element the converter already rotated, translated or faded keeps its
    /// mockup pose: opacity multiplies, translate adds, scale multiplies, rotate adds.
    public sealed class VisualTween
    {
        public enum Prop : byte { Opacity, TranslateX, TranslateY, Scale, Rotate, WidthPercent, Timer }

        private struct Slot
        {
            public bool Active;
            public VisualElement El;
            public Prop Prop;
            public float From, To, Duration, Delay, Elapsed;
            public Ease Ease;
            public int Loops;      // -1 forever
            public bool Yoyo;
            public int LoopsDone;
            public Vector3 Base;   // pose captured at start
            public ITweenCallback Callback;
            public int Id;
        }

        private readonly Slot[] _slots;

        public VisualTween(int capacity = 64) { _slots = new Slot[Mathf.Max(1, capacity)]; }

        public int ActiveCount
        {
            get { int n = 0; for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) n++; return n; }
        }

        public void To(VisualElement el, Prop prop, float from, float to, float seconds, Ease ease = Ease.OutCubic,
                       float delay = 0f, int loops = 1, bool yoyo = false, ITweenCallback callback = null, int id = 0)
        {
            if (el == null && prop != Prop.Timer) return;
            // Restarting a tween keeps the original rest pose: the element's resolved style
            // still holds the interrupted value until the next layout pass.
            Vector3 pose = default;
            bool restarted = false;
            if (el != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].Active || _slots[i].El != el || _slots[i].Prop != prop) continue;
                    pose = _slots[i].Base;
                    restarted = true;
                    _slots[i] = default;
                }
            }
            if (!restarted) pose = Capture(el, prop);
            int free = -1;
            for (int i = 0; i < _slots.Length; i++) if (!_slots[i].Active) { free = i; break; }
            var s = new Slot
            {
                Active = true, El = el, Prop = prop, From = from, To = to,
                Duration = Mathf.Max(0f, seconds), Delay = Mathf.Max(0f, delay), Ease = ease,
                Loops = loops == 0 ? 1 : loops, Yoyo = yoyo, Base = pose, Callback = callback, Id = id,
            };
            if (free < 0)
            {
                Apply(ref s, 1f); // full: snap to the end rather than strand the element mid-way
                callback?.OnTweenComplete(id);
                return;
            }
            _slots[free] = s;
            Apply(ref _slots[free], 0f);
        }

        public void Delay(float seconds, ITweenCallback callback, int id) =>
            To(null, Prop.Timer, 0f, 0f, seconds, Ease.Linear, 0f, 1, false, callback, id);

        public void Kill(VisualElement el, Prop prop)
        {
            if (el == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active && _slots[i].El == el && _slots[i].Prop == prop) Restore(ref _slots[i]);
        }

        public void Kill(VisualElement el)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active && _slots[i].El == el) Restore(ref _slots[i]);
        }

        public void KillAll()
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) Restore(ref _slots[i]);
        }

        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Active) continue;
                float step = dt;
                if (s.Delay > 0f)
                {
                    s.Delay -= step;
                    if (s.Delay > 0f) continue;
                    step = -s.Delay;
                    s.Delay = 0f;
                }
                s.Elapsed += step;
                if (s.Duration > 0f && s.Elapsed < s.Duration) { Apply(ref s, s.Elapsed / s.Duration); continue; }

                bool again = s.Duration > 0f && (s.Loops < 0 || s.LoopsDone + 1 < s.Loops);
                if (again)
                {
                    s.LoopsDone++;
                    s.Elapsed = Mathf.Repeat(s.Elapsed, s.Duration);
                    if (s.Yoyo) { float t = s.From; s.From = s.To; s.To = t; }
                    Apply(ref s, s.Elapsed / s.Duration);
                    continue;
                }
                Apply(ref s, 1f);
                var cb = s.Callback;
                int id = s.Id;
                s = default;
                cb?.OnTweenComplete(id);
            }
        }

        // ── Internals ────────────────────────────────────────────────
        /// The element's rest pose. Inline values (the UXML `style` the converter wrote) win
        /// over resolvedStyle, which is empty until an element has been laid out once.
        /// (UI Toolkit: an inline value that is set reports keyword Undefined; unset is Null.)
        private static Vector3 Capture(VisualElement el, Prop p)
        {
            if (el == null) return Vector3.zero;
            var s = el.style;
            switch (p)
            {
                case Prop.Opacity:
                    return new Vector3(s.opacity.keyword == StyleKeyword.Null ? el.resolvedStyle.opacity : s.opacity.value, 0f, 0f);
                case Prop.TranslateX:
                case Prop.TranslateY:
                    if (s.translate.keyword == StyleKeyword.Null) return el.resolvedStyle.translate;
                    return new Vector3(s.translate.value.x.value, s.translate.value.y.value, 0f);
                case Prop.Scale:
                {
                    var v = s.scale.keyword == StyleKeyword.Null ? el.resolvedStyle.scale.value : s.scale.value.value;
                    return v.x == 0f && v.y == 0f ? Vector3.one : v; // never captured as invisible
                }
                case Prop.Rotate:
                    return new Vector3(s.rotate.keyword == StyleKeyword.Null
                        ? el.resolvedStyle.rotate.angle.ToDegrees() : s.rotate.value.angle.ToDegrees(), 0f, 0f);
                default:
                    return Vector3.zero;
            }
        }

        private static void Apply(ref Slot s, float t)
        {
            float v = Mathf.LerpUnclamped(s.From, s.To, Easing.Evaluate(s.Ease, t));
            var el = s.El;
            if (el == null) return;
            switch (s.Prop)
            {
                case Prop.Opacity: el.style.opacity = s.Base.x * v; break;
                case Prop.TranslateX: el.style.translate = new Translate(s.Base.x + v, s.Base.y); break;
                case Prop.TranslateY: el.style.translate = new Translate(s.Base.x, s.Base.y + v); break;
                case Prop.Scale: el.style.scale = new Scale(new Vector3(s.Base.x * v, s.Base.y * v, 1f)); break;
                case Prop.Rotate: el.style.rotate = new Rotate(s.Base.x + v); break;
                case Prop.WidthPercent: el.style.width = Length.Percent(v); break;
            }
        }

        // A killed tween leaves its element at the captured pose, never half-way.
        private static void Restore(ref Slot s)
        {
            var el = s.El;
            if (el != null)
            {
                switch (s.Prop)
                {
                    case Prop.Opacity: el.style.opacity = s.Base.x; break;
                    case Prop.TranslateX:
                    case Prop.TranslateY: el.style.translate = new Translate(s.Base.x, s.Base.y); break;
                    case Prop.Scale: el.style.scale = new Scale(new Vector3(s.Base.x, s.Base.y, 1f)); break;
                    case Prop.Rotate: el.style.rotate = new Rotate(s.Base.x); break;
                    case Prop.WidthPercent: el.style.width = Length.Percent(s.To); break;
                }
            }
            s = default;
        }
    }
}
