using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core.Motion;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd
{
    /// One front-end screen: the UXML instance plus its behaviour (M10, ADR-0013).
    ///
    /// The base class gives every screen the kit's motion for free, driven by the classes the
    /// converter carried over from the mockup (`k-<css class>`):
    ///   * enter: the screen fades and slides in; its top-level blocks follow, staggered;
    ///   * `k-bar` meters fill from zero;
    ///   * `k-rays` / `k-raysr` / `k-speed` turn slowly; `k-live` dots blink;
    ///   * `nav-<Screen>` links navigate; `btn-<n>` buttons call OnButton.
    /// Subclasses add what is specific to one screen (a loader that loads, a search that finds).
    /// Wiring happens once at bind; callbacks carry their data as event arguments, so nothing
    /// captures a closure.
    public class ScreenView : ITweenCallback
    {
        private const int IdHidden = -100;

        public ScreenId Id { get; private set; }
        public VisualElement Root { get; private set; }
        public bool IsShown { get; private set; }

        protected ScreenManager Manager { get; private set; }
        protected UiMotionConfig Motion => Manager.Motion;
        protected readonly VisualTween Tw = new VisualTween(96);

        private readonly List<VisualElement> _blocks = new List<VisualElement>(24);
        private readonly List<VisualElement> _spinners = new List<VisualElement>(4);
        private readonly List<VisualElement> _liveDots = new List<VisualElement>(4);
        private readonly List<VisualElement> _meters = new List<VisualElement>(16);
        private readonly List<float> _meterTargets = new List<float>(16);

        internal void Bind(ScreenId id, VisualElement root, ScreenManager manager)
        {
            Id = id;
            Root = root;
            Manager = manager;

            root.Query<VisualElement>(className: "fe-link").ForEach(WireLink);
            root.Query<VisualElement>(className: "fe-btn").ForEach(WireButton);
            root.Query<VisualElement>(className: "fe-frame").ForEach(CollectBlocks);
            root.Query<VisualElement>(className: "k-rays").ForEach(_spinners.Add);
            root.Query<VisualElement>(className: "k-raysr").ForEach(_spinners.Add);
            root.Query<VisualElement>(className: "k-speed").ForEach(_spinners.Add);
            root.Query<VisualElement>(className: "k-live").ForEach(CollectLive);
            root.Query<VisualElement>(className: "k-bar").ForEach(CollectMeters);

            root.style.display = DisplayStyle.None;
            OnBind();
        }

        // ── Wiring ───────────────────────────────────────────────────
        private void WireLink(VisualElement link)
        {
            string n = link.name;
            if (n == null || !n.StartsWith("nav-")) return;
            if (!ScreenIds.TryParse(n.Substring(4), out var target))
            {
                SlapLog.Warn(LogChannel.UI, Id + ": link '" + n + "' names no screen");
                return;
            }
            link.RegisterCallback<ClickEvent, ScreenId>(OnLinkClicked, target);
            WirePress(link);
        }

        private void WireButton(VisualElement button)
        {
            if (string.IsNullOrEmpty(button.name)) return;
            button.RegisterCallback<ClickEvent, string>(OnButtonClicked, button.name);
            WirePress(button);
        }

        // Press squash, timed by UiMotion (buttonPressMs / buttonPressScale).
        private void WirePress(VisualElement el)
        {
            el.RegisterCallback<PointerDownEvent, VisualElement>(OnPressDown, el);
            el.RegisterCallback<PointerUpEvent, VisualElement>(OnPressUp, el);
            el.RegisterCallback<PointerLeaveEvent, VisualElement>(OnPressLeave, el);
        }

        private void OnPressDown(PointerDownEvent e, VisualElement el)
        {
            var m = Motion;
            if (m != null) Tw.To(el, VisualTween.Prop.Scale, 1f, m.buttonPressScale, m.buttonPressMs * 0.001f, Ease.OutQuad);
        }

        private void OnPressUp(PointerUpEvent e, VisualElement el) => Release(el);
        private void OnPressLeave(PointerLeaveEvent e, VisualElement el) => Release(el);

        private void Release(VisualElement el)
        {
            var m = Motion;
            if (m != null) Tw.To(el, VisualTween.Prop.Scale, m.buttonPressScale, 1f, m.buttonPressMs * 0.0015f, Ease.OutBack);
        }

        private void OnLinkClicked(ClickEvent e, ScreenId target)
        {
            if (!IsShown) return;
            e.StopPropagation();
            OnLink(target);
        }

        private void OnButtonClicked(ClickEvent e, string buttonName)
        {
            if (!IsShown) return;
            e.StopPropagation();
            OnButton(buttonName, (VisualElement)e.currentTarget);
        }

        /// Points an already-wired `nav-` link somewhere else, for a screen whose mockup button
        /// means something different once it has live data (Roster's UNLOCK becomes PLAY).
        protected void Relink(VisualElement link, ScreenId target)
        {
            if (link == null) return;
            link.UnregisterCallback<ClickEvent, ScreenId>(OnLinkClicked);
            link.RegisterCallback<ClickEvent, ScreenId>(OnLinkClicked, target);
        }

        /// A link was tapped. Default: navigate. Override to intercept (e.g. confirm first).
        protected virtual void OnLink(ScreenId target) => Manager.Navigate(target);

        /// A `btn-<n>` was tapped. Buttons whose feature has not landed yet (economy, social)
        /// acknowledge the tap so the screen never feels dead, and log what is missing.
        protected virtual void OnButton(string buttonName, VisualElement button)
        {
            SlapLog.Info(LogChannel.UI, Id + "." + buttonName + ": placeholder (feature not built yet)");
            Tw.To(button, VisualTween.Prop.TranslateX, -4f, 0f, 0.28f, Ease.OutElastic);
        }

        private void CollectBlocks(VisualElement frame)
        {
            foreach (var child in frame.Children()) _blocks.Add(child);
        }

        private void CollectLive(VisualElement badge)
        {
            if (badge.childCount > 0) _liveDots.Add(badge[0]);
        }

        private void CollectMeters(VisualElement bar)
        {
            foreach (var fill in bar.Children())
            {
                var w = fill.style.width;
                // Only meters whose UXML width is a percentage (Undefined = an inline value is set).
                if (w.keyword != StyleKeyword.Undefined || w.value.unit != LengthUnit.Percent) continue;
                _meters.Add(fill);
                _meterTargets.Add(w.value.value);
            }
        }

        // ── Show / hide ──────────────────────────────────────────────
        internal void Show(bool animate)
        {
            IsShown = true;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            var m = Motion;
            if (animate && m != null)
            {
                Tw.To(Root, VisualTween.Prop.Opacity, 0f, 1f, m.screenInMs * 0.001f, Ease.OutCubic);
                Tw.To(Root, VisualTween.Prop.TranslateY, m.screenSlidePx, 0f, m.screenInMs * 0.001f, Ease.OutCubic);
                int count = Mathf.Min(_blocks.Count, m.listStaggerMax);
                for (int i = 0; i < _blocks.Count; i++)
                {
                    float delay = Mathf.Min(i, count) * m.listStaggerMs * 0.001f;
                    Tw.To(_blocks[i], VisualTween.Prop.Opacity, 0f, 1f, m.screenInMs * 0.001f, Ease.OutCubic, delay);
                    Tw.To(_blocks[i], VisualTween.Prop.TranslateY, m.staggerSlidePx, 0f, m.screenInMs * 0.001f, Ease.OutCubic, delay);
                }
            }
            else
            {
                Tw.KillAll();
                Root.style.opacity = 1f;
            }
            StartAmbient(m);
            OnShow();
        }

        internal void Hide(bool animate)
        {
            if (!IsShown) return;
            IsShown = false;
            OnHide();
            var m = Motion;
            if (animate && m != null && m.screenOutMs > 0)
            {
                Tw.To(Root, VisualTween.Prop.Opacity, 1f, 0f, m.screenOutMs * 0.001f, Ease.InCubic, 0f, 1, false, this, IdHidden);
            }
            else
            {
                Tw.KillAll();
                Root.style.display = DisplayStyle.None;
            }
        }

        private void StartAmbient(UiMotionConfig m)
        {
            if (m == null) return;
            for (int i = 0; i < _spinners.Count; i++)
                Tw.To(_spinners[i], VisualTween.Prop.Rotate, 0f, 360f, m.raysSpinSeconds, Ease.Linear, 0f, -1);
            for (int i = 0; i < _liveDots.Count; i++)
                Tw.To(_liveDots[i], VisualTween.Prop.Opacity, 1f, 0.25f, m.livePulseMs * 0.0005f, Ease.InOutQuad, 0f, -1, true);
            for (int i = 0; i < _meters.Count; i++)
                Tw.To(_meters[i], VisualTween.Prop.WidthPercent, 0f, _meterTargets[i], m.countUpMs * 0.001f, Ease.OutCubic,
                      m.screenInMs * 0.001f);
        }

        internal void Tick(float dt)
        {
            Tw.Tick(dt);
            if (IsShown) OnTick(dt);
        }

        void ITweenCallback.OnTweenComplete(int id)
        {
            if (id == IdHidden)
            {
                if (!IsShown)
                {
                    Tw.KillAll();
                    Root.style.display = DisplayStyle.None;
                }
                return;
            }
            OnTween(id);
        }

        // ── Hooks ────────────────────────────────────────────────────
        protected virtual void OnBind() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnTick(float dt) { }
        protected virtual void OnTween(int id) { }

        /// The hook `elementName`: an element of that name, or - when the hook sits on a link
        /// or button, which keep their `nav-`/`btn-` names - the one with class
        /// `hook-<elementName>`. Logs when the UXML lost it (a re-converted mockup).
        protected T Find<T>(string elementName) where T : VisualElement
        {
            var e = Root.Q<T>(elementName) ?? Root.Q<T>(className: "hook-" + elementName);
            if (e == null) SlapLog.Warn(LogChannel.UI, Id + ": hook '" + elementName + "' missing from the UXML");
            return e;
        }
    }
}
