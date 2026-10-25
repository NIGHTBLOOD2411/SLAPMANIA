using System;
using System.Collections.Generic;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Presentation
{
    /// M16 CameraDirector (ADR-0020). Sits on the camera, which is a child of `CameraRig`, and
    /// owns the camera's final pose. It is built in layers every LateUpdate, after the rig moves:
    ///
    ///   base (the rig's pose, local identity) -> override (the recorder's free-fly)
    ///   -> playing sequence (blend in / out) -> trauma shake -> lens (FOV, DoF)
    ///
    /// The rig never learns that a sequence ran: when one ends the camera blends back to local
    /// identity, i.e. wherever the rig is now. Allocation-free per frame (R5): fixed arrays,
    /// `for` loops, no closures. Sequences evaluate from elapsed ms, never `pos += v * dt` (R8).
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraDirector : MonoBehaviour, ICameraTargets
    {
        [Serializable]
        public struct CueBinding
        {
            public CameraCue cue;
            public CameraSequence sequence;
        }

        [Serializable]
        public struct NamedTarget
        {
            public string name;
            public Transform transform;
        }

        [SerializeField] private CameraLens _lens;

        [Tooltip("Per game state: what the camera does when the state begins and how it gets there (M16). Edited live from the recorder's Rig tab.")]
        [SerializeField] private List<StateShot> _states = new List<StateShot>();

        [Tooltip("Recorded sequences played on match moments. Unbound cues leave the rig in charge.")]
        [SerializeField] private List<CueBinding> _cues = new List<CueBinding>();

        [Tooltip("Names a key's lookAt/follow can use. BoutController also binds attacker/defender each turn.")]
        [SerializeField] private List<NamedTarget> _targets = new List<NamedTarget>();

        [Tooltip("Trauma shake characters; AddTrauma picks one by index. 0 Light, 1 Impact, 2 Finisher, 3 Rumble.")]
        [SerializeField] private ShakeProfile[] _shakePresets =
        {
            new ShakeProfile("Light",    0.04f, new Vector3(0.6f, 0.6f, 1.0f), 18f, 3.0f),
            new ShakeProfile("Impact",   0.15f, new Vector3(2.0f, 2.0f, 3.0f), 25f, 3.0f),
            new ShakeProfile("Finisher", 0.28f, new Vector3(3.5f, 3.5f, 6.0f), 22f, 1.6f),
            new ShakeProfile("Rumble",   0.05f, new Vector3(0.8f, 0.8f, 0.4f),  6f, 0.8f),
        };

        [SerializeField] private Ease _blendEase = Ease.InOutCubic;

        [Header("Impact shake (every landed slap)")]
        [Tooltip("Trauma floor for any landed slap, so a weak hit still reads as contact. Shake goes with trauma squared.")]
        [SerializeField, Range(0f, 1f)] private float _impactMinTrauma = 0.35f;
        [Tooltip("Above 1 reaches full trauma on weaker slaps.")]
        [SerializeField, Range(0f, 3f)] private float _impactScale = 1f;
        [SerializeField] private int _impactPreset = 1;
        [SerializeField] private int _lethalPreset = 2;

        private Camera _cam;
        private float _restFov;
        private Vector3 _homePosition;
        private Quaternion _homeRotation = Quaternion.identity;

        private CameraSequenceData _seq;
        private bool _playing;
        private bool _paused;
        private float _speed = 1f;
        private double _elapsedMs;
        private double _lastMs;
        private double _sinceStartMs;

        private bool _blendingOut;
        private double _blendOutElapsedMs;
        private double _blendOutMs;
        private Ease _blendOutEase = Ease.InOutCubic;
        private CameraPose _blendFrom;
        private CameraPose _lastPose;

        private bool _override;
        private CameraPose _overridePose;

        private FighterSlot _attacker = FighterSlot.Player;
        private float _trauma;
        private float _traumaFloor;
        private int _preset = 1;
        private double _shakeClock;

        // ── State ────────────────────────────────────────────────────
        public bool IsPlaying => _playing;
        public bool IsBlendingOut => _blendingOut;
        public CameraSequenceData Current => _seq;
        public double ElapsedMs => _elapsedMs;
        public float Trauma => _trauma;
        public bool HasOverride => _override;
        /// The pose the camera showed last frame, before shake.
        public CameraPose LastPose => _lastPose;
        /// The rig's resting FOV (the base layer). Setting it at runtime changes the gameplay FOV.
        public float RestFov
        {
            get => _restFov;
            set => _restFov = float.IsNaN(value) ? _restFov : Mathf.Clamp(value, CameraPose.MinFov, CameraPose.MaxFov);
        }

        public bool Paused
        {
            get => _paused;
            set => _paused = value;
        }

        /// 0..4; 1 is real time. Applies to the playing sequence only, never to the blend in/out.
        public float PlaybackSpeed
        {
            get => _speed;
            set => _speed = float.IsNaN(value) ? 1f : Mathf.Clamp(value, 0f, 4f);
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _restFov = _cam.fieldOfView;
            if (_lens == null) _lens = GetComponent<CameraLens>();
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _lastPose = BasePose();
        }

        // ── Sequences ────────────────────────────────────────────────
        /// The game state `cue` began: run its state shot, else its bound sequence. False when
        /// the state changes nothing.
        public bool Cue(CameraCue cue)
        {
            var st = FindState(cue);
            if (st != null)
            {
                switch (st.mode)
                {
                    case StateCameraMode.Gameplay:
                        ReturnToGameplay(st.blendMs, st.blendEase);
                        return true;
                    case StateCameraMode.Shot:
                        Play(st.AsSequence(_attacker));
                        return true;
                    case StateCameraMode.Sequence:
                        if (PlayBound(cue)) return true;
                        SlapLog.Warn(LogChannel.Presentation, "Camera state " + cue + " is set to Sequence but has no bound CameraSequence");
                        return false;
                }
            }
            return PlayBound(cue);
        }

        private bool PlayBound(CameraCue cue)
        {
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i].cue != cue || _cues[i].sequence == null) continue;
                Play(_cues[i].sequence.data);
                SlapLog.Info(LogChannel.Presentation, "Camera cue " + cue + " -> " + _cues[i].sequence.name);
                return true;
            }
            return false;
        }

        public bool HasCue(CameraCue cue)
        {
            for (int i = 0; i < _cues.Count; i++)
                if (_cues[i].cue == cue && _cues[i].sequence != null) return true;
            return false;
        }

        public void Play(CameraSequence sequence) => Play(sequence != null ? sequence.data : null);

        /// Starts `data` at `startMs`, blending in from wherever the camera is now. A key at
        /// `startMs` fires its shake when starting from 0; a scrubbed start does not.
        public void Play(CameraSequenceData data, double startMs = 0)
        {
            if (data == null || data.keys == null || data.keys.Count == 0)
            {
                SlapLog.Warn(LogChannel.Presentation, "CameraDirector.Play: empty sequence ignored");
                return;
            }
            _seq = data;
            _blendFrom = _lastPose;
            _playing = true;
            _paused = false;
            _blendingOut = false;
            _sinceStartMs = 0;
            _elapsedMs = Clamp(startMs, 0, _seq.DurationMs);
            _lastMs = _elapsedMs <= 0 ? double.NegativeInfinity : _elapsedMs;
        }

        /// Ends the sequence, blending back to the rig unless `immediate`.
        public void Stop(bool immediate = false)
        {
            if (!_playing) { if (immediate) _blendingOut = false; return; }
            if (immediate) { _playing = false; _blendingOut = false; return; }
            ReturnToGameplay(_seq != null ? _seq.blendOutMs : 0, _seq != null ? _seq.blendOutEase : _blendEase);
        }

        /// Blends from wherever the camera is back to the rig over `ms` with `ease`. A no-op
        /// when the camera is already the rig's.
        public void ReturnToGameplay(double ms, Ease ease)
        {
            bool away = _playing || _blendingOut;
            _playing = false;
            if (!away || double.IsNaN(ms) || ms <= 0) { _blendingOut = false; return; }
            _blendFrom = _lastPose;
            _blendOutMs = ms;
            _blendOutEase = ease;
            _blendOutElapsedMs = 0;
            _blendingOut = true;
        }

        /// Moves the playhead without firing key shakes (the recorder's scrubber).
        public void Seek(double ms)
        {
            if (_seq == null) return;
            _elapsedMs = Clamp(ms, 0, _seq.DurationMs);
            _lastMs = _elapsedMs;
            _sinceStartMs = double.MaxValue; // a scrub is not a fresh start: no blend-in
        }

        // ── Override (recorder free-fly) ─────────────────────────────
        public void SetOverride(in CameraPose pose)
        {
            _override = true;
            _overridePose = pose;
        }

        public void ClearOverride()
        {
            if (!_override) return;
            _override = false;
            // Hand back smoothly rather than snapping to the rig.
            _blendFrom = _lastPose;
            _blendOutMs = 300;
            _blendOutEase = _blendEase;
            _blendOutElapsedMs = 0;
            _blendingOut = !_playing;
        }

        // ── Shake ────────────────────────────────────────────────────
        public int ShakePresetCount => _shakePresets != null ? _shakePresets.Length : 0;
        public string ShakePresetName(int i) => i >= 0 && i < ShakePresetCount ? _shakePresets[i].name : "-";

        /// Whose attacking turn it is: picks each two-sided state shot's camera.
        /// BoutController sets it on TurnStart, before the TurnStart cue.
        public FighterSlot Attacker
        {
            get => _attacker;
            set => _attacker = value;
        }

        public float ImpactMinTrauma { get => _impactMinTrauma; set => _impactMinTrauma = Mathf.Clamp01(value); }
        public float ImpactScale { get => _impactScale; set => _impactScale = Mathf.Max(0f, value); }
        public int ImpactPreset { get => _impactPreset; set => _impactPreset = value; }
        public int LethalPreset { get => _lethalPreset; set => _lethalPreset = value; }

        /// A slap landed: shake from its damage-scaled `intensity` (0..1). Lethal slaps get
        /// full trauma on the lethal preset.
        public void Impact(float intensity, bool lethal)
        {
            if (lethal) { AddTrauma(1f, _lethalPreset); return; }
            AddTrauma(CameraShakeMath.ImpactTrauma(intensity, _impactMinTrauma, _impactScale), _impactPreset);
        }

        /// `amount` 0..1 of trauma on top of the current; `preset` picks the shake character.
        public void AddTrauma(float amount, int preset = 1)
        {
            if (ShakePresetCount == 0) return;
            _preset = Mathf.Clamp(preset, 0, ShakePresetCount - 1);
            _trauma = CameraShakeMath.Add(_trauma, amount);
        }

        /// A trauma floor that never decays (crowd rumble, a finisher build-up). 0 clears it.
        public void SetRumble(float floor, int preset = 3)
        {
            _traumaFloor = Mathf.Clamp01(float.IsNaN(floor) ? 0f : floor);
            if (_traumaFloor > 0f) AddTrauma(_traumaFloor, preset);
        }

        // ── Targets ──────────────────────────────────────────────────
        public void SetTarget(string targetName, Transform t)
        {
            if (string.IsNullOrEmpty(targetName)) return;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (!string.Equals(_targets[i].name, targetName, StringComparison.Ordinal)) continue;
                _targets[i] = new NamedTarget { name = targetName, transform = t };
                return;
            }
            _targets.Add(new NamedTarget { name = targetName, transform = t });
        }

        public int TargetCount => _targets.Count;
        public string TargetName(int i) => i >= 0 && i < _targets.Count ? _targets[i].name : "";

        public bool TryGetPosition(string targetName, out Vector3 position)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t.transform == null || !string.Equals(t.name, targetName, StringComparison.Ordinal)) continue;
                position = t.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        public bool TryGetRotation(string targetName, out Quaternion rotation)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t.transform == null || !string.Equals(t.name, targetName, StringComparison.Ordinal)) continue;
                rotation = t.transform.rotation;
                return true;
            }
            rotation = Quaternion.identity;
            return false;
        }

        // ── State shots & presets (Rig tab, CameraRigSetup) ──────────
        public StateShot FindState(CameraCue cue)
        {
            for (int i = 0; i < _states.Count; i++)
                if (_states[i] != null && _states[i].cue == cue) return _states[i];
            return null;
        }

        /// The state's entry, created (mode Keep) when missing.
        public StateShot EnsureState(CameraCue cue)
        {
            var st = FindState(cue);
            if (st != null) return st;
            st = new StateShot { cue = cue };
            _states.Add(st);
            return st;
        }

        public StateShot[] CopyStateShots()
        {
            var a = new StateShot[_states.Count];
            for (int i = 0; i < a.Length; i++) a[i] = _states[i]?.Clone();
            return a;
        }

        public void SetStateShots(StateShot[] states)
        {
            _states.Clear();
            if (states == null) return;
            for (int i = 0; i < states.Length; i++) if (states[i] != null) _states.Add(states[i].Clone());
        }

        public ShakeProfile GetShakePreset(int i) => _shakePresets[i];
        public void SetShakePreset(int i, ShakeProfile p) { if (i >= 0 && i < ShakePresetCount) _shakePresets[i] = p; }
        public ShakeProfile[] CopyShakePresets() => _shakePresets != null ? (ShakeProfile[])_shakePresets.Clone() : Array.Empty<ShakeProfile>();
        public void SetShakePresets(ShakeProfile[] presets)
        {
            if (presets == null || presets.Length == 0) return;
            _shakePresets = (ShakeProfile[])presets.Clone();
            if (_preset >= _shakePresets.Length) _preset = 0;
        }

        public int CueCount => _cues.Count;
        public CueBinding CueAt(int i) => _cues[i];

        // ── Frame ────────────────────────────────────────────────────
        /// Where the rig wants the camera: local identity under the rig, lens at rest.
        public CameraPose BasePose()
        {
            var parent = transform.parent;
            var p = parent != null
                ? new CameraPose(parent.position, parent.rotation, _restFov)
                : new CameraPose(_homePosition, _homeRotation, _restFov);
            return p;
        }

        private void LateUpdate()
        {
            float udt = Time.unscaledDeltaTime;
            CameraPose pose;

            if (_override)
            {
                pose = _overridePose.Sanitized();
                if (_playing) AdvanceSequence(udt, out _); // keep the clock and the key shakes honest
            }
            else if (_playing)
            {
                AdvanceSequence(udt, out pose);
            }
            else if (_blendingOut)
            {
                _blendOutElapsedMs += udt * 1000.0;
                float k = Easing.Evaluate(_blendOutEase, (float)(_blendOutElapsedMs / _blendOutMs));
                pose = CameraTrack.Blend(in _blendFrom, BasePose(), k);
                if (k >= 1f) _blendingOut = false;
            }
            else
            {
                pose = BasePose();
            }
            _lastPose = pose;

            ApplyShake(udt, ref pose);
            _cam.fieldOfView = pose.fov;
            if (_lens != null) _lens.Apply(in pose);
        }

        private void AdvanceSequence(float udt, out CameraPose pose)
        {
            float dt = _seq.unscaledTime ? udt : Time.deltaTime;
            if (!_paused) _elapsedMs += dt * 1000.0 * _speed;
            _sinceStartMs += udt * 1000.0;

            double dur = _seq.DurationMs;
            bool finished = false;
            if (_elapsedMs >= dur)
            {
                if (_seq.loop && dur > 0)
                {
                    FireKeys(_lastMs, dur);
                    _elapsedMs %= dur;
                    _lastMs = double.NegativeInfinity;
                }
                else
                {
                    _elapsedMs = dur;
                    finished = !_paused && !_seq.holdAtEnd;
                }
            }
            FireKeys(_lastMs, _elapsedMs);
            _lastMs = _elapsedMs;

            CameraTrack.TryEvaluate(_seq.keys, _elapsedMs, this, out pose);
            if (_seq.blendInMs > 0 && _sinceStartMs < _seq.blendInMs)
            {
                float k = Easing.Evaluate(_seq.blendInEase, (float)(_sinceStartMs / _seq.blendInMs));
                pose = CameraTrack.Blend(in _blendFrom, in pose, k);
            }

            if (finished)
            {
                _lastPose = pose;
                Stop();
            }
        }

        private void FireKeys(double fromMs, double toMs)
        {
            int n = CameraTrack.KeysReached(_seq.keys, fromMs, toMs, out int first);
            for (int i = first; i < first + n; i++)
            {
                var k = _seq.keys[i];
                if (k.shake > 0f) AddTrauma(k.shake, k.shakePreset);
            }
        }

        private void ApplyShake(float udt, ref CameraPose pose)
        {
            Vector3 offset = Vector3.zero, angles = Vector3.zero;
            if (ShakePresetCount > 0)
            {
                ref readonly var profile = ref _shakePresets[_preset < ShakePresetCount ? _preset : 0];
                _trauma = CameraShakeMath.Decay(_trauma, profile.decayPerSecond, udt, _traumaFloor);
                _shakeClock += udt;
                CameraShakeMath.Sample(in profile, _trauma, _shakeClock, out offset, out angles);
            }
            var rot = pose.rotation * Quaternion.Euler(angles);
            // Offset in camera space, so "up" jitter is screen-up whatever the shot.
            transform.SetPositionAndRotation(pose.position + rot * offset, rot);
        }

        private static double Clamp(double v, double lo, double hi) =>
            double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;
    }
}
