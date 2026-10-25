using UnityEditor;
using Unity.VectorGraphics.Editor;

namespace SlapMania.Editor.FrontEnd
{
    /// Import defaults for the front end's art (M10, ADR-0013), applied only to a file's first
    /// import (importSettingsMissing), so a later hand-tweak in the Inspector survives:
    ///   * Art/UI/Icons/*.svg   -> UI Toolkit VectorImage keeping its viewBox (crisp at any DPI);
    ///   * Art/UI/Mockup/*      -> uncompressed-quality RGBA without the default
    ///     power-of-two rescale or mipmaps, so a 430 x 932 photo stays 430 x 932.
    internal sealed class UiAssetImportRules : AssetPostprocessor
    {
        public const string IconDir = "Assets/_Project/Art/UI/Icons/";
        public const string MockupDir = "Assets/_Project/Art/UI/Mockup/";

        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(IconDir) || !assetPath.EndsWith(".svg")) return;
            if (!(assetImporter is SVGImporter svg) || !assetImporter.importSettingsMissing) return;
            Apply(svg);
        }

        /// VectorImage for UI Toolkit, keeping the SVG's viewBox: the default crops each icon
        /// to its drawn bounds, which drops the padding the mockup's 24 x 24 icons rely on and
        /// draws them visibly larger than the mockup.
        public static bool Apply(SVGImporter svg)
        {
            bool changed = svg.SvgType != SVGType.VectorImage || svg.ViewportOptions != Unity.VectorGraphics.ViewportOptions.PreserveViewport;
            svg.SvgType = SVGType.VectorImage;
            svg.ViewportOptions = Unity.VectorGraphics.ViewportOptions.PreserveViewport;
            return changed;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(MockupDir) || !assetImporter.importSettingsMissing) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.mipmapEnabled = false;
            t.sRGBTexture = true;
            t.alphaIsTransparency = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.maxTextureSize = 2048;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
