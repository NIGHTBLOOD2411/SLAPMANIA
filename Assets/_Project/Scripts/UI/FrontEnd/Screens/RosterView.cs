using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// A3 Roster (the FIGHTERS tab) with live champions (M08, M10). The mockup's header, filter
    /// chips and bottom bar stay; its hard-coded cards are replaced by one card per
    /// ChampionRoster entry, in a scroll view. Tapping a card picks that champion (EQUIPPED pill,
    /// the others dim), the bottom bar names it, and its gold button - UNLOCK in the mockup -
    /// becomes PLAY.
    ///
    /// Cards are built once, on first show, in the mockup's own fonts (read off its labels), so
    /// a re-converted Roster.uxml keeps working. Without a roster the mockup stays as drawn.
    internal sealed class RosterView : ScreenView
    {
        private const float CardW = 185.98f, CardH = 262f, Gap = 14f, RowGap = 16f;
        private const string CardClip = "polygon(14% 0,86% 0,100% 6%,100% 91%,50% 100%,0 91%,0 6%)";
        private const string CardGloss = "linear-gradient(180deg,rgba(255,255,255,.18),rgba(255,255,255,0) 40%,rgba(0,0,0,.18))";
        private static readonly Color Gold = new Color32(0xE9, 0xB9, 0x49, 0xFF);
        private static readonly Color DarkInk = new Color32(0x24, 0x17, 0x03, 0xFF);

        private VisualElement _grid;
        private Label _selectedLabel, _ownedLabel, _playLabel;
        private StyleFontDefinition _fontDisplay, _fontStat, _fontCap;
        private bool _hasDisplay, _hasStat, _hasCap;
        private bool _built;
        private readonly List<VisualElement> _cards = new List<VisualElement>(16);
        private readonly List<VisualElement> _pills = new List<VisualElement>(16);

        protected override void OnBind()
        {
            var firstCard = Root.Q<VisualElement>(className: "k-card");
            _grid = firstCard != null ? firstCard.parent : null;
            if (_grid == null) { SlapLog.Warn(LogChannel.UI, "Roster: no k-card grid in the UXML"); return; }

            // The mockup's fonts, from the first card: the 38px overall, a stat, the class tag.
            firstCard.Query<Label>().ForEach(ReadFont);

            Root.Query<Label>().ForEach(FindBottomBar);
            var play = Root.Q<VisualElement>(className: "k-bgold");
            if (play != null)
            {
                Relink(play, ScreenId.Search);
                _playLabel = play.Q<Label>();
            }
        }

        private void ReadFont(Label l)
        {
            var f = l.style.unityFontDefinition;
            float size = l.style.fontSize.value.value;
            // First match wins: later labels on the card reuse the same three fonts.
            if (size >= 30f) { if (!_hasDisplay) { _fontDisplay = f; _hasDisplay = true; } }
            else if (l.ClassListContains("k-n") && size >= 13f) { if (!_hasStat) { _fontStat = f; _hasStat = true; } }
            else if (l.ClassListContains("k-cap") && !_hasCap) { _fontCap = f; _hasCap = true; }
        }

        private void FindBottomBar(Label l)
        {
            string t = l.text;
            if (t == null) return;
            if (t.StartsWith("Selected ")) _selectedLabel = l;
            else if (t.Contains("NOT OWNED")) _ownedLabel = l;
        }

        protected override void OnShow()
        {
            if (!_built) Build();
            Refresh();
        }

        private void Build()
        {
            var roster = Manager.Roster;
            if (_grid == null || roster == null || roster.Count == 0) return;
            _built = true;
            _grid.Clear();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            // From the grid's top (206) to just above the bottom bar (806).
            scroll.style.width = 386f;
            scroll.style.height = 590f;
            var content = scroll.contentContainer;
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexWrap = Wrap.Wrap;
            content.style.paddingBottom = 24f;
            _grid.Add(scroll);

            for (int i = 0; i < roster.Count; i++)
            {
                var c = roster.Get(i);
                var card = BuildCard(c, i);
                content.Add(card);
                _cards.Add(card);
            }
        }

        private VisualElement BuildCard(ChampionDefinition c, int index)
        {
            Color ink = c.cardFinish == ChampionCardFinish.Gold ? DarkInk : Color.white;
            var card = new PaintBox { Paint = FinishPaint(c.cardFinish), Clip = CardClip };
            card.AddToClassList("k-card");
            card.style.width = CardW;
            card.style.height = CardH;
            card.style.flexShrink = 0f;
            if (index % 2 == 1) card.style.marginLeft = Gap;
            if (index >= 2) card.style.marginTop = RowGap;
            card.RegisterCallback<ClickEvent, int>(OnCardClicked, index);

            var gloss = new PaintBox { Paint = CardGloss, Clip = CardClip, pickingMode = PickingMode.Ignore };
            Abs(gloss, 5f, 5f, 5f, 5f);
            card.Add(gloss);

            if (c.portrait != null)
            {
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                Place(img, 22f, 52f, 126f, 130f);
                img.style.backgroundImage = new StyleBackground(c.portrait);
                img.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                card.Add(img);
            }

            var ovr = new VisualElement { pickingMode = PickingMode.Ignore };
            ovr.style.position = Position.Absolute;
            ovr.style.top = 28f;
            ovr.style.left = 12f;
            ovr.style.alignItems = Align.Center;
            ovr.Add(Text(c.stats.Overall.ToString(), _fontDisplay, 38f, ink));
            var tag = Text("<cspace=1.54px>" + (string.IsNullOrEmpty(c.classTag) ? "---" : c.classTag) + "</cspace>", _fontCap, 11f, ink);
            tag.style.marginTop = 4f;
            ovr.Add(tag);
            card.Add(ovr);

            var name = Text(c.ShownName.ToUpperInvariant(), _fontDisplay, 23f, ink);
            name.style.position = Position.Absolute;
            name.style.top = 158f;
            name.style.left = 0f;
            name.style.width = Length.Percent(100f);
            name.style.unityTextAlign = TextAnchor.UpperCenter;
            card.Add(name);

            var stats = new VisualElement { pickingMode = PickingMode.Ignore };
            stats.style.position = Position.Absolute;
            stats.style.top = 188f;
            stats.style.left = 24f;
            stats.style.right = 24f;
            stats.style.paddingTop = 8f;
            stats.style.borderTopWidth = 1f;
            stats.style.borderTopColor = new Color(ink.r, ink.g, ink.b, 0.3f);
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.flexWrap = Wrap.Wrap;
            var s = c.stats;
            AddStat(stats, s.power, "PWR", ink, false);
            AddStat(stats, s.speed, "SPD", ink, false);
            AddStat(stats, s.chin, "CHN", ink, false);
            AddStat(stats, s.technique, "TEC", ink, true);
            AddStat(stats, s.rage, "RAG", ink, true);
            AddStat(stats, s.stamina, "STA", ink, true);
            card.Add(stats);

            var pillRow = new VisualElement { pickingMode = PickingMode.Ignore };
            pillRow.style.position = Position.Absolute;
            pillRow.style.top = 238f;
            pillRow.style.width = Length.Percent(100f);
            pillRow.style.flexDirection = FlexDirection.Row;
            pillRow.style.justifyContent = Justify.Center;
            var pill = Text("<cspace=1.12px>EQUIPPED</cspace>", _fontCap, 8f, Gold);
            pill.style.backgroundColor = DarkInk;
            pill.style.paddingTop = 3f; pill.style.paddingBottom = 3f;
            pill.style.paddingLeft = 6f; pill.style.paddingRight = 7f;
            pillRow.Add(pill);
            card.Add(pillRow);
            _pills.Add(pillRow);
            return card;
        }

        private void AddStat(VisualElement row, int value, string key, Color ink, bool secondRow)
        {
            var l = Text(value + " <cspace=1.26px><size=9>" + key + "</size></cspace>", _fontStat, 14f, ink);
            l.style.width = 45.98f;
            l.style.flexShrink = 0f;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (secondRow) l.style.marginTop = 5f;
            row.Add(l);
        }

        private static Label Text(string text, StyleFontDefinition font, float size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            l.style.unityFontDefinition = font;
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.paddingLeft = 0f; l.style.paddingRight = 0f;
            l.style.marginLeft = 0f; l.style.marginRight = 0f;
            return l;
        }

        private static void Abs(VisualElement e, float top, float right, float bottom, float left)
        {
            e.style.position = Position.Absolute;
            e.style.top = top; e.style.right = right; e.style.bottom = bottom; e.style.left = left;
        }

        private static void Place(VisualElement e, float top, float left, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.top = top; e.style.left = left; e.style.width = w; e.style.height = h;
        }

        private static string FinishPaint(ChampionCardFinish f)
        {
            switch (f)
            {
                case ChampionCardFinish.Red:   return "linear-gradient(160deg,#FF9DA1 0%,#E3262F 38%,#4A0509 100%)";
                case ChampionCardFinish.Black: return "linear-gradient(160deg,#4A505C 0%,#1C1F26 50%,#0A0B0E 100%)";
                default:                       return "linear-gradient(160deg,#FFEDB3 0%,#E9B949 38%,#8F600C 100%)";
            }
        }

        private void OnCardClicked(ClickEvent e, int index)
        {
            if (!IsShown) return;
            var c = Manager.Roster != null ? Manager.Roster.Get(index) : null;
            if (c == null) return;
            Manager.SelectChampion(c.id);
            Refresh();
        }

        /// Highlights the selected card and names it in the bottom bar. An unknown or empty id
        /// shows the roster's first champion, as the bout will use it.
        private void Refresh()
        {
            var roster = Manager.Roster;
            if (!_built || roster == null) return;
            int sel = roster.IndexOf(Manager.SelectedChampionId);
            if (sel < 0) sel = 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                bool on = i == sel;
                _cards[i].style.opacity = on ? 1f : 0.72f;
                _pills[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var c = roster.Get(sel);
            if (c == null) return;
            if (_selectedLabel != null)
                _selectedLabel.text = "Selected <b><color=#FFFFFF>" + c.ShownName + " · " + c.stats.Overall + " OVR</color></b>";
            if (_ownedLabel != null)
            {
                _ownedLabel.text = "<cspace=1.4px>" + (string.IsNullOrEmpty(c.originId) ? "EQUIPPED" : c.originId.ToUpperInvariant()) + "</cspace>";
                _ownedLabel.style.color = Gold;
            }
            if (_playLabel != null) _playLabel.text = "<cspace=-0.11px>PLAY</cspace>";
        }
    }
}
