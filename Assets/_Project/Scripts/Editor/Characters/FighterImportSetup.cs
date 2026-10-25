using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlapMania.Editor.Characters
{
    /// Converts a Mixamo-style character set (base "Model.fbx" + "Model@Clip.fbx" files, which
    /// need not share a folder) to Humanoid so Animator IK (OnAnimatorIK) and clip retargeting
    /// work (M08).
    ///
    ///   * the base model creates the Avatar and imports no animation of its own,
    ///   * every @clip file copies that Avatar and keeps only its "mixamo.com" take — the
    ///     6.63 s Blender action exported alongside each file is dropped,
    ///   * root motion is baked into the pose (rotation, height, XZ) so fighters stay planted,
    ///   * idle/stance clips loop.
    ///
    /// Idempotent: re-running re-applies the same settings. Import settings live in the .meta
    /// files, so this changes no source asset.
    public static class FighterImportSetup
    {
        /// The "@clip" files only. The base model lives with its materials and textures in
        /// Characters/FBX/Alucard/ and is found by name (FindBaseModel), so a move cannot strand
        /// the builder on a stale path again (S-085).
        public const string AlucardClipFolder = "Assets/_Project/Animations";
        public const string AlucardStem = "Alucard Sword Animation";
        private const string SearchRoot = "Assets/_Project";

        public static string AlucardClipPath(string clipName) => AlucardClipFolder + "/" + AlucardStem + "@" + clipName + ".fbx";

        /// Clips that should loop (idles and stances). Everything else plays once.
        private static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Bouncing Fight Idle", "Boxing", "Boxing (1)"
        };

        [MenuItem("Tools/Slap Mania/Characters/Set Up Alucard As Humanoid", priority = 40)]
        public static void SetUpAlucardMenu()
        {
            var report = SetUpHumanoidSet(FindBaseModel(AlucardStem), AlucardClipFolder);
            EditorUtility.DisplayDialog("Humanoid setup", report, "OK");
        }

        /// The base model "<stem>.<ext>" anywhere under Assets/_Project, matched on the exact file
        /// name ("<stem>@Clip" files never match). Throws when there is none, or more than one -
        /// which copy to rig would be a guess.
        public static string FindBaseModel(string stem)
        {
            string found = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { SearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != stem) continue;
                if (found != null)
                    throw new System.InvalidOperationException("Two base models named '" + stem + "': " + found + " and " + path + ". Keep one.");
                found = path;
            }
            if (found == null) throw new FileNotFoundException("No base model named '" + stem + "' under " + SearchRoot + ".");
            return found;
        }

        /// Rigs basePath as the Avatar source and every "<stem>@Clip" model in clipFolder from it.
        /// Returns a human-readable report; throws if the Avatar comes out invalid.
        public static string SetUpHumanoidSet(string basePath, string clipFolder)
        {
            var baseImp = AssetImporter.GetAtPath(basePath) as ModelImporter;
            if (baseImp == null) throw new FileNotFoundException("No model importer at " + basePath);

            baseImp.animationType = ModelImporterAnimationType.Human;
            baseImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            baseImp.importAnimation = false;
            baseImp.optimizeGameObjects = false; // IK, hit reaction and ragdoll need the bone transforms
            baseImp.SaveAndReimport();
            if (FixHipsMapping(baseImp)) baseImp.SaveAndReimport();

            var avatar = LoadAvatar(basePath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new System.InvalidOperationException("Avatar from " + basePath + " is not a valid humanoid (valid=" +
                    (avatar != null && avatar.isValid) + ", human=" + (avatar != null && avatar.isHuman) + "). Check the bone mapping in the Avatar configurator.");

            string stem = Path.GetFileNameWithoutExtension(basePath);
            var report = new System.Text.StringBuilder("Avatar OK: " + avatar.name + "\n");

            int clips = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { clipFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith(stem + "@")) continue;
                string clipName = file.Substring(stem.Length + 1);
                report.Append(ConfigureClipFile(path, clipName, avatar)).Append('\n');
                clips++;
            }
            if (clips == 0) report.Append("  ! no '" + stem + "@*' clip files in " + clipFolder + "\n");
            return report.ToString();
        }

        /// Unity's auto-mapper can bind Humanoid "Hips" to a Blender "Armature" object that sits
        /// above the real pelvis in the hierarchy (DEBUG S-072). The Armature rests 0.8 m below and
        /// 0.56 m behind the pelvis, so every animated pose then floats and drifts forward. Remap
        /// Hips to the skeleton's own "*Hips" bone. Returns true when it changed the mapping.
        public static bool FixHipsMapping(ModelImporter imp)
        {
            var hd = imp.humanDescription;
            var human = hd.human;
            if (human == null) return false;
            string realHips = null;
            foreach (var sb in hd.skeleton)
                if (sb.name == "Hips" || sb.name.EndsWith(":Hips")) { realHips = sb.name; break; }
            if (realHips == null) return false;
            for (int i = 0; i < human.Length; i++)
            {
                if (human[i].humanName != "Hips" || human[i].boneName == realHips) continue;
                human[i].boneName = realHips;
                hd.human = human;
                imp.humanDescription = hd;
                return true;
            }
            return false;
        }

        public static Avatar LoadAvatar(string modelPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Avatar a) return a;
            return null;
        }

        /// The imported clip for a "@Clip" file, by its configured name.
        public static AnimationClip LoadClip(string modelPath, string clipName)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is AnimationClip c && c.name == clipName) return c;
            return null;
        }

        private static string ConfigureClipFile(string path, string clipName, Avatar avatar)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = avatar;
            imp.importAnimation = true;
            imp.optimizeGameObjects = false;

            TakeInfo? take = null;
            foreach (var t in imp.importedTakeInfos)
                if (t.name == "mixamo.com") { take = t; break; }
            if (take == null)
            {
                imp.SaveAndReimport();
                return "  ! " + clipName + ": no 'mixamo.com' take; left as-is";
            }

            var ti = take.Value;
            float rate = ti.sampleRate > 0f ? ti.sampleRate : 30f;
            bool loop = Looping.Contains(clipName);
            imp.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = clipName,
                    takeName = ti.name,
                    firstFrame = Mathf.Round(ti.startTime * rate),
                    lastFrame = Mathf.Round(ti.stopTime * rate),
                    loopTime = loop,
                    loopPose = loop,
                    lockRootRotation = true,
                    lockRootHeightY = true,
                    lockRootPositionXZ = true,
                    keepOriginalOrientation = true,
                    // Centre the body over the GameObject and stand it on its feet. "Original"
                    // keeps the offset the clip was authored with (measured: head 0.67 m forward,
                    // 0.6 m up), which made two fighters at slapping distance interpenetrate.
                    keepOriginalPositionY = false,
                    heightFromFeet = true,
                    keepOriginalPositionXZ = false,
                }
            };
            imp.SaveAndReimport();

            var clip = LoadClip(path, clipName);
            return "  " + clipName + ": " + (clip != null ? clip.length.ToString("0.00") + "s humanMotion=" + clip.humanMotion + (loop ? " loop" : "") : "MISSING after import");
        }
    }
}
