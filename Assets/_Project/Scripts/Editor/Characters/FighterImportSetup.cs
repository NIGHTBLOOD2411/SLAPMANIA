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

        /// Clips that should loop (idles and stances), by shared clip name or by the role name a
        /// champion's override file uses ("<stem>@Idle.fbx"). Everything else plays once.
        private static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Bouncing Fight Idle", "Boxing", "Boxing (1)", "Idle", "Charge", "Victory"
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
            var avatar = SetUpHumanoidBase(basePath);
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

        /// Rigs one base model as Humanoid (its own Avatar, no animation, bone transforms kept)
        /// and returns the Avatar. Throws when the auto-mapper cannot make a valid humanoid -
        /// the fix is then in the Avatar configurator, by hand. Used by the Champion Creator for
        /// any rigged FBX (M08).
        public static Avatar SetUpHumanoidBase(string basePath)
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
                    (avatar != null && avatar.isValid) + ", human=" + (avatar != null && avatar.isHuman) + "). " +
                    "Select the FBX > Rig > Configure... and fix the bone mapping, then run the creator again.");
            return avatar;
        }

        /// True when basePath is already a valid Humanoid with Hips on the pelvis (S-072), so a
        /// rebuild need not reimport it.
        public static bool BaseReady(string basePath)
        {
            var imp = AssetImporter.GetAtPath(basePath) as ModelImporter;
            var avatar = LoadAvatar(basePath);
            if (imp == null || imp.animationType != ModelImporterAnimationType.Human || avatar == null || !avatar.isValid || !avatar.isHuman) return false;
            foreach (var hb in imp.humanDescription.human)
                if (hb.humanName == "Hips" && !(hb.boneName == "Hips" || hb.boneName.EndsWith(":Hips"))) return false;
            return true;
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

        /// One trimmed role from a mocap take (MocapSelection, ADR-0024): Humanoid with an avatar
        /// from the file's own skeleton, `firstSec`..`lastSec` of its take, `yawOffset` degrees so
        /// the clip faces the opponent. Rotation and height are baked; XZ is deliberately NOT, so
        /// the actor's walking goes to root motion, which fighters discard (applyRootMotion off):
        /// a baked 0.25 m drift would carry the body into the table. No-op when already set.
        public static string ConfigureMocapCut(string path, string clipName, float firstSec, float lastSec, float yawOffset, bool loop)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) return "  ! " + path + ": not a model file";
            if (imp.animationType != ModelImporterAnimationType.Human || imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.optimizeGameObjects = false;
                imp.SaveAndReimport();   // the takes and the avatar exist only after this
            }

            var takes = imp.importedTakeInfos;
            if (takes == null || takes.Length == 0) return "  ! " + clipName + ": " + path + " has no animation take";
            var ti = takes[0];
            foreach (var t in takes) if (t.name == "mixamo.com") { ti = t; break; }
            float rate = ti.sampleRate > 0f ? ti.sampleRate : 30f;
            float first = Mathf.Round(firstSec * rate), last = Mathf.Round(lastSec * rate);

            var current = imp.clipAnimations;
            bool same = current != null && current.Length == 1 && current[0].name == clipName
                     && Mathf.Approximately(current[0].firstFrame, first) && Mathf.Approximately(current[0].lastFrame, last)
                     && Mathf.Approximately(current[0].rotationOffset, yawOffset) && current[0].loopTime == loop
                     && !current[0].lockRootPositionXZ;
            if (!same)
            {
                imp.clipAnimations = new[]
                {
                    new ModelImporterClipAnimation
                    {
                        name = clipName,
                        takeName = ti.name,
                        firstFrame = first,
                        lastFrame = last,
                        loopTime = loop,
                        loopPose = loop,
                        lockRootRotation = true,
                        keepOriginalOrientation = true,
                        rotationOffset = yawOffset,
                        lockRootHeightY = true,
                        keepOriginalPositionY = false,
                        heightFromFeet = true,
                        lockRootPositionXZ = false,
                        keepOriginalPositionXZ = false,
                    }
                };
                imp.SaveAndReimport();
            }
            var clip = LoadClip(path, clipName);
            return "  " + clipName + ": " + (clip != null ? clip.length.ToString("0.00") + "s humanMotion=" + clip.humanMotion : "MISSING after import");
        }

        public static string ConfigureClipFile(string path, string clipName, Avatar avatar)
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
            // Not from Mixamo (a champion's own override clip): its first take is the clip.
            if (take == null && imp.importedTakeInfos.Length > 0) take = imp.importedTakeInfos[0];
            if (take == null)
            {
                imp.SaveAndReimport();
                return "  ! " + clipName + ": no animation take; left as-is";
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
