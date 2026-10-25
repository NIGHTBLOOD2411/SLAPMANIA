using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SlapMania.Data;
using SlapMania.Presentation;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// Builds a playable humanoid fighter from the Alucard FBX set (M08):
    ///   1. Humanoid import (FighterImportSetup) — skipped when already correct.
    ///   2. Measures each attack clip's contact: the time the striking hand is furthest forward,
    ///      sampled from the clip itself, and which hand that is.
    ///   3. Animator Controller: Idle / Charge / Attack_* / Hit / Block / Victory, IK pass on,
    ///      an AttackSpeed parameter so a windup can be fitted to the FSM's impact frame.
    ///   4. FighterRigProfile asset (created once — tuning survives rebuilds).
    ///   5. Ragdoll: 11 bodies, colliders and CharacterJoints, kinematic until the KO.
    ///   6. Prefab: model + Animator + SlapIK + HitReactor + HumanoidFighterView + ragdoll,
    ///      with the sword hidden.
    /// The controller and prefab are generated artifacts and are rebuilt every run.
    public static class FighterBuilder
    {
        private const string AnimDir = "Assets/_Project/Animation/Fighter";
        private const string PrefabDir = "Assets/_Project/Prefabs/Fighters";
        private const string DataDir = "Assets/_Project/Data/Prototype";
        public const string ControllerPath = AnimDir + "/Fighter_Alucard.controller";
        public const string PrefabPath = PrefabDir + "/Fighter_Alucard.prefab";
        public const string ProfilePath = DataDir + "/Fighter_Alucard_Rig.asset";

        private struct AttackSpec { public string Clip; public string State; }

        private static readonly AttackSpec[] Attacks =
        {
            new AttackSpec { Clip = "Hook Punch",  State = "Attack_Hook" },
            new AttackSpec { Clip = "Cross Punch", State = "Attack_Cross" },
        };

        [MenuItem("Tools/Slap Mania/Characters/Build Alucard Fighter Prefab", priority = 41)]
        public static void BuildMenu()
        {
            var prefab = BuildAlucard();
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("Fighter built", "Prefab: " + PrefabPath + "\nProfile: " + ProfilePath, "OK");
        }

        /// Returns the built prefab asset. Throws with a clear message on any missing input.
        public static GameObject BuildAlucard()
        {
            string basePath = FighterImportSetup.FindBaseModel(FighterImportSetup.AlucardStem);
            if (!HumanoidReady(basePath)) FighterImportSetup.SetUpHumanoidSet(basePath, FighterImportSetup.AlucardClipFolder);
            var avatar = FighterImportSetup.LoadAvatar(basePath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (model == null || avatar == null || !avatar.isHuman)
                throw new System.InvalidOperationException("Alucard base model or humanoid Avatar missing at " + basePath);

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var name in new[] { "Bouncing Fight Idle", "Boxing", "Boxing (1)", "Hook Punch", "Cross Punch",
                                          "Receive Uppercut To The Face", "Inward Block" })
            {
                var clip = FighterImportSetup.LoadClip(FighterImportSetup.AlucardClipPath(name), name);
                if (clip == null) throw new System.InvalidOperationException("Clip '" + name + "' not found - run Tools > Slap Mania > Characters > Set Up Alucard As Humanoid.");
                clips[name] = clip;
            }

            EnsureFolder(AnimDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);

            var controller = BuildController(clips);
            var profile = BuildProfile(model, avatar, clips);
            return BuildPrefab(model, avatar, controller, profile);
        }

        private static bool HumanoidReady(string basePath)
        {
            var imp = AssetImporter.GetAtPath(basePath) as ModelImporter;
            var avatar = FighterImportSetup.LoadAvatar(basePath);
            if (imp == null || imp.animationType != ModelImporterAnimationType.Human || avatar == null || !avatar.isValid) return false;
            foreach (var hb in imp.humanDescription.human)       // S-072: Hips must be the pelvis bone
                if (hb.humanName == "Hips" && !(hb.boneName == "Hips" || hb.boneName.EndsWith(":Hips"))) return false;
            foreach (var a in Attacks)
            {
                string path = FighterImportSetup.AlucardClipPath(a.Clip);
                if (FighterImportSetup.LoadClip(path, a.Clip) == null) return false;
                // Re-run the setup when the clip settings predate the centre-of-mass / feet fix.
                var clipImp = AssetImporter.GetAtPath(path) as ModelImporter;
                var settings = clipImp != null ? clipImp.clipAnimations : null;
                if (settings == null || settings.Length == 0 || settings[0].keepOriginalPositionXZ || !settings[0].heightFromFeet) return false;
            }
            return true;
        }

        // ── Animator Controller ──────────────────────────────────────
        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            c.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var layers = c.layers;
            layers[0].iKPass = true; // OnAnimatorIK is only called with this on
            c.layers = layers;
            var sm = c.layers[0].stateMachine;

            var idle = State(sm, "Idle", clips["Bouncing Fight Idle"], new Vector3(300f, 0f));
            sm.defaultState = idle;
            State(sm, "Charge", clips["Boxing"], new Vector3(300f, 80f));
            State(sm, "Victory", clips["Boxing (1)"], new Vector3(300f, 160f));
            var hit = State(sm, "Hit", clips["Receive Uppercut To The Face"], new Vector3(560f, 0f));
            var block = State(sm, "Block", clips["Inward Block"], new Vector3(560f, 80f));
            ReturnToIdle(hit, idle, 0.85f);
            ReturnToIdle(block, idle, 0.9f);

            float y = 240f;
            foreach (var a in Attacks)
            {
                var s = State(sm, a.State, clips[a.Clip], new Vector3(560f, y));
                s.speedParameterActive = true;
                s.speedParameter = "AttackSpeed";
                ReturnToIdle(s, idle, 0.92f);
                y += 80f;
            }
            AssetDatabase.SaveAssets();
            return c;
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
        {
            var s = sm.AddState(name, pos);
            s.motion = clip;
            s.writeDefaultValues = true;
            return s;
        }

        private static void ReturnToIdle(AnimatorState from, AnimatorState idle, float exitTime)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.2f;
            t.hasFixedDuration = true;
        }

        // ── Profile + contact measurement ───────────────────────────
        private static FighterRigProfile BuildProfile(GameObject model, Avatar avatar, Dictionary<string, AnimationClip> clips)
        {
            var profile = AssetDatabase.LoadAssetAtPath<FighterRigProfile>(ProfilePath);
            bool created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<FighterRigProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            if (created || profile.attacks == null || profile.attacks.Length == 0)
            {
                var moves = new FighterRigProfile.AttackMove[Attacks.Length];
                for (int i = 0; i < Attacks.Length; i++)
                {
                    MeasureContact(model, avatar, clips[Attacks[i].Clip], out float seconds, out bool right);
                    moves[i] = new FighterRigProfile.AttackMove
                    {
                        state = Attacks[i].State,
                        contactMs = Mathf.RoundToInt(seconds * 1000f),
                        rightHand = right,
                    };
                }
                profile.attacks = moves;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            return profile;
        }

        /// Samples the clip on a temporary copy of the model and returns when (and with which
        /// hand) a hand is furthest forward of the hips — the moment it would meet a face.
        public static void MeasureContact(GameObject model, Avatar avatar, AnimationClip clip, out float seconds, out bool rightHand)
        {
            seconds = clip.length * 0.5f;
            rightHand = true;
            var go = (GameObject)Object.Instantiate(model);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.avatar = avatar;
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (hips == null || lh == null || rh == null) return;

                AnimationMode.StartAnimationMode();
                float best = float.NegativeInfinity;
                const int samples = 90;
                for (int i = 0; i <= samples; i++)
                {
                    float t = clip.length * i / samples;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, t);
                    AnimationMode.EndSampling();
                    Vector3 fwd = go.transform.forward;
                    float l = Vector3.Dot(lh.position - hips.position, fwd);
                    float r = Vector3.Dot(rh.position - hips.position, fwd);
                    if (l > best) { best = l; seconds = t; rightHand = false; }
                    if (r > best) { best = r; seconds = t; rightHand = true; }
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
        }

        // ── Prefab ───────────────────────────────────────────────────
        private static GameObject BuildPrefab(GameObject model, Avatar avatar, AnimatorController controller, FighterRigProfile profile)
        {
            var root = new GameObject("Fighter_Alucard");
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.name = "Model";
                body.transform.SetParent(root.transform, false);

                var anim = body.GetComponent<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var ik = body.AddComponent<SlapIK>();
                ik.Profile = profile;
                var reactor = body.AddComponent<HitReactor>();
                reactor.Profile = profile;

                var hidden = new List<Object>();
                foreach (var t in body.GetComponentsInChildren<Transform>(true))
                    if (t.name.Trim().StartsWith("sword", System.StringComparison.OrdinalIgnoreCase)) hidden.Add(t.gameObject);

                var ragdoll = root.AddComponent<RagdollController>();
                RagdollBuilder.Build(anim, ragdoll);

                var view = root.AddComponent<HumanoidFighterView>();
                Set(view, "_animator", anim);
                Set(view, "_profile", profile);
                Set(view, "_ik", ik);
                Set(view, "_hitReactor", reactor);
                Set(view, "_ragdoll", ragdoll);
                SetArray(view, "_hidden", hidden.ToArray());

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok || prefab == null) throw new System.IO.IOException("Could not save " + PrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
