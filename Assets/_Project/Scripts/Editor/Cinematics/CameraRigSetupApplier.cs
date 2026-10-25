using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SlapMania.Presentation;

namespace SlapMania.Editor.Cinematics
{
    /// Carries a camera rig setup saved in Play (the recorder's "Rig & states" tab) into the
    /// scene, because Play Mode edits revert. When Play stops and a save is pending, asks to
    /// apply it to the open scene's CameraRig, Camera and CameraDirector through Undo, so
    /// Ctrl+Z takes it back. The file names must match `CameraRecorder.RigFolder` /
    /// `PendingApplyFile` (Editor does not reference the Diagnostics assembly).
    [InitializeOnLoad]
    public static class CameraRigSetupApplier
    {
        private const string PendingApplyFile = "_pending_apply.txt";

        private static string Folder =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", CameraRigSetup.FolderName);

        static CameraRigSetupApplier()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            string pending = Path.Combine(Folder, PendingApplyFile);
            if (!File.Exists(pending)) return;
            string name = File.ReadAllText(pending).Trim();
            File.Delete(pending);
            string file = Path.Combine(Folder, name + ".json");
            if (!File.Exists(file)) return;

            int choice = EditorUtility.DisplayDialogComplex("Camera rig save",
                "Apply the camera rig save '" + name + "' to the open scene?\n\n" +
                "Gameplay and intro angle, FOV, shake presets and per-state transitions. Ctrl+Z undoes it.",
                "Apply", "Not now", "Pick another...");
            if (choice == 0) Apply(file);
            else if (choice == 2) ShowPicker();
        }

        [MenuItem("Tools/Slap Mania/Camera/Apply Camera Rig Save...")]
        public static void ShowPicker()
        {
            var menu = new GenericMenu();
            string[] files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json") : new string[0];
            if (files.Length == 0) menu.AddDisabledItem(new GUIContent("No saves in Library/" + CameraRigSetup.FolderName));
            foreach (var f in files)
            {
                string path = f;
                menu.AddItem(new GUIContent(Path.GetFileNameWithoutExtension(f)), false, () => Apply(path));
            }
            menu.ShowAsContext();
        }

        [MenuItem("Tools/Slap Mania/Camera/Open Camera Rig Saves Folder")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            EditorUtility.RevealInFinder(Folder);
        }

        public static void Apply(string file)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Camera] Stop Play first: Play Mode changes revert.");
                return;
            }
            var setup = JsonUtility.FromJson<CameraRigSetup>(File.ReadAllText(file));
            var rig = Object.FindAnyObjectByType<CameraRig>(FindObjectsInactive.Include);
            if (setup == null || rig == null)
            {
                Debug.LogWarning("[Camera] Nothing applied: " + (rig == null ? "no CameraRig in the open scene" : "unreadable save " + file));
                return;
            }
            var cam = rig.GetComponentInChildren<Camera>(true);
            var director = rig.GetComponentInChildren<CameraDirector>(true);

            var targets = new System.Collections.Generic.List<Object> { rig, rig.transform };
            if (cam != null) targets.Add(cam);
            if (director != null) targets.Add(director);
            Undo.RecordObjects(targets.ToArray(), "Apply camera rig save");
            setup.ApplyTo(rig, cam, director);
            // Put the rig where its new gameplay angle says, so the Scene view matches.
            rig.transform.SetPositionAndRotation(setup.gameplayPosition, Quaternion.Euler(setup.gameplayEuler));
            EditorUtility.SetDirty(rig);
            if (cam != null) EditorUtility.SetDirty(cam);
            if (director != null) EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("[Camera] Applied camera rig save '" + setup.name + "' to " + rig.gameObject.scene.name +
                      ". Save the scene to keep it. Note: Build Playable Prototype rebuilds the rig from PrototypeSceneBuilder's constants.");
        }
    }
}
