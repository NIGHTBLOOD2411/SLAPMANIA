using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;
using SlapMania.Logging;
using SlapMania.Presentation;

namespace SlapMania.Diagnostics
{
    /// The recorder's "Rig & states" tab (M16): live-edit the rig's gameplay/intro angle,
    /// resting FOV, shake presets and every game state's camera transition, with undo/redo
    /// and named saves. Saves go to `<project>/Library/CameraRigSaves/` in the Editor
    /// (never committed); stopping Play offers to apply the last save to the scene
    /// (`Editor/Cinematics/CameraRigSetupApplier.cs`).
    public sealed partial class CameraRecorder
    {
        public const string PendingApplyFile = "_pending_apply.txt";
        private const int MaxUndo = 64;

        public static string RigFolder
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", CameraRigSetup.FolderName);
#else
                return Path.Combine(Application.persistentDataPath, CameraRigSetup.FolderName);
#endif
            }
        }

        // Game-flow order, not enum order (the enum is append-only).
        private static readonly CameraCue[] StateOrder =
        {
            CameraCue.BoutIntro, CameraCue.TurnStart, CameraCue.ChargeStart, CameraCue.ChargeEnd,
            CameraCue.TimingStart, CameraCue.TimingEnd, CameraCue.Windup, CameraCue.Impact,
            CameraCue.LethalImpact, CameraCue.Finisher, CameraCue.KO, CameraCue.TurnEnd, CameraCue.BoutEnd
        };
        private static readonly string[] StateNames = Array.ConvertAll(StateOrder, c => c.ToString());
        private static readonly string[] ModeNames = { "Keep", "Gameplay", "Shot", "Sequence" };
        // Fixed sides (player / opponent) for two-sided shots; attacker / defender swap per turn.
        private static readonly string[] RelativeTo = { "", "player", "opponent", "attacker", "defender", "attackerHead", "defenderHead" };
        private static readonly string[] RelativeNames = { "World", "Player", "Opponent", "Attacker", "Defender", "Atk head", "Def head" };
        private static readonly string[] SideNames = { "PLAYER attacking", "OPPONENT attacking" };

        private CameraRig _rig;
        private Camera _cam;
        private bool _rigTab;
        private readonly List<CameraRigSetup> _undo = new List<CameraRigSetup>();
        private readonly List<CameraRigSetup> _redo = new List<CameraRigSetup>();
        private CameraRigSetup _dragStart;
        private string _rigName = "rig_" + DateTime.Now.ToString("MMdd_HHmm");
        private string[] _rigFiles = Array.Empty<string>();
        private bool _rigFilesDirty = true;
        private int _stateIdx;
        private int _shakeIdx = 1;
        private int _sideIdx;

        // ── Undo ─────────────────────────────────────────────────────
        private CameraRigSetup SnapshotRig() => CameraRigSetup.Capture(_rig, _cam, _director);

        private void PushUndo()
        {
            PushUndo(SnapshotRig());
            _dragStart = null;
        }

        private void PushUndo(CameraRigSetup s)
        {
            _undo.Add(s);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            _redo.Clear();
        }

        private void UndoRig()
        {
            if (_undo.Count == 0) return;
            _redo.Add(SnapshotRig());
            var s = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            ApplyRig(s);
            _status = "Undo (" + _undo.Count + " left)";
        }

        private void RedoRig()
        {
            if (_redo.Count == 0) return;
            _undo.Add(SnapshotRig());
            var s = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            ApplyRig(s);
            _status = "Redo (" + _redo.Count + " left)";
        }

        private void ApplyRig(CameraRigSetup s)
        {
            s.Clone().ApplyTo(_rig, _cam, _director);
            if (_rig != null && !_rig.IsFlyingIntro) _rig.SnapToGameplay();
        }

        // ── Tab ──────────────────────────────────────────────────────
        private void DrawRigTab()
        {
            if (_rig == null) { GUILayout.Label("No CameraRig above this camera."); return; }

            // A slider drag is one undo step: remember the state at mouse-down, push it on the
            // first change. Buttons push their own step (PushUndo clears _dragStart).
            if (Event.current.type == EventType.MouseDown) _dragStart = SnapshotRig();

            GUILayout.BeginHorizontal();
            GUI.enabled = _undo.Count > 0;
            if (GUILayout.Button("Undo (" + _undo.Count + ")")) UndoRig();
            GUI.enabled = _redo.Count > 0;
            if (GUILayout.Button("Redo (" + _redo.Count + ")")) RedoRig();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            DrawAngles();
            DrawStates();
            DrawShakePresets();

            if (GUI.changed && _dragStart != null) { PushUndo(_dragStart); _dragStart = null; }

            DrawRigSaves(); // after the undo check: typing a save name is not an undo step
        }

        private CameraPose CurrentView() => _director.LastPose;

        private void DrawAngles()
        {
            GUILayout.Space(4);
            GUILayout.Label("GAMEPLAY ANGLE   pos " + _rig.GameplayPosition.ToString("0.00") + "  rot " + _rig.GameplayEuler.ToString("0.0"));
            DrawFlyControls();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set gameplay = view"))
            {
                PushUndo();
                var v = CurrentView();
                _rig.GameplayPosition = v.position;
                _rig.GameplayEuler = v.rotation.eulerAngles;
                _director.RestFov = v.fov;
                SetFly(false);
                _rig.SnapToGameplay();
                _status = "Gameplay angle set from the view";
            }
            if (GUILayout.Button("Set intro = view"))
            {
                PushUndo();
                var v = CurrentView();
                _rig.IntroPosition = v.position;
                _rig.IntroEuler = v.rotation.eulerAngles;
                _status = "Intro start angle set from the view";
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("View gameplay")) { SetFly(false); _director.Stop(true); _rig.SnapToGameplay(); }
            if (GUILayout.Button("Play intro")) { SetFly(false); _director.Stop(true); _rig.PlayIntro(2.5f); }
            GUILayout.EndHorizontal();
            _director.RestFov = Slider("Gameplay FOV", _director.RestFov, 15f, 90f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Intro ease", GUILayout.Width(110));
            if (GUILayout.Button("<", GUILayout.Width(26))) _rig.IntroEase = Step(Eases, _rig.IntroEase, -1);
            GUILayout.Label(_rig.IntroEase.ToString(), GUILayout.MinWidth(100));
            if (GUILayout.Button(">", GUILayout.Width(26))) _rig.IntroEase = Step(Eases, _rig.IntroEase, 1);
            GUILayout.EndHorizontal();
        }

        private void DrawStates()
        {
            GUILayout.Space(8);
            GUILayout.Label("GAME STATES   (what the camera does as each state begins)");
            _stateIdx = GUILayout.SelectionGrid(_stateIdx, StateNames, 3);
            var cue = StateOrder[_stateIdx];
            var st = _director.EnsureState(cue);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(70));
            var mode = (StateCameraMode)GUILayout.SelectionGrid((int)st.mode, ModeNames, ModeNames.Length);
            GUILayout.EndHorizontal();
            if (mode != st.mode)
            {
                // A never-captured shot would fly to the world origin: seed it with the view.
                if (mode == StateCameraMode.Shot && st.key.pose.position == Vector3.zero && string.IsNullOrEmpty(st.key.follow))
                {
                    var v = CurrentView();
                    CaptureShot(st.key, "", v.position, v.rotation, v);
                }
                st.mode = mode;
            }

            switch (st.mode)
            {
                case StateCameraMode.Keep:
                    GUILayout.Label(_director.HasCue(cue) ? "Keep: plays the bound sequence for " + cue + "." : "Keep: the camera carries on as it is.");
                    break;
                case StateCameraMode.Gameplay:
                    GUILayout.Label("Blend back to the gameplay angle:");
                    st.blendMs = Slider("Blend ms", (float)st.blendMs, 0f, 3000f);
                    EasePicker("Blend ease", ref st.blendEase);
                    break;
                case StateCameraMode.Shot:
                    DrawShot(st);
                    break;
                case StateCameraMode.Sequence:
                    GUILayout.Label(_director.HasCue(cue) ? "Plays the CameraSequence bound to " + cue + " on CameraDirector > Cues." : "No sequence bound to " + cue + ": add one under CameraDirector > Cues.");
                    break;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview " + cue))
            {
                SetFly(false);
                // A two-sided shot previews the side being edited, then the turn's side is restored.
                var turn = _director.Attacker;
                if (st.perSide) _director.Attacker = (SlapMania.Core.FighterSlot)_sideIdx;
                _director.Cue(cue);
                _director.Attacker = turn;
            }
            if (GUILayout.Button("Back to gameplay")) { SetFly(false); _director.ReturnToGameplay(350, Ease.InOutCubic); }
            GUILayout.EndHorizontal();
        }

        private void DrawShot(StateShot st)
        {
            if (st.opponentKey == null) st.opponentKey = st.key.Clone();
            bool perSide = GUILayout.Toggle(st.perSide, " Two-sided: a separate camera for each attacker");
            if (perSide != st.perSide)
            {
                // Start the opponent's camera from the player's, so it is never at the origin.
                if (perSide && st.opponentKey.pose.position == Vector3.zero && string.IsNullOrEmpty(st.opponentKey.follow))
                    st.opponentKey = st.key.Clone();
                st.perSide = perSide;
            }
            if (st.perSide) _sideIdx = GUILayout.SelectionGrid(_sideIdx, SideNames, 2);
            var k = st.perSide && _sideIdx == 1 ? st.opponentKey : st.key;
            bool oppSide = st.perSide && _sideIdx == 1;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Right shoulder")) { PushUndo(); ShoulderPreset(k, oppSide, 1f); }
            if (GUILayout.Button("Left shoulder")) { PushUndo(); ShoulderPreset(k, oppSide, -1f); }
            GUILayout.EndHorizontal();
            int rel = Array.IndexOf(RelativeTo, k.follow ?? "");
            if (rel < 0) rel = 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Relative to", GUILayout.Width(70));
            int newRel = GUILayout.SelectionGrid(rel, RelativeNames, 3);
            GUILayout.EndHorizontal();
            if (newRel != rel)
            {
                // Keep the shot where it is in the world, re-expressed in the new frame.
                var world = CameraTrack.Resolve(k, _director);
                CaptureShot(k, RelativeTo[newRel], world.position, world.rotation, world);
            }
            if (GUILayout.Button("Capture view -> " + st.cue + " shot"))
            {
                PushUndo();
                var v = CurrentView();
                CaptureShot(k, RelativeTo[newRel], v.position, v.rotation, v);
                _status = st.cue + " shot captured" + (newRel > 0 ? " relative to " + RelativeNames[newRel] : "");
            }
            if (newRel > 0 && !_director.TryGetPosition(RelativeTo[newRel], out _))
                GUILayout.Label("(" + RelativeNames[newRel] + " is bound at the first TurnStart; capture during a turn)");

            k.lookAt = TargetPicker("Look at", k.lookAt);
            if (!string.IsNullOrEmpty(k.follow) && k.followRotation)
            {
                // Offset in the followed fighter's own space: +x their right, +y up, -z behind.
                k.pose.position.x = Slider("Side (+R/-L)", k.pose.position.x, -1.5f, 1.5f);
                k.pose.position.y = Slider("Height", k.pose.position.y, 0f, 3f);
                k.pose.position.z = Slider("Fwd (-behind)", k.pose.position.z, -3f, 1f);
            }
            k.pose.fov = Slider("Shot FOV", k.pose.fov, 15f, 90f);

            st.blendMs = Slider(st.blendMs > 0 ? "Blend in ms" : "Blend in: CUT", (float)st.blendMs, 0f, 3000f);
            if (st.blendMs > 0) EasePicker("Blend in ease", ref st.blendEase);
            st.holdMs = Slider(st.holdMs > 0 ? "Hold ms" : "Hold: until next", (float)st.holdMs, 0f, 5000f);
            if (st.holdMs > 0)
            {
                st.returnMs = Slider("Return ms", (float)st.returnMs, 0f, 3000f);
                EasePicker("Return ease", ref st.returnEase);
            }
            k.shake = Slider("Shake", k.shake, 0f, 1f);
            if (k.shake > 0f)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Shake type", GUILayout.Width(110));
                if (GUILayout.Button("<", GUILayout.Width(26))) k.shakePreset = Mathf.Max(0, k.shakePreset - 1);
                GUILayout.Label(_director.ShakePresetName(k.shakePreset), GUILayout.MinWidth(100));
                if (GUILayout.Button(">", GUILayout.Width(26))) k.shakePreset = Mathf.Min(_director.ShakePresetCount - 1, k.shakePreset + 1);
                GUILayout.EndHorizontal();
            }
        }

        /// An over-the-shoulder camera on one fighter, aimed at the other's face. It follows the
        /// fighter's ROOT (stable) rather than the head bone, which rotates with every
        /// animation. `side` +1 = their right shoulder. These are starting values: tune them with
        /// the Side / Height / Fwd sliders (the fighter rig's scale is not checked from code, R11).
        private void ShoulderPreset(CameraKeyframe k, bool opponentAttacks, float side)
        {
            k.follow = opponentAttacks ? "opponent" : "player";
            k.followRotation = true;
            k.lookAt = opponentAttacks ? "playerHead" : "opponentHead";
            // Tuned in Play on Proto_Bout (fighters ~1 m apart, heads at ~1.45 m): closer to the
            // body than ~0.9 m sideways, the near head hides the far face.
            k.pose.position = new Vector3(0.95f * side, 1.5f, -1.0f);
            k.pose.rotation = Quaternion.identity; // roll 0; look-at sets the aim
            k.pose.fov = 42f;
            _status = (opponentAttacks ? "Opponent" : "Player") + " " + (side > 0 ? "right" : "left") + " shoulder cam set. Tune it with Side / Height / Fwd.";
        }

        /// Stores a world pose into `k`, in the frame of `target` when one is given and bound.
        private void CaptureShot(CameraKeyframe k, string target, Vector3 pos, Quaternion rot, in CameraPose lens)
        {
            k.pose = lens;
            k.follow = target;
            k.followRotation = !string.IsNullOrEmpty(target);
            if (k.followRotation && _director.TryGetPosition(target, out var anchor) && _director.TryGetRotation(target, out var frame))
            {
                var inv = Quaternion.Inverse(frame);
                k.pose.position = inv * (pos - anchor);
                k.pose.rotation = inv * rot;
            }
            else
            {
                k.follow = "";
                k.followRotation = false;
                k.pose.position = pos;
                k.pose.rotation = rot;
            }
        }

        private void DrawShakePresets()
        {
            int n = _director.ShakePresetCount;
            if (n == 0) return;
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SHAKE PRESET", GUILayout.Width(110));
            if (GUILayout.Button("<", GUILayout.Width(26))) _shakeIdx = (_shakeIdx - 1 + n) % n;
            GUILayout.Label(_director.ShakePresetName(_shakeIdx), GUILayout.MinWidth(80));
            if (GUILayout.Button(">", GUILayout.Width(26))) _shakeIdx = (_shakeIdx + 1) % n;
            if (GUILayout.Button("Test")) _director.AddTrauma(1f, _shakeIdx);
            GUILayout.EndHorizontal();
            DrawImpactShake();
            _shakeIdx = Mathf.Clamp(_shakeIdx, 0, n - 1);
            var p = _director.GetShakePreset(_shakeIdx);
            p.maxOffset = Slider("Offset m", p.maxOffset, 0f, 0.5f);
            p.maxAngles.x = Slider("Pitch deg", p.maxAngles.x, 0f, 10f);
            p.maxAngles.y = Slider("Yaw deg", p.maxAngles.y, 0f, 10f);
            p.maxAngles.z = Slider("Roll deg", p.maxAngles.z, 0f, 15f);
            p.frequency = Slider("Frequency Hz", p.frequency, 1f, 40f);
            p.decayPerSecond = Slider("Decay /s", p.decayPerSecond, 0.2f, 8f);
            _director.SetShakePreset(_shakeIdx, p);
            GUILayout.Label("Slaps use Impact (lethal: Finisher); shake = intensity squared.");
        }

        private void DrawImpactShake()
        {
            GUILayout.Space(6);
            GUILayout.Label("IMPACT SHAKE   (every landed slap)");
            _director.ImpactMinTrauma = Slider("Min trauma", _director.ImpactMinTrauma, 0f, 1f);
            _director.ImpactScale = Slider("Scale", _director.ImpactScale, 0f, 3f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Slap preset", GUILayout.Width(110));
            int n = _director.ShakePresetCount;
            if (GUILayout.Button("<", GUILayout.Width(26))) _director.ImpactPreset = Mathf.Max(0, _director.ImpactPreset - 1);
            GUILayout.Label(_director.ShakePresetName(_director.ImpactPreset), GUILayout.MinWidth(80));
            if (GUILayout.Button(">", GUILayout.Width(26))) _director.ImpactPreset = Mathf.Min(n - 1, _director.ImpactPreset + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Lethal preset", GUILayout.Width(110));
            if (GUILayout.Button("<", GUILayout.Width(26))) _director.LethalPreset = Mathf.Max(0, _director.LethalPreset - 1);
            GUILayout.Label(_director.ShakePresetName(_director.LethalPreset), GUILayout.MinWidth(80));
            if (GUILayout.Button(">", GUILayout.Width(26))) _director.LethalPreset = Mathf.Min(n - 1, _director.LethalPreset + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test weak (0.2)")) _director.Impact(0.2f, false);
            if (GUILayout.Button("Test strong (0.8)")) _director.Impact(0.8f, false);
            if (GUILayout.Button("Test lethal")) _director.Impact(1f, true);
            GUILayout.EndHorizontal();
            float w = CameraShakeMath.ImpactTrauma(0.2f, _director.ImpactMinTrauma, _director.ImpactScale);
            GUILayout.Label("A 0.2 slap -> trauma " + w.ToString("0.00") + " (shake " + (w * w).ToString("0.00") + " of max)");
        }

        private void DrawRigSaves()
        {
            GUILayout.Space(8);
            GUILayout.Label("SAVES   (" + RigFolder + ")");
            GUILayout.BeginHorizontal();
            _rigName = GUILayout.TextField(_rigName, GUILayout.MinWidth(160));
            if (GUILayout.Button("Save")) SaveRig();
            GUILayout.EndHorizontal();
            if (_rigFilesDirty) RefreshRigFiles();
            for (int i = 0; i < _rigFiles.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Path.GetFileNameWithoutExtension(_rigFiles[i]), GUILayout.MinWidth(160));
                if (GUILayout.Button("Load", GUILayout.Width(50))) LoadRig(_rigFiles[i]);
                if (GUILayout.Button("Delete", GUILayout.Width(56))) DeleteRig(_rigFiles[i]);
                GUILayout.EndHorizontal();
            }
        }

        private void SaveRig()
        {
            try
            {
                Directory.CreateDirectory(RigFolder);
                var s = SnapshotRig();
                s.name = SafeName(_rigName);
                s.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string file = Path.Combine(RigFolder, s.name + ".json");
                File.WriteAllText(file, JsonUtility.ToJson(s, true));
                File.WriteAllText(Path.Combine(RigFolder, PendingApplyFile), s.name);
                _rigFilesDirty = true;
                _status = "Saved '" + s.name + "'. Stop Play to apply it to the scene.";
                SlapLog.Info(LogChannel.Presentation, "Camera rig setup saved: " + file);
            }
            catch (Exception e)
            {
                _status = "Save failed: " + e.Message;
                SlapLog.Error(LogChannel.Presentation, "Camera rig save failed: " + e);
            }
        }

        private void LoadRig(string file)
        {
            try
            {
                var s = JsonUtility.FromJson<CameraRigSetup>(File.ReadAllText(file));
                if (s == null) throw new InvalidDataException("not a rig setup");
                PushUndo();
                ApplyRig(s);
                _rigName = s.name;
                File.WriteAllText(Path.Combine(RigFolder, PendingApplyFile), s.name);
                _status = "Loaded '" + s.name + "' (Undo to go back)";
            }
            catch (Exception e) { _status = "Load failed: " + e.Message; }
        }

        private void DeleteRig(string file)
        {
            try
            {
                File.Delete(file);
                _rigFilesDirty = true;
                _status = "Deleted " + Path.GetFileNameWithoutExtension(file);
            }
            catch (Exception e) { _status = "Delete failed: " + e.Message; }
        }

        private void RefreshRigFiles()
        {
            _rigFilesDirty = false;
            try { _rigFiles = Directory.Exists(RigFolder) ? Directory.GetFiles(RigFolder, "*.json") : Array.Empty<string>(); }
            catch (Exception) { _rigFiles = Array.Empty<string>(); }
        }

        private static void EasePicker(string label, ref Ease ease)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            if (GUILayout.Button("<", GUILayout.Width(26))) ease = Step(Eases, ease, -1);
            GUILayout.Label(ease.ToString(), GUILayout.MinWidth(100));
            if (GUILayout.Button(">", GUILayout.Width(26))) ease = Step(Eases, ease, 1);
            GUILayout.EndHorizontal();
        }
    }
}
