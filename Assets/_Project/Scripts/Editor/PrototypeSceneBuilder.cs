using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SlapMania.App;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Presentation;
using SlapMania.Editor.Arena;
using static SlapMania.Editor.SceneBuildUtil;
using Object = UnityEngine.Object;

namespace SlapMania.Editor
{
    /// One click from an empty project to a playable bout (ROADMAP P5). Generates:
    ///   * the config assets under Data/Prototype/ plus Resources/GameConfig.asset (M00),
    ///   * the humanoid fighter prefab (Characters.FighterBuilder) — capsules if it cannot be built
    ///     (interactive only; BuildNonInteractive throws instead, S-085),
    ///   * Boot.unity - GameRoot only, loads the bout additively (ARCHITECTURE §3),
    ///   * Proto_Bout.unity - two fighters (the rival re-tinted) slapping across the podium of
    ///     the generated arena (Arena.ArenaBuilder: lights, crowd, jumbotron, post, colliders for
    ///     the KO ragdoll), a camera rig with the intro fly-in, the V2 HUD (Hud.V2HudBuilder), the
    ///     UI Toolkit overlay above it (pause menu, replay, round end, rank up), BoutController,
    ///     EventSystem (Input System UI module - UnityEngine.Input does not exist here), and a
    ///     Tuning Hub selected and ready,
    ///   * both scenes at the top of Build Settings.
    ///
    /// Scenes are generated artifacts and are rebuilt on every run. Config assets are created
    /// only when missing and a GameConfig reference is filled only when empty, so re-running
    /// never throws away tuning. Every wire goes through SceneBuildUtil.Set, which throws on a
    /// renamed field, so the scene cannot silently come out half-wired (R11).
    public static class PrototypeSceneBuilder
    {
        private const string Root          = "Assets/_Project";
        private const string ScenesDir     = Root + "/Scenes";
        private const string DataDir       = Root + "/Data/Prototype";
        private const string ResourcesDir  = Root + "/Resources";
        private const string MaterialsDir  = Root + "/Art/Prototype/Materials";

        public const string BootScenePath  = ScenesDir + "/Boot.unity";
        public const string BoutScenePath  = ScenesDir + "/Proto_Bout.unity";
        public const string BoutSceneName  = "Proto_Bout";

        private const string MenuRoot = "Tools/Slap Mania/Prototype/";

        // World layout. The player stands screen-right to match the V2 scorebug (YOU on the right).
        // Humanoids slap across the podium (ADR-0023). With the authored slap set and every
        // champion at one humanScale (FighterFit), nothing below the tabletop comes more than
        // 0.23 m forward of a fighter's root (Hikaru: 0.31 m for a few frames of recovery), and
        // the leaning contact pose reaches ~0.7 m. Roots 0.84 m apart leave 0.24 m between body
        // and table edge, put the rival's cheek (0.11 m in front of their root) at the hand, and
        // fit the 0.36 m podium between them. Re-measure if a clip or the target scale changes.
        public const float HumanoidX = 0.42f;
        private const float CapsuleX = 0.8f;
        // Side-on, face height: both fighters, the podium and a KO fall fit a 9:16 frame at
        // ~4.7 m, with the jumbotron in the upper third.
        private static readonly Vector3 CameraPosition = new Vector3(0f, 1.6f, -4.7f);
        private static readonly Vector3 CameraLookAt   = new Vector3(0f, 1.22f, 0f);
        private static readonly Vector3 CapsuleCameraPosition = new Vector3(0f, 2.2f, -8.4f);
        private static readonly Vector3 CapsuleCameraLookAt   = new Vector3(0f, 1.0f, 0f);
        // The intro establishing shot: wide, raised, podium lower-centre, jumbotron upper-right.
        private static readonly Vector3 IntroPosition = new Vector3(2.4f, 3.4f, -10.5f);
        private static readonly Vector3 IntroEuler    = new Vector3(9f, -11f, 0f);
        private const float CameraFov = 50f;
        // Above the V2 HUD canvas (10): the pause menu and rank-up cover the HUD and take its clicks.
        private const int OverlaySortingOrder = 20;
        private static string s_fighterNote;

        [MenuItem(MenuRoot + "Build Playable Prototype", priority = 0)]
        public static void Build() => Run(interactive: true);

        /// The same build with no dialogs, for the Unity MCP bridge, CI and batchmode
        /// (-executeMethod SlapMania.Editor.PrototypeSceneBuilder.BuildNonInteractive). Modal dialogs
        /// would block an unattended Editor, so this rebuilds without asking and throws instead:
        /// on missing TMP Essentials, on Play Mode, and on unsaved open scenes (never discards work).
        /// It also throws, before any scene is touched, when the humanoid fighter cannot be built:
        /// nobody reads the capsule-fallback warning in an unattended run (S-085).
        public static void BuildNonInteractive() => Run(interactive: false);

        private static void Run(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!interactive) throw new System.InvalidOperationException("Exit Play Mode before building the prototype.");
                EditorUtility.DisplayDialog("Slap Mania", "Exit Play Mode before building the prototype.", "OK");
                return;
            }
            if (!TmpEssentialsPresent())
            {
                if (!interactive)
                    throw new System.InvalidOperationException(
                        "TMP Essentials are missing. Run Window > TextMeshPro > Import TMP Essential Resources first.");
                if (EditorUtility.DisplayDialog("TextMeshPro Essentials needed",
                        "The HUD uses TextMeshPro, whose essential resources (default font + shaders) are not in this project yet.\n\n" +
                        "Click Import, press Import in the package dialog that follows, then run\n" +
                        "Tools > Slap Mania > Prototype > Build Playable Prototype again.",
                        "Import", "Cancel"))
                    EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
                return;
            }
            if (interactive)
            {
                bool scenesExist = File.Exists(BootScenePath) || File.Exists(BoutScenePath);
                if (scenesExist && !EditorUtility.DisplayDialog("Rebuild prototype scenes?",
                        "Boot.unity and Proto_Bout.unity are regenerated from code; manual edits to those two scenes are lost.\n\n" +
                        "Config assets (your tuned values) are kept.",
                        "Rebuild", "Cancel"))
                    return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }
            else
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                        throw new System.InvalidOperationException("An open scene has unsaved changes; save or discard them first.");
            }

            try
            {
                EditorUtility.DisplayProgressBar("Slap Mania", "Config assets", 0.1f);
                var cfg = BuildConfigAssets();
                EditorUtility.DisplayProgressBar("Slap Mania", "Bout scene", 0.4f);
                BuildBoutScene(cfg, allowCapsules: interactive);
                EditorUtility.DisplayProgressBar("Slap Mania", "Boot scene", 0.8f);
                BuildBootScene();
                AddScenesToBuildSettings();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                if (!interactive) throw;
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Prototype build failed", e.Message + "\n\nSee the Console for the stack trace.", "OK");
                return;
            }
            EditorUtility.ClearProgressBar();

            PlayFromBoot.Apply();
            EditorSceneManager.OpenScene(BoutScenePath, OpenSceneMode.Single);
            var hub = Object.FindAnyObjectByType<TuningHub>();
            if (hub != null) Selection.activeObject = hub.gameObject;
            if (!interactive) return;

            EditorUtility.DisplayDialog("Prototype built",
                (s_fighterNote != null ? s_fighterNote + "\n\n" : "") +
                "Press Play. With Play From Boot on (Tools > Slap Mania > Prototype), Play always starts at Boot, " +
                "which loads Proto_Bout additively.\n\n" +
                "Controls: touch / click / Space. Tuning Hub > Bout > Attack Mode picks the loop:\n" +
                "  HoldRing (default, V2): press and hold to wind up; the white ring closes over the\n" +
                "    rival's face - let go as it touches the gold band.\n" +
                "  TapMarker: mash to charge, then tap as the swinging ring touches gold.\n\n" +
                "The Tuning Hub is selected: every value in one Inspector, including the fighter rig " +
                "(IK, hit spring, ragdoll). Asset edits persist after Play.\n" +
                "Set the Game view to a portrait resolution (e.g. 1080x1920).",
                "Let's slap");
        }

        [MenuItem(MenuRoot + "Select Tuning Hub", priority = 1)]
        public static void SelectTuningHub()
        {
            var hub = Object.FindAnyObjectByType<TuningHub>();
            if (hub == null)
            {
                EditorUtility.DisplayDialog("Slap Mania", "No Tuning Hub in the open scenes. Open Proto_Bout (or build the prototype).", "OK");
                return;
            }
            Selection.activeObject = hub.gameObject;
            EditorGUIUtility.PingObject(hub.gameObject);
        }

        private static bool TmpEssentialsPresent() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        // ── Config assets ────────────────────────────────────────────
        private static GameConfig BuildConfigAssets()
        {
            EnsureFolder(DataDir);
            EnsureFolder(ResourcesDir);

            var bout   = GetOrCreateAsset<BoutConfig>(DataDir + "/Bout.asset");
            var power  = GetOrCreateAsset<PowerPhaseConfig>(DataDir + "/PowerPhase.asset");
            var timing = GetOrCreateAsset<TimingPhaseConfig>(DataDir + "/TimingPhase.asset");
            var zones  = GetOrCreateAsset<TimingZoneLayout>(DataDir + "/TimingZoneLayout.asset");
            var damage = GetOrCreateAsset<DamageConfig>(DataDir + "/Damage.asset");
            var ring   = GetOrCreateAsset<HoldRingConfig>(DataDir + "/HoldRing.asset");
            var feel   = GetOrCreateAsset<FeelConfig>(DataDir + "/Feel.asset");
            var ladder = EnsureAiLadder();

            // GameRoot loads this by name from Resources (M00 §4).
            var cfg = GetOrCreateAsset<GameConfig>(ResourcesDir + "/GameConfig.asset");
            if (cfg.bout == null)             cfg.bout = bout;
            if (cfg.powerPhase == null)       cfg.powerPhase = power;
            if (cfg.timingPhase == null)      cfg.timingPhase = timing;
            if (cfg.timingZoneLayout == null) cfg.timingZoneLayout = zones;
            if (cfg.damage == null)           cfg.damage = damage;
            if (cfg.holdRing == null)         cfg.holdRing = ring;
            if (cfg.feel == null)             cfg.feel = feel;
            // M10: the in-bout HUD's attention budget (POLISH §9), beside the front end's UiMotion.
            if (cfg.hudAttention == null)
            {
                EnsureFolder("Assets/_Project/Data/UI");
                cfg.hudAttention = GetOrCreateAsset<HudAttentionConfig>("Assets/_Project/Data/UI/HudAttention.asset");
            }
            // The AI ladder (M07 §4b, S-095). A config still on the single pre-ladder profile moves over.
            if (cfg.aiProfiles == null || cfg.aiProfiles.Length < 2) cfg.aiProfiles = ladder;
            EditorUtility.SetDirty(cfg);

            FrontEnd.FrontEndSceneBuilder.EnsureMotion(); // M10: the front end's motion asset
            if (!cfg.Validate(out string err)) throw new System.InvalidOperationException("GameConfig invalid after build: " + err);
            if (!cfg.timingZoneLayout.TilesUnitInterval(out string tileErr))
                throw new System.InvalidOperationException("TimingZoneLayout does not tile [0,1]: " + tileErr);
            return cfg;
        }

        // ── AI ladder (M07 §4b) ──────────────────────────────────────
        // name, taps/s, tps variance, fatigue, aim zone, aim sigma, reaction s, first-pass odds.
        // Rookie mashes slowly and aims at Weak; Champion is the old "too strong" opponent and more.
        private static readonly (string file, string name, float tps, float var, float fatigue, TimingZone aim,
                                 float sigma, float reaction, float firstPass)[] LadderTiers =
        {
            ("Ai_Tier1_Rookie",    "ROOKIE",    3.5f, 0.25f, 0.20f, TimingZone.Weak,    0.10f,  0.30f, 0.40f),
            ("Ai_Tier2_Brawler",   "BRAWLER",   4.5f, 0.20f, 0.14f, TimingZone.Good,    0.12f,  0.24f, 0.50f),
            ("Ai_Tier3_Contender", "CONTENDER", 5.5f, 0.15f, 0.10f, TimingZone.Good,    0.09f,  0.18f, 0.60f),
            ("Ai_Tier4_Pro",       "PRO",       6.5f, 0.12f, 0.06f, TimingZone.Great,   0.07f,  0.14f, 0.70f),
            ("Ai_Tier5_Champion",  "CHAMPION",  8.0f, 0.10f, 0.04f, TimingZone.Perfect, 0.045f, 0.12f, 0.80f),
        };

        /// Creates any missing tier asset with its ladder values; existing assets keep their
        /// (possibly hand-tuned) values. Easiest first.
        public static AiProfile[] EnsureAiLadder()
        {
            EnsureFolder(DataDir);
            var tiers = new AiProfile[LadderTiers.Length];
            for (int i = 0; i < LadderTiers.Length; i++)
            {
                var t = LadderTiers[i];
                string path = DataDir + "/" + t.file + ".asset";
                var p = AssetDatabase.LoadAssetAtPath<AiProfile>(path);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<AiProfile>();
                    p.displayName = t.name;
                    p.targetTps = t.tps;
                    p.tpsVariance = t.var;
                    p.tapFatigue = t.fatigue;
                    p.timingAimZone = t.aim;
                    p.aimSigma = t.sigma;
                    p.reactionDelay = t.reaction;
                    p.commitOnFirstPass = t.firstPass;
                    AssetDatabase.CreateAsset(p, path);
                }
                tiers[i] = p;
            }
            AssetDatabase.SaveAssets();
            return tiers;
        }

        // ── Bout scene ───────────────────────────────────────────────
        private static void BuildBoutScene(GameConfig cfg, bool allowCapsules)
        {
            EnsureFolder(ScenesDir);

            // The fighter first: it may reimport models, which must not happen mid-scene.
            GameObject fighterPrefab = null;
            s_fighterNote = null;
            try { fighterPrefab = Characters.FighterBuilder.BuildAlucard(); }
            catch (System.Exception e)
            {
                if (!allowCapsules)
                    throw new System.InvalidOperationException(
                        "Humanoid fighter could not be built, and an unattended build does not fall back to capsules (S-085): " + e.Message, e);
                s_fighterNote = "Humanoid fighter unavailable, using capsules: " + e.Message;
                Debug.LogWarning(s_fighterNote);
            }
            bool humanoid = fighterPrefab != null;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // A Single-mode scene switch unloads assets no scene references, which can leave the
            // C# handles loaded above pointing at destroyed objects; wiring one writes null.
            // Re-load by path now that the new scene exists.
            cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ResourcesDir + "/GameConfig.asset");
            if (cfg == null) throw new System.InvalidOperationException("GameConfig missing after scene switch.");
            if (humanoid)
            {
                // A prefab that was just built and then vanished is a bug, not a missing FBX:
                // never let it quietly become capsules.
                fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Characters.FighterBuilder.PrefabPath);
                if (fighterPrefab == null) throw new System.InvalidOperationException("Fighter prefab missing after scene switch: " + Characters.FighterBuilder.PrefabPath);
            }

            // EditorOnly: Unity strips the whole object from player builds (R4 - stripped, not disabled).
            var hubGo = new GameObject("Tuning Hub") { tag = "EditorOnly" };
            var hub = hubGo.AddComponent<TuningHub>();

            var cam = BuildCamera(humanoid, out var rig);
            var arena = ArenaBuilder.Build();

            var fighters = new GameObject("Fighters").transform;
            FighterViewBase player, opponent;
            if (humanoid)
            {
                player = PlaceFighter(fighterPrefab, "Player (You)", fighters, new Vector3(HumanoidX, 0f, 0f), -90f, rival: false);
                opponent = PlaceFighter(fighterPrefab, "Opponent", fighters, new Vector3(-HumanoidX, 0f, 0f), 90f, rival: true);
                ArenaBuilder.AddContactShadow(player.transform, arena.Blob, 0.95f);
                ArenaBuilder.AddContactShadow(opponent.transform, arena.Blob, 0.95f);
            }
            else
            {
                var fighterMat = GetOrCreateMaterial("Proto_Fighter", Color.white, 0.45f);
                var visorMat   = GetOrCreateMaterial("Proto_Visor", Hex("07080A"), 0.8f);
                player   = BuildFighter("Player (You)", fighters, new Vector3(CapsuleX, 1f, 0f), -90f, fighterMat, visorMat, Hex("3FD2FF"));
                opponent = BuildFighter("Opponent", fighters, new Vector3(-CapsuleX, 1f, 0f), 90f, fighterMat, visorMat, Hex("E9B949"));
            }

            var systems = new GameObject("Bout Systems");
            var hitstop = systems.AddComponent<HitstopController>();
            var bout = systems.AddComponent<BoutController>();

            var hud = Hud.V2HudBuilder.Build();
            // M10: the match-adjacent V2 screens, a UI Toolkit stack drawn (and clicked) above the
            // HUD canvas (sorting 10). It opens empty and presents only while a screen is up.
            var overlay = FrontEnd.FrontEndSceneBuilder.BuildScreenHost("Bout Screens", OverlaySortingOrder,
                UI.FrontEnd.ScreenId.None, FrontEnd.FrontEndSceneBuilder.BoutOverlayScreens, overlay: true);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Set(bout, "_config", cfg);
            Set(bout, "_playerView", player);
            Set(bout, "_opponentView", opponent);
            Set(bout, "_hud", hud.Hud);
            Set(bout, "_hitstop", hitstop);
            Set(bout, "_camera", cam);
            Set(bout, "_arena", arena.Director);
            Set(bout, "_cameraRig", rig);
            Set(bout, "_screens", overlay.Screens);

            Set(hub, "_config", cfg);
            Set(hub, "_bout", bout);
            var profile = AssetDatabase.LoadAssetAtPath<FighterRigProfile>(Characters.FighterBuilder.ProfilePath);
            var extras = new List<Object> { arena.Look, arena.Post };
            if (humanoid && profile != null) extras.Insert(0, profile);
            SetArray(hub, "_extraAssets", extras.ToArray());
            SetArray(hub, "_sceneComponents", new Object[] { player, opponent, hitstop, rig, cam, arena.Director, arena.DamageCam });

            AssertWired(bout, "_config", "_playerView", "_opponentView", "_hud", "_hitstop", "_camera", "_arena", "_cameraRig", "_screens");
            AssertWired(hub, "_config", "_bout");

            // Last: the probe renders every static object, and nothing static is added after it.
            ArenaBuilder.BakeReflections(arena);

            if (!EditorSceneManager.SaveScene(scene, BoutScenePath))
                throw new System.IO.IOException("Could not save " + BoutScenePath);
        }

        private static FighterViewBase PlaceFighter(GameObject prefab, string name, Transform parent, Vector3 pos, float yaw, bool rival)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            SetLayerRecursive(go, Layer("Fighter"));
            if (rival) ApplyRivalTint(go);
            return go.GetComponent<FighterViewBase>();
        }

        // The rival wears crimson-shifted copies of the model's materials, so two instances of
        // one character read as two fighters. The FBX's own materials are never modified.
        private static readonly Color RivalTint = new Color(1f, 0.6f, 0.58f, 1f);

        private static void ApplyRivalTint(GameObject go)
        {
            EnsureFolder(MaterialsDir + "/Rival");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = RivalMaterial(mats[i]);
                r.sharedMaterials = mats;
            }
        }

        private static Material RivalMaterial(Material src)
        {
            string safe = string.Join("_", src.name.Split(Path.GetInvalidFileNameChars())).Replace('.', '_');
            string path = MaterialsDir + "/Rival/" + safe + "_Rival.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(src) { name = src.name + "_Rival" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * RivalTint);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// The camera is a child of the rig: the rig flies the intro and holds the gameplay pose,
        /// and the camera's CameraDirector layers recorded sequences and trauma shake on top.
        private static Camera BuildCamera(bool humanoid, out CameraRig rig)
        {
            var rigGo = new GameObject("Camera Rig");
            rig = rigGo.AddComponent<CameraRig>();
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(rigGo.transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("050307");
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;
            // Photo-only rim lights and FX never reach the live view (BUILD-AND-RUN §Tags and layers).
            cam.cullingMask = ~(1 << Layer("SnapshotOnly"));
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            go.AddComponent<AudioListener>();
            // M16 (ADR-0020): the director owns the camera's final pose; the lens owns DoF.
            var lens = go.AddComponent<CameraLens>();
            var director = go.AddComponent<CameraDirector>();
            Set(director, "_lens", lens);

            var pos = humanoid ? CameraPosition : CapsuleCameraPosition;
            var rot = Quaternion.LookRotation((humanoid ? CameraLookAt : CapsuleCameraLookAt) - pos);
            rigGo.transform.SetPositionAndRotation(pos, rot);
            Set(rig, "_camera", go.transform);
            Set(rig, "_gameplayPosition", pos);
            Set(rig, "_gameplayEuler", rot.eulerAngles);
            Set(rig, "_introPosition", IntroPosition);
            Set(rig, "_introEuler", IntroEuler);
            return cam;
        }

        private static PlaceholderFighterView BuildFighter(string name, Transform parent, Vector3 position, float yaw,
                                                           Material bodyMat, Material visorMat, Color idle)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var body = Primitive(PrimitiveType.Capsule, "Body", root.transform, Vector3.zero, Vector3.one, bodyMat);
            // The visor shows which way the capsule faces - it is the "face" that gets slapped.
            Primitive(PrimitiveType.Cube, "Visor", root.transform, new Vector3(0f, 0.55f, 0.42f), new Vector3(0.55f, 0.16f, 0.2f), visorMat);

            SetLayerRecursive(root, Layer("Fighter"));
            var view = root.AddComponent<PlaceholderFighterView>();
            Set(view, "_renderer", body.GetComponent<Renderer>());
            Set(view, "_idleColour", idle);
            Set(view, "_windupColour", Color.white); // the default yellow reads as the gold idle tint
            return view;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                                            Vector3 localScale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null && !keepCollider) Object.DestroyImmediate(col);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material GetOrCreateMaterial(string name, Color colour, float smoothness)
        {
            EnsureFolder(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("URP Lit shader not found - is URP the active pipeline?");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }


        // ── Boot scene ───────────────────────────────────────────────
        private static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GameRoot").AddComponent<GameRoot>();
            Set(root, "_boutSceneName", BoutSceneName);
            // Boot opens the menus once they have been generated (M10, ADR-0013).
            if (File.Exists(FrontEnd.FrontEndSceneBuilder.ScenePath))
                Set(root, "_frontEndSceneName", FrontEnd.FrontEndSceneBuilder.SceneName);
            if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                throw new System.IO.IOException("Could not save " + BootScenePath);
        }

        // Boot first (index 0 is what a device build launches), then the bout. Existing entries
        // are kept after them; nothing of yours is removed.
        private static void AddScenesToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == BootScenePath || s.path == BoutScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(BoutScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            FrontEnd.FrontEndSceneBuilder.AddToBuildSettings(); // keeps FrontEnd right after Boot
        }
    }
}
