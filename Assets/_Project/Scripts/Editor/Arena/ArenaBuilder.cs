using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SlapMania.Data;
using SlapMania.Presentation;
using static SlapMania.Editor.SceneBuildUtil;
using Object = UnityEngine.Object;

namespace SlapMania.Editor.Arena
{
    /// Generates the bout arena into the open scene (M15, ADR-0012) — the reference clip's
    /// look, built from mobile-true fakes: a procedural crowd impostor on stepped stands, an LED
    /// sponsor ribbon and ring-side boards, a truss rig of lamp heads with additive beam cones,
    /// haze sheets, a gold-ringed floor, the brass podium the fighters slap across, and a
    /// jumbotron fed by the live damage cam. Four real spots (key, two rims, fill) and one baked
    /// reflection probe carry the lighting; the colour grade is a URP Volume.
    ///
    /// Regenerated on every build: textures (overwritten in place, GUIDs kept), meshes, the
    /// hierarchy. Created once and then yours to tune: the materials, the ArenaLook asset and
    /// the Volume profile — "Reset Arena Look" recreates those from these defaults.
    ///
    /// Layout, metres: ring centre at the origin, podium on it, fighters facing each other
    /// along X. The gameplay camera looks down +Z, so +Z is "behind the fight".
    public static class ArenaBuilder
    {
        public const string ArtDir = "Assets/_Project/Art/Arena";
        public const string GenDir = ArtDir + "/Generated";
        public const string MatDir = ArtDir + "/Materials";
        public const string LookPath = "Assets/_Project/Data/Prototype/ArenaLook.asset";
        public const string PostPath = "Assets/_Project/Data/Prototype/ArenaPost.asset";
        private const string RtPath = GenDir + "/DamageCam.renderTexture";
        private const string ProbePath = GenDir + "/ArenaReflection.exr";

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

        public const float FloorRadius = 12f;
        public static readonly Vector3 PodiumSize = new Vector3(0.45f, 0.8f, 0.9f);
        private const float BoardRadius = 7.8f, BoardHeight = 1f;
        private const float LowerR0 = 8.4f, LowerDepth = 0.85f, LowerRise = 0.45f;
        private const int LowerRows = 14;
        private const float UpperDepth = 0.8f, UpperRise = 0.55f;
        private const int UpperRows = 11;
        private const int Segs = 128;
        private const float TunnelAngleDeg = 5f, TunnelHalfAngle = 0.2f;
        private const int TunnelRows = 6;
        private const float JumboRadius = 12.6f, JumboY = 4.45f, JumboW = 4f, JumboH = 2.25f;
        private const float TrussY = 9.2f, TrussHalf = 5.5f;
        private const int UiLayer = 5;

        private static float LowerTop => LowerRows * LowerRise;
        private static float LowerBack => LowerR0 + LowerRows * LowerDepth;
        private static float RibbonY0 => LowerTop + 1.1f;
        private static float FasciaTop => RibbonY0 + 1.05f;
        private static float UpperR0 => LowerBack + 0.05f;
        private static float UpperBack => UpperR0 + UpperRows * UpperDepth;
        private static float UpperTop => FasciaTop + UpperRows * UpperRise;

        public sealed class Result
        {
            public GameObject Root;
            public ArenaDirector Director;
            public ArenaLookConfig Look;
            public VolumeProfile Post;
            public Light Key;
            public DamageCam DamageCam;
            public ReflectionProbe Probe;
            public Material Blob;
        }

        private struct Art
        {
            public Texture2D Ribbon, Boards, Floor, FloorMask, Top, TopMask, Medallion, Overlay, Sign, Side;
        }

        [MenuItem("Tools/Slap Mania/Prototype/Regenerate Arena Art", priority = 20)]
        public static void GenerateArtMenu()
        {
            MakeArt();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Slap Mania/Prototype/Reset Arena Look", priority = 21)]
        public static void ResetLookMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset arena look?",
                    "Deletes the arena materials, ArenaLook.asset and ArenaPost.asset so the next " +
                    "Build Playable Prototype recreates them from the builder defaults.\n\nYour arena tuning is lost.",
                    "Reset", "Cancel"))
                return;
            ResetLookAssets();
            EditorUtility.DisplayDialog("Slap Mania", "Done. Run Tools > Slap Mania > Prototype > Build Playable Prototype.", "OK");
        }

        /// No dialog; for the MCP bridge and batchmode.
        public static void ResetLookAssets()
        {
            if (AssetDatabase.IsValidFolder(MatDir))
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.DeleteAsset(LookPath);
            AssetDatabase.DeleteAsset(PostPath);
        }

        /// Builds the arena into the active scene and applies the look's ambient and fog to it.
        /// Call BakeReflections once everything static is in place.
        public static Result Build()
        {
            var art = MakeArt();
            var look = GetOrCreateAsset<ArenaLookConfig>(LookPath);
            var post = GetOrCreatePostProfile();
            ArenaDirector.ApplySceneSettings(look);
            RenderSettings.skybox = null;

            var root = new GameObject("Arena");
            var t = root.transform;
            var res = new Result { Root = root, Look = look, Post = post };
            res.Blob = Mat("Arena_Blob", "SlapMania/Arena/Blob", m => { m.SetFloat("_Darkness", 0.22f); m.SetFloat("_Power", 1.5f); });

            var beams = Group("Beams", t, false);
            var sweep = new List<Transform>();
            var surge = new List<Renderer>();

            BuildFloor(t, art);
            BuildPodium(t, art, res.Blob);
            BuildBowl(t, art);
            BuildTunnel(t);
            BuildRig(t, beams, sweep, surge);
            res.DamageCam = BuildJumbotron(t, art);
            var atmosphere = BuildAtmosphere(t);

            var lights = Group("Lights", t, false);
            res.Key = Spot("Key Spot", lights, new Vector3(0f, TrussY - 0.5f, 0f), Vector3.zero, look.keySpotAngle, 16f, LightShadows.Soft);
            res.Key.innerSpotAngle = look.keySpotAngle * 0.55f;
            var rimL = Spot("Rim Left", lights, new Vector3(-4.2f, 4.6f, 4.2f), new Vector3(0f, 1.45f, 0f), 42f, 12f, LightShadows.None);
            var rimR = Spot("Rim Right", lights, new Vector3(4.2f, 4.8f, 3.8f), new Vector3(0f, 1.45f, 0f), 42f, 12f, LightShadows.None);
            var fill = Spot("Fill", lights, new Vector3(0f, 2.3f, -5.2f), new Vector3(0f, 1.3f, 0f), 60f, 10f, LightShadows.None);
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(lights, false);
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            Tint(res.Key, look.keyColour, look.keyIntensity);
            Tint(rimL, look.rimLeftColour, look.rimLeftIntensity);
            Tint(rimR, look.rimRightColour, look.rimRightIntensity);
            Tint(fill, look.fillColour, look.fillIntensity);
            Tint(moon, look.moonColour, look.moonIntensity);
            RenderSettings.sun = moon;

            var probeGo = new GameObject("Reflection Probe");
            probeGo.transform.SetParent(t, false);
            probeGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            res.Probe = probeGo.AddComponent<ReflectionProbe>();
            res.Probe.mode = ReflectionProbeMode.Baked;
            res.Probe.size = new Vector3(70f, 34f, 70f);
            res.Probe.resolution = 256;
            res.Probe.hdr = true;
            res.Probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            res.Probe.backgroundColor = Hex("050307");

            var volume = new GameObject("Post Volume").AddComponent<Volume>();
            volume.transform.SetParent(t, false);
            volume.isGlobal = true;
            volume.sharedProfile = post;

            // Layers (BUILD-AND-RUN): the arena on Arena, crowd strips on Crowd (cullable), and
            // the Volume back on Default, where the cameras' volume mask looks for it.
            SetLayerRecursive(root, Layer("Arena"));
            int crowdLayer = Layer("Crowd");
            foreach (var crowdName in new[] { "Bowl/Lower Crowd", "Bowl/Upper Crowd" })
                t.Find(crowdName).gameObject.layer = crowdLayer;
            volume.gameObject.layer = 0;

            var dir = root.AddComponent<ArenaDirector>();
            res.Director = dir;
            Set(dir, "_look", look);
            SetArray(dir, "_highOnly", new Object[] { beams.gameObject, atmosphere.gameObject, res.DamageCam.gameObject });
            Set(dir, "_key", res.Key);
            Set(dir, "_rimLeft", rimL);
            Set(dir, "_rimRight", rimR);
            Set(dir, "_fill", fill);
            Set(dir, "_moon", moon);
            SetArray(dir, "_sweepBeams", sweep.ToArray());
            SetArray(dir, "_beamRenderers", surge.ToArray());
            Set(dir, "_damageCam", res.DamageCam);
            AssertWired(dir, "_look", "_key", "_rimLeft", "_rimRight", "_fill", "_moon", "_damageCam");
            return res;
        }

        /// Bakes the centre probe to an EXR and pins it as a custom cubemap (and as the scene's
        /// default reflection), so it needs no lighting-data asset. Only static objects render
        /// into it: the arena, not the fighters.
        public static void BakeReflections(Result r)
        {
            r.Probe.mode = ReflectionProbeMode.Baked;
            if (!Lightmapping.BakeReflectionProbe(r.Probe, ProbePath))
                throw new System.InvalidOperationException("Arena reflection probe bake failed.");
            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var cube = AssetDatabase.LoadAssetAtPath<Texture>(ProbePath);
            if (cube == null) throw new System.InvalidOperationException("Baked reflection missing at " + ProbePath);
            r.Probe.mode = ReflectionProbeMode.Custom;
            r.Probe.customBakedTexture = cube;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
        }

        /// A soft multiplicative contact shadow under a fighter (a child, so it follows the root;
        /// the knock-out ragdoll leaves it behind, which reads as the spot they fell from).
        public static void AddContactShadow(Transform fighter, Material blob, float size)
        {
            var mb = new MeshBuilder();
            ArenaMeshes.FaceQuad(mb, Vector3.zero, Vector3.up, 1f, 1f);
            var go = Node("Contact Shadow", fighter, SaveMesh(mb.ToMesh("Arena_BlobQuad")), blob, false);
            go.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.layer = fighter.gameObject.layer;
        }

        // ── Art ──────────────────────────────────────────────────────
        private static Art MakeArt()
        {
            EnsureFolder(GenDir);
            var a = new Art
            {
                Ribbon = ArenaArt.Ribbon(GenDir + "/Arena_Ribbon.png"),
                Boards = ArenaArt.Boards(GenDir + "/Arena_Boards.png"),
                Medallion = ArenaArt.Medallion(GenDir + "/Arena_Medallion.png"),
                Overlay = ArenaArt.JumboOverlay(GenDir + "/Arena_JumboOverlay.png"),
                Sign = ArenaArt.Sign(GenDir + "/Arena_Sign.png"),
                Side = ArenaArt.Side(GenDir + "/Arena_Side.png"),
            };
            ArenaArt.Floor(GenDir + "/Arena_Floor.png", GenDir + "/Arena_FloorMask.png", FloorRadius, out a.Floor, out a.FloorMask);
            ArenaArt.PodiumTop(GenDir + "/Arena_PodiumTop.png", GenDir + "/Arena_PodiumTopMask.png", out a.Top, out a.TopMask);
            return a;
        }

        // ── Pieces ───────────────────────────────────────────────────
        private static void BuildFloor(Transform t, Art art)
        {
            var mat = Lit("Arena_Floor", Color.white, 0f, 1f, art.Floor, art.FloorMask);
            var go = Node("Floor", t, SaveMesh(ArenaMeshes.Disc("Arena_Floor", FloorRadius, 96)), mat);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            // The knock-out ragdoll lands on this. Thick on purpose (S-074): a limb that starts
            // a KO overlapping something gets depenetrated downwards, and a thin slab lets it
            // out through the bottom; 3 m of floor always pushes it back up.
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -1.5f, 0f);
            col.size = new Vector3(FloorRadius * 2f, 3f, FloorRadius * 2f);
        }

        /// A slap table, not a solid chest (S-074): the reference's brass-rimmed top and
        /// medallions on a shallow apron, carried on four red corner legs. The fighters' clips
        /// step in hard — the Charge clip puts a knee at the centre line at 0.44 m — so the
        /// space under the apron must stay open for knees and shins.
        private static void BuildPodium(Transform t, Art art, Material blob)
        {
            var root = Group("Podium", t, true);
            var body = Lit("Arena_PodiumBody", Hex("12151E"), 0.2f, 0.42f);
            var brass = Lit("Arena_Brass", Hex("C8963E"), 1f, 0.72f);
            var red = Lit("Arena_PodiumRed", Hex("7C0F1A"), 0.1f, 0.62f);
            var top = Lit("Arena_PodiumTop", Color.white, 0f, 1f, art.Top, art.TopMask);
            var medal = Mat("Arena_Medallion", LitShader, m =>
            {
                m.SetTexture("_BaseMap", art.Medallion);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0.85f);
                m.SetFloat("_Smoothness", 0.62f);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            });

            float w = PodiumSize.x, h = PodiumSize.y, d = PodiumSize.z;
            const float slab = 0.08f, apronTop = 0.72f, apronBottom = 0.58f, apronHalfW = 0.15f, leg = 0.07f;
            float legX = w * 0.5f - leg * 0.5f, legZ = d * 0.5f - leg * 0.5f;
            float apronH = apronTop - apronBottom, apronD = d - 0.1f;

            var mb = new MeshBuilder(); // top slab core + apron
            mb.Box(new Vector3(0f, h - slab * 0.5f, 0f), new Vector3(w, slab - 0.02f, d), Quaternion.identity);
            mb.Box(new Vector3(0f, apronBottom + apronH * 0.5f, 0f), new Vector3(apronHalfW * 2f, apronH, apronD), Quaternion.identity);
            var bb = new MeshBuilder(); // brass: top rim, apron trim, leg collars and feet
            bb.Box(new Vector3(0f, h - 0.012f, 0f), new Vector3(w + 0.05f, 0.024f, d + 0.05f), Quaternion.identity);
            bb.Box(new Vector3(0f, h - slab + 0.012f, 0f), new Vector3(w + 0.03f, 0.024f, d + 0.03f), Quaternion.identity);
            bb.Box(new Vector3(0f, apronBottom + 0.006f, 0f), new Vector3(apronHalfW * 2f + 0.012f, 0.012f, apronD + 0.012f), Quaternion.identity);
            var rb = new MeshBuilder(); // red lacquered legs
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var c = new Vector3(sx * legX, 0f, sz * legZ);
                    rb.Box(c + Vector3.up * ((h - slab) * 0.5f), new Vector3(leg, h - slab, leg), Quaternion.identity);
                    bb.Box(c + Vector3.up * 0.02f, new Vector3(leg + 0.02f, 0.04f, leg + 0.02f), Quaternion.identity);
                    bb.Box(c + Vector3.up * (h - slab - 0.03f), new Vector3(leg + 0.012f, 0.02f, leg + 0.012f), Quaternion.identity);
                }
            var tb = new MeshBuilder();
            float ty = h + 0.002f, hw = w * 0.5f - 0.01f, hd = d * 0.5f - 0.01f;
            tb.Quad(new Vector3(-hw, ty, -hd), new Vector3(hw, ty, -hd), new Vector3(hw, ty, hd), new Vector3(-hw, ty, hd), Vector3.up,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            var md = new MeshBuilder(); // medallions hang on the apron, the front one facing the camera
            float my = apronBottom + apronH * 0.5f, big = 0.17f, small = 0.13f;
            ArenaMeshes.FaceQuad(md, new Vector3(0f, my, -apronD * 0.5f - 0.006f), Vector3.back, big, big);
            ArenaMeshes.FaceQuad(md, new Vector3(0f, my, apronD * 0.5f + 0.006f), Vector3.forward, big, big);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    ArenaMeshes.FaceQuad(md, new Vector3(sx * (apronHalfW + 0.006f), my, sz * 0.22f), new Vector3(sx, 0f, 0f), small, small);

            Shadowed(Node("Body", root, SaveMesh(mb.ToMesh("Podium_Body")), body));
            Shadowed(Node("Brass", root, SaveMesh(bb.ToMesh("Podium_Brass")), brass));
            Shadowed(Node("Legs", root, SaveMesh(rb.ToMesh("Podium_Legs")), red));
            Node("Top Inlay", root, SaveMesh(tb.ToMesh("Podium_Top")), top).GetComponent<MeshRenderer>().receiveShadows = true;
            Node("Medallions", root, SaveMesh(md.ToMesh("Podium_Medallions")), medal);

            // Colliders follow the table's real shape, so a KO'd fighter bounces off it without
            // starting inside it.
            AddBox(root, new Vector3(0f, h - slab * 0.5f, 0f), new Vector3(w + 0.05f, slab, d + 0.05f));
            AddBox(root, new Vector3(0f, apronBottom + apronH * 0.5f, 0f), new Vector3(apronHalfW * 2f, apronH, apronD));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    AddBox(root, new Vector3(sx * legX, (h - slab) * 0.5f, sz * legZ), new Vector3(leg, h - slab, leg));

            var sb = new MeshBuilder();
            ArenaMeshes.FaceQuad(sb, new Vector3(0f, 0.012f, 0f), Vector3.up, 1.1f, 1.5f);
            Node("Contact Shadow", root, SaveMesh(sb.ToMesh("Podium_Shadow")), blob, false);
        }

        private static void AddBox(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void BuildBowl(Transform t, Art art)
        {
            var root = Group("Bowl", t, true);
            var stands = Unlit("Arena_Stands", Hex("0D090C"));
            var dark = Unlit("Arena_Dark", Hex("050307"));
            var crowd = Mat("Arena_Crowd", "SlapMania/Arena/Crowd", m => { });
            var boards = Led("Arena_Boards", art.Boards, 200f, 32f, 0.12f, 0.95f);
            var ribbon = Led("Arena_Ribbon", art.Ribbon, 420f, 26f, 0.3f, 1.35f);
            var trim = Lit("Arena_Trim", Hex("2A2E37"), 0.9f, 0.55f);

            var gap = new ArenaMeshes.Gap { Centre = TunnelAngleDeg * Mathf.Deg2Rad, HalfAngle = TunnelHalfAngle, Rows = TunnelRows };
            Node("Boards", root, SaveMesh(ArenaMeshes.InnerBand("Arena_BoardBand", BoardRadius, 0f, BoardHeight, Segs, 8f)), boards);
            var rail = new MeshBuilder();
            ArenaMeshes.Annulus(rail, BoardRadius - 0.02f, BoardRadius + 0.25f, BoardHeight + 0.04f, Segs);
            var railFace = ArenaMeshes.InnerBand("tmp", BoardRadius - 0.02f, BoardHeight, BoardHeight + 0.04f, Segs, 1f);
            Node("Board Rail", root, SaveMesh(Merge("Arena_BoardRail", rail.ToMesh("tmp"), railFace)), trim);

            Node("Lower Stands", root, SaveMesh(ArenaMeshes.Stands("Arena_LowerStands", LowerR0, LowerDepth, 0f, LowerRise, LowerRows, Segs, gap)), stands);
            Node("Lower Crowd", root, SaveMesh(ArenaMeshes.Crowd("Arena_LowerCrowd", LowerR0, LowerDepth, 0f, LowerRise, LowerRows, Segs, 0, gap)), crowd);
            Node("Fascia", root, SaveMesh(ArenaMeshes.InnerBand("Arena_Fascia", LowerBack + 0.05f, LowerTop, FasciaTop, Segs, 1f)), dark);
            Node("Ribbon", root, SaveMesh(ArenaMeshes.InnerBand("Arena_RibbonBand", LowerBack, RibbonY0, RibbonY0 + 0.8f, 160, 10f)), ribbon);
            Node("Upper Stands", root, SaveMesh(ArenaMeshes.Stands("Arena_UpperStands", UpperR0, UpperDepth, FasciaTop, UpperRise, UpperRows, Segs)), stands);
            Node("Upper Crowd", root, SaveMesh(ArenaMeshes.Crowd("Arena_UpperCrowd", UpperR0, UpperDepth, FasciaTop, UpperRise, UpperRows, Segs, LowerRows)), crowd);
            Node("Back Wall", root, SaveMesh(ArenaMeshes.InnerBand("Arena_BackWall", UpperBack, UpperTop, 24f, 64, 1f)), dark);
            var ceiling = Node("Ceiling", root, SaveMesh(ArenaMeshes.Disc("Arena_Ceiling", UpperBack + 0.5f, 64)), dark);
            ceiling.transform.SetPositionAndRotation(new Vector3(0f, 24f, 0f), Quaternion.Euler(180f, 0f, 0f));

            // Red house lights along the upper tier (the reference's exit-sign dots).
            var dots = new MeshBuilder();
            var glows = new MeshBuilder();
            var rng = new System.Random(7);
            for (int i = 0; i < 16; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / 16f;
                bool high = i % 2 == 0;
                float r = high ? UpperBack - 0.1f : LowerBack + 0.02f;
                float y = high ? UpperTop + 0.7f : FasciaTop - 0.12f;
                var p = ArenaMeshes.Around(a, r, y);
                var n = new Vector3(-p.x, 0f, -p.z).normalized;
                ArenaMeshes.FaceQuad(dots, p + n * 0.02f, n, 0.34f, 0.12f);
                ArenaMeshes.FaceQuad(glows, p + n * 0.05f, n, 1.8f, 1.2f);
            }
            Node("House Lights", root, SaveMesh(dots.ToMesh("Arena_HouseLights")), Unlit("Arena_RedLamp", new Color(4f, 0.35f, 0.4f)));
            Node("House Light Glow", root, SaveMesh(glows.ToMesh("Arena_HouseGlow")), Soft("Arena_RedGlow", new Color(1f, 0.12f, 0.15f), 0.55f, 2.4f), false);
        }

        private static void BuildTunnel(Transform t)
        {
            var root = Group("Tunnel", t, true);
            var wall = Lit("Arena_TunnelWall", Hex("1C0709"), 0f, 0.35f);
            var door = Unlit("Arena_TunnelDoor", new Color(2.4f, 0.16f, 0.2f));
            var strip = Unlit("Arena_StepLight", new Color(3.2f, 0.22f, 0.26f));
            var glow = Soft("Arena_TunnelGlow", new Color(1f, 0.1f, 0.12f), 0.5f, 1.6f, 0.35f, 2f, 0.04f, 2f);

            float c = TunnelAngleDeg * Mathf.Deg2Rad, h = TunnelHalfAngle;
            float rIn = LowerR0, rOut = LowerR0 + TunnelRows * LowerDepth, yTop = TunnelRows * LowerRise;
            var wb = new MeshBuilder();
            var sb = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                float a = c + s * h;
                var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                var n = -s * tangent;
                wb.Quad(ArenaMeshes.Around(a, rIn, 0f), ArenaMeshes.Around(a, rOut, 0f), ArenaMeshes.Around(a, rOut, yTop), ArenaMeshes.Around(a, rIn, LowerRise), n,
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                sb.Beam(ArenaMeshes.Around(a, rIn, LowerRise + 0.03f) + n * 0.03f, ArenaMeshes.Around(a, rOut, yTop + 0.03f) + n * 0.03f, 0.045f);
            }
            var inward = -ArenaMeshes.Around(c, 1f, 0f);
            wb.Quad(ArenaMeshes.Around(c - h, rOut, 0f), ArenaMeshes.Around(c + h, rOut, 0f), ArenaMeshes.Around(c + h, rOut, yTop), ArenaMeshes.Around(c - h, rOut, yTop), inward,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            wb.Quad(ArenaMeshes.Around(c - h, BoardRadius, 0.003f), ArenaMeshes.Around(c + h, BoardRadius, 0.003f), ArenaMeshes.Around(c + h, rOut, 0.003f), ArenaMeshes.Around(c - h, rOut, 0.003f), Vector3.up,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            Node("Walls", root, SaveMesh(wb.ToMesh("Arena_TunnelWalls")), wall);
            Node("Rake Lights", root, SaveMesh(sb.ToMesh("Arena_TunnelStrips")), strip);
            var db = new MeshBuilder();
            ArenaMeshes.FaceQuad(db, ArenaMeshes.Around(c, rOut - 0.02f, 1.2f), inward, 1.7f, 2.4f);
            Node("Door", root, SaveMesh(db.ToMesh("Arena_TunnelDoor")), door);
            var gb = new MeshBuilder();
            ArenaMeshes.FaceQuad(gb, ArenaMeshes.Around(c, (rIn + rOut) * 0.5f + 1f, 1.5f), inward, 5f, 3.6f);
            Node("Glow", root, SaveMesh(gb.ToMesh("Arena_TunnelGlow")), glow, false);
        }

        private static void JumboPose(out Vector3 position, out Quaternion rotation)
        {
            position = ArenaMeshes.Around(TunnelAngleDeg * Mathf.Deg2Rad, JumboRadius, JumboY);
            rotation = Quaternion.LookRotation(new Vector3(position.x, 0f, position.z).normalized);
        }

        private static void BuildRig(Transform t, Transform beams, List<Transform> sweep, List<Renderer> surge)
        {
            var root = Group("Rig", t, true);
            var truss = Lit("Arena_Truss", Hex("3A3D45"), 0.85f, 0.45f);
            var housing = Lit("Arena_LampHousing", Hex("141519"), 0.6f, 0.4f);
            var lens = Unlit("Arena_LampLens", new Color(7f, 7f, 7.6f));
            var flareMat = Soft("Arena_LampFlare", new Color(0.85f, 0.9f, 1f), 1.1f, 3.2f);
            var beamMat = BeamMat("Arena_Beam", new Color(0.72f, 0.8f, 1f), 0.45f, 2.5f, 1.3f, 0.35f);
            var keyMat = BeamMat("Arena_KeyBeam", new Color(1f, 0.94f, 0.86f), 0.5f, 3f, 0.9f, 0.45f);
            var cone = SaveMesh(ArenaMeshes.Cone(24));

            var tm = new MeshBuilder();
            var hm = new MeshBuilder();
            var lm = new MeshBuilder();
            var fm = new MeshBuilder();
            float y = TrussY, s = TrussHalf;
            var c00 = new Vector3(-s, y, -s); var c10 = new Vector3(s, y, -s);
            var c11 = new Vector3(s, y, s); var c01 = new Vector3(-s, y, s);
            ArenaMeshes.Truss(tm, c00, c10); ArenaMeshes.Truss(tm, c10, c11);
            ArenaMeshes.Truss(tm, c11, c01); ArenaMeshes.Truss(tm, c01, c00);
            ArenaMeshes.Truss(tm, new Vector3(-s, y, 0f), new Vector3(s, y, 0f));
            foreach (var corner in new[] { c00, c10, c11, c01 })
                ArenaMeshes.Truss(tm, corner, new Vector3(corner.x * 2.1f, 14.5f, corner.z * 2.1f));

            var viewer = new Vector3(0f, 3f, -8f);
            float hang = y - 0.38f;
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-s * 0.8f, s * 0.8f, i / 5f);
                var back = new Vector3(x, hang, s);
                Lamp(hm, lm, fm, back, new Vector3(x * 0.6f, 0f, -1f), viewer, 0.9f);
                if (i == 1 || i == 4) Beam(beams, "Beam Back " + i, back, new Vector3(x * 1.4f, 1f, -9f), 5f, beamMat, cone, sweep, surge);
                var front = new Vector3(x, hang, -s);
                Lamp(hm, lm, fm, front, new Vector3(x * 0.3f, 0.8f, 0.5f), viewer, 0.9f);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 5; i++)
                {
                    float z = Mathf.Lerp(-s * 0.8f, s * 0.8f, i / 4f);
                    var p = new Vector3(side * s, hang, z);
                    Lamp(hm, lm, fm, p, new Vector3(side * 1f, 0.8f, z * 0.4f), viewer, 0.9f);
                    if (i == 1 || i == 3)
                        Beam(beams, "Beam Side " + (side < 0 ? "L" : "R") + i, p, new Vector3(-side * 13f, 3.5f, z * 1.2f + 4f), 4.5f, beamMat, cone, sweep, surge);
                }
            // Key lamp over the podium: its cone is the reference's hero shaft, not swept.
            var keyLamp = new Vector3(0f, y - 0.3f, 0f);
            Lamp(hm, lm, fm, keyLamp, Vector3.zero, viewer, 1.4f);
            Beam(beams, "Key Beam", keyLamp + Vector3.down * 0.15f, Vector3.zero, 19f, keyMat, cone, null, surge);

            // PAR bars flanking the jumbotron, raking down onto the ring behind the fighters.
            JumboPose(out var jp, out var jr);
            for (int side = -1; side <= 1; side += 2)
            {
                var a = jp + jr * new Vector3(side * (JumboW * 0.5f + 0.7f), JumboH * 0.5f + 0.6f, -0.1f);
                var b = jp + jr * new Vector3(side * (JumboW * 0.5f + 2.5f), JumboH * 0.5f + 0.6f, -0.1f);
                ArenaMeshes.Truss(tm, a, b, 0.22f, 0.03f, 0.015f);
                for (int i = 0; i < 4; i++)
                {
                    var p = Vector3.Lerp(a, b, (i + 0.5f) / 4f) + Vector3.down * 0.25f;
                    Lamp(hm, lm, fm, p, new Vector3(side * 1.5f, 0.5f, 0f), viewer, 0.8f);
                    if (i == 1 || i == 2)
                        Beam(beams, "Beam Par " + (side < 0 ? "L" : "R") + i, p, new Vector3(side * (0.6f + i * 0.5f), 0f, 1.3f), 4f, beamMat, cone, sweep, surge);
                }
            }

            Node("Truss", root, SaveMesh(tm.ToMesh("Arena_Truss")), truss);
            Node("Lamp Housings", root, SaveMesh(hm.ToMesh("Arena_LampHousings")), housing);
            Node("Lamp Lenses", root, SaveMesh(lm.ToMesh("Arena_LampLenses")), lens);
            var flares = Node("Lamp Flares", root, SaveMesh(fm.ToMesh("Arena_LampFlares")), flareMat, false);
            surge.Add(flares.GetComponent<Renderer>());
        }

        private static void Lamp(MeshBuilder housing, MeshBuilder lens, MeshBuilder flare, Vector3 p, Vector3 aimAt, Vector3 viewer, float flareSize)
        {
            var dir = (aimAt - p).normalized;
            housing.Cylinder(p - dir * 0.3f, dir, 0.3f, 0.16f, 10);
            housing.Beam(p + Vector3.up * 0.38f, p - dir * 0.15f, 0.035f);
            ArenaMeshes.DiscFacing(lens, p + dir * 0.005f, dir, 0.13f, 12);
            var n = (dir + (viewer - p).normalized).normalized;
            ArenaMeshes.FaceQuad(flare, p + dir * 0.03f, n, flareSize, flareSize);
        }

        private static void Beam(Transform parent, string name, Vector3 from, Vector3 to, float halfAngleDeg, Material mat, Mesh cone,
                                 List<Transform> sweep, List<Renderer> surge)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var d = to - from;
            float len = d.magnitude;
            float r = len * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.95f ? Vector3.forward : Vector3.up));
            go.transform.localScale = new Vector3(r, r, len);
            go.AddComponent<MeshFilter>().sharedMesh = cone;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            Quiet(mr);
            if (sweep != null) sweep.Add(go.transform);
            surge.Add(mr);
        }

        private static DamageCam BuildJumbotron(Transform t, Art art)
        {
            JumboPose(out var pos, out var rot);
            var root = Group("Jumbotron", t, true);
            root.SetPositionAndRotation(pos, rot);
            var rt = GetOrCreateRt();
            var screen = Led("Arena_JumboScreen", rt, 256f, 144f, 0.28f, 1.25f);
            var overlay = Mat("Arena_JumboOverlay", UnlitShader, m =>
            {
                m.SetTexture("_BaseMap", art.Overlay);
                m.SetColor("_BaseColor", new Color(1.4f, 1.4f, 1.4f, 1f));
                MakeTransparent(m);
            });
            var frame = Lit("Arena_JumboFrame", Hex("121419"), 0.7f, 0.5f);
            var sign = Led("Arena_JumboSign", art.Sign, 400f, 60f, 0.2f, 1.3f);
            var side = Led("Arena_JumboSide", art.Side, 30f, 190f, 0.2f, 1.2f);

            // Local space: the ring is towards -Z, so every face quad faces Vector3.back.
            var sq = new MeshBuilder();
            ArenaMeshes.FaceQuad(sq, Vector3.zero, Vector3.back, JumboW, JumboH);
            Node("Screen", root, SaveMesh(sq.ToMesh("Jumbo_Screen")), screen);
            var oq = new MeshBuilder();
            ArenaMeshes.FaceQuad(oq, new Vector3(0f, 0f, -0.01f), Vector3.back, JumboW, JumboH);
            Node("Overlay", root, SaveMesh(oq.ToMesh("Jumbo_Overlay")), overlay);
            var pq = new MeshBuilder();
            ArenaMeshes.FaceQuad(pq, new Vector3(-(JumboW * 0.5f + 0.32f), 0f, -0.005f), Vector3.back, 0.44f, JumboH + 0.3f);
            ArenaMeshes.FaceQuad(pq, new Vector3(JumboW * 0.5f + 0.32f, 0f, -0.005f), Vector3.back, 0.44f, JumboH + 0.3f);
            Node("Side Panels", root, SaveMesh(pq.ToMesh("Jumbo_Sides")), side);
            var gq = new MeshBuilder();
            ArenaMeshes.FaceQuad(gq, new Vector3(0f, -(JumboH * 0.5f + 0.36f), -0.005f), Vector3.back, 2.3f, 0.36f);
            Node("Sign", root, SaveMesh(gq.ToMesh("Jumbo_Sign")), sign);

            var fb = new MeshBuilder();
            fb.Box(new Vector3(0f, -0.2f, 0.17f), new Vector3(JumboW + 1.3f, JumboH + 0.95f, 0.3f), Quaternion.identity);
            float bw = 0.07f;
            fb.Box(new Vector3(0f, JumboH * 0.5f + bw * 0.5f, -0.01f), new Vector3(JumboW + bw * 2f, bw, 0.06f), Quaternion.identity);
            fb.Box(new Vector3(0f, -JumboH * 0.5f - bw * 0.5f, -0.01f), new Vector3(JumboW + bw * 2f, bw, 0.06f), Quaternion.identity);
            fb.Box(new Vector3(-JumboW * 0.5f - bw * 0.5f, 0f, -0.01f), new Vector3(bw, JumboH, 0.06f), Quaternion.identity);
            fb.Box(new Vector3(JumboW * 0.5f + bw * 0.5f, 0f, -0.01f), new Vector3(bw, JumboH, 0.06f), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
                fb.Beam(new Vector3(s * 1.6f, JumboH * 0.5f + 0.25f, 0.15f), new Vector3(s * 1.6f, 24f - JumboY, 0.15f), 0.025f);
            Node("Frame", root, SaveMesh(fb.ToMesh("Jumbo_Frame")), frame);

            var camGo = new GameObject("Damage Cam");
            camGo.transform.SetParent(t, false);
            camGo.transform.position = new Vector3(-1.3f, 1.5f, -0.6f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.targetTexture = rt;
            cam.depth = -10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("050307");
            cam.cullingMask = ~((1 << UiLayer) | (1 << Layer("SnapshotOnly")) | (1 << Layer("HideInSnapshot")));
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            return camGo.AddComponent<DamageCam>();
        }

        private static Transform BuildAtmosphere(Transform t)
        {
            var root = Group("Atmosphere", t, false);
            var haze = Soft("Arena_Haze", new Color(0.42f, 0.48f, 0.8f), 0.02f, 0.9f, 0.85f, 2.4f, 0.025f, 4f);
            var hb = new MeshBuilder();
            float[] zs = { 3.5f, 8f, 13f };
            for (int i = 0; i < zs.Length; i++)
                ArenaMeshes.FaceQuad(hb, new Vector3(0f, 4.5f + i * 0.8f, zs[i]), Vector3.back, 22f, 10f);
            Node("Haze", root, SaveMesh(hb.ToMesh("Arena_Haze")), haze, false);
            var pb = new MeshBuilder();
            ArenaMeshes.FaceQuad(pb, new Vector3(0f, 0.015f, 0f), Vector3.up, 9.5f, 9.5f);
            Node("Light Pool", root, SaveMesh(pb.ToMesh("Arena_LightPool")), Soft("Arena_LightPool", new Color(1f, 0.93f, 0.82f), 0.2f, 1.4f), false);
            return root;
        }

        // ── Assets ───────────────────────────────────────────────────
        private static VolumeProfile GetOrCreatePostProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (existing != null) return existing;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, PostPath);
            AddOverride<Tonemapping>(p).mode.value = TonemappingMode.ACES;
            var bloom = AddOverride<Bloom>(p);
            bloom.threshold.value = 1f;
            bloom.intensity.value = 1f;
            bloom.scatter.value = 0.65f;
            var grade = AddOverride<ColorAdjustments>(p);
            grade.postExposure.value = 0.05f;
            grade.contrast.value = 28f;
            grade.saturation.value = 14f;
            // A touch of negative lift: the reference's blacks are crushed, not milky.
            AddOverride<LiftGammaGain>(p).lift.value = new Vector4(1f, 1f, 1f, -0.03f);
            var vignette = AddOverride<Vignette>(p);
            vignette.intensity.value = 0.38f;
            vignette.smoothness.value = 0.42f;
            var split = AddOverride<SplitToning>(p);
            split.shadows.value = new Color(0.42f, 0.47f, 0.62f);
            split.highlights.value = new Color(0.62f, 0.54f, 0.45f);
            split.balance.value = -10f;
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        private static T AddOverride<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        private static RenderTexture GetOrCreateRt()
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt != null) return rt;
            rt = new RenderTexture(512, 288, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "DamageCam",
                antiAliasing = 1,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
            };
            AssetDatabase.CreateAsset(rt, RtPath);
            return rt;
        }

        private static Mesh SaveMesh(Mesh m)
        {
            string path = GenDir + "/" + m.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Mesh Merge(string name, Mesh a, Mesh b)
        {
            var m = new Mesh { name = name };
            m.CombineMeshes(new[] { new CombineInstance { mesh = a, transform = Matrix4x4.identity }, new CombineInstance { mesh = b, transform = Matrix4x4.identity } }, true, false);
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            return m;
        }

        // ── Materials (created once; yours to tune after that) ──────────
        private static Material Mat(string name, string shader, System.Action<Material> init)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find(shader);
            if (sh == null) throw new System.InvalidOperationException("Shader not found: " + shader);
            m = new Material(sh) { name = name };
            init(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material Lit(string name, Color c, float metallic, float smooth, Texture2D albedo = null, Texture2D mask = null) =>
            Mat(name, LitShader, m =>
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smooth);
                if (albedo != null) m.SetTexture("_BaseMap", albedo);
                if (mask != null)
                {
                    m.SetTexture("_MetallicGlossMap", mask);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_Smoothness", 1f);
                }
            });

        private static Material Unlit(string name, Color hdr) => Mat(name, UnlitShader, m => m.SetColor("_BaseColor", hdr));

        private static Material Led(string name, Texture tex, float gridX, float gridY, float grid, float intensity) =>
            Mat(name, "SlapMania/Arena/Led", m =>
            {
                m.SetTexture("_MainTex", tex);
                m.SetFloat("_GridX", gridX);
                m.SetFloat("_GridY", gridY);
                m.SetFloat("_GridStrength", grid);
                m.SetFloat("_Intensity", intensity);
            });

        private static Material Soft(string name, Color c, float intensity, float power, float noise = 0f, float noiseScale = 3f,
                                     float noiseSpeed = 0.05f, float nearFade = 0f) =>
            Mat(name, "SlapMania/Arena/Soft", m =>
            {
                m.SetColor("_Color", c);
                m.SetFloat("_Intensity", intensity);
                m.SetFloat("_Power", power);
                m.SetFloat("_Noise", noise);
                m.SetFloat("_NoiseScale", noiseScale);
                m.SetFloat("_NoiseSpeed", noiseSpeed);
                m.SetFloat("_NearFade", nearFade);
            });

        private static Material BeamMat(string name, Color c, float intensity, float edge, float falloff, float dust) =>
            Mat(name, "SlapMania/Arena/Beam", m =>
            {
                m.SetColor("_Color", c);
                m.SetFloat("_Intensity", intensity);
                m.SetFloat("_EdgeSoftness", edge);
                m.SetFloat("_Falloff", falloff);
                m.SetFloat("_Dust", dust);
            });

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ── Scene helpers ────────────────────────────────────────────
        private static Transform Group(string name, Transform parent, bool isStatic)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go.transform;
        }

        /// A renderer child. Static ones batch and render into the reflection probe; additive
        /// fakes stay dynamic (ArenaDirector drives some, and they would smear the probe).
        private static GameObject Node(string name, Transform parent, Mesh mesh, Material mat, bool isStatic = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            Quiet(mr);
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        private static void Quiet(MeshRenderer mr)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }

        private static void Shadowed(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static Light Spot(string name, Transform parent, Vector3 pos, Vector3 target, float angle, float range, LightShadows shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(parent, false);
            var d = target - pos;
            l.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up));
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.range = range;
            l.shadows = shadows;
            return l;
        }

        private static void Tint(Light l, Color c, float intensity)
        {
            l.color = c;
            l.intensity = intensity;
        }
    }
}
