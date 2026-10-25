using System.IO;
using UnityEditor;
using UnityEngine;
using SlapMania.Core.Cinematics;
using SlapMania.Data;

namespace SlapMania.Editor.Cinematics
{
    /// Bridges the runtime camera recorder (Diagnostics, ADR-0020) and the project: recorder
    /// JSON in persistentDataPath becomes `CameraSequence` assets, and an asset can go back to
    /// JSON to be edited in the running game again. The folder name must match
    /// `CameraRecorder.FolderName`; it is repeated here because Editor does not reference the
    /// define-constrained Diagnostics assembly.
    public static class CameraSequenceImporter
    {
        private const string FolderName = "CameraSequences";
        private const string AssetFolder = "Assets/_Project/Data/Camera";

        private static string RecordingsFolder => Path.Combine(Application.persistentDataPath, FolderName);

        [MenuItem("Tools/Slap Mania/Camera/Import Recorded Sequences")]
        public static void ImportAll()
        {
            if (!Directory.Exists(RecordingsFolder))
            {
                EditorUtility.DisplayDialog("Camera sequences", "No recordings yet in\n" + RecordingsFolder, "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Camera");

            int created = 0, updated = 0;
            foreach (var file in Directory.GetFiles(RecordingsFolder, "*.json"))
            {
                var data = JsonUtility.FromJson<CameraSequenceData>(File.ReadAllText(file));
                if (data == null || data.keys == null || data.keys.Count == 0)
                {
                    Debug.LogWarning("[Camera] Skipped " + file + ": no keys");
                    continue;
                }
                CameraTrack.Sort(data.keys);
                string name = Path.GetFileNameWithoutExtension(file);
                string path = AssetFolder + "/" + name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<CameraSequence>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CameraSequence>();
                    asset.data = data;
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    Undo.RecordObject(asset, "Import camera sequence");
                    asset.data = data;
                    EditorUtility.SetDirty(asset);
                    updated++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Camera] Imported camera sequences: " + created + " new, " + updated + " updated -> " + AssetFolder);
        }

        [MenuItem("Tools/Slap Mania/Camera/Send Selected Sequence To Recorder")]
        public static void ExportSelected()
        {
            Directory.CreateDirectory(RecordingsFolder);
            int n = 0;
            foreach (var o in Selection.objects)
            {
                if (!(o is CameraSequence seq) || seq.data == null) continue;
                File.WriteAllText(Path.Combine(RecordingsFolder, seq.name + ".json"), JsonUtility.ToJson(seq.data, true));
                n++;
            }
            Debug.Log("[Camera] Wrote " + n + " sequence(s) to " + RecordingsFolder + " - Load them from the recorder panel.");
        }

        [MenuItem("Tools/Slap Mania/Camera/Send Selected Sequence To Recorder", true)]
        private static bool ExportSelectedValid() => Selection.activeObject is CameraSequence;

        [MenuItem("Tools/Slap Mania/Camera/Open Recordings Folder")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(RecordingsFolder);
            EditorUtility.RevealInFinder(RecordingsFolder);
        }
    }
}
