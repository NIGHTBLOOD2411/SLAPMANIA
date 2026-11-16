using System.IO;
using UnityEditor;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;

namespace SlapMania.Editor.Characters
{
    /// Tools > Slap Mania > Characters > Champion Creator (M08). Drop a rigged FBX in, name the
    /// champion, set its card and stats, press Create: the result is a fighter prefab, a
    /// portrait, a ChampionDefinition and a roster entry - selectable on the Roster tab and
    /// spawned by the bout. Picking an FBX that already has a champion loads its values, so the
    /// same window re-builds or re-tunes it.
    public sealed class ChampionCreatorWindow : EditorWindow
    {
        private GameObject _model;
        private string _displayName = "";
        private string _id = "";
        private bool _idEdited;
        private string _origin = "";
        private string _classTag = "";
        private string _hide = "";
        private bool _tint;
        private Color _skin = new Color(0.72f, 0.55f, 0.42f, 1f);
        private Texture2D _portrait;
        private ChampionCardFinish _finish = ChampionCardFinish.Gold;
        private ChampionStats _stats = ChampionStats.Neutral;
        private string _report = "";
        private Vector2 _scroll;

        [MenuItem("Tools/Slap Mania/Characters/Champion Creator", priority = 30)]
        public static void Open()
        {
            var w = GetWindow<ChampionCreatorWindow>("Champion Creator");
            w.minSize = new Vector2(380f, 560f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "1. Import your rigged FBX anywhere under Assets/_Project.\n" +
                "2. Drop it below. It is made Humanoid and plays the authored slap moves.\n" +
                "3. Optional: put '<FBX name>@Idle.fbx', '@Charge', '@Hit', '@Block', '@Victory', " +
                "'@Slap_Right' or '@Slap_Left' next to it to override a move.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _model = (GameObject)EditorGUILayout.ObjectField("Rigged FBX", _model, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) OnModelChanged();

            string modelPath = _model != null ? AssetDatabase.GetAssetPath(_model) : "";
            bool isModel = AssetImporter.GetAtPath(modelPath) is ModelImporter;
            if (_model != null && !isModel) EditorGUILayout.HelpBox("That is not a model file (.fbx).", MessageType.Error);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Card", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _displayName = EditorGUILayout.TextField("Display name", _displayName);
            if (EditorGUI.EndChangeCheck() && !_idEdited) _id = ChampionCreator.MakeId(_displayName);
            EditorGUI.BeginChangeCheck();
            _id = EditorGUILayout.TextField(new GUIContent("Id", "Letters and digits. Names the assets and is saved as the player's pick."), _id);
            if (EditorGUI.EndChangeCheck()) _idEdited = true;
            _origin = EditorGUILayout.TextField(new GUIContent("Universe", "Shown in the bottom bar when selected."), _origin);
            _classTag = EditorGUILayout.TextField(new GUIContent("Class tag", "Under the overall, e.g. HVY."), _classTag);
            _finish = (ChampionCardFinish)EditorGUILayout.EnumPopup("Card finish", _finish);
            _portrait = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Portrait", "Empty: rendered from the model's head."), _portrait, typeof(Texture2D), false);
            _hide = EditorGUILayout.TextField(new GUIContent("Hide props", "Comma-separated name prefixes to switch off, e.g. sword, staff."), _hide);
            _tint = EditorGUILayout.Toggle(new GUIContent("Tint bare materials",
                "Materials with no texture get images found next to the FBX, or a generated detail set. " +
                "On: they also take this colour. Off: they keep the FBX's colour."), _tint);
            using (new EditorGUI.DisabledScope(!_tint)) _skin = EditorGUILayout.ColorField("Base colour", _skin);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stats (75 = neutral)", EditorStyles.boldLabel);
            _stats.power = Stat("PWR  damage dealt", _stats.power);
            _stats.chin = Stat("CHN  damage taken", _stats.chin);
            _stats.stamina = Stat("STA  max health", _stats.stamina);
            _stats.speed = Stat("SPD  (card only)", _stats.speed);
            _stats.technique = Stat("TEC  (card only)", _stats.technique);
            _stats.rage = Stat("RAG  (card only)", _stats.rage);
            EditorGUILayout.LabelField("Overall", _stats.Overall.ToString());

            EditorGUILayout.Space();
            string idError = string.IsNullOrEmpty(_id) ? "Needs an id." :
                             ChampionCreator.MakeId(_id) != _id ? "Id: letters and digits only." : null;
            if (idError != null && _model != null) EditorGUILayout.HelpBox(idError, MessageType.Warning);
            bool exists = !string.IsNullOrEmpty(_id) && AssetDatabase.LoadAssetAtPath<ChampionDefinition>(ChampionCreator.DefinitionPath(_id)) != null;

            using (new EditorGUI.DisabledScope(!isModel || idError != null))
            {
                if (GUILayout.Button(exists ? "Rebuild Champion" : "Create Champion", GUILayout.Height(32f))) Create(modelPath);
            }
            if (GUILayout.Button("Select Roster")) Selection.activeObject = ChampionCreator.LoadOrCreateRoster();

            if (!string.IsNullOrEmpty(_report)) EditorGUILayout.HelpBox(_report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private static string[] SplitList(string csv)
        {
            var parts = (csv ?? "").Split(',');
            var list = new System.Collections.Generic.List<string>(parts.Length);
            foreach (var p in parts) if (!string.IsNullOrWhiteSpace(p)) list.Add(p.Trim());
            return list.ToArray();
        }

        private static int Stat(string label, int v) => EditorGUILayout.IntSlider(label, v, ChampionRules.StatMin, ChampionRules.StatMax);

        // A model that already has a champion loads that champion's values.
        private void OnModelChanged()
        {
            _report = "";
            if (_model == null) return;
            string stem = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(_model));
            var existing = FindChampionFor(_model);
            if (existing != null)
            {
                _id = existing.id; _idEdited = true;
                _displayName = existing.displayName;
                _origin = existing.originId;
                _classTag = existing.classTag;
                _finish = existing.cardFinish;
                _stats = existing.stats;
                _portrait = existing.portrait;
                _hide = existing.hiddenProps != null ? string.Join(", ", existing.hiddenProps) : "";
                _report = "Loaded existing champion " + existing.id + ".";
                return;
            }
            bool alucard = stem == FighterImportSetup.AlucardStem; // keep the bout scene's Fighter_Alucard paths
            _displayName = alucard ? FighterBuilder.AlucardId : stem;
            _id = alucard ? FighterBuilder.AlucardId : ChampionCreator.MakeId(stem);
            _hide = alucard ? "sword" : "";
            _idEdited = false;
            _portrait = null;
        }

        private static ChampionDefinition FindChampionFor(GameObject model)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ChampionDefinition", new[] { ChampionCreator.ChampionDir }))
            {
                var def = AssetDatabase.LoadAssetAtPath<ChampionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.fighterPrefab == null) continue;
                var view = def.fighterPrefab.transform.Find("Model");
                if (view != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(view.gameObject) == model) return def;
            }
            return null;
        }

        private void Create(string modelPath)
        {
            var req = new ChampionCreator.Request
            {
                id = _id,
                displayName = string.IsNullOrEmpty(_displayName) ? _id : _displayName,
                originId = _origin,
                classTag = _classTag,
                modelPath = modelPath,
                hidePrefixes = SplitList(_hide),
                // A portrait the creator rendered earlier is re-rendered, not kept, so a rebuilt rig refreshes it.
                portrait = _portrait != null && AssetDatabase.GetAssetPath(_portrait) != ChampionCreator.PortraitPath(_id) ? _portrait : null,
                finish = _finish,
                baseColor = _tint ? _skin : (Color?)null,
                stats = _stats,
            };
            try
            {
                EditorUtility.DisplayProgressBar("Champion Creator", "Building " + _id + "...", 0.3f);
                var def = ChampionCreator.Create(req, out _report);
                _portrait = def.portrait;
                EditorGUIUtility.PingObject(def);
            }
            catch (System.Exception e)
            {
                _report = "Failed: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
