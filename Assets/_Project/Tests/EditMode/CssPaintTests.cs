using NUnit.Framework;
using UnityEngine;
using SlapMania.Core.Paint;

namespace SlapMania.Tests.EditMode
{
    /// Core.Paint turns the V2 mockups' CSS into baked textures (M10). These pin the CSS
    /// semantics a front-end screen depends on to look like its mockup.
    public class CssColorTests
    {
        private const float Eps = 1.5f / 255f;

        private static void Near(Color expected, Color actual, string msg = null)
        {
            Assert.AreEqual(expected.r, actual.r, Eps, msg + " r");
            Assert.AreEqual(expected.g, actual.g, Eps, msg + " g");
            Assert.AreEqual(expected.b, actual.b, Eps, msg + " b");
            Assert.AreEqual(expected.a, actual.a, Eps, msg + " a");
        }

        [Test]
        public void Hex_AllFourLengths()
        {
            Near(new Color(1f, 0f, 0f, 1f), CssColor.Parse("#f00"));
            Near(new Color(1f, 0f, 0f, 0.533f), CssColor.Parse("#f008"));
            Near(new Color(0xE3 / 255f, 0x26 / 255f, 0x2F / 255f, 1f), CssColor.Parse("#E3262F"));
            Near(new Color(0f, 0f, 0f, 0x80 / 255f), CssColor.Parse("#00000080"));
        }

        [Test]
        public void Rgba_CommaAndSlashSyntax_AndPercentAlpha()
        {
            Near(new Color(4 / 255f, 5 / 255f, 7 / 255f, 0.92f), CssColor.Parse("rgba(4,5,7,0.92)"));
            Near(new Color(1f, 1f, 1f, 0.06f), CssColor.Parse("rgba(255, 255, 255, .06)"));
            Near(new Color(1f, 0f, 0f, 0.5f), CssColor.Parse("rgb(255 0 0 / 50%)"));
        }

        [Test]
        public void Channels_AreClamped_NotWrapped()
        {
            Near(new Color(1f, 0f, 1f, 1f), CssColor.Parse("rgba(300,-20,255,7)"));
        }

        [Test]
        public void Garbage_IsRejected()
        {
            Assert.IsFalse(CssColor.TryParse(null, out _));
            Assert.IsFalse(CssColor.TryParse("", out _));
            Assert.IsFalse(CssColor.TryParse("#12", out _));
            Assert.IsFalse(CssColor.TryParse("#ggg", out _));
            Assert.IsFalse(CssColor.TryParse("rgba(1,2)", out _));
            Assert.IsFalse(CssColor.TryParse("hsl(0,0%,0%)", out _));
            Assert.IsFalse(CssColor.TryParse("rgba(NaN,0,0,1)x", out _));
        }

        [Test]
        public void Transparent_IsZeroEverything()
        {
            Near(new Color(0f, 0f, 0f, 0f), CssColor.Parse("transparent"));
        }
    }

    public class CssLengthTests
    {
        [TestCase("12px", 12f)]
        [TestCase("12", 12f)]
        [TestCase("0", 0f)]
        [TestCase("-4px", -4f)]
        [TestCase("50%", 100f)]
        [TestCase("center", 100f)]
        [TestCase("right", 200f)]
        [TestCase("calc(100% - 14px)", 186f)]
        [TestCase("calc(50% + 2px)", 102f)]
        [TestCase("calc(3px + 50%)", 103f)]
        [TestCase("calc(10px - 4px)", 6f)]
        public void Resolves_Against200(string css, float expected)
        {
            Assert.IsTrue(CssLength.TryParse(css, out var l), css);
            Assert.AreEqual(expected, l.Resolve(200f), 1e-4f, css);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("abc")]
        [TestCase("NaN")]
        [TestCase("Infinitypx")]
        [TestCase("calc(10px - 50%)")]
        [TestCase("calc(100% * 2)")]
        [TestCase("calc(1px + 2px + 3px)")]
        public void Rejects(string css)
        {
            Assert.IsFalse(CssLength.TryParse(css, out _), css ?? "null");
        }
    }

    public class CssPaintParserTests
    {
        [Test]
        public void Background_SplitsLayers_OnTopLevelCommasOnly()
        {
            Assert.IsTrue(CssPaintParser.TryParseBackground(
                "linear-gradient(90deg, rgba(7,8,10,0.92) 0%, rgba(7,8,10,0) 100%), linear-gradient(0deg, #000 0%, rgba(0,0,0,0) 45%)",
                out var layers));
            Assert.AreEqual(2, layers.Length);
            Assert.AreEqual(90f, layers[0].AngleDeg);
            Assert.AreEqual(0f, layers[1].AngleDeg);
            Assert.AreEqual(2, layers[0].Stops.Length);
        }

        [Test]
        public void Linear_DefaultsTo180_AndAcceptsToSide()
        {
            Assert.IsTrue(CssPaintParser.TryParseLayer("linear-gradient(#fff, #000)", out var a));
            Assert.AreEqual(180f, a.AngleDeg);
            Assert.IsTrue(CssPaintParser.TryParseLayer("linear-gradient(to right, #fff, #000)", out var b));
            Assert.AreEqual(90f, b.AngleDeg);
            Assert.IsTrue(CssPaintParser.TryParseLayer("linear-gradient(-62deg, #fff, #000)", out var c));
            Assert.AreEqual(-62f, c.AngleDeg);
        }

        [Test]
        public void DoublePositionStop_BecomesTwoStops()
        {
            Assert.IsTrue(CssPaintParser.TryParseLayer(
                "repeating-linear-gradient(-62deg,rgba(255,255,255,.045) 0 2px,rgba(255,255,255,0) 2px 9px)", out var l));
            Assert.IsTrue(l.Repeating);
            Assert.AreEqual(4, l.Stops.Length);
            Assert.AreEqual(ColorStop.PositionUnit.Px, l.Stops[3].Unit);
            Assert.AreEqual(9f, l.Stops[3].Position);
        }

        [Test]
        public void Radial_EllipseSizeAndPosition()
        {
            Assert.IsTrue(CssPaintParser.TryParseLayer(
                "radial-gradient(ellipse 90% 40% at 50% 0%, #2A0B0F 0%, #07080A 70%)", out var l));
            Assert.AreEqual(PaintKind.Radial, l.Kind);
            Assert.IsFalse(l.Circle);
            Assert.IsTrue(l.HasSize);
            Assert.AreEqual(90f, l.SizeX.Value);
            Assert.AreEqual(40f, l.SizeY.Value);
            Assert.AreEqual(0f, l.AtY.Value);
        }

        [Test]
        public void Radial_WithoutConfig_StartsWithColour()
        {
            Assert.IsTrue(CssPaintParser.TryParseLayer("radial-gradient(rgba(255,255,255,.09) 1px,rgba(255,255,255,0) 1.3px)", out var l));
            Assert.IsFalse(l.HasSize);
            Assert.AreEqual(2, l.Stops.Length);
        }

        [Test]
        public void Conic_FromAt_AndDegreeStops()
        {
            Assert.IsTrue(CssPaintParser.TryParseLayer(
                "repeating-conic-gradient(from 0deg at 50% 50%,rgba(255,255,255,.24) 0deg 1deg,rgba(255,255,255,0) 1deg 6deg)", out var l));
            Assert.AreEqual(PaintKind.Conic, l.Kind);
            Assert.AreEqual(4, l.Stops.Length);
            Assert.AreEqual(6f / 360f, l.Stops[3].Position, 1e-6f);
        }

        [Test]
        public void UnknownFunction_OrHint_FailsInsteadOfThrowing()
        {
            Assert.IsFalse(CssPaintParser.TryParseBackground("url(foo.png)", out _));
            Assert.IsFalse(CssPaintParser.TryParseBackground("linear-gradient(#fff, 30%, #000)", out _));
            Assert.IsFalse(CssPaintParser.TryParseBackground("", out _));
            Assert.IsFalse(CssPaintParser.TryParseBackground(null, out _));
        }

        [Test]
        public void Clip_KitSkew_ResolvesCalc()
        {
            var p = CssPaintParser.ParseClip("polygon(10px 0,100% 0,calc(100% - 10px) 100%,0 100%)");
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual(190f, p.Xs[2].Resolve(200f), 1e-4f);
            Assert.AreEqual(10f, p.Xs[0].Resolve(200f), 1e-4f);
        }

        [Test]
        public void Clip_PolygonAndCircle()
        {
            var p = CssPaintParser.ParseClip("polygon(14% 0,86% 0,100% 6%,100% 91%,50% 100%,0 91%,0 6%)");
            Assert.AreEqual(7, p.PointCount);
            var c = CssPaintParser.ParseClip("circle(150px at 0 100%)");
            Assert.AreEqual(ClipShape.ShapeKind.Circle, c.Kind);
            Assert.AreEqual(150f, c.Radius.Value);
            Assert.AreEqual(100f, c.CenterY.Value);
            Assert.IsNull(CssPaintParser.ParseClip("inset(10px)"));
            Assert.IsNull(CssPaintParser.ParseClip("polygon(0 0, 1px 1px)"));
        }

        [Test]
        public void Shadows_OuterInsetAndRing()
        {
            var s = CssPaintParser.ParseShadows("0 0 0 1px rgba(255,75,83,.5),0 0 34px rgba(227,38,47,.35)");
            Assert.AreEqual(2, s.Length);
            Assert.AreEqual(1f, s[0].Spread);
            Assert.AreEqual(34f, s[1].Blur);
            var i = CssPaintParser.ParseShadows("inset 0 0 0 6px rgba(255,75,83,0.7), inset 0 0 60px rgba(255,40,50,0.6)");
            Assert.IsTrue(i[0].Inset && i[1].Inset);
            Assert.AreEqual(0f, i[0].Reach);
            Assert.AreEqual(0, CssPaintParser.ParseShadows("none").Length);
        }
    }

    public class PaintRasterTests
    {
        private const float Eps = 0.01f;

        private static PaintLayer[] Bg(string css)
        {
            Assert.IsTrue(CssPaintParser.TryParseBackground(css, out var l), css);
            return l;
        }

        [Test]
        public void Stops_UnsetSpreadEvenly_AndPositionsNeverDecrease()
        {
            var stops = new[]
            {
                new ColorStop(Color.red),
                new ColorStop(Color.green),
                new ColorStop(Color.blue, 0.8f, ColorStop.PositionUnit.Fraction),
                new ColorStop(Color.white, 0.5f, ColorStop.PositionUnit.Fraction), // clamps to 0.8
                new ColorStop(Color.black),
            };
            PaintRaster.ResolveStops(stops, 100f, out var pos, out _);
            Assert.AreEqual(0f, pos[0], 1e-6f);
            Assert.AreEqual(0.4f, pos[1], 1e-6f);
            Assert.AreEqual(0.8f, pos[2], 1e-6f);
            Assert.AreEqual(0.8f, pos[3], 1e-6f);
            Assert.AreEqual(1f, pos[4], 1e-6f);
        }

        [Test]
        public void Stops_PxConvertAgainstLength()
        {
            var stops = new[] { new ColorStop(Color.red, 0f, ColorStop.PositionUnit.Px), new ColorStop(Color.blue, 25f, ColorStop.PositionUnit.Px) };
            PaintRaster.ResolveStops(stops, 100f, out var pos, out _);
            Assert.AreEqual(0.25f, pos[1], 1e-6f);
        }

        [Test]
        public void Stops_ZeroLength_DoesNotProduceNaN()
        {
            var stops = new[] { new ColorStop(Color.red, 0f, ColorStop.PositionUnit.Px), new ColorStop(Color.blue, 25f, ColorStop.PositionUnit.Px) };
            PaintRaster.ResolveStops(stops, 0f, out var pos, out _);
            Assert.IsFalse(float.IsNaN(pos[1]));
        }

        [Test]
        public void Linear180_TopIsFirstStop_BottomIsLast_MiddleIsHalfway()
        {
            var l = Bg("linear-gradient(180deg, #000000, #ffffff)");
            Assert.AreEqual(0f, PaintRaster.Sample(l, 50f, 0f, 100f, 200f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 50f, 200f, 100f, 200f).r, Eps);
            Assert.AreEqual(0.5f, PaintRaster.Sample(l, 50f, 100f, 100f, 200f).r, Eps);
        }

        [Test]
        public void LinearAngled_CornersLandExactlyOnEndStops()
        {
            // CSS sizes the gradient line so the far corners hit 0% and 100% for any angle.
            var l = Bg("linear-gradient(115deg, #000000, #ffffff)");
            Assert.AreEqual(0f, PaintRaster.Sample(l, 0f, 0f, 390f, 104f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 390f, 104f, 390f, 104f).r, Eps);
        }

        [Test]
        public void HardStop_ExactEdgeAndBeyond()
        {
            var l = Bg("linear-gradient(90deg, #ff0000 50%, #0000ff 50%)");
            Assert.AreEqual(1f, PaintRaster.Sample(l, 49f, 5f, 100f, 10f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 51f, 5f, 100f, 10f).b, Eps);
            // Outside the box clamps to the end colours rather than extrapolating.
            Assert.AreEqual(1f, PaintRaster.Sample(l, -1000f, 5f, 100f, 10f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 1e7f, 5f, 100f, 10f).b, Eps);
        }

        [Test]
        public void FadeToTransparent_InterpolatesPremultiplied_NoGreyFringe()
        {
            // rgba(255,0,0,1) -> rgba(0,0,0,0): browsers keep the midpoint pure red at half alpha.
            var l = Bg("linear-gradient(90deg, rgba(255,0,0,1), rgba(0,0,0,0))");
            var mid = PaintRaster.Sample(l, 50f, 1f, 100f, 2f);
            Assert.AreEqual(1f, mid.r, Eps);
            Assert.AreEqual(0.5f, mid.a, Eps);
        }

        [Test]
        public void Radial_FarthestCornerEllipse_CentreAndCorner()
        {
            var l = Bg("radial-gradient(ellipse at 50% 50%, #000000, #ffffff)");
            Assert.AreEqual(0f, PaintRaster.Sample(l, 50f, 25f, 100f, 50f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 0f, 0f, 100f, 50f).r, Eps);
        }

        [Test]
        public void Radial_PxStopsInCircle_MakeAHardRing()
        {
            // The Brace ring mask: transparent inside 57px, opaque from 58px.
            var l = Bg("radial-gradient(circle, rgba(0,0,0,0) 57px, #000 58px)");
            Assert.AreEqual(0f, PaintRaster.Sample(l, 65f, 65f + 50f, 130f, 130f).a, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 65f, 65f + 60f, 130f, 130f).a, Eps);
        }

        [Test]
        public void Conic_ZeroAtTop_Clockwise()
        {
            var l = Bg("conic-gradient(#ff0000 0deg 90deg, #0000ff 90deg 360deg)");
            Assert.AreEqual(1f, PaintRaster.Sample(l, 60f, 10f, 100f, 100f).r, Eps, "top-right quadrant is red");
            Assert.AreEqual(1f, PaintRaster.Sample(l, 40f, 10f, 100f, 100f).b, Eps, "top-left quadrant is blue");
        }

        [Test]
        public void Repeating_WrapsByStopSpan_IncludingNegative()
        {
            var l = Bg("repeating-linear-gradient(90deg, #ff0000 0px 5px, #0000ff 5px 10px)");
            Assert.AreEqual(1f, PaintRaster.Sample(l, 2f, 1f, 100f, 2f).r, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, 17f, 1f, 100f, 2f).b, Eps);
            Assert.AreEqual(1f, PaintRaster.Sample(l, -3f, 1f, 100f, 2f).b, Eps, "-3 wraps to 7");
        }

        [Test]
        public void Layers_FirstIsOnTop()
        {
            var l = Bg("linear-gradient(#ff0000, #ff0000), #0000ff");
            Assert.AreEqual(1f, PaintRaster.Sample(l, 1f, 1f, 2f, 2f).r, Eps);
            Assert.AreEqual(0f, PaintRaster.Sample(l, 1f, 1f, 2f, 2f).b, Eps);
        }

        [Test]
        public void Tile_RepeatsTheLayer()
        {
            var l = Bg("linear-gradient(90deg, #000000, #ffffff)");
            l[0].TileW = 10f; l[0].TileH = 10f;
            Assert.AreEqual(PaintRaster.Sample(l, 2.5f, 1f, 100f, 100f).r, PaintRaster.Sample(l, 52.5f, 31f, 100f, 100f).r, 1e-4f);
        }

        [Test]
        public void Rasterize_SkewClip_AntialiasesEdges_AndFlipsRows()
        {
            var req = new PaintRaster.Request
            {
                Layers = Bg("#ffffff"),
                Clip = CssPaintParser.ParseClip("polygon(10% 0, 100% 0, 90% 100%, 0 100%)"),
                CssW = 100f, CssH = 10f, TexW = 100, TexH = 10,
            };
            var px = new Color32[100 * 10];
            PaintRaster.Rasterize(req, px);
            // Top CSS row is the last texture row. Its left 10% is clipped away.
            Assert.AreEqual(0, px[9 * 100 + 0].a);
            Assert.AreEqual(255, px[9 * 100 + 50].a);
            // Bottom CSS row keeps its left edge.
            Assert.AreEqual(255, px[0 * 100 + 2].a);
        }

        [Test]
        public void Rasterize_RejectsTooSmallBuffer()
        {
            var req = new PaintRaster.Request { Layers = Bg("#fff"), CssW = 4, CssH = 4, TexW = 4, TexH = 4 };
            Assert.Throws<System.ArgumentException>(() => PaintRaster.Rasterize(req, new Color32[3]));
        }

        [Test]
        public void PolygonDistance_SignAndMagnitude()
        {
            var xs = new[] { 0f, 10f, 10f, 0f };
            var ys = new[] { 0f, 0f, 10f, 10f };
            Assert.AreEqual(5f, PaintRaster.PolygonSignedDistance(xs, ys, 5f, 5f), 1e-4f);
            Assert.AreEqual(-3f, PaintRaster.PolygonSignedDistance(xs, ys, 13f, 5f), 1e-4f);
        }

        [Test]
        public void RoundRectDistance_EdgeIsZero()
        {
            Assert.AreEqual(0f, PaintRaster.RoundRectDistance(10f, 5f, 0f, 0f, 10f, 10f, 0f), 1e-4f);
            Assert.AreEqual(-5f, PaintRaster.RoundRectDistance(5f, 5f, 0f, 0f, 10f, 10f, 2f), 1e-4f);
            Assert.Greater(PaintRaster.RoundRectDistance(0f, 0f, 0f, 0f, 10f, 10f, 5f), 0f, "rounded corner is outside");
        }

        [Test]
        public void Erfc_KnownValues()
        {
            Assert.AreEqual(1f, PaintRaster.Erfc(0f), 1e-6f);
            Assert.AreEqual(0.157299f, PaintRaster.Erfc(1f), 1e-5f);
            Assert.AreEqual(1.842701f, PaintRaster.Erfc(-1f), 1e-5f);
            Assert.AreEqual(0f, PaintRaster.Erfc(1e6f), 1e-6f);
        }

        [Test]
        public void Shadow_OuterGlow_HalfAtEdge_InsetFrameInside()
        {
            var glow = CssPaintParser.ParseShadows("0 0 34px #ff0000")[0];
            Assert.AreEqual(0.5f, PaintRaster.ShadowAlpha(glow, 100f, 50f, 100f, 100f, 0f), 0.01f, "edge is half");
            Assert.Less(PaintRaster.ShadowAlpha(glow, 140f, 50f, 100f, 100f, 0f), 0.05f, "one blur radius out is faint");

            var frame = CssPaintParser.ParseShadows("inset 0 0 0 6px #ff0000")[0];
            Assert.AreEqual(1f, PaintRaster.ShadowAlpha(frame, 2f, 50f, 100f, 100f, 0f), 0.01f, "inside the 6px frame");
            Assert.AreEqual(0f, PaintRaster.ShadowAlpha(frame, 50f, 50f, 100f, 100f, 0f), 0.01f, "centre is clear");
        }
    }
}
