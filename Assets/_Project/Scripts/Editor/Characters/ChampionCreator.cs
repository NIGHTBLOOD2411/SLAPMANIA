using UnityEditor;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// FBX in, playable champion out (M08, ADR-0022). The headless half of the Champion
    /// Creator window, so a batch script or test can drive it too:
    ///   1. FighterBuilder turns the rigged model into a fighter prefab + rig profile
    ///      (Humanoid, shared or override clips, measured contacts, ragdoll).
    ///   2. A portrait is rendered from the model's head unless one is given.
    ///   3. Champion_<id>.asset is written and appended to the roster, and the roster is wired
    ///      into GameConfig, so the Roster tab lists it and the bout can spawn it.
    public static class ChampionCreator
    {
        public const string ChampionDir = "Assets/_Project/Data/Champions";
        public const string RosterPath = ChampionDir + "/ChampionRoster.asset";
        public const string PortraitDir = "Assets/_Project/Art/Portraits";
        private const string GameConfigPath = "Assets/_Project/Resources/GameConfig.asset";

        public struct Request
        {
            public string id;
            public string displayName;
            public string originId;
            public string classTag;
            public string modelPath;
            public string[] hidePrefixes;
            public Texture2D portrait;          // null = render one
            public ChampionCardFinish finish;
            public ChampionStats stats;
            /// Tints texture-less materials (ChampionTextures); null keeps the model's own colours.
            public Color? baseColor;
        }

        public static string DefinitionPath(string id) => ChampionDir + "/Champion_" + id + ".asset";
        public static string PortraitPath(string id) => PortraitDir + "/Portrait_" + id + ".png";

        /// "Dark Sorceress!" -> "DarkSorceress": letters and digits only, so it is safe in asset
        /// names and as the saved pick.
        public static string MakeId(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new System.Text.StringBuilder(name.Length);
            bool upper = true;
            foreach (char ch in name)
            {
                if (char.IsLetterOrDigit(ch)) { sb.Append(upper ? char.ToUpperInvariant(ch) : ch); upper = false; }
                else upper = true;
            }
            return sb.ToString();
        }

        /// Runs the whole pipeline; returns the definition and a human-readable report. Throws
        /// with a clear message on any bad input (the window shows it).
        public static ChampionDefinition Create(in Request req, out string report)
        {
            if (string.IsNullOrEmpty(req.id) || MakeId(req.id) != req.id)
                throw new System.ArgumentException("Id '" + req.id + "' must be letters and digits only (e.g. " + MakeId(req.id) + ").");
            if (!(AssetImporter.GetAtPath(req.modelPath) is ModelImporter))
                throw new System.ArgumentException("Not a model file: " + req.modelPath);

            // Before the prefab: extracting materials reimports the model.
            string textureReport = ChampionTextures.Apply(req.modelPath, req.id, req.baseColor);
            var prefab = FighterBuilder.Build(req.id, req.modelPath, req.hidePrefixes, out var profile, out string clipReport);

            var portrait = req.portrait;
            string portraitNote = "given";
            if (portrait == null)
            {
                // A failed render must not lose the champion: the portrait can be assigned by hand.
                try { portrait = PortraitRenderer.Render(prefab, PortraitPath(req.id)); }
                catch (System.Exception e) { Debug.LogWarning("Portrait render failed for " + req.id + ": " + e.Message); }
                portraitNote = portrait != null ? "rendered to " + PortraitPath(req.id) : "could not render (assign one by hand)";
            }

            EnsureFolder(ChampionDir);
            var def = GetOrCreateAsset<ChampionDefinition>(DefinitionPath(req.id));
            def.id = req.id;
            def.displayName = req.displayName;
            def.originId = req.originId;
            def.classTag = req.classTag;
            def.cardFinish = req.finish;
            def.stats = req.stats;
            def.portrait = portrait;
            def.fighterPrefab = prefab;
            def.rigProfile = profile;
            def.hiddenProps = req.hidePrefixes ?? new string[0];
            EditorUtility.SetDirty(def);

            var roster = AddToRoster(def, out bool added);
            bool wired = WireRoster(roster);
            AssetDatabase.SaveAssets();

            report = "Champion " + req.id + " (" + def.ShownName + ")\n" +
                     "Prefab: " + AssetDatabase.GetAssetPath(prefab) + "\n" +
                     "Portrait: " + portraitNote + "\n" +
                     "Roster: " + (added ? "added" : "already listed") + " (" + roster.Count + " champions)" +
                     (wired ? ", wired into GameConfig" : "") + "\n" +
                     textureReport +
                     "Clips:\n" + clipReport;
            return def;
        }

        [MenuItem("Tools/Slap Mania/Characters/Rebuild All Champion Fighters", priority = 42)]
        public static void RebuildAllMenu()
        {
            string report = RebuildAllFighters();
            EditorUtility.DisplayDialog("Slap Mania", report, "OK");
        }

        /// Rebuilds every roster champion's fighter prefab, controller and profile from its own
        /// model (after an animation-set change, ADR-0023). Textures, portraits, stats and attack
        /// tuning are kept. Returns a report, one line per champion.
        public static string RebuildAllFighters()
        {
            var roster = LoadOrCreateRoster();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < roster.Count; i++)
            {
                var def = roster.Get(i);
                if (def == null || def.fighterPrefab == null) continue;
                var model = def.fighterPrefab.transform.Find("Model");
                var source = model != null ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(model.gameObject) : null;
                string modelPath = source != null ? AssetDatabase.GetAssetPath(source) : null;
                if (string.IsNullOrEmpty(modelPath)) { sb.Append(def.id).Append(": no model found, skipped\n"); continue; }
                def.fighterPrefab = FighterBuilder.Build(def.id, modelPath, def.hiddenProps, out var profile, out _);
                def.rigProfile = profile;
                EditorUtility.SetDirty(def);
                sb.Append(def.id).Append(": rebuilt from ").Append(modelPath).Append('\n');
            }
            AssetDatabase.SaveAssets();
            return sb.Length > 0 ? sb.ToString() : "No champions in the roster.";
        }

        public static ChampionRoster LoadOrCreateRoster()
        {
            EnsureFolder(ChampionDir);
            return GetOrCreateAsset<ChampionRoster>(RosterPath);
        }

        private static ChampionRoster AddToRoster(ChampionDefinition def, out bool added)
        {
            var roster = LoadOrCreateRoster();
            added = false;
            if (System.Array.IndexOf(roster.champions, def) >= 0) return roster;
            var list = new System.Collections.Generic.List<ChampionDefinition>(roster.champions);
            list.RemoveAll(c => c == null || c.id == def.id); // a stale copy under the same id
            list.Add(def);
            roster.champions = list.ToArray();
            EditorUtility.SetDirty(roster);
            added = true;
            return roster;
        }

        /// Points GameConfig.roster at the roster when it points nowhere. Never replaces a
        /// roster someone chose by hand. Returns whether it changed GameConfig.
        private static bool WireRoster(ChampionRoster roster)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (cfg == null || cfg.roster != null) return false;
            cfg.roster = roster;
            EditorUtility.SetDirty(cfg);
            return true;
        }
    }
}
