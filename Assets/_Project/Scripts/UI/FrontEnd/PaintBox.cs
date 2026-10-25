using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core.Paint;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd
{
    /// A VisualElement that paints a CSS background the way the V2 mockups write it (M10,
    /// ADR-0013): gradients of every kind, patterns, clip-path shapes, masks and box-shadows.
    /// UI Toolkit draws only flat colours and images natively, so this element turns the CSS
    /// into one of three renderings:
    ///
    ///   Solid  - a flat colour with no clip: plain `background-color`, so the native
    ///            border-radius and border keep working.
    ///   Vector - a clip polygon filled with one colour or one linear gradient (the kit's
    ///            `.skew` / `.cc` buttons and tags): Painter2D, anti-aliased at any DPI.
    ///   Baked  - everything else: Core.Paint rasterises a texture, shown as the element's
    ///            background image (radius-clipped by UI Toolkit). Smooth gradients bake at
    ///            low resolution, axis-aligned ones into a 1-texel strip; hard edges bake at
    ///            device resolution. Textures are shared through PaintTextureCache.
    ///
    /// box-shadow is baked separately and drawn as a quad that reaches past the border box.
    /// Bakes happen on attach and size change only, never per frame.
    [UxmlElement]
    public partial class PaintBox : VisualElement
    {
        private const int MaxTexture = 1024;

        private string _paint, _mask, _clip, _shadow, _tile, _edge;

        [UxmlAttribute("paint")]  public string Paint  { get => _paint;  set { _paint = value;  Invalidate(); } }
        [UxmlAttribute("mask")]   public string Mask   { get => _mask;   set { _mask = value;   Invalidate(); } }
        [UxmlAttribute("clip")]   public string Clip   { get => _clip;   set { _clip = value;   Invalidate(); } }
        [UxmlAttribute("shadow")] public string Shadow { get => _shadow; set { _shadow = value; Invalidate(); } }
        /// The border of a clip-path shape, "1px rgba(...)": CSS clips the border with the shape,
        /// which a UI Toolkit border cannot do, so the converter hands it to this element.
        [UxmlAttribute("edge")]   public string Edge   { get => _edge;   set { _edge = value;   Invalidate(); } }
        /// background-size for patterns, e.g. "8px 8px".
        [UxmlAttribute("tile")]   public string Tile   { get => _tile;   set { _tile = value;   Invalidate(); } }

        private enum Mode : byte { None, Solid, Vector, Baked }

        private bool _parsed;
        private Mode _mode;
        private PaintLayer[] _layers;
        private PaintLayer[] _maskLayers;
        private ClipShape _clipShape;
        private BoxShadow[] _shadows;
        private Color _solid;
        private Gradient _vectorGradient;
        private Vector2 _bakedSize = new Vector2(-1f, -1f);
        private Texture2D _paintTex, _shadowTex;
        private string _paintKey, _shadowKey;
        private float _shadowPad;
        private readonly Vector2[] _pts = new Vector2[16];
        private int _ptCount;
        private readonly Vector2[] _inner = new Vector2[24];
        private readonly Vector2[] _scratch = new Vector2[24];
        private int _innerCount;
        private float _edgeWidth;
        private Color _edgeColor;

        public PaintBox()
        {
            generateVisualContent += OnGenerate;
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        private void Invalidate()
        {
            _parsed = false;
            _bakedSize = new Vector2(-1f, -1f);
            if (panel != null) Rebuild();
        }

        private void OnGeometryChanged(GeometryChangedEvent e)
        {
            var size = e.newRect.size;
            if (Mathf.Abs(size.x - _bakedSize.x) < 0.5f && Mathf.Abs(size.y - _bakedSize.y) < 0.5f) return;
            Rebuild();
        }

        private void OnDetach(DetachFromPanelEvent e)
        {
            ReleaseTextures();
            _bakedSize = new Vector2(-1f, -1f);
        }

        // ── Parse ────────────────────────────────────────────────────
        private void Parse()
        {
            _parsed = true;
            _layers = null; _maskLayers = null; _clipShape = null; _shadows = System.Array.Empty<BoxShadow>();
            if (!string.IsNullOrEmpty(_paint) && !CssPaintParser.TryParseBackground(_paint, out _layers))
                SlapLog.Warn(LogChannel.UI, "PaintBox '" + name + "': cannot parse paint \"" + _paint + "\"");
            if (!string.IsNullOrEmpty(_mask) && !CssPaintParser.TryParseBackground(_mask, out _maskLayers))
                _maskLayers = null;
            _clipShape = CssPaintParser.ParseClip(_clip);
            _shadows = CssPaintParser.ParseShadows(_shadow);
            ParseEdge();
            ApplyTile();

            _mode = Mode.None;
            if (_layers == null || _layers.Length == 0) return;
            bool single = _layers.Length == 1 && !_layers[0].IsTiled && _maskLayers == null;
            var l0 = _layers[0];
            if (single && _clipShape == null && l0.Kind == PaintKind.Solid) { _mode = Mode.Solid; _solid = l0.Solid; return; }
            if (single && _clipShape != null && _clipShape.Kind == ClipShape.ShapeKind.Polygon
                && (l0.Kind == PaintKind.Solid || (l0.Kind == PaintKind.Linear && !l0.Repeating && l0.Stops.Length <= 8)))
            {
                _mode = Mode.Vector;
                _solid = l0.Kind == PaintKind.Solid ? l0.Solid : Color.white;
                return;
            }
            _mode = Mode.Baked;
        }

        private void ParseEdge()
        {
            _edgeWidth = 0f;
            if (string.IsNullOrEmpty(_edge)) return;
            var parts = CssPaintParser.SplitTopLevel(_edge, ' ');
            if (parts.Count < 2 || !CssLength.TryParse(parts[0], out var w) || w.IsPercent) return;
            if (!CssColor.TryParse(parts[1], out _edgeColor)) return;
            _edgeWidth = w.Value;
        }

        /// `tile` from the attribute, or an exact repeat tile for a single repeating linear
        /// gradient measured in px (the kit's `.hatch`): stepping one tile along x or y moves
        /// the pattern by exactly one period, so a small texture repeats seamlessly.
        private void ApplyTile()
        {
            if (_layers == null) return;
            if (!string.IsNullOrEmpty(_tile))
            {
                var parts = CssPaintParser.SplitTopLevel(_tile, ' ');
                if (parts.Count >= 1 && CssLength.TryParse(parts[0], out var tw) && !tw.IsPercent)
                {
                    float th = tw.Value;
                    if (parts.Count >= 2 && CssLength.TryParse(parts[1], out var t2) && !t2.IsPercent) th = t2.Value;
                    foreach (var l in _layers) { l.TileW = tw.Value; l.TileH = th; }
                }
                return;
            }
            if (_layers.Length != 1) return;
            var g = _layers[0];
            if (g.Kind != PaintKind.Linear || !g.Repeating || g.Stops.Length < 2) return;
            foreach (var s in g.Stops)
                if (s.HasPosition && s.Unit == ColorStop.PositionUnit.Fraction && s.Position != 0f) return;
            float period = g.Stops[g.Stops.Length - 1].Position - g.Stops[0].Position;
            if (period <= 0.01f) return;
            float a = g.AngleDeg * Mathf.Deg2Rad;
            float dx = Mathf.Abs(Mathf.Sin(a)), dy = Mathf.Abs(Mathf.Cos(a));
            g.TileW = dx > 1e-3f ? period / dx : 1f;
            g.TileH = dy > 1e-3f ? period / dy : 1f;
        }

        // ── Build ────────────────────────────────────────────────────
        private void Rebuild()
        {
            if (!_parsed) Parse();
            var size = layout.size;
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x < 0.5f || size.y < 0.5f) return;
            _bakedSize = size;
            ReleaseTextures();

            switch (_mode)
            {
                case Mode.Solid:
                    style.backgroundColor = _solid;
                    style.backgroundImage = StyleKeyword.Null;
                    break;
                case Mode.Vector:
                    style.backgroundImage = StyleKeyword.Null;
                    BuildVector(size);
                    break;
                case Mode.Baked:
                    BakePaint(size);
                    break;
                default:
                    style.backgroundImage = StyleKeyword.Null;
                    break;
            }
            if (_shadows.Length > 0) BakeShadows(size);
            BuildEdge(size);
            MarkDirtyRepaint();
        }

        private void ResolvePolygon(Vector2 size)
        {
            _ptCount = Mathf.Min(_clipShape.PointCount, _pts.Length);
            for (int i = 0; i < _ptCount; i++)
                _pts[i] = new Vector2(_clipShape.Xs[i].Resolve(size.x), _clipShape.Ys[i].Resolve(size.y));
        }

        /// The clipped border is the shape minus (shape ∩ the border-inset box): where the shape
        /// follows the box edge the border shows, where it cuts across (a skew, a chamfer) it
        /// does not - exactly what CSS draws for a bordered element with a clip-path.
        private void BuildEdge(Vector2 size)
        {
            _innerCount = 0;
            if (_edgeWidth <= 0f || _clipShape == null || _clipShape.Kind != ClipShape.ShapeKind.Polygon) return;
            if (_mode != Mode.Vector) ResolvePolygon(size);
            float b = _edgeWidth;
            int n = _ptCount;
            System.Array.Copy(_pts, _inner, n);
            n = ClipAxis(_inner, n, _scratch, 0, b, true);        // x >= b
            n = ClipAxis(_scratch, n, _inner, 0, size.x - b, false); // x <= w - b
            n = ClipAxis(_inner, n, _scratch, 1, b, true);        // y >= b
            n = ClipAxis(_scratch, n, _inner, 1, size.y - b, false); // y <= h - b
            _innerCount = n;
        }

        // Sutherland-Hodgman against one axis-aligned line.
        private static int ClipAxis(Vector2[] src, int count, Vector2[] dst, int axis, float at, bool keepAbove)
        {
            int outCount = 0;
            if (count == 0) return 0;
            Vector2 prev = src[count - 1];
            bool prevIn = keepAbove ? prev[axis] >= at : prev[axis] <= at;
            for (int i = 0; i < count && outCount < dst.Length - 1; i++)
            {
                Vector2 cur = src[i];
                bool curIn = keepAbove ? cur[axis] >= at : cur[axis] <= at;
                if (curIn != prevIn)
                {
                    float t = (at - prev[axis]) / (cur[axis] - prev[axis]);
                    dst[outCount++] = Vector2.LerpUnclamped(prev, cur, t);
                }
                if (curIn) dst[outCount++] = cur;
                prev = cur;
                prevIn = curIn;
            }
            return outCount;
        }

        private void BuildVector(Vector2 size)
        {
            ResolvePolygon(size);
            _vectorGradient = null;
            var l = _layers[0];
            if (l.Kind != PaintKind.Linear) return;
            PaintRaster.ResolveStops(l.Stops, GradientLength(l, size), out var pos, out _);
            var colors = new GradientColorKey[l.Stops.Length];
            var alphas = new GradientAlphaKey[l.Stops.Length];
            for (int i = 0; i < l.Stops.Length; i++)
            {
                float t = Mathf.Clamp01(pos[i]);
                colors[i] = new GradientColorKey(l.Stops[i].Color, t);
                alphas[i] = new GradientAlphaKey(l.Stops[i].Color.a, t);
            }
            _vectorGradient = new Gradient { mode = GradientMode.Blend };
            _vectorGradient.SetKeys(colors, alphas);
        }

        private static float GradientLength(PaintLayer l, Vector2 size)
        {
            float a = l.AngleDeg * Mathf.Deg2Rad;
            return Mathf.Abs(size.x * Mathf.Sin(a)) + Mathf.Abs(size.y * Mathf.Cos(a));
        }

        private void BakePaint(Vector2 size)
        {
            bool hard = HasHardEdges();
            int texW, texH;
            float cssW = size.x, cssH = size.y;
            bool tiled = _layers[0].IsTiled;
            if (tiled) { cssW = _layers[0].TileW; cssH = _layers[0].TileH; }

            if (!hard && !tiled && AxisAligned(out bool vertical))
            {
                // A pure top-to-bottom or left-to-right gradient varies on one axis only.
                texW = vertical ? 1 : Mathf.Clamp(Mathf.CeilToInt(cssW), 2, 512);
                texH = vertical ? Mathf.Clamp(Mathf.CeilToInt(cssH), 2, 512) : 1;
            }
            else
            {
                float scale = hard ? PixelsPerPoint() : 0.5f;
                float maxSide = Mathf.Max(cssW, cssH) * scale;
                if (maxSide > MaxTexture) scale *= MaxTexture / maxSide;
                texW = Mathf.Max(2, Mathf.CeilToInt(cssW * scale));
                texH = Mathf.Max(2, Mathf.CeilToInt(cssH * scale));
            }

            _paintKey = _paint + "|" + _mask + "|" + _clip + "|" + _tile + "|" + cssW.ToString("F1") + "x" + cssH.ToString("F1")
                        + "@" + texW + "x" + texH;
            _paintTex = PaintTextureCache.Acquire(_paintKey, out bool fresh, texW, texH, tiled);
            if (fresh)
            {
                var req = new PaintRaster.Request
                {
                    Layers = _layers, Mask = _maskLayers, Clip = _clipShape,
                    CssW = cssW, CssH = cssH, TexW = texW, TexH = texH,
                };
                PaintTextureCache.Fill(_paintTex, req);
            }
            style.backgroundImage = new StyleBackground(Background.FromTexture2D(_paintTex));
            style.backgroundColor = StyleKeyword.Null;
            if (tiled)
            {
                style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                style.backgroundSize = new BackgroundSize(new Length(cssW, LengthUnit.Pixel), new Length(cssH, LengthUnit.Pixel));
            }
            else
            {
                style.backgroundRepeat = StyleKeyword.Null;
                style.backgroundSize = new BackgroundSize(new Length(100f, LengthUnit.Percent), new Length(100f, LengthUnit.Percent));
            }
        }

        private bool AxisAligned(out bool vertical)
        {
            vertical = true;
            if (_clipShape != null || _maskLayers != null) return false;
            bool any = false;
            foreach (var l in _layers)
            {
                if (l.Kind == PaintKind.Solid) continue;
                if (l.Kind != PaintKind.Linear || l.Repeating) return false;
                float a = Mathf.Repeat(l.AngleDeg, 360f);
                bool v = Mathf.Approximately(a, 0f) || Mathf.Approximately(a, 180f);
                bool h = Mathf.Approximately(a, 90f) || Mathf.Approximately(a, 270f);
                if (!v && !h) return false;
                if (any && v != vertical) return false;
                vertical = v;
                any = true;
            }
            return any;
        }

        private bool HasHardEdges()
        {
            if (_clipShape != null) return true;
            foreach (var l in _layers)
            {
                if (l.Kind == PaintKind.Conic || l.Repeating || l.IsTiled) return true;
                if (HasHardStop(l)) return true;
            }
            if (_maskLayers != null)
                foreach (var l in _maskLayers)
                    if (HasHardStop(l)) return true;
            return false;
        }

        // Two stops within ~2 px of each other (a ring edge like "57px, 58px") need full res.
        private static bool HasHardStop(PaintLayer l)
        {
            for (int i = 1; i < l.Stops.Length; i++)
            {
                var a = l.Stops[i - 1]; var b = l.Stops[i];
                if (!a.HasPosition || !b.HasPosition || a.Unit != b.Unit) continue;
                float d = Mathf.Abs(b.Position - a.Position);
                if (a.Unit == ColorStop.PositionUnit.Px ? d <= 2f : d <= 0.005f) return true;
            }
            return false;
        }

        private void BakeShadows(Vector2 size)
        {
            float reach = 0f;
            foreach (var s in _shadows) reach = Mathf.Max(reach, s.Reach);
            _shadowPad = Mathf.Ceil(reach);
            float radius = resolvedStyle.borderTopLeftRadius;
            float fullW = size.x + 2f * _shadowPad, fullH = size.y + 2f * _shadowPad;
            // Blurred shadows are smooth; hard rings (blur 0) want device resolution.
            bool hard = false;
            foreach (var s in _shadows) if (s.Blur < 2f) hard = true;
            float scale = hard ? PixelsPerPoint() : 0.5f;
            float maxSide = Mathf.Max(fullW, fullH) * scale;
            if (maxSide > MaxTexture) scale *= MaxTexture / maxSide;
            int texW = Mathf.Max(2, Mathf.CeilToInt(fullW * scale));
            int texH = Mathf.Max(2, Mathf.CeilToInt(fullH * scale));
            _shadowKey = "shadow|" + _shadow + "|" + size.x.ToString("F1") + "x" + size.y.ToString("F1") + "|" + radius.ToString("F1")
                         + "@" + texW + "x" + texH;
            _shadowTex = PaintTextureCache.Acquire(_shadowKey, out bool fresh, texW, texH, false);
            if (fresh) PaintTextureCache.FillShadows(_shadowTex, _shadows, size.x, size.y, radius, _shadowPad);
        }

        private float PixelsPerPoint()
        {
            var root = panel?.visualTree;
            float h = root != null ? root.layout.height : 0f;
            if (h <= 1f || float.IsNaN(h)) return 2f;
            return Mathf.Clamp(Screen.height / h, 1f, 4f);
        }

        private void ReleaseTextures()
        {
            if (_paintTex != null) { PaintTextureCache.Release(_paintKey); _paintTex = null; }
            if (_shadowTex != null) { PaintTextureCache.Release(_shadowKey); _shadowTex = null; }
        }

        // ── Draw ─────────────────────────────────────────────────────
        private void OnGenerate(MeshGenerationContext mgc)
        {
            var size = layout.size;
            if (_shadowTex != null) DrawQuad(mgc, _shadowTex, -_shadowPad, -_shadowPad, size.x + 2f * _shadowPad, size.y + 2f * _shadowPad);
            bool edge = _innerCount >= 3 && _ptCount >= 3;
            // Ring first, then the fill inside it: the two never overlap, and the ring is drawn
            // before any fill gradient is set on the painter (Painter2D keeps it between fills).
            if (edge) DrawEdge(mgc);
            if (_mode == Mode.Vector && _ptCount >= 3)
            {
                if (edge) DrawVectorFill(mgc, size, _inner, _innerCount);
                else DrawVectorFill(mgc, size, _pts, _ptCount);
            }
        }

        private void DrawEdge(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            p.BeginPath();
            p.MoveTo(_pts[0]);
            for (int i = 1; i < _ptCount; i++) p.LineTo(_pts[i]);
            p.ClosePath();
            p.MoveTo(_inner[0]);
            for (int i = 1; i < _innerCount; i++) p.LineTo(_inner[i]);
            p.ClosePath();
            p.fillColor = _edgeColor;
            p.Fill(FillRule.OddEven);
        }

        private void DrawVectorFill(MeshGenerationContext mgc, Vector2 size, Vector2[] pts, int count)
        {
            var p = mgc.painter2D;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < count; i++) p.LineTo(pts[i]);
            p.ClosePath();
            if (_vectorGradient != null)
            {
                var l = _layers[0];
                float len = GradientLength(l, size);
                float a = l.AngleDeg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                var start = size * 0.5f - dir * (len * 0.5f);
                p.fillColor = Color.white;
                p.fillGradient = FillGradient.MakeLinearGradient(_vectorGradient, start, start + dir * len, AddressMode.Clamp);
            }
            else
            {
                p.fillColor = _solid;
            }
            p.Fill();
        }

        private static void DrawQuad(MeshGenerationContext mgc, Texture tex, float x, float y, float w, float h)
        {
            var md = mgc.Allocate(4, 6, tex);
            md.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ),         tint = Color.white, uv = new Vector2(0f, 1f) });
            md.SetNextVertex(new Vertex { position = new Vector3(x + w, y, Vertex.nearZ),     tint = Color.white, uv = new Vector2(1f, 1f) });
            md.SetNextVertex(new Vertex { position = new Vector3(x + w, y + h, Vertex.nearZ), tint = Color.white, uv = new Vector2(1f, 0f) });
            md.SetNextVertex(new Vertex { position = new Vector3(x, y + h, Vertex.nearZ),     tint = Color.white, uv = new Vector2(0f, 0f) });
            md.SetNextIndex(0); md.SetNextIndex(1); md.SetNextIndex(2);
            md.SetNextIndex(0); md.SetNextIndex(2); md.SetNextIndex(3);
        }
    }
}
