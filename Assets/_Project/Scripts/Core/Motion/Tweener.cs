using UnityEngine;
using SlapMania.Logging;

namespace SlapMania.Core.Motion
{
    public enum LoopType : byte { Restart, Yoyo }

    /// Receives float tweens for things Core cannot see — a uGUI Graphic's colour, an Image's
    /// fillAmount, a counted-up number. The view implements it; `channel` lets one sink host
    /// several tweens without closures.
    public interface ITweenSink { void OnTweenValue(int channel, float value); }

    /// Completion callback without closures: the owner implements it and switches on `id`.
    public interface ITweenCallback { void OnTweenComplete(int id); }

    /// A value-type reference to one tween. Stale handles (the tween finished, was killed or
    /// its slot was reused) are safe: every operation on them is a no-op.
    public readonly struct TweenHandle
    {
        private readonly Tweener _owner;
        private readonly int _slot;
        private readonly int _version;

        internal TweenHandle(Tweener owner, int slot, int version)
        {
            _owner = owner;
            _slot = slot;
            _version = version;
        }

        public bool IsActive => _owner != null && _owner.IsActive(_slot, _version);

        /// Total cycles; -1 loops forever. Yoyo plays every other cycle backwards.
        public TweenHandle SetLoops(int loops, LoopType type = LoopType.Restart)
        {
            _owner?.SetLoops(_slot, _version, loops, type);
            return this;
        }

        public TweenHandle OnComplete(ITweenCallback callback, int id = 0)
        {
            _owner?.SetCallback(_slot, _version, callback, id);
            return this;
        }

        public void Kill() => _owner?.Kill(_slot, _version);
        public void Complete() => _owner?.Complete(_slot, _version);
    }

    /// The shared in-house tween engine (CONVENTIONS §Time, ADR-0007 — no DOTween).
    ///
    /// Owned, not global: every view holds its own Tweener and calls Tick(Time.unscaledDeltaTime)
    /// from its Update, so there is no runner singleton and UI keeps moving through hitstop.
    /// Storage is a fixed array of structs sized at construction; starting, ticking and
    /// finishing tweens allocate nothing (R5). Pure: dt is a parameter, no Time.* read (R2).
    ///
    /// A new tween on the same target and property replaces the running one. When every slot
    /// is busy the new tween snaps straight to its end value (logged once) so a UI element is
    /// never stranded half-animated.
    public sealed class Tweener
    {
        private enum Kind : byte
        {
            LocalScale, LocalPosition, AnchoredPosition, SizeDelta, LocalEulerZ,
            Alpha, Float, Timer, ShakePosition, ShakeRotationZ, PunchScale
        }

        private struct Slot
        {
            public bool Active;
            public bool Fresh;
            public int Version;
            public Kind Kind;
            public Transform T;
            public RectTransform Rt;
            public CanvasGroup Cg;
            public ITweenSink Sink;
            public int Channel;
            public Vector3 From;   // shake/punch: the rest value
            public Vector3 To;     // shake/punch: the strength
            public float Duration;
            public float Delay;
            public float Elapsed;
            public Ease Ease;
            public int Loops;
            public int LoopsDone;
            public LoopType LoopType;
            public int Vibrato;
            public uint Seed;
            public ITweenCallback Callback;
            public int CallbackId;
        }

        private readonly Slot[] _slots;
        private bool _ticking;
        private bool _warnedFull;
        private uint _seedCounter = 0x9E3779B9u;

        public Tweener(int capacity = 16)
        {
            _slots = new Slot[capacity < 1 ? 1 : capacity];
        }

        public int Capacity => _slots.Length;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) n++;
                return n;
            }
        }

        // ── Builders ─────────────────────────────────────────────────
        public TweenHandle Scale(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.LocalScale, t, null, null, null, 0, from, to, duration, ease, delay, 0);

        public TweenHandle Scale(Transform t, float from, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Scale(t, new Vector3(from, from, from), new Vector3(to, to, to), duration, ease, delay);

        public TweenHandle LocalMove(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.LocalPosition, t, null, null, null, 0, from, to, duration, ease, delay, 0);

        public TweenHandle AnchorMove(RectTransform rt, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.AnchoredPosition, rt, rt, null, null, 0, from, to, duration, ease, delay, 0);

        public TweenHandle Size(RectTransform rt, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.SizeDelta, rt, rt, null, null, 0, from, to, duration, ease, delay, 0);

        public TweenHandle RotateZ(Transform t, float fromDeg, float toDeg, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.LocalEulerZ, t, null, null, null, 0, new Vector3(fromDeg, 0f, 0f), new Vector3(toDeg, 0f, 0f), duration, ease, delay, 0);

        public TweenHandle Fade(CanvasGroup cg, float from, float to, float duration, Ease ease = Ease.Linear, float delay = 0f)
            => Begin(Kind.Alpha, null, null, cg, null, 0, new Vector3(from, 0f, 0f), new Vector3(to, 0f, 0f), duration, ease, delay, 0);

        public TweenHandle Float(ITweenSink sink, int channel, float from, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
            => Begin(Kind.Float, null, null, null, sink, channel, new Vector3(from, 0f, 0f), new Vector3(to, 0f, 0f), duration, ease, delay, 0);

        /// Fires `callback.OnTweenComplete(id)` after `seconds` — sequencing without coroutines.
        public TweenHandle Delay(float seconds, ITweenCallback callback, int id)
        {
            var h = Begin(Kind.Timer, null, null, null, null, id, Vector3.zero, Vector3.zero, seconds, Ease.Linear, 0f, 0);
            return h.OnComplete(callback, id);
        }

        /// Decaying random jitter around the current localPosition, returning to it at the end.
        public TweenHandle Shake(Transform t, Vector3 strength, float duration, int vibrato = 12, float delay = 0f)
            => Begin(Kind.ShakePosition, t, null, null, null, 0, t != null ? t.localPosition : Vector3.zero, strength, duration, Ease.Linear, delay, vibrato);

        public TweenHandle ShakeRotation(Transform t, float degrees, float duration, int vibrato = 12, float delay = 0f)
            => Begin(Kind.ShakeRotationZ, t, null, null, null, 0, new Vector3(t != null ? t.localEulerAngles.z : 0f, 0f, 0f),
                     new Vector3(degrees, 0f, 0f), duration, Ease.Linear, delay, vibrato);

        /// Elastic scale kick around the current localScale, returning to it at the end.
        public TweenHandle Punch(Transform t, float amount, float duration, int vibrato = 6, float delay = 0f)
            => Begin(Kind.PunchScale, t, null, null, null, 0, t != null ? t.localScale : Vector3.one,
                     new Vector3(amount, amount, amount), duration, Ease.Linear, delay, vibrato);

        // ── Control ──────────────────────────────────────────────────
        /// Stops every tween on `target` where it is, without jumping to the end.
        public void Kill(Object target)
        {
            if ((object)target == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Active) continue;
                if ((object)s.T == target || (object)s.Rt == target || (object)s.Cg == target || ReferenceEquals(s.Sink, target))
                    Deactivate(ref s);
            }
        }

        public void KillAll()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active) Deactivate(ref _slots[i]);
        }

        /// Jumps every running tween to its end value and fires its callback.
        public void CompleteAll()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Active) Finish(i);
        }

        /// Advance every tween by `dt` seconds. Pass Time.unscaledDeltaTime for UI.
        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;
            _ticking = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Active) continue;
                if (s.Fresh) { s.Fresh = false; continue; } // started by a callback during this Tick
                if (TargetDestroyed(ref s)) { Deactivate(ref s); continue; }

                float step = dt;
                if (s.Delay > 0f)
                {
                    s.Delay -= step;
                    if (s.Delay > 0f) continue;
                    step = -s.Delay;
                    s.Delay = 0f;
                }

                s.Elapsed += step;
                if (s.Duration <= 0f || s.Elapsed >= s.Duration)
                {
                    bool loopAgain = s.Duration > 0f && (s.Loops < 0 || s.LoopsDone + 1 < s.Loops);
                    if (loopAgain)
                    {
                        s.LoopsDone++;
                        s.Elapsed -= s.Duration;
                        if (s.Elapsed > s.Duration) s.Elapsed = 0f; // a huge dt never runs more than one lap
                        if (s.LoopType == LoopType.Yoyo && !IsRestValueKind(s.Kind))
                        {
                            var tmp = s.From; s.From = s.To; s.To = tmp;
                        }
                        Apply(ref s, s.Elapsed / s.Duration);
                        continue;
                    }
                    Finish(i);
                    continue;
                }
                Apply(ref s, s.Elapsed / s.Duration);
            }
            _ticking = false;
            // Tweens started by callbacks this Tick run from the next one, whichever slot they took.
            for (int i = 0; i < _slots.Length; i++) _slots[i].Fresh = false;
        }

        // ── Handle plumbing ──────────────────────────────────────────
        internal bool IsActive(int slot, int version) =>
            slot >= 0 && slot < _slots.Length && _slots[slot].Active && _slots[slot].Version == version;

        internal void SetLoops(int slot, int version, int loops, LoopType type)
        {
            if (!IsActive(slot, version)) return;
            _slots[slot].Loops = loops == 0 ? 1 : loops;
            _slots[slot].LoopType = type;
        }

        internal void SetCallback(int slot, int version, ITweenCallback callback, int id)
        {
            if (!IsActive(slot, version)) return;
            _slots[slot].Callback = callback;
            _slots[slot].CallbackId = id;
        }

        internal void Kill(int slot, int version)
        {
            if (IsActive(slot, version)) Deactivate(ref _slots[slot]);
        }

        internal void Complete(int slot, int version)
        {
            if (IsActive(slot, version)) Finish(slot);
        }

        // ── Internals ────────────────────────────────────────────────
        private TweenHandle Begin(Kind kind, Transform t, RectTransform rt, CanvasGroup cg, ITweenSink sink, int channel,
                                  Vector3 from, Vector3 to, float duration, Ease ease, float delay, int vibrato)
        {
            if (float.IsNaN(duration) || duration < 0f) duration = 0f;
            if (float.IsNaN(delay) || delay < 0f) delay = 0f;

            if (kind != Kind.Timer)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    ref var o = ref _slots[i];
                    if (o.Active && o.Kind == kind && (object)o.T == t && (object)o.Rt == rt && (object)o.Cg == cg
                        && ReferenceEquals(o.Sink, sink) && o.Channel == channel)
                    {
                        // A shake/punch restarted mid-flight keeps the original rest pose;
                        // taking the current (displaced) value would make the target drift.
                        if (IsRestValueKind(kind)) from = o.From;
                        Deactivate(ref o);
                    }
                }
            }

            int free = -1;
            for (int i = 0; i < _slots.Length; i++) if (!_slots[i].Active) { free = i; break; }

            var probe = new Slot { Kind = kind, T = t, Rt = rt, Cg = cg, Sink = sink, Channel = channel, From = from, To = to };
            if (free < 0)
            {
                if (!_warnedFull)
                {
                    SlapLog.Warn(LogChannel.Presentation, "Tweener full (" + _slots.Length + " slots); snapping new tweens to their end value. Raise the capacity.");
                    _warnedFull = true;
                }
                Apply(ref probe, 1f);
                return default;
            }

            ref var s = ref _slots[free];
            int version = s.Version + 1;
            s = probe;
            s.Active = true;
            s.Fresh = _ticking;
            s.Version = version;
            s.Duration = duration;
            s.Delay = delay;
            s.Elapsed = 0f;
            s.Ease = ease;
            s.Loops = 1;
            s.LoopsDone = 0;
            s.LoopType = LoopType.Restart;
            s.Vibrato = vibrato < 1 ? 1 : vibrato;
            s.Seed = NextSeed();
            if (kind != Kind.Timer) Apply(ref s, 0f); // show the start value at once, even while delayed
            return new TweenHandle(this, free, version);
        }

        private void Finish(int index)
        {
            ref var s = ref _slots[index];
            Apply(ref s, 1f);
            var callback = s.Callback;
            int id = s.CallbackId;
            Deactivate(ref s);
            // After deactivating, so the callback may start a new tween in this very slot.
            callback?.OnTweenComplete(id);
        }

        private static void Deactivate(ref Slot s)
        {
            s.Active = false;
            s.Fresh = false;
            s.T = null;
            s.Rt = null;
            s.Cg = null;
            s.Sink = null;
            s.Callback = null;
        }

        private static bool IsRestValueKind(Kind k) =>
            k == Kind.ShakePosition || k == Kind.ShakeRotationZ || k == Kind.PunchScale;

        private static bool TargetDestroyed(ref Slot s)
        {
            switch (s.Kind)
            {
                case Kind.Timer: return false;
                case Kind.Alpha: return s.Cg == null;
                case Kind.Float: return s.Sink == null || (s.Sink is Object uo && uo == null);
                case Kind.AnchoredPosition:
                case Kind.SizeDelta: return s.Rt == null;
                default: return s.T == null;
            }
        }

        private static void Apply(ref Slot s, float t)
        {
            switch (s.Kind)
            {
                case Kind.LocalScale:
                    if (s.T != null) s.T.localScale = Vector3.LerpUnclamped(s.From, s.To, Easing.Evaluate(s.Ease, t));
                    break;
                case Kind.LocalPosition:
                    if (s.T != null) s.T.localPosition = Vector3.LerpUnclamped(s.From, s.To, Easing.Evaluate(s.Ease, t));
                    break;
                case Kind.AnchoredPosition:
                    if (s.Rt != null) s.Rt.anchoredPosition = Vector3.LerpUnclamped(s.From, s.To, Easing.Evaluate(s.Ease, t));
                    break;
                case Kind.SizeDelta:
                    if (s.Rt != null) s.Rt.sizeDelta = Vector3.LerpUnclamped(s.From, s.To, Easing.Evaluate(s.Ease, t));
                    break;
                case Kind.LocalEulerZ:
                    if (s.T != null)
                    {
                        var e = s.T.localEulerAngles;
                        e.z = Mathf.LerpUnclamped(s.From.x, s.To.x, Easing.Evaluate(s.Ease, t));
                        s.T.localEulerAngles = e;
                    }
                    break;
                case Kind.Alpha:
                    if (s.Cg != null) s.Cg.alpha = Mathf.LerpUnclamped(s.From.x, s.To.x, Easing.Evaluate(s.Ease, t));
                    break;
                case Kind.Float:
                    s.Sink?.OnTweenValue(s.Channel, Mathf.LerpUnclamped(s.From.x, s.To.x, Easing.Evaluate(s.Ease, t)));
                    break;
                case Kind.ShakePosition:
                    if (s.T != null)
                    {
                        float decay = 1f - Mathf.Clamp01(t);
                        float x = t * s.Vibrato;
                        s.T.localPosition = s.From + new Vector3(
                            Noise(s.Seed, x, 0) * s.To.x * decay,
                            Noise(s.Seed, x, 1) * s.To.y * decay,
                            Noise(s.Seed, x, 2) * s.To.z * decay);
                    }
                    break;
                case Kind.ShakeRotationZ:
                    if (s.T != null)
                    {
                        var e = s.T.localEulerAngles;
                        e.z = s.From.x + Noise(s.Seed, t * s.Vibrato, 0) * s.To.x * (1f - Mathf.Clamp01(t));
                        s.T.localEulerAngles = e;
                    }
                    break;
                case Kind.PunchScale:
                    if (s.T != null)
                    {
                        float c = Mathf.Clamp01(t);
                        float k = Mathf.Sin(c * Mathf.PI * s.Vibrato) * (1f - c);
                        s.T.localScale = s.From + s.To * k;
                    }
                    break;
            }
        }

        private uint NextSeed()
        {
            _seedCounter = _seedCounter * 1664525u + 1013904223u;
            return _seedCounter;
        }

        // Smooth value noise in [-1, 1]: deterministic per (seed, axis), no Random state.
        private static float Noise(uint seed, float x, int axis)
        {
            int k = (int)x;
            float f = x - k;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(seed, k, axis), Hash(seed, k + 1, axis), f);
        }

        private static float Hash(uint seed, int k, int axis)
        {
            uint h = seed ^ unchecked((uint)(k * 73856093)) ^ unchecked((uint)(axis * 19349663));
            h ^= h >> 16; h *= 0x7feb352du;
            h ^= h >> 15; h *= 0x846ca68bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f * 2f - 1f;
        }
    }
}
