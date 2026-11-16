using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SlapMania.Editor.Characters
{
    /// Which captured take fills which fighter role, and which seconds of it (M08, ADR-0024).
    /// The source packs are whole scenes (the slap take is 20 s of an actor walking in, slapping
    /// and walking off), so a role needs a trim, not just a file. The picked takes live in
    /// Mocap/Selected/<Role>.fbx and the cut in that copy's import settings, so the game needs
    /// nothing else. The 2 GB packs are kept outside the project and repo, in
    /// C:/Unity/SlapMania Mocap Source: to use another take, copy its FBX back under
    /// Animations/Mocap at the Source path below, or straight to Selected/<Role>.fbx.
    ///
    /// The windows were found by measuring the takes on a fighter over MCP: hand speed for the
    /// strike (peak 10.2 m/s at 9.27 s), head angular speed for the reaction (584 deg/s at
    /// 10.53 s), and a best-loop search over hips, yaw and hands for the idle.
    public static class MocapSelection
    {
        public const string PackDir = "Assets/_Project/Animations/Mocap";
        public const string SelectedDir = PackDir + "/Selected";

        private struct Cut
        {
            public string Role, Source;
            public float First, Last, YawOffset;
            public bool Loop;
            public Cut(string role, string source, float first, float last, float yawOffset, bool loop)
            { Role = role; Source = source; First = first; Last = last; YawOffset = yawOffset; Loop = loop; }
        }

        private const string Legacy = "Rokoko Studio Legacy Mocap (older)/";
        private const string SlapTake = Legacy + "Meme/WillSmithSlap_Will_HUMANIK_769.fbx";

        // Charge ends on the exact frame Slap starts, so the load flows straight into the strike.
        // YawOffset is the clip's measured facing: the importer subtracts it, so that yaw becomes 0.
        private static readonly Cut[] Cuts =
        {
            new Cut("Idle",    "Rokoko Studio (Mocap)/IdlesMocapPack_noFACE/Regular_Light_mixamo.fbx", 51.8f, 55.8f, -17f, true),
            new Cut("Charge",  SlapTake, 8.20f, 8.87f, 0f, false),
            new Cut("Slap",    SlapTake, 8.87f, 10.5f, 0f, false),
            new Cut("Hit",     Legacy + "Meme/WillSmithSlap_ChrisRock_HUMANIK_769.fbx", 10.45f, 12.5f, 0f, false),
            new Cut("Victory", "Rokoko Studio (Mocap)/Combat/KnockOut_Winner_mixamo.fbx", 1.0f, 3.4f, 26f, false),
        };

        [MenuItem("Tools/Slap Mania/Characters/Apply Mocap Selection", priority = 43)]
        public static void ApplyMenu() => EditorUtility.DisplayDialog("Slap Mania", Apply(), "OK");

        /// Copies any missing pick to Selected/ and applies every cut. Returns a report.
        public static string Apply()
        {
            if (!AssetDatabase.IsValidFolder(SelectedDir)) AssetDatabase.CreateFolder(PackDir, "Selected");
            var sb = new System.Text.StringBuilder();
            foreach (var c in Cuts)
            {
                string dst = SelectedDir + "/" + c.Role + ".fbx";
                if (AssetImporter.GetAtPath(dst) == null && !AssetDatabase.CopyAsset(PackDir + "/" + c.Source, dst))
                { sb.Append(c.Role).Append(": source missing, ").Append(c.Source).Append('\n'); continue; }
                sb.Append(FighterImportSetup.ConfigureMocapCut(dst, ClipName(c.Role), c.First, c.Last, c.YawOffset, c.Loop)).Append('\n');
            }
            return sb.ToString();
        }

        public static string ClipName(string role) => "Mocap_" + role;

        /// The selected clip for `role`, or null when the role has no pick (it stays authored).
        public static AnimationClip Load(string role)
        {
            foreach (var c in Cuts)
            {
                if (c.Role != role) continue;
                string path = SelectedDir + "/" + role + ".fbx";
                if (AssetImporter.GetAtPath(path) == null) return null;
                FighterImportSetup.ConfigureMocapCut(path, ClipName(role), c.First, c.Last, c.YawOffset, c.Loop);
                return FighterImportSetup.LoadClip(path, ClipName(role));
            }
            return null;
        }
    }
}
