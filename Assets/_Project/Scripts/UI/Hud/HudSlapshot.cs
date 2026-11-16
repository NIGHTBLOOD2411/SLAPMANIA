using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// The slapshot Polaroid (M09/M10, GLOSSARY: Slapshot). A powerful slap is "photographed":
    /// a camera-flash blink, then a tilted Polaroid drops in - SLAPSHOT! header, the photo of the
    /// slapped fighter with the palm on their cheek developing from white, and on the bottom
    /// strip the slapped fighter's name with how hard the slap was. It flies off before the next
    /// turn (the director holds the Snapshot state for BoutConfig.slapshotHoldMs).
    ///
    /// Built in code at Bind rather than by V2HudBuilder, so adding it does not regenerate the
    /// bout scene's HUD; fonts are borrowed from the HUD's own text. Nothing on it takes a
    /// raycast. Runs on the HudView tweener (unscaled, no closures, R5); its strings are built
    /// once per slapshot, in the Snapshot state - never in Charge or Timing.
    public sealed class HudSlapshot : HudView
    {
        private const int FlyOff = 1;
        private const int Done = 2;

        // V2 frame px (430 x 932). The card is a Polaroid: thin sides, a header strip on top,
        // the deep caption strip at the bottom. The photo is the snapshotter's 4:5.
        private const float CardW = 232f, Side = 12f, HeaderH = 32f, CaptionH = 72f;
        private const float PhotoW = CardW - 2f * Side, PhotoH = PhotoW * 1.25f;
        private const float CardH = HeaderH + PhotoH + CaptionH;
        private const float Tilt = -6f;
        private static readonly Vector2 Home = new Vector2(36f, 96f);
        private static readonly Vector2 Away = new Vector2(230f, 380f);

        private static readonly Color Paper = new Color(0.976f, 0.965f, 0.937f);
        private static readonly Color Ink = new Color(0.08f, 0.07f, 0.09f);
        private static readonly Color SlapRed = new Color(0.878f, 0.094f, 0.176f);
        private static readonly Color Muted = new Color(0.42f, 0.4f, 0.42f);

        private RectTransform _card;
        private RawImage _photo;
        private CanvasGroup _develop;
        private CanvasGroup _flash;
        private TMP_Text _round, _name, _detail, _damage;
        private float _hold = 1.15f;

        /// Builds the hierarchy under `hudRoot` once; `display` / `body` are the HUD's fonts
        /// (either may be null: TMP's default is used).
        public static HudSlapshot Create(RectTransform hudRoot, TMP_FontAsset display, TMP_FontAsset body)
        {
            var go = new GameObject("Slapshot (runtime)", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(hudRoot, false);
            Stretch(rt);
            var view = go.AddComponent<HudSlapshot>();
            view.Build(display, body);
            return view;
        }

        /// Shows the Polaroid. `holdSeconds` is how long it stays before flying off.
        public void Play(Texture photo, string slappedName, int damage, string detail, int round, float holdSeconds)
        {
            if (IsShown) HideImmediate();
            _hold = Mathf.Max(0.2f, holdSeconds);
            _photo.texture = photo;
            _photo.color = photo != null ? Color.white : Ink;
            _name.text = slappedName;
            _damage.text = damage.ToString();
            _detail.text = detail;
            _round.text = round > 0 ? "ROUND " + round : "";
            Show();

            // Flash: the shutter.
            Tw.Fade(_flash, 0.9f, 0f, 0.32f, Ease.OutQuad);
            // The card lands, tilted, with a little overshoot.
            _card.anchoredPosition = Home;
            Tw.Scale(_card, 1.4f, 1f, 0.38f, Ease.OutBack);
            Tw.RotateZ(_card, 4f, Tilt, 0.38f, Ease.OutBack);
            Tw.AnchorMove(_card, Home + new Vector2(0f, -40f), Home, 0.38f, Ease.OutCubic);
            // The photo develops from white, like the real thing (sped up).
            Tw.Fade(_develop, 1f, 0f, 0.5f, Ease.OutQuad, 0.1f);
            Tw.Delay(_hold, this, FlyOff);
        }

        /// The next turn owns the screen: hurry off if still up.
        public void Yield()
        {
            if (IsShown) Hide();
        }

        protected override void OnShow()
        {
            Group.blocksRaycasts = false;   // never eats a tap meant for the slap
            Group.interactable = false;
        }

        protected override void OnTween(int id)
        {
            if (id == FlyOff)
            {
                Tw.AnchorMove(_card, Home, Away, 0.3f, Ease.InBack);
                Tw.Scale(_card, 1f, 0.45f, 0.3f, Ease.InQuad);
                Tw.Delay(0.24f, this, Done);
            }
            else if (id == Done) Hide();
        }

        // ── Build ────────────────────────────────────────────────────
        private void Build(TMP_FontAsset display, TMP_FontAsset body)
        {
            var root = (RectTransform)transform;

            var flash = Rect("Flash", root);
            Stretch(flash);
            Fill(flash, Color.white);
            _flash = flash.gameObject.AddComponent<CanvasGroup>();
            _flash.alpha = 0f;

            _card = Rect("Card", root);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardW, CardH);

            var shadow = Rect("Shadow", _card);
            Place(shadow, new Vector2(7f, -9f), new Vector2(CardW, CardH));
            Fill(shadow, new Color(0f, 0f, 0f, 0.38f));
            Fill(Rect("Paper", _card, stretch: true), Paper);

            // Header strip: SLAPSHOT! in slap red, the round on the right.
            var header = Text("Header", _card, display, "SLAPSHOT!", 22f, SlapRed, FontStyles.Bold | FontStyles.Italic,
                              TextAlignmentOptions.Left);
            Top(header, Side, 0f, CardW - 2f * Side, HeaderH);
            header.characterSpacing = 4f;
            _round = Text("Round", _card, body, "", 10f, Muted, FontStyles.Bold, TextAlignmentOptions.Right);
            Top(_round, Side, 0f, CardW - 2f * Side, HeaderH);

            // The photo, with its developing veil.
            var photoFrame = Rect("Photo", _card);
            photoFrame.anchorMin = photoFrame.anchorMax = photoFrame.pivot = new Vector2(0.5f, 1f);
            photoFrame.anchoredPosition = new Vector2(0f, -HeaderH);
            photoFrame.sizeDelta = new Vector2(PhotoW, PhotoH);
            _photo = photoFrame.gameObject.AddComponent<RawImage>();
            _photo.raycastTarget = false;
            var veil = Rect("Develop", photoFrame, stretch: true);
            Fill(veil, Paper);
            _develop = veil.gameObject.AddComponent<CanvasGroup>();

            // Caption strip: who got slapped, and how hard.
            float captionTop = HeaderH + PhotoH;
            _name = Text("Name", _card, display, "", 26f, Ink, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.BottomLeft);
            Top(_name, Side, captionTop + 6f, CardW - 2f * Side - 70f, 36f);
            _name.enableAutoSizing = true;
            _name.fontSizeMin = 14f;
            _name.fontSizeMax = 26f;
            _detail = Text("Detail", _card, body, "", 10f, Muted, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Top(_detail, Side, captionTop + 44f, CardW - 2f * Side - 64f, 22f);
            _damage = Text("Damage", _card, display, "", 40f, SlapRed, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.BottomRight);
            Top(_damage, Side, captionTop - 6f, CardW - 2f * Side, 52f);
            _damage.overflowMode = TextOverflowModes.Overflow; // a 40 pt line is taller than its box: never drop it
            var dmgLabel = Text("DamageLabel", _card, body, "DMG", 10f, Ink, FontStyles.Bold, TextAlignmentOptions.TopRight);
            Top(dmgLabel, Side, captionTop + 46f, CardW - 2f * Side, 16f);
            dmgLabel.characterSpacing = 3f;
        }

        private static RectTransform Rect(string name, Transform parent, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretch) Stretch(rt);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        /// Top-left anchored box `y` px down from the card's top edge.
        private static void Top(TMP_Text t, float x, float y, float w, float h)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Fill(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
        }

        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
                                     FontStyles style, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }
    }
}
