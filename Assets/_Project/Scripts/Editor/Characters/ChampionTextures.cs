using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Characters
{
    /// Gives a champion model textured materials (M08). Many downloaded rigs arrive as bare FBX
    /// files whose materials have no images (S-102), and render as grey clay.
    ///   1. Embedded materials are extracted to "<folder>/<stem>_Materials/" so they can be edited.
    ///   2. Images next to the model (its folder, "<stem>.fbm/", "Textures/") are matched by name
    ///      (basecolor / albedo / diffuse, normal, metallic) and wired to every material without
    ///      a base map.
    ///   3. No images: a seamless procedural detail set (tonal variation + normal map) is generated
    ///      for the champion and wired in, and `baseColor`, when given, tints the materials.
    ///      Procedural, not painted: it reads right on any UV layout.
    /// Materials that already have a base map are never touched.
    public static class ChampionTextures
    {
        public const string GeneratedDir = "Assets/_Project/Art/Generated/Champions";
        private const int Size = 512;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int MetallicMap = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public static string Apply(string modelPath, string id, Color? baseColor)
        {
            var sb = new System.Text.StringBuilder();
            var mats = ExtractMaterials(modelPath, sb);
            var bare = new List<Material>();
            foreach (var m in mats) if (m.HasProperty(BaseMap) && m.GetTexture(BaseMap) == null) bare.Add(m);
            if (bare.Count == 0) { sb.Append("  textures: every material already has a base map\n"); return sb.ToString(); }

            FindImages(modelPath, out var albedo, out var normal, out var metallic);
            if (albedo == null)
            {
                GenerateDetail(id, out albedo, out normal);
                sb.Append("  textures: none found; generated ").Append(AssetDatabase.GetAssetPath(albedo)).Append(" + normal\n");
            }
            else sb.Append("  textures: found ").Append(albedo.name).Append(normal != null ? " + normal" : "").Append(metallic != null ? " + metallic" : "").Append('\n');

            foreach (var m in bare)
            {
                m.SetTexture(BaseMap, albedo);
                if (normal != null && m.HasProperty(BumpMap)) { m.SetTexture(BumpMap, normal); m.EnableKeyword("_NORMALMAP"); }
                if (metallic != null && m.HasProperty(MetallicMap)) { m.SetTexture(MetallicMap, metallic); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                if (baseColor.HasValue && m.HasProperty(BaseColor)) m.SetColor(BaseColor, baseColor.Value);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            sb.Append("  textures: wired into ").Append(bare.Count).Append(" material(s)\n");
            return sb.ToString();
        }

        // ── Materials ────────────────────────────────────────────────
        /// The model's materials as editable assets: embedded ones are extracted once and remapped.
        private static List<Material> ExtractMaterials(string modelPath, System.Text.StringBuilder sb)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            var embedded = new List<Material>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is Material m) embedded.Add(m);
            if (embedded.Count > 0)
            {
                string stem = Path.GetFileNameWithoutExtension(modelPath);
                string dir = Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/" + stem + "_Materials";
                EnsureFolder(dir);
                foreach (var m in embedded)
                {
                    string target = dir + "/" + Safe(m.name) + ".mat";
                    string err = AssetDatabase.ExtractAsset(m, target);
                    if (!string.IsNullOrEmpty(err)) sb.Append("  ! extract ").Append(m.name).Append(": ").Append(err).Append('\n');
                    else AssetDatabase.WriteImportSettingsIfDirty(target);
                }
                imp.SaveAndReimport();
                sb.Append("  materials: extracted ").Append(embedded.Count).Append(" to ").Append(dir).Append('\n');
            }

            var list = new List<Material>();
            foreach (var kv in imp.GetExternalObjectMap())
                if (kv.Value is Material m && !list.Contains(m)) list.Add(m);
            return list;
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        // ── Found images ─────────────────────────────────────────────
        private static void FindImages(string modelPath, out Texture2D albedo, out Texture2D normal, out Texture2D metallic)
        {
            albedo = normal = metallic = null;
            string dir = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(modelPath);
            // The model's own folders only: a shared parent folder holds other characters' images.
            foreach (var folder in new[] { dir, dir + "/" + stem + ".fbm", dir + "/Textures", dir + "/textures" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue; // not recursive
                    string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (albedo == null && (n.Contains("basecolor") || n.Contains("base_color") || n.Contains("albedo") || n.Contains("diffuse"))) albedo = tex;
                    else if (normal == null && n.Contains("normal")) { normal = tex; MarkNormal(path); }
                    else if (metallic == null && n.Contains("metal")) metallic = tex;
                }
            }
        }

        private static void MarkNormal(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        // ── Generated detail ─────────────────────────────────────────
        /// A seamless neutral detail set: an albedo near white (so _BaseColor decides the colour)
        /// with soft mottling, and a normal map of fine pores over broad lumps. Seeded by the id,
        /// so a rebuild produces the same images.
        public static void GenerateDetail(string id, out Texture2D albedo, out Texture2D normal)
        {
            EnsureFolder(GeneratedDir);
            int seed = 17;
            foreach (char c in id) seed = unchecked(seed * 31 + c); // stable across runs, unlike string.GetHashCode
            var height = new float[Size * Size];
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float broad = Fbm(x, y, 8, 4, seed);
                float fine = Fbm(x, y, 64, 3, seed + 17);
                height[y * Size + x] = broad * 0.6f + fine * 0.4f;
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (0.86f + 0.14f * broad - 0.05f * fine)), 0, 255);
                px[y * Size + x] = new Color32(v, v, v, 255);
            }
            albedo = Save(GeneratedDir + "/" + id + "_Detail_Albedo.png", px, false);

            const float strength = 2.2f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (H(height, x + 1, y) - H(height, x - 1, y)) * strength;
                float dy = (H(height, x, y + 1) - H(height, x, y - 1)) * strength;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                px[y * Size + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
            }
            normal = Save(GeneratedDir + "/" + id + "_Detail_Normal.png", px, true);
        }

        private static float H(float[] h, int x, int y) => h[((y + Size) % Size) * Size + (x + Size) % Size];

        // Tileable fractal value noise: `cells` lattice cells across the texture, doubled per octave.
        private static float Fbm(int x, int y, int cells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value((float)x / Size * cells, (float)y / Size * cells, cells, seed + o * 101);
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / norm;
        }

        private static float Value(float fx, float fy, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period; // wraps, so the texture tiles
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static Texture2D Save(string path, Color32[] px, bool isNormal)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = Size;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
