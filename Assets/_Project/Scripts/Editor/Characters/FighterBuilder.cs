using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SlapMania.Data;
using SlapMania.Presentation;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// Builds a playable humanoid fighter from any rigged FBX (M08). Alucard is one champion
    /// built this way; the Champion Creator builds the rest.
    ///   1. Humanoid import of the base model (FighterImportSetup) - skipped when already correct.
    ///   2. Clips: every role (Idle, Charge, Hit, Block, Victory, two slaps) uses the authored
    ///      slap set (SlapClipAuthor, ADR-0023), retargeted through Humanoid, unless the base
    ///      model's folder has an override "<stem>@<State>.fbx".
    ///   3. Attack contact: the authored slap's contact is exact (SlapClipAuthor.ContactSeconds);
    ///      an override clip is measured on THIS rig (the striking hand furthest forward).
    ///   4. Animator Controller: Idle / Charge / Slap_Right / Slap_Left (the same clip, mirrored)
    ///      / Hit / Block / Victory, IK pass on, an AttackSpeed parameter so a windup can be
    ///      fitted to the FSM's impact frame.
    ///   5. FighterRigProfile asset (created once - tuning survives rebuilds).
    ///   6. Ragdoll: 11 bodies, colliders and CharacterJoints, kinematic until the KO.
    ///   7. Prefab: model + Animator + SlapIK + PhysicalAnimationLayer + HumanoidFighterView + ragdoll,
    ///      with props whose names start with a hide prefix switched off (Alucard's sword).
    /// The controller and prefab are generated artifacts and are rebuilt every run.
    public static class FighterBuilder
    {
        private const string AnimDir = "Assets/_Project/Animation/Fighter";
        private const string PrefabDir = "Assets/_Project/Prefabs/Fighters";
        private const string DataDir = "Assets/_Project/Data/Prototype";
        public const string AlucardId = "Alucard";
        public static readonly string ControllerPath = ControllerPathFor(AlucardId);
        public static readonly string PrefabPath = PrefabPathFor(AlucardId);
        public static readonly string ProfilePath = ProfilePathFor(AlucardId);

        public static string ControllerPathFor(string id) => AnimDir + "/Fighter_" + id + ".controller";
        public static string PrefabPathFor(string id) => PrefabDir + "/Fighter_" + id + ".prefab";
        public static string ProfilePathFor(string id) => DataDir + "/Fighter_" + id + "_Rig.asset";

        /// One animation role: the Animator state it becomes and the authored slap clip that fills
        /// it (SlapClipAuthor, ADR-0023). `Mirror` plays the clip mirrored: the left-hand slap is
        /// the forehand on a mirrored state.
        /// `Mocap` is the role in MocapSelection (ADR-0024); a role with no pick plays the authored clip.
        private struct Role { public string State; public string Clip; public string Mocap; public bool Attack; public bool Mirror; }

        private static readonly Role[] Roles =
        {
            new Role { State = "Idle",       Clip = SlapClipAuthor.Idle,     Mocap = "Idle" },
            new Role { State = "Charge",     Clip = SlapClipAuthor.Charge,   Mocap = "Charge" },
            new Role { State = "Victory",    Clip = SlapClipAuthor.Victory,  Mocap = "Victory" },
            new Role { State = "Hit",        Clip = SlapClipAuthor.Hit,      Mocap = "Hit" },
            new Role { State = "Block",      Clip = SlapClipAuthor.Block,    Mocap = "Block" },
            new Role { State = "Slap_Right", Clip = SlapClipAuthor.Forehand, Mocap = "Slap", Attack = true },
            new Role { State = "Slap_Left",  Clip = SlapClipAuthor.Forehand, Mocap = "Slap", Attack = true, Mirror = true },
        };

        // Seconds the mocap Charge (a one-shot wind-up) is stretched over: about the charge phase.
        private const float MocapChargeSeconds = 2.2f;
        // A mocap slap ends with the body turned far past square: the way back to Idle needs longer.
        private const float MocapAttackReturnSeconds = 0.45f;

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
            return Build(AlucardId, basePath, new[] { "sword" }, out _, out _);
        }

        /// Builds the fighter `id` from the rigged model at basePath. `hidePrefixes` names props
        /// to switch off (case-insensitive name prefixes). `report` lists where every clip came
        /// from. Throws with a clear message on any missing input.
        public static GameObject Build(string id, string basePath, string[] hidePrefixes,
                                       out FighterRigProfile profile, out string report)
        {
            if (string.IsNullOrEmpty(id)) throw new System.ArgumentException("Fighter id is empty.");
            if (!FighterImportSetup.BaseReady(basePath)) FighterImportSetup.SetUpHumanoidBase(basePath);
            var avatar = FighterImportSetup.LoadAvatar(basePath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (model == null || avatar == null || !avatar.isHuman)
                throw new System.InvalidOperationException("Base model or humanoid Avatar missing at " + basePath);

            var clips = ResolveClips(basePath, avatar, out report, out var overridden);

            EnsureFolder(AnimDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);

            var controller = BuildController(ControllerPathFor(id), clips, overridden);
            profile = BuildProfile(ProfilePathFor(id), model, avatar, clips, overridden);
            return BuildPrefab(id, model, avatar, controller, profile, hidePrefixes);
        }

        // ── Clips ────────────────────────────────────────────────────
        /// State name -> clip. An override "<stem>@<State>.fbx" next to the base model wins;
        /// otherwise the authored slap clip, retargeted by the Humanoid Avatar. Overridden states
        /// are recorded in `overridden` so their contact is measured rather than assumed.
        private static Dictionary<string, AnimationClip> ResolveClips(string basePath, Avatar avatar, out string report,
                                                                      out HashSet<string> overridden)
        {
            string stem = Path.GetFileNameWithoutExtension(basePath);
            string folder = Path.GetDirectoryName(basePath).Replace('\\', '/');
            var authored = SlapClipAuthor.BuildAll();
            var sb = new System.Text.StringBuilder();
            var clips = new Dictionary<string, AnimationClip>();
            overridden = new HashSet<string>();
            foreach (var r in Roles)
            {
                var clip = LoadOverride(folder, stem, r.State, avatar);
                if (clip != null)
                {
                    overridden.Add(r.State);
                    sb.Append("  ").Append(r.State).Append(": override ").Append(clip.name).Append('\n');
                }
                else if ((clip = MocapSelection.Load(r.Mocap)) != null)
                {
                    overridden.Add(r.State);   // its contact is measured, not assumed
                    sb.Append("  ").Append(r.State).Append(": mocap ").Append(AssetDatabase.GetAssetPath(clip))
                      .Append(r.Mirror ? " (mirrored)" : "").Append('\n');
                }
                else
                {
                    clip = authored[r.Clip];
                    sb.Append("  ").Append(r.State).Append(": authored ").Append(r.Clip).Append(r.Mirror ? " (mirrored)" : "").Append('\n');
                }
                clips[r.State] = clip;
            }
            report = sb.ToString();
            return clips;
        }

        private static AnimationClip LoadOverride(string folder, string stem, string clipName, Avatar avatar)
        {
            string path = folder + "/" + stem + "@" + clipName + ".fbx";
            if (AssetImporter.GetAtPath(path) is ModelImporter imp)
            {
                // Configure once: an override already bound to this Avatar is left alone.
                if (imp.sourceAvatar != avatar || FighterImportSetup.LoadClip(path, clipName) == null)
                    FighterImportSetup.ConfigureClipFile(path, clipName, avatar);
                return FighterImportSetup.LoadClip(path, clipName);
            }
            return null;
        }

        // ── Animator Controller ──────────────────────────────────────
        private static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips, HashSet<string> overridden)
        {
            // Rebuilt in place, never deleted (S-104): a delete + create at the same path reuses the
            // GUID, and any prefab already loaded keeps a pointer to the destroyed controller.
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (c == null) c = AnimatorController.CreateAnimatorControllerAtPath(path);
            else
            {
                while (c.layers.Length > 0) c.RemoveLayer(0);
                foreach (var p in c.parameters) c.RemoveParameter(p);
                c.AddLayer("Base Layer");
            }
            c.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var layers = c.layers;
            layers[0].iKPass = true; // OnAnimatorIK is only called with this on
            c.layers = layers;
            var sm = c.layers[0].stateMachine;

            var idle = State(sm, "Idle", clips["Idle"], new Vector3(300f, 0f));
            sm.defaultState = idle;
            var charge = State(sm, "Charge", clips["Charge"], new Vector3(300f, 80f));
            // A captured wind-up is a one-shot: spread it over the charge, then it holds at the load.
            if (overridden.Contains("Charge") && !clips["Charge"].isLooping)
                charge.speed = clips["Charge"].length / MocapChargeSeconds;
            State(sm, "Victory", clips["Victory"], new Vector3(300f, 160f));
            var hit = State(sm, "Hit", clips["Hit"], new Vector3(560f, 0f));
            var block = State(sm, "Block", clips["Block"], new Vector3(560f, 80f));
            ReturnToIdle(hit, idle, 0.85f);
            ReturnToIdle(block, idle, 0.9f);

            float y = 240f;
            foreach (var r in Roles)
            {
                if (!r.Attack) continue;
                var s = State(sm, r.State, clips[r.State], new Vector3(560f, y));
                s.mirror = r.Mirror;
                s.speedParameterActive = true;
                s.speedParameter = "AttackSpeed";
                ReturnToIdle(s, idle, 0.92f, overridden.Contains(r.State) ? MocapAttackReturnSeconds : 0.2f);
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

        private static void ReturnToIdle(AnimatorState from, AnimatorState idle, float exitTime, float seconds = 0.2f)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = seconds;
            t.hasFixedDuration = true;
        }

        // ── Profile + contact measurement ───────────────────────────
        private static FighterRigProfile BuildProfile(string path, GameObject model, Avatar avatar,
                                                      Dictionary<string, AnimationClip> clips, HashSet<string> overridden)
        {
            var profile = AssetDatabase.LoadAssetAtPath<FighterRigProfile>(path);
            bool created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<FighterRigProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            // The attacks are measured data, not tuning: re-derived on every build, so swapping a
            // clip (authored -> mocap, or a new override) never leaves a stale contact time.
            if (created || !AttacksMatchRoles(profile.attacks) || overridden.Count > 0 || AnyAuthoredContactDrift(profile.attacks, overridden))
            {
                var moves = new List<FighterRigProfile.AttackMove>();
                foreach (var r in Roles)
                {
                    if (!r.Attack) continue;
                    // The authored slap's contact is exact; an override clip is measured on this rig.
                    float seconds = SlapClipAuthor.ContactSeconds;
                    bool right = !r.Mirror;
                    if (overridden.Contains(r.State))
                    {
                        MeasureContact(model, avatar, clips[r.State], out seconds, out right);
                        if (r.Mirror) right = !right;
                    }
                    moves.Add(new FighterRigProfile.AttackMove
                    {
                        state = r.State,
                        contactMs = Mathf.RoundToInt(seconds * 1000f),
                        rightHand = right,
                    });
                }
                profile.attacks = moves.ToArray();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            return profile;
        }

        // An attack that now plays the authored clip but carries another contact (it was mocap).
        private static bool AnyAuthoredContactDrift(FighterRigProfile.AttackMove[] attacks, HashSet<string> overridden)
        {
            int authoredMs = Mathf.RoundToInt(SlapClipAuthor.ContactSeconds * 1000f);
            foreach (var a in attacks)
                if (!overridden.Contains(a.state) && a.contactMs != authoredMs) return true;
            return false;
        }

        private static bool AttacksMatchRoles(FighterRigProfile.AttackMove[] attacks)
        {
            if (attacks == null || attacks.Length == 0) return false;
            foreach (var a in attacks)
            {
                bool known = false;
                foreach (var r in Roles) if (r.Attack && r.State == a.state) { known = true; break; }
                if (!known) return false;
            }
            return true;
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
        private static GameObject BuildPrefab(string id, GameObject model, Avatar avatar, AnimatorController controller,
                                              FighterRigProfile profile, string[] hidePrefixes)
        {
            string prefabPath = PrefabPathFor(id);
            var root = new GameObject("Fighter_" + id);
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
                // The spring layer takes the hits (ADR-0024); HitReactor is no longer added.
                var physics = body.AddComponent<PhysicalAnimationLayer>();
                physics.Profile = profile;
                physics.StrikeIK = ik;

                var hidden = new List<Object>();
                if (hidePrefixes != null)
                    foreach (var t in body.GetComponentsInChildren<Transform>(true))
                        foreach (var prefix in hidePrefixes)
                            if (!string.IsNullOrWhiteSpace(prefix) && t != body.transform &&
                                t.name.Trim().StartsWith(prefix.Trim(), System.StringComparison.OrdinalIgnoreCase))
                            { hidden.Add(t.gameObject); break; }

                var ragdoll = root.AddComponent<RagdollController>();
                RagdollBuilder.Build(anim, ragdoll);

                var view = root.AddComponent<HumanoidFighterView>();
                Set(view, "_animator", anim);
                Set(view, "_profile", profile);
                Set(view, "_ik", ik);
                Set(view, "_physics", physics);
                Set(view, "_ragdoll", ragdoll);
                SetArray(view, "_hidden", hidden.ToArray());

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                if (!ok || prefab == null) throw new IOException("Could not save " + prefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
