using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UnityEngine.UI;
using SlapMania.App;
using SlapMania.Data;
using SlapMania.UI.FrontEnd;
using static SlapMania.Editor.SceneBuildUtil;
using Object = UnityEngine.Object;

namespace SlapMania.Editor.FrontEnd
{
    /// Generates the front end (M10, ADR-0013) the same way PrototypeSceneBuilder generates
    /// the bout: from code, every serialized wire checked (R11).
    ///
    ///   * Settings/UI: PanelSettings (430 x 932 reference, Expand - the HUD's rule), a theme,
    ///     and PanelTextSettings whose gradient presets back the kit's gold / red / silver text;
    ///   * Data/UI/UiMotion.asset, wired into GameConfig.uiMotion when that is empty;
    ///   * Scenes/FrontEnd.unity: UIDocument + ScreenManager (one entry per generated UXML in
    ///     Prefabs/UI/Screens) + FrontEndBridge, an EventSystem and a clearing camera;
    ///   * Boot's GameRoot pointed at FrontEnd, and FrontEnd added to Build Settings.
    ///
    /// Assets are created only when missing and never overwritten (tuning survives); the scene
    /// is regenerated every run. Run Tools/mockup2uxml first when the mockups change.
    public static class FrontEndSceneBuilder
    {
        public const string ScenePath   = "Assets/_Project/Scenes/FrontEnd.unity";
        public const string SceneName   = "FrontEnd";
        private const string ScreensDir = "Assets/_Project/Prefabs/UI/Screens";
        private const string UiDir      = "Assets/_Project/Settings/UI";
        private const string PanelPath  = UiDir + "/FrontEnd.PanelSettings.asset";
        private const string ThemePath  = UiDir + "/FrontEndTheme.tss";
        private const string TextPath   = UiDir + "/FrontEnd.PanelTextSettings.asset";
        private const string GradientDir = "Assets/_Project/Resources/UI/TextGradients";
        private const string GradientResourcePath = "UI/TextGradients/";
        private const string MotionPath = "Assets/_Project/Data/UI/UiMotion.asset";
        private const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        private const string CompositeShader = "SlapMania/UI/GammaComposite";
        private const string CompositePath = "Assets/_Project/Art/UI/Materials/GammaComposite.mat";

        private const string MenuRoot = "Tools/Slap Mania/Front End/";

        [MenuItem(MenuRoot + "Build Front End Scene", priority = 0)]
        public static void Build() => Run(interactive: true);

        /// No dialogs: for the Unity MCP bridge, CI and batchmode
        /// (-executeMethod SlapMania.Editor.FrontEnd.FrontEndSceneBuilder.BuildNonInteractive).
        public static void BuildNonInteractive() => Run(interactive: false);

        private static void Run(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Exit Play Mode before building the front end.");
            if (interactive)
            {
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
                FixMockupTextureImports();
                FixIconImports();
                var motion = EnsureMotion();
                var panel = EnsurePanelSettings();
                EnsureGradients();
                var screens = CollectScreens();
                BuildScene(panel, motion, screens);
                PointBootAtFrontEnd();
                AddToBuildSettings();
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e)
            {
                if (!interactive) throw;
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Front end build failed", e.Message, "OK");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (interactive)
                EditorUtility.DisplayDialog("Front end built",
                    "FrontEnd.unity is generated and Boot now opens it.\n\nPress Play (Play From Boot on): " +
                    "Title -> Loading -> Main Menu. PLAY RANKED / career fights run the bout; results' MAIN MENU comes back.\n\n" +
                    "Set the Game view to a portrait phone resolution (e.g. 1290x2796).", "OK");
        }

        // ── Assets ───────────────────────────────────────────────────
        public static UiMotionConfig EnsureMotion()
        {
            EnsureFolder(Path.GetDirectoryName(MotionPath).Replace('\\', '/'));
            var motion = GetOrCreateAsset<UiMotionConfig>(MotionPath);
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg != null && cfg.uiMotion == null)
            {
                cfg.uiMotion = motion;
                EditorUtility.SetDirty(cfg);
            }
            return motion;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            EnsureFolder(UiDir);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel != null) return panel;

            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = EnsureTheme();
            panel.textSettings = EnsureTextSettings();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(430, 932); // the mockup frame, CSS px
            panel.screenMatchMode = PanelScreenMatchMode.Expand;   // whole frame always visible
            // The mockups were drawn by a browser, which blends in sRGB; a Linear project
            // blends UI in linear light, so every translucent overlay would read lighter.
            panel.forceGammaRendering = true;
            // The template never clears: GammaPresenter's runtime copy clears its render target.
            // A clearing overlay panel would wipe the screen every frame (DEBUG S-077).
            panel.clearColor = false;
            panel.colorClearValue = Hex("07080A");
            panel.sortingOrder = 0;
            AssetDatabase.CreateAsset(panel, PanelPath);
            return panel;
        }

        private static Material EnsureCompositeMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CompositePath);
            if (mat != null) return mat;
            var shader = Shader.Find(CompositeShader);
            if (shader == null) throw new System.InvalidOperationException("Shader " + CompositeShader + " not found (Shaders/UI/GammaComposite.shader).");
            EnsureFolder(Path.GetDirectoryName(CompositePath).Replace('\\', '/'));
            mat = new Material(shader) { name = "GammaComposite" };
            AssetDatabase.CreateAsset(mat, CompositePath);
            return mat;
        }

        private static ThemeStyleSheet EnsureTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null) return theme;
            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new System.InvalidOperationException("Could not create " + ThemePath);
            return theme;
        }

        /// The kit's `.gt` / `.rt` / `.st` gradient text, as TextCore gradient presets the
        /// converter references with <gradient="Gold">. TextCore gradients have two stops, so
        /// the CSS middle stop is dropped (top and bottom colours are exact).
        private static PanelTextSettings EnsureTextSettings()
        {
            var ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextPath);
            if (ts == null)
            {
                ts = ScriptableObject.CreateInstance<PanelTextSettings>();
                ts.defaultColorGradientPresetsPath = GradientResourcePath;
                AssetDatabase.CreateAsset(ts, TextPath);
            }
            EnsureGradients();
            return ts;
        }

        /// Run on every build, not only with a new PanelSettings, so a page added later gets
        /// its presets. V1's `.goldtxt` / `.redtxt` / `.silvertxt` are their own colours.
        private static void EnsureGradients()
        {
            EnsureFolder(GradientDir);
            Gradient("Gold", "FFF3C9", "B8820F");
            Gradient("Red", "FF7A80", "A30F18");
            Gradient("Silver", "FFFFFF", "8C929C");
            Gradient("V1Gold", "FFF1C4", "D98A0B");
            Gradient("V1Red", "FF6166", "B0101A");
            Gradient("V1Silver", "FFFFFF", "8A8C92");
        }

        private static void Gradient(string name, string top, string bottom)
        {
            string path = GradientDir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TextColorGradient>(path) != null) return;
            var g = ScriptableObject.CreateInstance<TextColorGradient>();
            g.colorMode = ColorGradientMode.VerticalGradient;
            g.topLeft = g.topRight = Hex(top);
            g.bottomLeft = g.bottomRight = Hex(bottom);
            AssetDatabase.CreateAsset(g, path);
        }

        /// The mockup photos were copied in before the import rule existed; give them the
        /// rule's settings once (no power-of-two rescale, no mipmaps).
        private static void FixMockupTextureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiAssetImportRules.MockupDir.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter t)) continue;
                if (t.npotScale == TextureImporterNPOTScale.None && !t.mipmapEnabled) continue;
                t.npotScale = TextureImporterNPOTScale.None;
                t.mipmapEnabled = false;
                t.alphaIsTransparency = true;
                t.wrapMode = TextureWrapMode.Clamp;
                t.maxTextureSize = 2048;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                EditorUtility.SetDirty(t);
                t.SaveAndReimport();
            }
        }

        /// (screen, UXML path) pairs. Paths, not loaded objects: the scene switch in BuildScene
        /// can unload assets nothing references yet, and a stale handle wires as null.
        /// Icons imported before the rule learned PreserveViewport get the rule's settings once.
        private static void FixIconImports()
        {
            foreach (var path in Directory.GetFiles(UiAssetImportRules.IconDir.TrimEnd('/'), "*.svg"))
            {
                var p = path.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(p) is Unity.VectorGraphics.Editor.SVGImporter svg) || !UiAssetImportRules.Apply(svg)) continue;
                EditorUtility.SetDirty(svg); // a ScriptedImporter is not saved on reimport unless dirty
                svg.SaveAndReimport();
            }
        }

        /// A page's subfolder prefixes its screens: Screens/V1/Home.uxml is ScreenId.V1Home.
        private static List<KeyValuePair<ScreenId, string>> CollectScreens()
        {
            var list = new List<KeyValuePair<ScreenId, string>>();
            foreach (var guid in AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { ScreensDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var folder = Path.GetDirectoryName(path).Replace('\\', '/');
                var page = folder.Length > ScreensDir.Length ? folder.Substring(ScreensDir.Length + 1).Replace("/", "") : "";
                var file = page + Path.GetFileNameWithoutExtension(path);
                if (!ScreenIds.TryParse(file, out var id))
                {
                    Debug.LogWarning("Front end: " + path + " names no ScreenId; skipped.");
                    continue;
                }
                list.Add(new KeyValuePair<ScreenId, string>(id, path));
            }
            if (list.Count == 0)
                throw new System.InvalidOperationException("No screens in " + ScreensDir + ". Run: python Tools/mockup2uxml/mockup2uxml.py");
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }

        // ── Scene ────────────────────────────────────────────────────
        private static void BuildScene(PanelSettings panel, UiMotionConfig motion, List<KeyValuePair<ScreenId, string>> screens)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // A Single-mode switch can unload assets nothing references; reload by path.
            panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            motion = AssetDatabase.LoadAssetAtPath<UiMotionConfig>(MotionPath);

            // A camera only to clear the frame: UI Toolkit draws the menus as an overlay.
            var camGo = new GameObject("FrontEnd Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("07080A");
            cam.cullingMask = 0;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var host = CreateHost("Front End", 0, panel, motion, screens, ScreenId.Title, overlay: false);
            var bridge = host.Root.AddComponent<FrontEndBridge>();
            Set(bridge, "_screens", host.Screens);
            Set(bridge, "_standaloneBoutScene", PrototypeSceneBuilder.BoutSceneName);
            AssertWired(bridge, "_screens");

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);
        }

        // ── Screen host (shared with the bout scene) ─────────────────
        /// The screens the bout's overlay hosts (M10): D5 PauseMenu, D4 Replay, E1 RoundEnd,
        /// E4 RankUp, and B6 Settings for PauseMenu's Settings link.
        public static readonly ScreenId[] BoutOverlayScreens =
        {
            ScreenId.PauseMenu, ScreenId.Replay, ScreenId.RoundEnd, ScreenId.RankUp, ScreenId.Settings,
        };

        public sealed class ScreenHost
        {
            public GameObject Root;
            public ScreenManager Screens;
            public GammaPresenter Presenter;
            public UIDocument Document;
        }

        /// A UI Toolkit screen stack in the open scene, presented through GammaPresenter: a
        /// UIDocument + GammaPresenter + ScreenManager object named `name`, and a Screen Space -
        /// Overlay canvas at `sortingOrder` showing the panel texture (the panel takes input at
        /// the same order). `only` limits the stack to those screens (null: every generated one).
        /// `overlay` presents only while a screen is open - the bout, over its HUD.
        ///
        /// Call after NewScene: every asset is loaded by path here (S-070, S-075).
        public static ScreenHost BuildScreenHost(string name, int sortingOrder, ScreenId initial, ScreenId[] only, bool overlay)
        {
            var screens = CollectScreens();
            if (only != null)
            {
                screens.RemoveAll(e => System.Array.IndexOf(only, e.Key) < 0);
                foreach (var id in only)
                    if (!screens.Exists(e => e.Key == id))
                        throw new System.InvalidOperationException("No generated UXML for " + id + " in " + ScreensDir + ". Run: python Tools/mockup2uxml/mockup2uxml.py");
            }
            EnsureMotion();
            var panel = EnsurePanelSettings();
            var motion = AssetDatabase.LoadAssetAtPath<UiMotionConfig>(MotionPath);
            return CreateHost(name, sortingOrder, panel, motion, screens, initial, overlay);
        }

        private static ScreenHost CreateHost(string name, int sortingOrder, PanelSettings panel, UiMotionConfig motion,
                                             List<KeyValuePair<ScreenId, string>> screens, ScreenId initial, bool overlay)
        {
            // The panel renders into a texture this overlay shows (GammaPresenter): browser-like
            // sRGB blending in a Linear project. Inactive until the presenter has a texture.
            var canvasGo = new GameObject(name + " Output", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var imageGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var imageRt = (RectTransform)imageGo.transform;
            imageRt.SetParent(canvasGo.transform, false);
            imageRt.anchorMin = Vector2.zero; imageRt.anchorMax = Vector2.one;
            imageRt.offsetMin = Vector2.zero; imageRt.offsetMax = Vector2.zero;
            var raw = imageGo.GetComponent<RawImage>();
            raw.raycastTarget = false;
            imageGo.SetActive(false);

            var go = new GameObject(name);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            var gamma = go.AddComponent<GammaPresenter>();
            var manager = go.AddComponent<ScreenManager>();
            Set(gamma, "_document", doc);
            Set(gamma, "_output", raw);
            Set(gamma, "_composite", EnsureCompositeMaterial());
            if (overlay) Set(gamma, "_presentWhileOpen", manager);

            Set(manager, "_document", doc);
            Set(manager, "_motion", motion);
            var so = new SerializedObject(manager);
            var list = so.FindProperty("_screens");
            list.arraySize = screens.Count;
            for (int i = 0; i < screens.Count; i++)
            {
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(screens[i].Value);
                if (tree == null) throw new System.InvalidOperationException("Could not load " + screens[i].Value);
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").intValue = (int)screens[i].Key;
                el.FindPropertyRelative("tree").objectReferenceValue = tree;
            }
            so.FindProperty("_initial").intValue = (int)initial;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssertWired(manager, "_document", "_motion");
            var check = new SerializedObject(manager).FindProperty("_screens");
            for (int i = 0; i < check.arraySize; i++)
                if (check.GetArrayElementAtIndex(i).FindPropertyRelative("tree").objectReferenceValue == null)
                    throw new System.InvalidOperationException("ScreenManager._screens[" + i + "].tree is null after wiring.");
            AssertWired(gamma, "_document", "_output", "_composite");
            if (overlay) AssertWired(gamma, "_presentWhileOpen");

            return new ScreenHost { Root = go, Screens = manager, Presenter = gamma, Document = doc };
        }

        private static void PointBootAtFrontEnd()
        {
            if (!File.Exists(PrototypeSceneBuilder.BootScenePath))
            {
                Debug.LogWarning("Front end: no Boot scene yet; run Build Playable Prototype, which points Boot here.");
                return;
            }
            var boot = EditorSceneManager.OpenScene(PrototypeSceneBuilder.BootScenePath, OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<GameRoot>();
            if (root == null) throw new System.InvalidOperationException("Boot.unity has no GameRoot.");
            Set(root, "_frontEndSceneName", SceneName);
            EditorSceneManager.MarkSceneDirty(boot);
            if (!EditorSceneManager.SaveScene(boot)) throw new IOException("Could not save Boot.unity");
        }

        /// Boot stays at index 0 (what a device launches); FrontEnd goes right after it.
        public static void AddToBuildSettings()
        {
            if (!File.Exists(ScenePath)) return;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            int bootAt = scenes.FindIndex(s => s.path == PrototypeSceneBuilder.BootScenePath);
            scenes.Insert(bootAt >= 0 ? bootAt + 1 : 0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
