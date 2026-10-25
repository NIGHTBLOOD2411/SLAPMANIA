using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;
using SlapMania.Logging;
using SlapMania.Presentation;

namespace SlapMania.Diagnostics
{
    /// The runtime camera recorder (M16, ADR-0020): fly the camera in the running game, drop
    /// keys, pick each key's transition, preview, and save the move as JSON for
    /// `Tools > Slap Mania > Camera > Import Recorded Sequences`.
    ///
    /// Debug-only by assembly (R4): this file is not compiled without SLAPMANIA_DEBUG. It
    /// attaches itself to the scene's CameraDirector, so no scene wiring is needed. IMGUI
    /// allocates; that is accepted here because none of it ships.
    ///
    /// Controls: F8 opens the panel; F9 toggles fly. Fly uses WASD to move, Q/E for down/up,
    /// right mouse to look, Z/C to roll, the wheel for FOV, Shift for fast and Ctrl for slow.
    /// On touch, drag the left third to move and the rest to look.
    public sealed partial class CameraRecorder : MonoBehaviour
    {
        public const string FolderName = "CameraSequences";
        public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        private static readonly Ease[] Eases = (Ease[])Enum.GetValues(typeof(Ease));
        private static readonly string[] PathNames = { "Cut", "Linear", "Spline" };
        private static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] SpeedNames = { "0.25x", "0.5x", "1x", "2x" };
        private static bool _installed;

        private CameraDirector _director;
        private CameraSequenceData _data = NewSequence();
        private int _sel = -1;

        private bool _open;
        private bool _fly;
        private CameraPose _flyPose;
        private float _yaw, _pitch, _roll;
        private float _moveSpeed = 2.5f;

        private double _playhead;
        private int _speedIndex = 2;
        private bool _showLoad;
        private string[] _files = Array.Empty<string>();
        private string _status = "";
        private Vector2 _scroll;
        private Rect _window = new Rect(8, 8, 400, 680);
        private float _scale = 1f;

        // ── Install ──────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _installed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!_installed)
            {
                _installed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            Attach();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Attach();

        private static void Attach()
        {
            var director = FindAnyObjectByType<CameraDirector>();
            if (director == null || director.GetComponent<CameraRecorder>() != null) return;
            director.gameObject.AddComponent<CameraRecorder>();
        }

        private void Awake()
        {
            _director = GetComponent<CameraDirector>();
            _rig = GetComponentInParent<CameraRig>();
            _cam = GetComponent<Camera>();
        }

        private void OnDisable()
        {
            if (_director != null && _fly) _director.ClearOverride();
            _fly = false;
        }

        private static CameraSequenceData NewSequence() => new CameraSequenceData { name = "shot_" + DateTime.Now.ToString("MMdd_HHmm") };

        // ── Frame ────────────────────────────────────────────────────
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f8Key.wasPressedThisFrame) _open = !_open;
                if (kb.f9Key.wasPressedThisFrame) SetFly(!_fly);
            }
            if (_fly) UpdateFly(Time.unscaledDeltaTime);
            if (_director != null && _director.IsPlaying && _director.Current == _data) _playhead = _director.ElapsedMs;
        }

        private void SetFly(bool on)
        {
            if (_director == null || on == _fly) return;
            _fly = on;
            if (on)
            {
                if (_director.IsPlaying) _director.Stop(true);
                FlyTo(_director.LastPose);
            }
            else
            {
                _director.ClearOverride();
            }
        }

        private void FlyTo(in CameraPose p)
        {
            _flyPose = p;
            var e = p.rotation.eulerAngles;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            _yaw = e.y;
            _roll = e.z > 180f ? e.z - 360f : e.z;
            _director.SetOverride(in _flyPose);
        }

        private void UpdateFly(float dt)
        {
            Vector3 move = Vector3.zero;
            Vector2 look = Vector2.zero;
            float speed = _moveSpeed;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.eKey.isPressed) move.y += 1f;
                if (kb.qKey.isPressed) move.y -= 1f;
                if (kb.cKey.isPressed) _roll -= 30f * dt;
                if (kb.zKey.isPressed) _roll += 30f * dt;
                if (kb.shiftKey.isPressed) speed *= 4f;
                if (kb.ctrlKey.isPressed) speed *= 0.25f;
            }

            var mouse = Mouse.current;
            if (mouse != null && !PointerOverPanel(mouse.position.ReadValue()))
            {
                if (mouse.rightButton.isPressed) look += mouse.delta.ReadValue() * 0.15f;
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f) _flyPose.fov = Mathf.Clamp(_flyPose.fov - Mathf.Sign(wheel) * 2f, 5f, 120f);
            }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                var touches = touch.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (!t.press.isPressed) continue;
                    Vector2 pos = t.position.ReadValue();
                    if (PointerOverPanel(pos)) continue;
                    Vector2 d = t.delta.ReadValue();
                    if (pos.x < Screen.width / 3f) { move.x += d.x * 0.02f; move.z += d.y * 0.02f; }
                    else look += d * 0.12f;
                }
            }

            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            var rot = Quaternion.Euler(_pitch, _yaw, _roll);
            _flyPose.position += rot * move * (speed * dt);
            _flyPose.rotation = rot;
            _director.SetOverride(in _flyPose);
        }

        private bool PointerOverPanel(Vector2 screenPos)
        {
            if (!_open) return false;
            var gui = new Vector2(screenPos.x, Screen.height - screenPos.y) / _scale;
            return _window.Contains(gui);
        }

        // ── Keys ─────────────────────────────────────────────────────
        private CameraKeyframe Selected => _sel >= 0 && _sel < _data.keys.Count ? _data.keys[_sel] : null;

        private void AddKey()
        {
            var keys = _data.keys;
            double t = keys.Count == 0 ? 0 : _playhead > keys[keys.Count - 1].timeMs ? _playhead : keys[keys.Count - 1].timeMs + 1000;
            var k = Selected != null ? Selected.Clone() : new CameraKeyframe();
            k.timeMs = Math.Round(t);
            k.shake = 0f;
            Capture(k);
            keys.Add(k);
            Resort(k);
            _status = "Added key at " + k.timeMs + " ms";
        }

        /// Stores the camera you see into `k`, keeping a follow key relative to its target.
        private void Capture(CameraKeyframe k)
        {
            var p = _fly ? _flyPose : _director.LastPose;
            if (!string.IsNullOrEmpty(k.follow) && _director.TryGetPosition(k.follow, out var anchor)) p.position -= anchor;
            k.pose = p;
        }

        private void Resort(CameraKeyframe keep)
        {
            CameraTrack.Sort(_data.keys);
            _sel = _data.keys.IndexOf(keep);
        }

        /// Changing a key's follow target keeps its world position: re-express the offset.
        private void SetFollow(CameraKeyframe k, string target)
        {
            var world = CameraTrack.Resolve(k, _director).position;
            k.follow = target;
            if (!string.IsNullOrEmpty(target) && _director.TryGetPosition(target, out var anchor)) k.pose.position = world - anchor;
            else k.pose.position = world;
        }

        // ── Playback ─────────────────────────────────────────────────
        private void Preview(bool fromStart)
        {
            if (_data.keys.Count == 0) { _status = "No keys yet"; return; }
            SetFly(false);
            _director.PlaybackSpeed = Speeds[_speedIndex];
            _director.Play(_data, fromStart ? 0 : _playhead);
        }

        private void Scrub(double ms)
        {
            _playhead = ms;
            if (_data.keys.Count == 0) return;
            if (_fly && CameraTrack.TryEvaluate(_data.keys, ms, _director, out var p)) { FlyTo(p); return; }
            if (!_director.IsPlaying || _director.Current != _data) _director.Play(_data, ms);
            _director.Paused = true;
            _director.Seek(ms);
        }

        // ── Files ────────────────────────────────────────────────────
        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string file = Path.Combine(Folder, SafeName(_data.name) + ".json");
                CameraTrack.Sort(_data.keys);
                File.WriteAllText(file, JsonUtility.ToJson(_data, true));
                _status = "Saved " + file;
                SlapLog.Info(LogChannel.Presentation, "Camera sequence saved: " + file);
            }
            catch (Exception e)
            {
                _status = "Save failed: " + e.Message;
                SlapLog.Error(LogChannel.Presentation, "Camera sequence save failed: " + e);
            }
        }

        private void RefreshFiles()
        {
            try { _files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json") : Array.Empty<string>(); }
            catch (Exception e) { _files = Array.Empty<string>(); _status = e.Message; }
        }

        private void Load(string file)
        {
            try
            {
                var d = JsonUtility.FromJson<CameraSequenceData>(File.ReadAllText(file));
                if (d == null || d.keys == null) throw new InvalidDataException("not a camera sequence");
                Open(d);
                _status = "Loaded " + Path.GetFileName(file);
            }
            catch (Exception e) { _status = "Load failed: " + e.Message; }
        }

        private void Open(CameraSequenceData d)
        {
            if (_director.IsPlaying) _director.Stop(true);
            _data = d;
            CameraTrack.Sort(_data.keys);
            _sel = _data.keys.Count > 0 ? 0 : -1;
            _playhead = 0;
            _showLoad = false;
        }

        private static string SafeName(string n)
        {
            if (string.IsNullOrWhiteSpace(n)) n = "untitled";
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        // ── GUI ──────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_director == null) return;
            _scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));

            if (!_open)
            {
                if (GUI.Button(new Rect(8, 8, 56, 28), "CAM")) _open = true;
                return;
            }
            _window.height = Mathf.Min(_window.height, Screen.height / _scale - 16f);
            _window = GUI.Window(0x5A9C, _window, DrawWindow, "CAMERA RECORDER  [F8]");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_rigTab, "Sequence", "Button")) _rigTab = false;
            if (GUILayout.Toggle(_rigTab, "Rig & states", "Button")) _rigTab = true;
            if (GUILayout.Button("X", GUILayout.Width(24))) _open = false;
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_rigTab) DrawRigTab();
            else DrawSequenceTab();
            if (_status.Length > 0) GUILayout.Label(_status);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawFlyControls()
        {
            GUILayout.BeginHorizontal();
            bool fly = GUILayout.Toggle(_fly, _fly ? " FLY ON  [F9]" : " Fly camera [F9]", "Button");
            if (fly != _fly) SetFly(fly);
            GUILayout.Label("Speed", GUILayout.Width(40));
            _moveSpeed = GUILayout.HorizontalSlider(_moveSpeed, 0.2f, 10f, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            if (_fly)
            {
                _flyPose.fov = Slider("FOV", _flyPose.fov, 5f, 120f);
                _roll = Slider("Roll", _roll, -45f, 45f);
                _flyPose.dof = GUILayout.Toggle(_flyPose.dof, " Depth of field");
                if (_flyPose.dof)
                {
                    _flyPose.focusDistance = Slider("Focus m", _flyPose.focusDistance, 0.1f, 20f);
                    _flyPose.aperture = Slider("f-stop", _flyPose.aperture, 1f, 22f);
                }
            }
        }

        private void DrawSequenceTab()
        {
            // Sequence
            GUILayout.BeginHorizontal();
            _data.name = GUILayout.TextField(_data.name, GUILayout.MinWidth(140));
            if (GUILayout.Button("New")) Open(NewSequence());
            if (GUILayout.Button("Save")) Save();
            if (GUILayout.Button(_showLoad ? "Close" : "Load")) { _showLoad = !_showLoad; if (_showLoad) RefreshFiles(); }
            GUILayout.EndHorizontal();
            if (_showLoad) DrawLoad();

            // Camera
            GUILayout.Space(4);
            DrawFlyControls();

            // Keys
            GUILayout.Space(6);
            GUILayout.Label("KEYS  (" + _data.keys.Count + ")   length " + Math.Round(_data.DurationMs) + " ms");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _data.keys.Count; i++)
            {
                if (i > 0 && i % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                bool on = GUILayout.Toggle(i == _sel, "#" + i + " " + Math.Round(_data.keys[i].timeMs), "Button");
                if (on && i != _sel) _sel = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Key")) AddKey();
            GUI.enabled = Selected != null;
            if (GUILayout.Button("Update")) { Capture(Selected); _status = "Key #" + _sel + " updated from camera"; }
            if (GUILayout.Button("Go to")) { SetFly(true); FlyTo(CameraTrack.Resolve(Selected, _director)); _playhead = Selected.timeMs; }
            if (GUILayout.Button("Delete")) { _data.keys.RemoveAt(_sel); _sel = Mathf.Min(_sel, _data.keys.Count - 1); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (Selected != null) DrawKey(Selected);

            // Playback
            GUILayout.Space(6);
            GUILayout.Label("PLAYBACK");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("|<")) Scrub(0);
            bool playingThis = _director.IsPlaying && _director.Current == _data && !_director.Paused;
            if (GUILayout.Button(playingThis ? "Pause" : "Play"))
            {
                if (playingThis) _director.Paused = true;
                else if (_director.IsPlaying && _director.Current == _data) _director.Paused = false;
                else Preview(_playhead >= _data.DurationMs);
            }
            if (GUILayout.Button("Stop")) _director.Stop();
            _data.loop = GUILayout.Toggle(_data.loop, " Loop");
            GUILayout.EndHorizontal();
            int sp = GUILayout.SelectionGrid(_speedIndex, SpeedNames, SpeedNames.Length);
            if (sp != _speedIndex) { _speedIndex = sp; _director.PlaybackSpeed = Speeds[sp]; }
            double dur = Math.Max(1, _data.DurationMs);
            GUILayout.Label(Math.Round(_playhead) + " / " + Math.Round(_data.DurationMs) + " ms");
            float scrub = GUILayout.HorizontalSlider((float)_playhead, 0f, (float)dur);
            if (Math.Abs(scrub - _playhead) > 0.5) Scrub(scrub);

            // Sequence options
            GUILayout.Space(6);
            GUILayout.Label("SEQUENCE");
            _data.blendInMs = Slider("Blend in ms", (float)_data.blendInMs, 0f, 2000f);
            _data.blendOutMs = Slider("Blend out ms", (float)_data.blendOutMs, 0f, 2000f);
            _data.unscaledTime = GUILayout.Toggle(_data.unscaledTime, " Unscaled time (runs through hitstop)");

            // Shake test
            GUILayout.Space(6);
            GUILayout.Label("SHAKE TEST   trauma " + _director.Trauma.ToString("0.00"));
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _director.ShakePresetCount; i++)
                if (GUILayout.Button(_director.ShakePresetName(i))) _director.AddTrauma(i == 0 ? 0.5f : 1f, i);
            GUILayout.EndHorizontal();

            // Bound cues: open one to edit it, then Save and re-import.
            if (_director.CueCount > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("BOUND CUES  (edits a copy; Save, then re-import)");
                for (int i = 0; i < _director.CueCount; i++)
                {
                    var c = _director.CueAt(i);
                    if (c.sequence == null) continue;
                    if (GUILayout.Button(c.cue + ": " + c.sequence.name)) Open(c.sequence.data.Clone());
                }
            }
        }

        private void DrawKey(CameraKeyframe k)
        {
            GUILayout.Label("KEY #" + _sel + "   (transition INTO this key)");
            double t = MsField("Time", k.timeMs);
            if (t != k.timeMs) { k.timeMs = Math.Max(0, t); Resort(k); }
            k.holdMs = Math.Max(0, MsField("Hold", k.holdMs));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Path", GUILayout.Width(70));
            k.path = (PathMode)GUILayout.SelectionGrid((int)k.path, PathNames, PathNames.Length);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Ease", GUILayout.Width(70));
            if (GUILayout.Button("<", GUILayout.Width(26))) k.ease = Step(Eases, k.ease, -1);
            GUILayout.Label(k.useCurve ? "(custom curve)" : k.ease.ToString(), GUILayout.MinWidth(110));
            if (GUILayout.Button(">", GUILayout.Width(26))) k.ease = Step(Eases, k.ease, 1);
            GUILayout.EndHorizontal();
            if (k.useCurve) k.useCurve = GUILayout.Toggle(k.useCurve, " Use custom curve (edit it on the imported asset)");

            k.lookAt = TargetPicker("Look at", k.lookAt);
            string follow = TargetPicker("Follow", k.follow);
            if (!string.Equals(follow, k.follow, StringComparison.Ordinal)) SetFollow(k, follow);

            k.shake = Slider("Shake", k.shake, 0f, 1f);
            if (k.shake > 0f)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Shake type", GUILayout.Width(70));
                if (GUILayout.Button("<", GUILayout.Width(26))) k.shakePreset = Mathf.Max(0, k.shakePreset - 1);
                GUILayout.Label(_director.ShakePresetName(k.shakePreset), GUILayout.MinWidth(110));
                if (GUILayout.Button(">", GUILayout.Width(26))) k.shakePreset = Mathf.Min(_director.ShakePresetCount - 1, k.shakePreset + 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("FOV " + k.pose.fov.ToString("0.#") + (k.pose.dof ? "   DoF " + k.pose.focusDistance.ToString("0.0") + " m f/" + k.pose.aperture.ToString("0.0") : ""));
        }

        private string TargetPicker(string label, string current)
        {
            int n = _director.TargetCount;
            int idx = -1;
            for (int i = 0; i < n; i++) if (string.Equals(_director.TargetName(i), current, StringComparison.Ordinal)) idx = i;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            if (GUILayout.Button("<", GUILayout.Width(26))) idx = idx <= -1 ? n - 1 : idx - 1;
            GUILayout.Label(idx < 0 ? "(none)" : _director.TargetName(idx), GUILayout.MinWidth(110));
            if (GUILayout.Button(">", GUILayout.Width(26))) idx = idx >= n - 1 ? -1 : idx + 1;
            GUILayout.EndHorizontal();
            return idx < 0 ? "" : _director.TargetName(idx);
        }

        private void DrawLoad()
        {
            if (_files.Length == 0) GUILayout.Label("No recordings in " + Folder);
            for (int i = 0; i < _files.Length; i++)
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(_files[i]))) Load(_files[i]);
        }

        private static float Slider(string label, float v, float lo, float hi)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " " + v.ToString("0.##"), GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, lo, hi);
            GUILayout.EndHorizontal();
            return v;
        }

        private static double MsField(string label, double v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(40));
            if (GUILayout.Button("-100")) v -= 100;
            if (GUILayout.Button("-10")) v -= 10;
            string s = GUILayout.TextField(Math.Round(v).ToString(), GUILayout.Width(60));
            if (double.TryParse(s, out double parsed) && Math.Abs(parsed - Math.Round(v)) > 0.5) v = parsed;
            if (GUILayout.Button("+10")) v += 10;
            if (GUILayout.Button("+100")) v += 100;
            GUILayout.Label("ms");
            GUILayout.EndHorizontal();
            return v;
        }

        private static Ease Step(Ease[] all, Ease cur, int dir)
        {
            int i = Array.IndexOf(all, cur);
            i = (i + dir + all.Length) % all.Length;
            return all[i];
        }
    }
}
