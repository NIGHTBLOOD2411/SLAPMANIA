using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd
{
    /// The front-end screen stack (M10 §3, ADR-0013). Owns one UIDocument; each screen is a
    /// UXML instance created on first use and kept, hidden, afterwards.
    ///
    /// Not a singleton: the FrontEnd scene holds one and the composition root (App) talks to
    /// it through the events below. It never loads scenes - starting a bout is the App's job,
    /// so a link into the match raises MatchRequested instead.
    ///
    /// The bout scene holds a second one as an overlay over the uGUI HUD (D5 PauseMenu, D4
    /// Replay, E1 RoundEnd, E4 RankUp, Settings). It opens empty (initial None), and Close()
    /// empties it again; links to screens it does not host raise LeaveRequested.
    [DefaultExecutionOrder(-50)]
    public sealed class ScreenManager : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public ScreenId id;
            public VisualTreeAsset tree;
        }

        [SerializeField] private UIDocument _document;
        [SerializeField] private Entry[] _screens = Array.Empty<Entry>();
        [SerializeField] private ScreenId _initial = ScreenId.Title;
        [Tooltip("Fallback when no GameRoot supplies GameConfig.uiMotion (Play pressed in this scene).")]
        [SerializeField] private UiMotionConfig _motion;

        /// A link into the bout (Ready, Aim, ...). The argument is the screen it pointed at.
        public event Action<ScreenId> MatchRequested;
        /// A link to a screen this stack does not host (NavOp.Leave): another scene's job.
        public event Action<ScreenId> LeaveRequested;
        public event Action<ScreenId> ScreenShown;
        /// D4 Instant Replay was dismissed (Close or Next) while ReplayShot.EndOfBout: the bout
        /// moves on to its result card (M09/M10).
        public event Action ReplayFinished;

        /// The bout overlay's D4 data, filled by the composition root (the front end leaves it
        /// empty, so D4 there shows the mockup still).
        public ReplayShot Replay { get; } = new ReplayShot();

        private readonly List<ScreenId> _stack = new List<ScreenId>(8);
        private readonly Dictionary<ScreenId, ScreenView> _views = new Dictionary<ScreenId, ScreenView>(24);
        // Ticked by index: a view's tick can navigate, which creates the next view mid-loop
        // (Loader -> MainMenu). Appending to a List is safe there; enumerating the dictionary
        // is not (DEBUG S-078).
        private readonly List<ScreenView> _tickOrder = new List<ScreenView>(24);
        private VisualElement _layer;

        public UiMotionConfig Motion { get => _motion; set => _motion = value; }
        public ScreenId Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : ScreenId.None;
        public int Depth => _stack.Count;

        /// Set before OnEnable runs (e.g. by the App returning from a bout) to open somewhere
        /// other than the serialized initial screen.
        public ScreenId StartScreen { get => _initial; set => _initial = value; }

        private void OnEnable()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                SlapLog.Error(LogChannel.UI, "ScreenManager: no UIDocument root. Disabling.");
                enabled = false;
                return;
            }
            _layer = new VisualElement { name = "fe-layer", pickingMode = PickingMode.Ignore };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0; _layer.style.top = 0; _layer.style.right = 0; _layer.style.bottom = 0;
            root.Add(_layer);
            if (_initial != ScreenId.None) ResetTo(_initial, animate: false);
        }

        private void OnDisable()
        {
            _views.Clear();
            _tickOrder.Clear();
            _stack.Clear();
            _layer?.RemoveFromHierarchy();
            _layer = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _tickOrder.Count; i++) _tickOrder[i].Tick(dt);
        }

        // ── Navigation ───────────────────────────────────────────────
        /// Go to `to` the way the stack rules say (ResolveNav).
        public void Navigate(ScreenId to)
        {
            var op = ResolveNav(Current, to);
            switch (op)
            {
                case NavOp.Push:    Push(to); break;
                case NavOp.Replace: Replace(to); break;
                case NavOp.PopTo:   PopTo(to); break;
                case NavOp.ResetTo: ResetTo(to); break;
                case NavOp.Match:   MatchRequested?.Invoke(to); break;
                case NavOp.Leave:
                    if (LeaveRequested != null) LeaveRequested(to);
                    else SlapLog.Warn(LogChannel.UI, "ScreenManager: no UXML registered for " + to);
                    break;
            }
        }

        /// How a link from `from` to `to` should move the stack.
        ///
        /// The mockups only say *where* a link goes (Search's Cancel -> MainMenu, Settings'
        /// back arrow -> Profile, every tab bar -> its six tabs); this decides what Back does
        /// afterwards. It reads ScreenIds.IsMatch / IsTab and `_stack`.
        public NavOp ResolveNav(ScreenId from, ScreenId to)
        {
            if (to == ScreenId.None || to == from) return NavOp.Ignore;
            if (ScreenIds.IsMatch(to)) return NavOp.Match;
            if (!Hosts(to)) return NavOp.Leave;
            // TODO(human): decide the stack rule for every other link.
            return NavOp.Push;
        }

        public void Push(ScreenId id, bool animate = true)
        {
            var next = View(id);
            if (next == null) return;
            var top = TopView();
            _stack.Add(id);
            top?.Hide(animate);
            next.Show(animate);
            Shown(id);
        }

        public void Replace(ScreenId id, bool animate = true)
        {
            if (_stack.Count == 0) { Push(id, animate); return; }
            var next = View(id);
            if (next == null) return;
            var top = TopView();
            _stack[_stack.Count - 1] = id;
            if (top != next) top?.Hide(animate);
            next.Show(animate);
            Shown(id);
        }

        /// Pop to `id` if it is in the stack; push it otherwise.
        public void PopTo(ScreenId id, bool animate = true)
        {
            int at = _stack.LastIndexOf(id);
            if (at < 0) { Push(id, animate); return; }
            var top = TopView();
            _stack.RemoveRange(at + 1, _stack.Count - at - 1);
            var next = View(id);
            if (top != next) top?.Hide(animate);
            next?.Show(animate);
            Shown(id);
        }

        public void ResetTo(ScreenId id, bool animate = true)
        {
            var next = View(id);
            if (next == null) return;
            var top = TopView();
            _stack.Clear();
            _stack.Add(id);
            if (top != next) top?.Hide(animate);
            next.Show(animate);
            Shown(id);
        }

        /// Hide the top screen and empty the stack (Current becomes None). For an overlay that
        /// is not always up - the bout's pause menu on Resume.
        public void Close(bool animate = true)
        {
            if (_stack.Count == 0) return;
            TopView()?.Hide(animate);
            _stack.Clear();
            Shown(ScreenId.None);
        }

        internal void FinishReplay() => ReplayFinished?.Invoke();

        /// Whether this stack has a UXML for `id` (the serialized table).
        public bool Hosts(ScreenId id)
        {
            for (int i = 0; i < _screens.Length; i++)
                if (_screens[i].id == id && _screens[i].tree != null) return true;
            return false;
        }

        /// The device back button / Escape. Returns false at the bottom of the stack, so the
        /// caller can decide (quit prompt, ignore).
        public bool Back()
        {
            if (_stack.Count <= 1) return false;
            PopTo(_stack[_stack.Count - 2]);
            return true;
        }

        // ── Views ────────────────────────────────────────────────────
        private ScreenView TopView() => _stack.Count > 0 && _views.TryGetValue(Current, out var v) ? v : null;

        private ScreenView View(ScreenId id)
        {
            if (_views.TryGetValue(id, out var existing)) return existing;
            VisualTreeAsset tree = null;
            for (int i = 0; i < _screens.Length; i++)
                if (_screens[i].id == id) { tree = _screens[i].tree; break; }
            if (tree == null)
            {
                SlapLog.Warn(LogChannel.UI, "ScreenManager: no UXML registered for " + id);
                return null;
            }
            var container = tree.Instantiate();
            container.name = "screen-container-" + id;
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.left = 0; container.style.top = 0; container.style.right = 0; container.style.bottom = 0;
            _layer.Add(container);
            var view = ScreenViews.Create(id);
            view.Bind(id, container, this);
            _views[id] = view;
            _tickOrder.Add(view);
            return view;
        }

        private void Shown(ScreenId id)
        {
            SlapLog.Info(LogChannel.UI, "Screen " + id + " (depth " + _stack.Count + ")");
            ScreenShown?.Invoke(id);
        }
    }
}
