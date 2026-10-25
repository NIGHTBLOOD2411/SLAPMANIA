using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SlapMania.App;
using SlapMania.Core;
using SlapMania.Data;
using Object = UnityEngine.Object;

namespace SlapMania.Editor
{
    /// The Tuning Hub Inspector (M13): every designer-facing number for the bout in one place.
    ///   1. Rules & balance - the GameConfig assets, drawn inline with their own inspectors.
    ///      Edits persist after Play Mode, so you can tune while playing and keep the result.
    ///   2. Scene feel - camera, lights, fighter views, callouts. These revert after Play Mode.
    ///   3. Live - the FSM state, HP, power and marker while playing.
    /// A new module joins by adding its config to GameConfig plus one Section line in
    /// DrawRules, or by adding its scene component to the hub's _sceneComponents list.
    [CustomEditor(typeof(TuningHub))]
    public sealed class TuningHubEditor : UnityEditor.Editor
    {
        private readonly Dictionary<Object, UnityEditor.Editor> _editors = new Dictionary<Object, UnityEditor.Editor>();

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_config"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_bout"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_extraAssets"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sceneComponents"), true);
            serializedObject.ApplyModifiedProperties();

            var hub = (TuningHub)target;
            if (Application.isPlaying) DrawLive(hub);
            DrawRules(hub.Config);
            DrawFrontEnd(hub.Config);
            DrawExtras(hub.ExtraAssets);
            DrawScene(hub.SceneComponents);
        }

        private SerializedObject _configSo;

        /// GameConfig's own front-end switch (M10): which Design-canvas page the menus show.
        private void DrawFrontEnd(GameConfig cfg)
        {
            if (cfg == null) return;
            Header("FRONT END", "Which Design-canvas page the menus show (V2, or the earlier V1). Takes effect the next time the menus open.", MessageType.Info);
            if (_configSo == null || _configSo.targetObject != cfg) _configSo = new SerializedObject(cfg);
            _configSo.Update();
            EditorGUILayout.PropertyField(_configSo.FindProperty("frontEndPage"), new GUIContent("Menu page"));
            _configSo.ApplyModifiedProperties();
        }

        private void DrawRules(GameConfig cfg)
        {
            Header("RULES & BALANCE", "Asset values - edits persist after Play Mode.", MessageType.Info);
            if (cfg == null)
            {
                EditorGUILayout.HelpBox("No GameConfig assigned. Run Tools > Slap Mania > Prototype > Build Playable Prototype.", MessageType.Warning);
                return;
            }
            Section("Bout - HP, turn order, pacing (M01)", cfg.bout);
            Section("Charge - power phase (M03)", cfg.powerPhase);
            Section("Timing - marker speed and windows (M04)", cfg.timingPhase);
            Section("Timing - zone layout and multipliers (M04)", cfg.timingZoneLayout);
            Section("Hold ring - V2 attack: close time, wind-up (M04 §5b; Bout > attackMode picks it)", cfg.holdRing);
            Section("Damage - formula, crit, finisher (M05)", cfg.damage);
            Section("Feel - hitstop, recoil, callouts (M08/M10)", cfg.feel);
            if (cfg.aiProfiles != null)
                for (int i = 0; i < cfg.aiProfiles.Length; i++)
                    Section("Opponent AI - ladder tier " + (i + 1) + " (M07 §4b)", cfg.aiProfiles[i]);
        }

        private void DrawExtras(ScriptableObject[] assets)
        {
            if (assets == null || assets.Length == 0) return;
            Header("FIGHTERS & MORE", "Asset values - edits persist after Play Mode. Rig profile: animation, IK, hit spring, ragdoll.", MessageType.Info);
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] != null) Section(ObjectNames.NicifyVariableName(assets[i].name), assets[i]);
        }

        private void DrawScene(Component[] components)
        {
            Header("SCENE FEEL", "Component values - these revert when Play Mode ends. Note a good value, then set it in Edit Mode.", MessageType.Warning);
            if (components == null) return;
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;
                Section(c.gameObject.name + " - " + ObjectNames.NicifyVariableName(c.GetType().Name), c);
            }
        }

        private static void DrawLive(TuningHub hub)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LIVE", EditorStyles.boldLabel);
            var d = hub.Bout != null ? hub.Bout.Director : null;
            if (d == null)
            {
                EditorGUILayout.HelpBox("No running bout.", MessageType.None);
                return;
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var t = d.CurrentTurn;
                EditorGUILayout.LabelField("State", d.CurrentState.ToString());
                EditorGUILayout.LabelField("Turn", "slap " + (t.SlapIndex + 1) + " - round " + (t.RoundIndex + 1) + " - " + t.Attacker + " attacking");
                EditorGUILayout.LabelField("HP", "You " + d.PlayerHealth.CurrentHp + " / Opponent " + d.OpponentHealth.CurrentHp);
                if (d.CurrentState == MatchState.Charge)
                    EditorGUILayout.LabelField("Power fill", d.PowerMeter.Fill.ToString("0.00") + "  (taps " + d.PowerMeter.TapsAccepted + ", rejected " + d.PowerMeter.TapsRejected + ")");
                if (d.CurrentState == MatchState.Timing)
                    EditorGUILayout.LabelField("Marker", d.TimingMeter.NormalizedPosition.ToString("0.000"));
                EditorGUILayout.LabelField("Attack mode", d.CurrentAttackMode.ToString());
                if (d.CurrentState == MatchState.HoldRing)
                {
                    var r = d.HoldRingMeter;
                    EditorGUILayout.LabelField("Hold ring", r.CurrentState + "  closure " + r.Closure.ToString("0.000")
                        + "  wind-up " + r.WindUp01.ToString("0.00") + "  power " + r.Power.ToString("0.00")
                        + "  (closes in " + (r.CloseSeconds * 1000.0).ToString("0") + " ms)");
                }
            }
        }

        private static void Header(string title, string note, MessageType type)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(note, type);
        }

        private void Section(string label, Object obj)
        {
            if (obj == null)
            {
                EditorGUILayout.HelpBox(label + ": not assigned.", MessageType.Warning);
                return;
            }
            string key = "SlapMania.TuningHub." + label;
            bool open = SessionState.GetBool(key, false);
            bool now = EditorGUILayout.Foldout(open, label, true, EditorStyles.foldoutHeader);
            if (now != open) SessionState.SetBool(key, now);
            if (!now) return;

            _editors.TryGetValue(obj, out var ed);
            UnityEditor.Editor.CreateCachedEditor(obj, null, ref ed);
            _editors[obj] = ed;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(obj, obj.GetType(), false);
                ed.OnInspectorGUI();
            }
        }

        private void OnDisable()
        {
            foreach (var ed in _editors.Values)
                if (ed != null) DestroyImmediate(ed);
            _editors.Clear();
        }
    }
}
