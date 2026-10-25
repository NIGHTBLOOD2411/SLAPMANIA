#!/usr/bin/env python3
"""mockup2uxml - converts the Slap Mania Design-canvas mockups (*.dc.html + kit.css) into
UI Toolkit UXML for the front end (M10, ADR-0013).

Why a converter: the mockups are absolute + flexbox CSS. UI Toolkit lays out with the same
flexbox engine (Yoga), so a faithful transcription is mostly mechanical - but only if every
element's *computed* style is resolved first (cascade, inheritance, shorthands, em units,
text-transform, grid tracks, gap). Doing that by hand for ~900 boxes would drift; doing it
here keeps "pixel perfect" a property of the tool, not of anyone's patience.

Run from the repo root:
    python Tools/mockup2uxml/mockup2uxml.py            # both pages: V2 and V1
    python Tools/mockup2uxml/mockup2uxml.py --page V1  # one page
    python Tools/mockup2uxml/mockup2uxml.py --list     # what would be converted

The canvas has two pages (PAGES). V2 shares kit.css and the Barlow fonts; V1 keeps its classes
in each page's own <style> and uses Cabin + Archivo Black. Each page writes its own folder,
and its links are named `nav-<prefix><Target>` so they parse into ScreenId (V1Home, ...).

Outputs (all generated - re-run instead of hand-editing where possible):
    Assets/_Project/Prefabs/UI/Screens/<Name>.uxml       (V2)
    Assets/_Project/Prefabs/UI/Screens/V1/<Name>.uxml    (V1)
    Assets/_Project/Art/UI/Icons/ico_<hash>.svg          (inline SVGs, de-duplicated)
    Docs/Reference/Mockups/CONVERSION-REPORT.md          (every approximation made)

Standard library only (ADR-0007: no third-party anything).
"""
from __future__ import annotations

import argparse
import hashlib
import html
import os
import re
import sys
from html.parser import HTMLParser

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pngio  # noqa: E402
import ttfmetrics  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MOCKUPS = os.path.join(REPO, "Docs", "Reference", "Mockups")
OUT_SCREENS = os.path.join(REPO, "Assets", "_Project", "Prefabs", "UI", "Screens")
OUT_STYLES = os.path.join(REPO, "Assets", "_Project", "Prefabs", "UI", "Styles")
OUT_ICONS = os.path.join(REPO, "Assets", "_Project", "Art", "UI", "Icons")
ICON_URL = "project://database/Assets/_Project/Art/UI/Icons/"
IMAGE_URL = "project://database/Assets/_Project/Art/UI/Mockup/"
FONT_URL = "project://database/Assets/_Project/Art/Fonts/"
REPORT = os.path.join(MOCKUPS, "CONVERSION-REPORT.md")
FONT_DIR = os.path.join(REPO, "Assets", "_Project", "Art", "Fonts")
_FACES = {}


def font_face(path):
    """ttfmetrics.Face for a FONTS path (`Cabin/Cabin-Regular`), loaded once."""
    if path not in _FACES:
        _FACES[path] = ttfmetrics.Face(os.path.join(FONT_DIR, path + ".ttf"))
    return _FACES[path]
IMAGES = os.path.join(REPO, "Assets", "_Project", "Art", "UI", "Mockup")
OUT_MASKED = os.path.join(IMAGES, "Masked")

FRAME_W, FRAME_H = 430.0, 932.0

# The V2 screens that live outside the uGUI match HUD (M10): front end + match-adjacent.
V2_SCREENS = [
    "Title", "Loader", "MainMenu", "Career", "Roster", "FighterCard", "SeasonPass",
    "Store", "PackOpen", "Challenges", "Rankings", "Profile", "Settings",
    "Search", "Walkout", "TaleOfTape",
    "Replay", "PauseMenu", "RoundEnd", "RankUp",
]

# Names the screen behaviours (Scripts/UI/FrontEnd/Screens) look up. Each hook pins one
# mockup element by what it *is* (its text, class, paint or inline style), so the names
# survive a re-conversion. Matching is first-in-document-order.
HOOKS = {
    "Title": [("title-tap", "text", "Tap to start"),
              ("title-beam", "style", "clip-path: polygon(40% 0, 60% 0")],
    "Loader": [("loader-pct", "text", "72%"),
               ("loader-fill", "class", "fr")],
    "MainMenu": [("ticker", "parent-of-text", "Double XP weekend starts Fri")],
    "Search": [("search-timer", "text", "00:07"),
               ("search-sweep", "style", "conic-gradient(from 20deg")],
    "Walkout": [("walkout-vs", "text", "VS"),
                ("walkout-champion", "style", "left: 20px; top: 128px"),
                ("walkout-challenger", "style", "left: 130px; top: 640px")],
    "TaleOfTape": [("tape-autostart", "text", "Auto-start in 0:08")],
    "PackOpen": [("pack-card", "class", "card"),
                 ("pack-title", "parent-of-text", "Legendary pull!")],
}

BLOBS = {
    "089a203a6e05678cfd31b4bf1cd3dfd6": "Portrait_Slasher.png",
    "0468ebc12b533b41d19c51629316657f": "Portrait_Buck.png",
    "2c6b3b4398fd0d3cb2dfeda617de1437": "Portrait_Glorp.png",
    "1db9009be971a82a346f7e9dff4e2ad6": "Bg_MatchPov.jpg",
    "41bdb3c5515d0775580d5be78239f938": "Logo_SlapMania.png",
    "3a67bb3ef68e85c6857351dbce3a4203": "Bg_SlapContact.jpg",
    "3356258440f5029146c3532f02a2827f": "Bg_FaceOff.jpg",
    "1d6cf7507254d9e27115dfc380d7097d": "Bg_ArenaNight.jpg",
    "55c46d0e17d2f3ff0d91187f5d58b006": "Bg_CrowdPov.jpg",
    "47b7713c06aa34f839468eb3398de2e9": "Bg_SlasherFull.jpg",
}

# The font families the mockups use, keyed by the converter's family name. `files` maps
# (weight, italic) to a TTF in Art/Fonts/<dir>. Vertical metrics are fractions of the em:
# `css` is what Edge lays a line out with (on Windows: OS/2 typo when USE_TYPO_METRICS is set,
# win otherwise - measured), `tc` is where TextCore puts the glyphs (FreeType: hhea). They
# differ only for Archivo Black, whose glyphs would otherwise sit ~0.03 em high.
FONTS = {
    "cond": {"name": "Barlow Condensed", "dir": "Barlow", "css": (1.0, 0.2), "tc": (1.0, 0.2), "files": {
        (700, False): "BarlowCondensed-Bold",
        (800, False): "BarlowCondensed-ExtraBold",
        (800, True): "BarlowCondensed-ExtraBoldItalic",
        (900, True): "BarlowCondensed-BlackItalic"}},
    "body": {"name": "Barlow", "dir": "Barlow", "css": (1.0, 0.2), "tc": (1.0, 0.2), "files": {
        (400, False): "Barlow-Regular",
        (600, False): "Barlow-SemiBold"}},
    "cabin": {"name": "Cabin", "dir": "Cabin", "css": (0.965, 0.25), "tc": (0.965, 0.25), "files": {
        (400, False): "Cabin-Regular",
        (500, False): "Cabin-Medium",
        (600, False): "Cabin-SemiBold",
        (700, False): "Cabin-Bold"}},
    "archivo": {"name": "Archivo Black", "dir": "ArchivoBlack", "css": (1.035, 0.312), "tc": (0.878, 0.21), "files": {
        (400, False): "ArchivoBlack-Regular"}},
}

V1_SCREENS = [
    "Main", "Loading", "Home", "FighterSelect", "Shop", "Leaderboard", "Matchmaking",
    "VsIntro", "Countdown", "SlapTurn", "SmashHit", "Defend", "Pause", "RoundKO",
    "Victory", "Defeat",
]

# V1 reuses two V2 behaviours (ScreenViews): the timed loader, found by the same hook names.
V1_HOOKS = {
    "Loading": [("loader-pct", "text", "68%"),
                ("loader-fill", "style", "width: 68%; height: 100%")],
}

# One entry per Design-canvas page. `prefix` names the ScreenIds (V1Home) and `gradient` the
# TextCore gradient presets (V1Red) - V1's red, gold and silver are not V2's.
PAGES = {
    "V2": {"screens": V2_SCREENS, "kit": "kit.css", "out": "", "prefix": "", "hooks": HOOKS},
    "V1": {"screens": V1_SCREENS, "kit": None, "out": "V1", "prefix": "V1", "hooks": V1_HOOKS},
}

# Glyphs the mockups use that Barlow does not contain (the browser fell back to a system
# font that will not exist on device). They become small inline vector icons instead.
GLYPHS = {
    "★": ('<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path d="M12 2l3 6.5 7 .8-5.2 4.8 1.4 7L12 17.6 5.8 21l1.4-7L2 9.3l7-.8z" fill="{c}"/></svg>', 0.92),
    "▾": ('<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><path d="M5 8h14l-7 9z" fill="{c}"/></svg>', 0.9),
    "●": ('<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"><circle cx="12" cy="13" r="7" fill="{c}"/></svg>', 0.9),
}

INHERITED = {
    "color", "font-size", "font-family", "font-weight", "font-style", "letter-spacing",
    "line-height", "text-transform", "text-align", "white-space", "text-shadow", "visibility",
    "-gradient-text",
}

INLINE_TAGS = {"span", "a", "b", "i", "small", "strong", "em", "label", "img", "svg", "button", "input", "br"}
TEXT_INLINE = {"span", "b", "i", "small", "strong", "em", "a", "label"}
VOID = {"img", "input", "br", "link", "meta", "hr"}


# ════════════════════════════════════════════════════════════════════════════
# HTML
# ════════════════════════════════════════════════════════════════════════════
class Node:
    __slots__ = ("tag", "attrs", "children", "parent", "svg", "cs", "idx")

    def __init__(self, tag, attrs, parent):
        self.tag = tag
        self.attrs = attrs
        self.children = []
        self.parent = parent
        self.svg = None
        self.cs = None
        self.idx = 0

    @property
    def classes(self):
        return self.attrs.get("class", "").split()

    def __repr__(self):
        return "<%s %s>" % (self.tag, " ".join(self.classes))


class Parser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.root = Node("#root", {}, None)
        self.cur = self.root
        self.svg_depth = 0
        self.svg_buf = None
        self.svg_names = []

    def handle_starttag(self, tag, attrs):
        raw = self.get_starttag_text()
        if self.svg_depth:
            self.svg_depth += 1
            self.svg_buf.append(raw)
            self.svg_names.append(re.match(r"<\s*([\w:-]+)", raw).group(1))
            return
        node = Node(tag, {k: (v if v is not None else "") for k, v in attrs}, self.cur)
        self.cur.children.append(node)
        if tag == "svg":
            self.svg_depth = 1
            self.svg_buf = [raw]
            self.svg_names = ["svg"]
            self.svg_node = node
            return
        if tag not in VOID:
            self.cur = node

    def handle_startendtag(self, tag, attrs):
        if self.svg_depth:
            self.svg_buf.append(self.get_starttag_text())
            return
        node = Node(tag, {k: (v if v is not None else "") for k, v in attrs}, self.cur)
        self.cur.children.append(node)

    def handle_endtag(self, tag):
        if self.svg_depth:
            name = self.svg_names.pop()
            self.svg_buf.append("</%s>" % name)
            self.svg_depth -= 1
            if self.svg_depth == 0:
                self.svg_node.svg = "".join(self.svg_buf)
            return
        if tag in VOID:
            return
        n = self.cur
        while n is not None and n.tag != tag:
            n = n.parent
        if n is not None and n.parent is not None:
            self.cur = n.parent

    def handle_data(self, data):
        if self.svg_depth:
            self.svg_buf.append(html.escape(data, quote=False))
            return
        if data:
            self.cur.children.append(data)


def all_text(n):
    if isinstance(n, str):
        return n
    return "".join(all_text(c) for c in n.children)


def walk(n):
    if isinstance(n, Node):
        yield n
        for c in n.children:
            yield from walk(c)


def apply_hooks(screen, scr, hooks):
    def text_of(n):
        return re.sub(r"\s+", " ", all_text(n)).strip().lower()

    for hook, kind, value in hooks.get(screen, []):
        nodes = list(walk(scr))
        found = None
        if kind == "text":
            # Outermost element carrying exactly this text: the one emitted as the Label.
            found = next((n for n in nodes if text_of(n) == value.lower()), None)
        elif kind == "parent-of-text":
            # Deepest element carrying the text, then its container.
            hits = [n for n in nodes if text_of(n) == value.lower()]
            found = hits[-1].parent if hits else None
        elif kind == "class":
            found = next((n for n in nodes if value in n.classes), None)
        elif kind == "style":
            found = next((n for n in nodes if value in n.attrs.get("style", "")), None)
        if found is None:
            raise SystemExit("%s: hook '%s' (%s %r) matches nothing - the mockup changed" % (screen, hook, kind, value))
        found.attrs["data-hook"] = hook


def segments(n):
    """(text, computed style) for every text run under n, in order."""
    out = []
    for c in n.children:
        if isinstance(c, str):
            out.append((collapse(c, n.cs), n.cs))
        elif c.tag == "br":
            out.append(("\n", n.cs))
        else:
            out += segments(c)
    return out


def inherit_clips(n):
    """CSS clip-path clips the element's descendants too. UI Toolkit cannot clip a subtree
    to a polygon, but a full-size overlay inside a clipped box (a card's shine, a pack's
    gloss) is the same box, so it takes the same clip-path and stays inside the shape."""
    if not isinstance(n, Node):
        return
    clip = n.cs.get("clip-path")
    for c in n.children:
        if not isinstance(c, Node):
            continue
        same_box = (c.cs.get("position") == "absolute" and c.cs.get("width") == "100%"
                    and c.cs.get("height") == "100%" and px(c.cs.get("left"), 0) == 0
                    and px(c.cs.get("top"), 0) == 0)
        if clip and "clip-path" not in c.cs and same_box:
            c.cs["clip-path"] = clip
        inherit_clips(c)


def find_scr(node):
    """The 430 x 932 frame: V2's `.scr`, or (V1) the first <div> directly in <x-dc>, which
    then gets the `scr` class so the rest of the converter treats both pages alike."""
    def first(n, test):
        if isinstance(n, Node):
            if test(n):
                return n
            for c in n.children:
                r = first(c, test)
                if r is not None:
                    return r
        return None

    scr = first(node, lambda n: n.tag == "div" and "scr" in n.classes)
    if scr is None:
        scr = first(node, lambda n: n.tag == "div" and n.parent is not None and n.parent.tag == "x-dc")
        if scr is not None:
            scr.attrs["class"] = (scr.attrs.get("class", "") + " scr").strip()
    return scr


def page_styles(text):
    return "\n".join(re.findall(r"<style>(.*?)</style>", text, re.S))


# ════════════════════════════════════════════════════════════════════════════
# CSS
# ════════════════════════════════════════════════════════════════════════════
def split_top(s, sep):
    out, depth, start = [], 0, 0
    for i, ch in enumerate(s):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        elif depth == 0 and ch == sep:
            out.append(s[start:i])
            start = i + 1
    out.append(s[start:])
    return out


def split_ws(s):
    out, depth, cur = [], 0, ""
    for ch in s:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if depth == 0 and ch.isspace():
            if cur:
                out.append(cur)
            cur = ""
        else:
            cur += ch
    if cur:
        out.append(cur)
    return out


def parse_decls(text):
    d = {}
    for part in split_top(text, ";"):
        if ":" not in part:
            continue
        k, v = part.split(":", 1)
        k, v = k.strip().lower(), v.strip()
        if k and v:
            d[k] = v
    return d


class Rule:
    def __init__(self, sel, decls, order):
        self.sel = sel  # list of (combinator, compound)
        self.decls = decls
        self.order = order
        spec = 0
        for _, c in sel:
            spec += 10 * len(c["classes"]) + (1 if c["tag"] else 0)
        self.spec = spec


def parse_selector(s):
    s = s.strip()
    if "::" in s or re.search(r":(hover|checked|active|focus|before|after)", s):
        return None
    tokens = re.findall(r">|[^\s>]+", s)
    sel, comb = [], " "
    for t in tokens:
        if t == ">":
            comb = ">"
            continue
        m = re.match(r"^([a-zA-Z*][\w-]*)?((?:\.[\w-]+)*)$", t)
        if not m:
            return None
        tag = m.group(1)
        if tag == "*":
            return None  # only box-sizing; UI Toolkit is border-box already
        sel.append((comb, {"tag": tag.lower() if tag else None,
                           "classes": [c for c in m.group(2).split(".") if c]}))
        comb = " "
    return sel or None


def parse_css(text, order0=0):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    rules, order = [], order0
    for m in re.finditer(r"([^{}]+)\{([^{}]*)\}", text):
        decls = parse_decls(m.group(2))
        for sel_text in m.group(1).split(","):
            sel = parse_selector(sel_text)
            if sel:
                rules.append(Rule(sel, decls, order))
                order += 1
    return rules


def matches_compound(node, c):
    if c["tag"] and node.tag != c["tag"]:
        return False
    cls = node.classes
    return all(k in cls for k in c["classes"])


def matches(node, sel, i=None):
    if i is None:
        i = len(sel) - 1
    comb, c = sel[i]
    if not matches_compound(node, c):
        return False
    if i == 0:
        return True
    p = node.parent
    if comb == ">":
        return p is not None and p.tag != "#root" and matches(p, sel, i - 1)
    while p is not None and p.tag != "#root":
        if matches(p, sel, i - 1):
            return True
        p = p.parent
    return False


SIDES = ("top", "right", "bottom", "left")


def box4(v):
    p = split_ws(v)
    if len(p) == 1:
        p = p * 4
    elif len(p) == 2:
        p = [p[0], p[1], p[0], p[1]]
    elif len(p) == 3:
        p = [p[0], p[1], p[2], p[1]]
    return p[:4]


def expand(decl, k, v, into):
    if k in ("padding", "margin"):
        for s, x in zip(SIDES, box4(v)):
            into["%s-%s" % (k, s)] = x
    elif k == "border" or (k.startswith("border-") and k[7:] in SIDES):
        sides = SIDES if k == "border" else (k[7:],)
        w, c = None, None
        for t in split_ws(v):
            if re.match(r"^-?[\d.]+(px)?$", t):
                w = t
            elif t in ("solid", "dashed", "dotted", "none"):
                if t == "none":
                    w = "0"
            else:
                c = t
        for s in sides:
            if w is not None:
                into["border-%s-width" % s] = w
            if c is not None:
                into["border-%s-color" % s] = c
    elif k == "border-color":
        for s, x in zip(SIDES, box4(v)):
            into["border-%s-color" % s] = x
    elif k == "border-width":
        for s, x in zip(SIDES, box4(v)):
            into["border-%s-width" % s] = x
    elif k == "border-radius":
        p = split_ws(v)
        corners = ("top-left", "top-right", "bottom-right", "bottom-left")
        vals = box4(v) if len(p) <= 4 else p[:4]
        for cn, x in zip(corners, vals):
            into["border-%s-radius" % cn] = x
    elif k == "gap":
        p = split_ws(v)
        into["row-gap"] = p[0]
        into["column-gap"] = p[1] if len(p) > 1 else p[0]
    elif k == "background":
        into["background"] = v
        into.pop("background-image", None)
        into.pop("background-color", None)
    elif k == "-webkit-mask-image":
        into.setdefault("mask-image", v)
    elif k == "font":
        pass
    elif k == "flex":
        p = split_ws(v)
        into["flex-grow"] = p[0]
        if len(p) > 1:
            into["flex-shrink"] = p[1]
    else:
        into[k] = v


def px(v, ref=None, default=None):
    """'12px' -> 12.0, '50%' -> ref*0.5, '0' -> 0. None when not resolvable."""
    if v is None:
        return default
    v = str(v).strip()
    if v.endswith("px"):
        return float(v[:-2])
    if v.endswith("%"):
        return None if ref is None else float(v[:-1]) * 0.01 * ref
    if v.endswith("em"):
        return None
    try:
        return float(v)
    except ValueError:
        return default


def fmt(x):
    if x is None:
        return "0"
    s = ("%.2f" % x).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


# ════════════════════════════════════════════════════════════════════════════
# Computed style
# ════════════════════════════════════════════════════════════════════════════
BASE = {
    "color": "#F3F4F6", "font-size": "16px", "font-family": "body", "font-weight": "400",
    "font-style": "normal", "letter-spacing": "0px", "line-height": "normal",
    "text-transform": "none", "text-align": "left", "white-space": "normal",
}

TAG_DEFAULTS = {
    "b": {"font-weight": "700"}, "strong": {"font-weight": "700"},
    "i": {"font-style": "italic"}, "em": {"font-style": "italic"},
    "small": {"font-size": "smaller"},
    "button": {"text-align": "center", "padding-top": "1px", "padding-bottom": "1px",
               "padding-left": "6px", "padding-right": "6px"},
}


def family_of(v):
    v = v.lower()
    if "archivo" in v:
        return "archivo"
    if "cabin" in v:
        return "cabin"
    if "condensed" in v:
        return "cond"
    return "body"


def compute_styles(node, rules, parent_cs, prefix=""):
    if not isinstance(node, Node):
        return
    decl = {}
    for k, v in TAG_DEFAULTS.get(node.tag, {}).items():
        decl[k] = v
    matched = [r for r in rules if matches(node, r.sel)]
    matched.sort(key=lambda r: (r.spec, r.order))
    for r in matched:
        for k, v in r.decls.items():
            expand(decl, k, v, decl)
    for k, v in parse_decls(node.attrs.get("style", "")).items():
        expand(decl, k, v, decl)

    cs = {k: parent_cs[k] for k in INHERITED if k in parent_cs}
    fs_parent = float(parent_cs["font-size"][:-2])
    for k, v in decl.items():
        if v == "inherit":
            if k in parent_cs:
                cs[k] = parent_cs[k]
            continue
        cs[k] = v

    # font-size first: em letter-spacing resolves against it.
    fs = cs.get("font-size", "16px")
    if fs == "smaller":
        fs = "%spx" % fmt(fs_parent * 0.833)
    elif fs.endswith("em"):
        fs = "%spx" % fmt(float(fs[:-2]) * fs_parent)
    cs["font-size"] = fs
    if "letter-spacing" in decl and decl["letter-spacing"].endswith("em"):
        cs["letter-spacing"] = "%spx" % fmt(float(decl["letter-spacing"][:-2]) * float(fs[:-2]))
    if "font-family" in decl:
        cs["font-family"] = family_of(decl["font-family"])
    if decl.get("background-clip") == "text" or decl.get("-webkit-background-clip") == "text":
        cs["-gradient-text"] = prefix + gradient_name(node, decl)
        for k in ("background", "background-image", "background-color"):
            cs.pop(k, None)
    node.cs = cs
    for c in node.children:
        compute_styles(c, rules, cs, prefix)


# Chrome's UA sheet makes these border-box whatever the page says.
BORDER_BOX_TAGS = {"button", "input", "select", "textarea"}


def apply_box_sizing(n, border_box, report):
    """UI Toolkit sizes are always border-box. On a page without `*{box-sizing:border-box}`
    (V2's kit has it, V1 does not) an element is content-box unless it says otherwise, and its
    padding + border sit *outside* its width/height - add them to every px size."""
    if not isinstance(n, Node):
        return
    bs = n.cs.get("box-sizing") or ("border-box" if border_box or n.tag in BORDER_BOX_TAGS else "content-box")
    if bs == "content-box":
        for axis, sides in (("width", ("left", "right")), ("height", ("top", "bottom"))):
            extra = sum((px(n.cs.get("padding-" + s), default=0) or 0) + (px(n.cs.get("border-%s-width" % s), default=0) or 0)
                        for s in sides)
            if not extra:
                continue
            for k in (axis, "min-" + axis, "max-" + axis):
                v = n.cs.get(k)
                if v is None or v in ("auto", "none"):
                    continue
                if px(v) is not None:
                    n.cs[k] = "%spx" % fmt(px(v) + extra)
                else:
                    msg = "content-box %s `%s` with padding/border kept as border-box" % (k, v)
                    if msg not in report:
                        report.append(msg)
    for c in n.children:
        apply_box_sizing(c, border_box, report)


def gradient_name(node, decl):
    cls = node.classes
    if "gt" in cls or "goldtxt" in cls:
        return "Gold"
    if "rt" in cls or "redtxt" in cls:
        return "Red"
    if "st" in cls or "silvertxt" in cls:
        return "Silver"
    bg = decl.get("background", decl.get("background-image", "")).upper()
    if "FFF3C9" in bg or "FFD34D" in bg or "F4CB5C" in bg:
        return "Gold"
    if "FF7A80" in bg or "FF6166" in bg:
        return "Red"
    return "Silver"


# ════════════════════════════════════════════════════════════════════════════
# Converter
# ════════════════════════════════════════════════════════════════════════════
class Converter:
    def __init__(self, screen, report, icons, prefix=""):
        self.screen = prefix + screen   # the ScreenId name: Title, V1Home
        self.prefix = prefix
        self.report = report
        self.icons = icons
        self.btn = 0
        self.width_memo = {}
        self._twin = None   # (dx, dy, colour) of a hard shadow under gradient text, for emit_label

    def warn(self, msg):
        if msg not in self.report:
            self.report.append(msg)

    # ── classification ─────────────────────────────────────────────
    @staticmethod
    def display(n):
        d = n.cs.get("display")
        if d:
            return d
        return "inline" if n.tag in INLINE_TAGS else "block"

    @staticmethod
    def absolute(n):
        return n.cs.get("position") in ("absolute", "fixed")

    @staticmethod
    def interactive(n):
        return n.tag in ("a", "button", "input") or n.tag == "a"

    def has_interactive(self, n):
        if self.interactive(n):
            return True
        return any(isinstance(c, Node) and self.has_interactive(c) for c in n.children)

    def has_box(self, n):
        cs = n.cs
        if any(k in cs for k in ("background", "background-image", "background-color", "box-shadow",
                                 "clip-path", "transform", "opacity", "width", "height", "filter",
                                 "mask-image")):
            return True
        for s in SIDES:
            if px(cs.get("padding-" + s), default=0) or px(cs.get("border-%s-width" % s), default=0):
                return True
        return False

    def is_textual(self, n, root=True):
        """True when n's content can be one Label: only text and plain inline spans.
        A flex/grid container never qualifies: its children are separate flex items, and
        it positions them (centring a lone label) - merging would lose both."""
        if root and self.display(n) in ("flex", "inline-flex", "grid"):
            return False
        has_text = False
        for c in n.children:
            if isinstance(c, str):
                if c.strip():
                    has_text = True
                continue
            if c.tag == "br":
                continue
            if c.tag not in TEXT_INLINE or c.tag == "a" and c.attrs.get("href"):
                return False
            if self.display(c) not in ("inline",) or self.absolute(c) or self.has_box(c):
                return False
            if not self.is_textual(c, False):
                return False
            has_text = True
        return has_text

    def needs_paint(self, n):
        cs = n.cs
        bg = cs.get("background")
        if bg and not is_color(bg):
            return True
        if "background-image" in cs or "clip-path" in cs or "box-shadow" in cs or "mask-image" in cs:
            return True
        return False

    # ── widths (for grid tracks) ───────────────────────────────────
    def width(self, n):
        key = id(n)
        if key in self.width_memo:
            return self.width_memo[key]
        w = self._width(n)
        self.width_memo[key] = w
        return w

    def content_width(self, n):
        w = self.width(n)
        if w is None:
            return None
        cs = n.cs
        for s in ("left", "right"):
            w -= px(cs.get("padding-" + s), default=0) or 0
            w -= px(cs.get("border-%s-width" % s), default=0) or 0
        return w

    def _width(self, n):
        cs = n.cs
        if "scr" in n.classes:
            return FRAME_W
        p = n.parent
        pw = self.content_width(p) if p is not None and p.tag != "#root" else FRAME_W
        w = px(cs.get("width"), pw)
        if w is not None:
            return w
        if self.absolute(n):
            l, r = px(cs.get("left"), pw), px(cs.get("right"), pw)
            if l is not None and r is not None and pw is not None:
                return pw - l - r
            return None
        if p is None or pw is None:
            return None
        pd = self.display(p)
        if pd in ("flex", "inline-flex"):
            direction = p.cs.get("flex-direction", "row")
            if direction.startswith("column"):
                if p.cs.get("align-items", "stretch") in ("stretch", "normal") and n.cs.get("align-self") is None:
                    return pw - self.hmargins(n)
                return None
            if float(cs.get("flex-grow", "0")) > 0:
                used = 0.0
                sibs = [c for c in p.children if isinstance(c, Node) and not self.absolute(c)]
                gap = px(p.cs.get("column-gap"), default=0) or 0
                for s in sibs:
                    if s is n:
                        continue
                    sw = px(s.cs.get("width"), pw)
                    if sw is None:
                        return None
                    used += sw + self.hmargins(s)
                return pw - used - gap * (len(sibs) - 1) - self.hmargins(n)
            return None
        if pd == "grid":
            return self.grid_width(p, n)
        if self.display(n) == "block" or n.tag in ("div", "nav"):
            return pw - self.hmargins(n)
        return None

    @staticmethod
    def hmargins(n):
        return (px(n.cs.get("margin-left"), default=0) or 0) + (px(n.cs.get("margin-right"), default=0) or 0)

    @staticmethod
    def hinset(n):
        """Horizontal padding + border of n."""
        return sum((px(n.cs.get("padding-" + s), default=0) or 0) + (px(n.cs.get("border-%s-width" % s), default=0) or 0)
                   for s in ("left", "right"))

    def avail(self, n):
        """Border-box width CSS gives n, best effort - width() plus what it leaves open: a
        shrinkable row item takes what its siblings leave, a stretched or block child takes its
        parent's content width, inline text wraps at its container's. None when unknown.
        Used only to predict text wrapping."""
        w = self.width(n)
        if w is not None:
            return w
        p = n.parent
        if p is None or p.tag == "#root" or self.absolute(n):
            return None
        pw = self.avail(p)
        if pw is None:
            return None
        pw -= self.hinset(p)
        if self.display(p) in ("flex", "inline-flex"):
            if p.cs.get("flex-direction", "row").startswith("column"):
                if p.cs.get("align-items", "stretch") in ("stretch", "normal") and n.cs.get("align-self") is None:
                    return pw - self.hmargins(n)
                return None
            if n.cs.get("flex-shrink") == "0":
                return None
            sibs = [c for c in p.children if isinstance(c, Node) and not self.absolute(c)]
            used = 0.0
            for s in sibs:
                if s is n:
                    continue
                sw = self.width(s)
                if sw is None and (s.svg is not None or s.tag == "img"):
                    sw = px(s.attrs.get("width"))
                if sw is None:
                    return None
                used += sw + self.hmargins(s)
            gap = px(p.cs.get("column-gap"), default=0) or 0
            return pw - used - gap * (len(sibs) - 1) - self.hmargins(n)
        if self.display(p) == "grid":
            return None
        return pw - self.hmargins(n)

    def wrap_lines(self, n):
        """How many lines CSS breaks n's text into (explicit <br> included), predicted from the
        font's advance widths and n's content width; 0 when it cannot tell. UI Toolkit labels
        never wrap unless told to (the theme's .unity-label is nowrap), so the converter must."""
        key = ("wrap", id(n))
        if key in self.width_memo:
            return self.width_memo[key]
        self.width_memo[key] = 0
        cs = self.text_source(n).cs
        w = self.avail(n)
        if w is None or cs.get("white-space") == "nowrap" or cs.get("font-family") == "cond":
            return 0
        w -= self.hinset(n)
        face = font_face(self.face(cs)[0])
        fs = float(cs["font-size"][:-2])
        ls = px(cs.get("letter-spacing"), default=0) or 0
        text = "".join(t for t, _ in segments(n)).strip()
        count = 0
        for para in text.split("\n"):
            line, k = "", 1
            for word in para.strip().split(" "):
                cand = word if not line else line + " " + word
                if line and face.width(cand, fs, ls) - ls > w + 0.5:
                    k += 1
                    line = word
                else:
                    line = cand
            count += k
        self.width_memo[key] = count
        return count

    def grid_tracks(self, g):
        spec = g.cs.get("grid-template-columns", "1fr")
        spec = re.sub(r"repeat\(\s*(\d+)\s*,\s*(minmax\([^)]*\)|[^)]+)\)",
                      lambda m: " ".join([m.group(2)] * int(m.group(1))), spec)
        tracks = []
        for t in split_ws(spec):
            m = re.match(r"minmax\(\s*[^,]+,\s*([\d.]+)fr\s*\)", t)
            if m:
                tracks.append(("fr", float(m.group(1))))
            elif t.endswith("fr"):
                tracks.append(("fr", float(t[:-2])))
            elif t.endswith("px"):
                tracks.append(("px", float(t[:-2])))
            else:
                tracks.append(("fr", 1.0))
                self.warn("grid track `%s` treated as 1fr" % t)
        return tracks

    def grid_layout(self, g):
        """[(child, col, row, span)] and track px widths (or None when unknown)."""
        tracks = self.grid_tracks(g)
        n = len(tracks)
        items, col, row = [], 0, 0
        for c in g.children:
            if not isinstance(c, Node) or self.absolute(c):
                continue
            span = 1
            gc = c.cs.get("grid-column", "")
            if gc:
                if "-1" in gc or "1 / -1" in gc:
                    span = n
                else:
                    m = re.search(r"span\s+(\d+)", gc)
                    if m:
                        span = int(m.group(1))
            if col + span > n:
                col, row = 0, row + 1
            items.append((c, col, row, span))
            col += span
            if col >= n:
                col, row = 0, row + 1
        cw = self.content_width(g)
        gap = px(g.cs.get("column-gap"), default=0) or 0
        if cw is None:
            return items, None, tracks
        fixed = sum(v for k, v in tracks if k == "px")
        frs = sum(v for k, v in tracks if k == "fr")
        unit = (cw - fixed - gap * (n - 1)) / frs if frs else 0
        widths = [v if k == "px" else v * unit for k, v in tracks]
        return items, widths, tracks

    def grid_width(self, g, child):
        items, widths, _ = self.grid_layout(g)
        if widths is None:
            return None
        gap = px(g.cs.get("column-gap"), default=0) or 0
        for c, col, row, span in items:
            if c is child:
                return sum(widths[col:col + span]) + gap * (span - 1)
        return None

    # ── emit ───────────────────────────────────────────────────────
    def convert(self, scr):
        out = []
        cs = scr.cs
        root_style = ["position: absolute", "left: 0", "top: 0", "right: 0", "bottom: 0", "overflow: hidden"]
        bg = cs.get("background", cs.get("background-color"))
        paint = None
        if bg:
            if is_color(bg):
                root_style.append("background-color: %s" % uss_color(bg))
            else:
                paint = bg
        out.append('<ui:VisualElement name="screen-%s" class="fe-screen" picking-mode="Ignore" style="%s;">'
                   % (self.screen, "; ".join(root_style)))
        if paint:
            out.append('  <fe:PaintBox class="fe-bleed" picking-mode="Ignore" paint="%s" style="position: absolute; left: 0; top: 0; right: 0; bottom: 0;" />'
                       % attr(paint))

        # Children: full-screen `.fill` layers bleed to the panel edge; every run of other
        # children goes into a 430 x 932 frame centred on the panel. Runs keep DOM order,
        # so draw order is exactly the mockup's.
        run = []
        def flush():
            if not run:
                return
            out.append('  <ui:VisualElement class="fe-frame" picking-mode="Ignore" style="position: absolute; left: 50%; top: 50%; width: 430px; height: 932px; margin-left: -215px; margin-top: -466px;">')
            for c in run:
                self.emit(c, out, 2, scr)
            out.append("  </ui:VisualElement>")
            run.clear()

        for c in scr.children:
            if isinstance(c, str):
                if c.strip():
                    self.warn("loose text in .scr ignored")
                continue
            if self.is_fill(c):
                flush()
                self.emit(c, out, 1, scr, bleed=True)
            else:
                run.append(c)
        flush()
        out.append("</ui:VisualElement>")
        return out

    def is_fill(self, n):
        """A full-frame layer: V2's `.fill` (100%), or V1's same box spelled 430 x 932 px."""
        cs = n.cs
        return (self.absolute(n) and px(cs.get("left"), FRAME_W) == 0 and px(cs.get("top"), FRAME_H) == 0
                and px(cs.get("width"), FRAME_W) == FRAME_W and px(cs.get("height"), FRAME_H) == FRAME_H)

    def emit(self, n, out, depth, parent, bleed=False, extra=None):
        ind = "  " * depth
        if n.tag == "br":
            return
        if n.svg is not None:
            return self.emit_svg(n, out, ind, parent, extra)
        if n.tag == "img":
            return self.emit_img(n, out, ind, parent, bleed, extra)
        if n.tag == "input":
            return self.emit_input(n, out, ind, parent, extra)

        style = self.layout_style(n, parent, bleed, extra)
        attrs = self.common_attrs(n)
        textual = self.is_textual(n)
        paint = self.needs_paint(n)

        if textual and not paint and any(g in all_text(n) for g in GLYPHS):
            return self.emit_glyph_row(n, out, ind, parent, extra)

        if textual and not paint:
            style += self.box_style(n, label=True)
            style += self.text_style(self.text_source(n), n)
            self.emit_label(out, ind, attrs, self.rich(n), style)
            return

        tag = "fe:PaintBox" if paint else "ui:VisualElement"
        style += self.box_style(n, label=False, paint=paint)
        style += self.container_style(n)
        pattrs = self.paint_attrs(n) if paint else ""
        kids = [c for c in n.children if isinstance(c, Node) or (isinstance(c, str) and c.strip())]
        if not kids:
            out.append('%s<%s%s%s style="%s" />' % (ind, tag, attrs, pattrs, join_style(style)))
            return
        out.append('%s<%s%s%s style="%s">' % (ind, tag, attrs, pattrs, join_style(style)))
        if textual:
            ls = self.text_style(self.text_source(n), n, label_is_box=False)
            self.emit_label(out, ind + "  ", ' picking-mode="Ignore"', self.rich(n), ls)
        else:
            self.emit_children(n, out, depth + 1)
        out.append("%s</%s>" % (ind, tag))

    # What a shadow twin's container takes from the label's style; the rest stays on the labels.
    TWIN_BOX_KEYS = ("position", "left", "top", "right", "bottom", "width", "min-width", "max-width",
                     "flex-grow", "flex-shrink", "flex-basis", "align-self", "rotate", "scale",
                     "translate", "transform-origin", "opacity", "filter", "visibility")

    def emit_label(self, out, ind, attrs, rich, style):
        """A Label - or, when text_style kept a hard shadow under gradient text (S-082), a
        shadow twin. A container takes the label's place in the layout. Inside it, an in-flow
        label draws the plain text in the shadow colour with its glyphs pushed down by the
        shadow's offset (padding grows, a negative margin hands the space back, so the
        container keeps the label's size), and an absolute label after it draws the gradient
        text where CSS has it - later siblings draw on top. A Label with children would stop
        measuring its text (Yoga measures leaves only), hence siblings, not a child."""
        twin, self._twin = self._twin, None
        kv = {}
        for it in style:
            k, v = it.split(":", 1)
            kv[k.strip()] = v.strip()
        boxed = any(k.startswith(("background", "border-")) and not k.endswith("radius") for k in kv)
        if twin and (boxed or ("height" in kv and px(kv["height"]) is None)):
            self.warn("shadow twin skipped (label has a box): shadow dropped")
            twin = None
        if not twin:
            out.append('%s<ui:Label%s text="%s" style="%s" />' % (ind, attrs, attr(rich), join_style(style)))
            return
        dx, dy, col = twin
        if dx:
            self.warn("shadow twin ignores the horizontal offset")
        outer = [k for k in kv if k in self.TWIN_BOX_KEYS or k.startswith("margin-")]
        box = ["%s: %s" % (k, kv[k]) for k in outer] or ["flex-direction: column"]   # (the default)
        text =["%s: %s" % (k, v) for k, v in kv.items() if k not in outer]
        shadow = text + ["padding-top: %spx" % fmt((px(kv.get("padding-top"), default=0) or 0) + dy),
                         "margin-bottom: %spx" % fmt(-dy), "color: %s" % uss_color(col)]
        if "height" in kv:
            shadow.append("height: %spx" % fmt(px(kv["height"]) + dy))
        face = [x for x in text if not x.startswith("height:")] + [
            "position: absolute", "left: 0", "top: 0", "right: 0", "bottom: 0", "color: #FFFFFF"]
        plain = re.sub(r"</?(gradient|color)\b[^>]*>", "", rich)
        out.append('%s<ui:VisualElement%s style="%s">' % (ind, attrs, join_style(box)))
        out.append('%s  <ui:Label picking-mode="Ignore" class="fe-shadow" text="%s" style="%s" />'
                   % (ind, attr(plain), join_style(shadow)))
        out.append('%s  <ui:Label picking-mode="Ignore" class="fe-shadow-face" text="%s" style="%s" />'
                   % (ind, attr(rich), join_style(face)))
        out.append("%s</ui:VisualElement>" % ind)

    def emit_children(self, n, out, depth):
        d = self.display(n)
        grid = d == "grid"
        kids = [c for c in n.children if isinstance(c, Node) or (isinstance(c, str) and c.strip())]
        flow = [c for c in kids if isinstance(c, str) or not self.absolute(c)]
        direction = self.flow_direction(n)
        gap_r = px(n.cs.get("row-gap"), default=0) or 0
        gap_c = px(n.cs.get("column-gap"), default=0) or 0

        grid_info = {}
        if grid:
            items, widths, tracks = self.grid_layout(n)
            for c, col, row, span in items:
                if widths is not None:
                    w = sum(widths[col:col + span]) + gap_c * (span - 1)
                    # A hair under the exact width so float rounding never wraps a row early.
                    grid_info[id(c)] = {"width": "%spx" % fmt(w - 0.02)}
                else:
                    total = sum(v for k, v in tracks)
                    share = sum(v for k, v in tracks[col:col + span]) / total * 100
                    grid_info[id(c)] = {"width": "%s%%" % fmt(share)}
                    self.warn("grid without a known width: tracks became percentages, gaps dropped")
                if col > 0 and gap_c:
                    grid_info[id(c)]["margin-left"] = gap_c
                if row > 0 and gap_r:
                    grid_info[id(c)]["margin-top"] = gap_r

        first = True
        for c in kids:
            extra = {}
            if isinstance(c, str):
                text = collapse(c, n.cs)
                if not text.strip():
                    continue
                if not first:
                    self.gap_margin(extra, direction, gap_r, gap_c)
                first = False
                ls = self.text_style(n, n) + ["flex-shrink: 0"] + ["%s: %spx" % (k, fmt(v)) for k, v in extra.items()]
                out.append('%s<ui:Label picking-mode="Ignore" text="%s" style="%s" />'
                           % ("  " * depth, attr(text.strip()), join_style(ls)))
                continue
            if c in flow and not grid:
                if not first:
                    self.gap_margin(extra, direction, gap_r, gap_c)
                first = False
            if grid and id(c) in grid_info:
                extra.update(grid_info[id(c)])
            self.emit(c, out, depth, n, extra=extra)

    @staticmethod
    def gap_margin(extra, direction, gap_r, gap_c):
        if direction == "row" and gap_c:
            extra["margin-left"] = gap_c
        elif direction == "row-reverse" and gap_c:
            extra["margin-right"] = gap_c
        elif direction == "column" and gap_r:
            extra["margin-top"] = gap_r
        elif direction == "column-reverse" and gap_r:
            extra["margin-bottom"] = gap_r

    def flow_direction(self, n):
        d = self.display(n)
        if d in ("flex", "inline-flex"):
            return n.cs.get("flex-direction", "row")
        if d == "grid":
            return "row"
        # Block container: inline children line up in a row, block children stack.
        kids = [c for c in n.children if isinstance(c, Node) and not self.absolute(c)]
        if kids and all(self.display(c) in ("inline", "inline-block", "inline-flex") for c in kids) and len(kids) > 1:
            return "row"
        return "column"

    # ── style builders ─────────────────────────────────────────────
    def common_attrs(self, n):
        a = ""
        hook = n.attrs.get("data-hook")
        if n.tag == "a" and n.attrs.get("href", "").endswith(".dc.html"):
            target = n.attrs["href"].split("/")[-1][:-len(".dc.html")]
            a += ' name="nav-%s%s"' % (self.prefix, target)
        elif n.tag == "button":
            self.btn += 1
            a += ' name="btn-%d"' % self.btn
        elif hook:
            a += ' name="%s"' % hook
        cls = ["k-" + c for c in n.classes]
        if hook and (n.tag in ("a", "button")):
            cls.append("hook-" + hook)
        if n.tag == "a":
            cls.insert(0, "fe-link")
        elif n.tag == "button":
            cls.insert(0, "fe-btn")
        if cls:
            a += ' class="%s"' % " ".join(cls)
        if not self.has_interactive(n):
            a += ' picking-mode="Ignore"'
        if n.attrs.get("aria-label"):
            a += ' tooltip="%s"' % attr(n.attrs["aria-label"])
        return a

    def layout_style(self, n, parent, bleed=False, extra=None):
        cs = n.cs
        s = []
        pw = self.content_width(parent) if parent is not None else FRAME_W
        if self.absolute(n):
            s.append("position: absolute")
            for side in SIDES:
                v = cs.get(side)
                if v is not None and v != "auto":
                    s.append("%s: %s" % (side, uss_len(v)))
        if bleed:
            s = ["position: absolute", "left: 0", "top: 0", "right: 0", "bottom: 0"]
        else:
            for k in ("width", "height", "min-width", "min-height", "max-width", "max-height"):
                v = cs.get(k)
                if v and v not in ("auto", "none"):
                    s.append("%s: %s" % (k, uss_len(v)))
        if extra:
            for k, v in extra.items():
                if k == "width":
                    s = [x for x in s if not x.startswith("width:")]
                    s.append("width: %s" % v)
        # flex item properties
        pd = self.display(parent) if parent is not None else "block"
        in_flow = not self.absolute(n)
        if in_flow:
            fg = cs.get("flex-grow")
            if fg:
                s.append("flex-grow: %s" % fg)
            fsh = cs.get("flex-shrink")
            if fsh is not None:
                s.append("flex-shrink: %s" % fsh)
            elif pd in ("flex", "inline-flex", "grid") or self.flow_direction(parent) == "row":
                if self.flow_direction(parent).startswith("row") and not fg and "width" not in cs:
                    s.append("flex-shrink: 0")
            a = cs.get("align-self")
            if a:
                s.append("align-self: %s" % align(a))
            elif pd not in ("flex", "inline-flex", "grid") and self.display(n) in ("inline", "inline-block", "inline-flex") \
                    and self.flow_direction(parent) == "column" and self.has_box(n):
                # An inline-level box in a block parent shrink-wraps instead of stretching.
                ta = parent.cs.get("text-align", "left")
                s.append("align-self: %s" % {"center": "center", "right": "flex-end"}.get(ta, "flex-start"))
        # margins (+ gap margins)
        mg = {}
        for side in SIDES:
            v = cs.get("margin-" + side)
            if v is not None:
                mg[side] = v
        if extra:
            for side in SIDES:
                k = "margin-" + side
                if k in extra:
                    base = px(mg.get(side), default=0) or 0
                    mg[side] = "%spx" % fmt(base + extra[k])
        for side, v in mg.items():
            s.append("margin-%s: %s" % (side, "auto" if v == "auto" else uss_len(v)))
        return s

    def box_style(self, n, label, paint=False):
        cs = n.cs
        s = []
        for side in SIDES:
            v = cs.get("padding-" + side)
            if v is not None and px(v, default=0) != 0:
                s.append("padding-%s: %s" % (side, uss_len(v)))
        clipped = paint and cs.get("clip-path")
        for side in SIDES:
            w = cs.get("border-%s-width" % side)
            c = cs.get("border-%s-color" % side)
            if clipped:
                break  # PaintBox draws it, clipped to the shape (paint_attrs: edge)
            if w is not None and px(w, default=0):
                s.append("border-%s-width: %s" % (side, uss_len(w)))
                s.append("border-%s-color: %s" % (side, uss_color(c or cs.get("color", "#fff"))))
        w = self.width(n)
        for corner in ("top-left", "top-right", "bottom-right", "bottom-left"):
            v = cs.get("border-%s-radius" % corner)
            if v and v not in ("0", "0px"):
                if v.endswith("%"):
                    if w is not None:
                        v = "%spx" % fmt(float(v[:-1]) * 0.01 * w)
                    else:
                        h = px(cs.get("height"))
                        v = "%spx" % fmt(float(v[:-1]) * 0.01 * h) if h else "9999px"
                s.append("border-%s-radius: %s" % (corner, uss_len(v)))
        bg = cs.get("background", cs.get("background-color"))
        if bg and is_color(bg) and "background-image" not in cs and not paint:
            s.append("background-color: %s" % uss_color(bg))
        if cs.get("opacity"):
            s.append("opacity: %s" % cs["opacity"])
        if cs.get("overflow") == "hidden":
            s.append("overflow: hidden")
        s += self.transform_style(n)
        f = self.filter_style(n, cs.get("filter"))
        if f:
            s.append(f)
        if cs.get("visibility") == "hidden":
            s.append("visibility: hidden")
        if cs.get("mix-blend-mode"):
            self.warn("mix-blend-mode ignored (UI Toolkit has no blend modes)")
        if "backdrop-filter" in cs or "-webkit-backdrop-filter" in cs:
            self.warn("`.glass` backdrop blur approximated by its own translucent fill")
        return s

    def container_style(self, n):
        d = self.display(n)
        s = []
        direction = self.flow_direction(n)
        if d == "grid":
            s += ["flex-direction: row", "flex-wrap: wrap"]
            ai = n.cs.get("align-items")
            if ai:
                s.append("align-items: %s" % align(ai))
        else:
            s.append("flex-direction: %s" % direction)
            if d in ("flex", "inline-flex"):
                ai = n.cs.get("align-items")
                if ai:
                    s.append("align-items: %s" % align(ai))
                jc = n.cs.get("justify-content")
                if jc:
                    s.append("justify-content: %s" % justify(jc))
                if n.cs.get("flex-wrap") == "wrap":
                    s.append("flex-wrap: wrap")
            elif direction == "row":
                s.append("align-items: center")
                ta = n.cs.get("text-align")
                if ta == "center":
                    s.append("justify-content: center")
                elif ta == "right":
                    s.append("justify-content: flex-end")
        return s

    def transform_style(self, n):
        t = n.cs.get("transform")
        s = []
        if t:
            for fn, arg in re.findall(r"([a-zA-Z]+)\(([^)]*)\)", t):
                fn = fn.lower()
                if fn == "rotate":
                    s.append("rotate: %s" % arg.strip())
                elif fn == "scale":
                    v = arg.strip()
                    s.append("scale: %s %s" % (v, v) if "," not in v else "scale: %s" % v.replace(",", " "))
                elif fn == "translatex":
                    s.append("translate: %s 0" % uss_len(arg.strip()))
                elif fn == "translatey":
                    s.append("translate: 0 %s" % uss_len(arg.strip()))
                elif fn == "translate":
                    a = [uss_len(x.strip()) for x in arg.split(",")]
                    s.append("translate: %s %s" % (a[0], a[1] if len(a) > 1 else "0"))
                else:
                    self.warn("transform `%s()` unsupported, dropped" % fn)
        o = n.cs.get("transform-origin")
        if o:
            parts = [uss_len(p) if p not in ("center", "left", "right", "top", "bottom") else p for p in split_ws(o)]
            s.append("transform-origin: %s" % " ".join(parts))
        return s

    def filter_style(self, n, f):
        if not f:
            return None
        out = []
        for fn, arg in re.findall(r"([a-z-]+)\(([^)]*\)?)\)", f):
            arg = arg.strip()
            if fn in ("grayscale", "contrast", "sepia", "invert", "opacity"):
                out.append("%s(%s)" % (fn, arg))
            elif fn == "blur":
                out.append("blur(%s)" % uss_len(arg))
            elif fn == "brightness":
                b = max(0.0, min(1.0, float(arg)))
                g = int(round(b * 255))
                out.append("tint(rgb(%d, %d, %d))" % (g, g, g))
                if float(arg) > 1:
                    self.warn("brightness > 1 clamped to 1 (tint cannot brighten)")
            elif fn == "saturate":
                v = float(arg)
                if v < 1:
                    out.append("grayscale(%s)" % fmt(1 - v))
                else:
                    self.warn("saturate(>1) dropped")
            elif fn == "drop-shadow":
                if n.tag not in ("span", "b", "div"):
                    self.warn("drop-shadow on <%s> dropped" % n.tag)
            elif fn == "hue-rotate":
                out.append("hue-rotate(%s)" % arg)
            else:
                self.warn("filter `%s` dropped" % fn)
        return "filter: %s" % " ".join(out) if out else None

    def text_source(self, n):
        """When all of n's text sits in one inline child, that child styles the Label."""
        kids = [c for c in n.children if isinstance(c, Node) or (isinstance(c, str) and c.strip())]
        if len(kids) == 1 and isinstance(kids[0], Node) and kids[0].tag in TEXT_INLINE and not self.has_box(kids[0]):
            return self.text_source(kids[0])
        return n

    def text_style(self, src, box, label_is_box=True):
        cs = src.cs
        s = []
        fam = cs.get("font-family", "body")
        s.append("-unity-font-definition: url('%s%s.ttf')" % (FONT_URL, self.face(cs)[0]))
        fs = float(cs["font-size"][:-2])
        (a_css, d_css), (a_tc, d_tc) = FONTS[fam]["css"], FONTS[fam]["tc"]
        s.append("font-size: %spx" % fmt(fs))
        grad = cs.get("-gradient-text")
        color = "#FFFFFF" if grad else cs.get("color", "#F3F4F6")
        if color == "transparent":
            color = "#FFFFFF"
        s.append("color: %s" % uss_color(color))
        # USS letter-spacing is ignored by UI Toolkit labels (DEBUG S-076); the tracking is a
        # <cspace> tag in the text (rich()). CSS also spaces *after* the last glyph, so the
        # box is one spacing wider - padding-right keeps centred text where CSS puts it.
        ls = px(cs.get("letter-spacing"), default=0) or 0
        if abs(ls) > 0.01:
            pr = (px(box.cs.get("padding-right"), default=0) or 0) if label_is_box else 0
            s.append("padding-right: %spx" % fmt(pr + ls))
        ta = cs.get("text-align", "left")
        explicit = self.rich(box).count("\n") + 1
        wrapped = self.wrap_lines(box)        # lines CSS wraps to (0: cannot tell)
        n_lines = max(explicit, wrapped)
        multi = n_lines > 1
        h = {"center": "center", "right": "right", "end": "right"}.get(ta, "left")
        lh = cs.get("line-height", "normal")
        strut = box is not src and self.display(box) not in ("flex", "inline-flex", "grid") and not multi \
            and "height" not in box.cs
        if strut:
            # Text inside a plain block sits on the block's own font *strut* (CSS 2.1 §10.8):
            # the line box is the taller of the strut and the span, and the span's glyphs
            # hang from the shared baseline. Reproduce that box and the glyphs' offset in it.
            H, y0 = strut_box(box.cs, cs)
            y0 += fs * (a_css - a_tc)   # TextCore hangs the glyphs from its own ascent
            pt = (px(box.cs.get("padding-top"), default=0) or 0) if label_is_box else 0
            pb = (px(box.cs.get("padding-bottom"), default=0) or 0) if label_is_box else 0
            s.append("height: %spx" % fmt(H + pt + pb))   # UI Toolkit heights are border-box
            if y0 + pt >= 0:
                s.append("padding-top: %spx" % fmt(y0 + pt))
            else:
                # Big text in a small-strut block starts above the box; USS has no negative
                # padding, so lift the glyphs visually instead.
                s.append("translate: 0 %spx" % fmt(y0 + pt))
            s.append("-unity-text-align: upper-%s" % h)
        else:
            s.append("-unity-text-align: %s-%s" % ("upper" if multi else "middle", h))
            if lh != "normal" and "height" not in box.cs and not multi:
                # A lone line box is font-size x line-height; the glyphs overflow it, centred.
                s.append("height: %spx" % fmt(line_px(cs)))
            elif lh != "normal" and multi:
                # TextCore starts line 1 at the ascender; CSS starts it half-leading higher
                # (negative leading for the kit's .86 titles). Size the box like CSS and
                # shift the glyphs by the half-leading; the <line-height> tag spaces the rest.
                lpx = line_px(cs)
                if "height" not in box.cs:
                    s.append("height: %spx" % fmt(n_lines * lpx))
                s.append("translate: 0 %spx" % fmt((lpx - fs * (a_css + d_css)) / 2 + fs * (a_css - a_tc)))
            if not multi:
                # CSS centres the em box by the metrics it laid out with, TextCore by its own;
                # where those differ (Archivo Black) the baseline moves by half the difference.
                dy = fs * ((a_css - d_css) - (a_tc - d_tc)) / 2
                if abs(dy) > 0.01:
                    s.append("translate: 0 %spx" % fmt(dy))
        if fam == "cond" or cs.get("white-space") == "nowrap":
            s.append("white-space: nowrap")
        elif wrapped > explicit:
            s.append("white-space: normal")   # CSS wraps here; the theme's labels would not
        ts = cs.get("text-shadow")
        shadow = None
        if ts and ts != "none":
            shadow = split_top(ts, ",")
            if len(shadow) > 1:
                self.warn("multiple text-shadows (`.chroma`): kept the first")
            shadow = shadow[0]
        f = box.cs.get("filter", "") + " " + cs.get("filter", "")
        m = re.search(r"drop-shadow\(([^()]*(?:\([^)]*\))?[^()]*)\)", f)
        if not shadow and m:
            shadow = m.group(1)
        self._twin = None
        if shadow and (grad or "<gradient" in self.rich(box)):
            # UI Toolkit draws a label that has both a text-shadow and <gradient> runs in its
            # plain colour - the gradient is lost (DEBUG S-082). A hard shadow (no blur) can
            # be redrawn as a second label (emit_label); a soft one is dropped.
            parts = split_ws(shadow.strip())
            lens = [px(p) for p in parts if not is_color(p)]
            col = next((p for p in parts if is_color(p)), "#000")
            if len(lens) >= 2 and None not in lens and (len(lens) < 3 or lens[2] == 0) and lens[1] > 0:
                self._twin = (lens[0], lens[1], col)
                self.warn("hard text-shadow under gradient text drawn by a shadow twin label (S-082)")
            else:
                self.warn("soft text-shadow under gradient text dropped (UI Toolkit loses the gradient, S-082)")
            shadow = None
        if shadow:
            parts = split_ws(shadow.strip())
            col = next((p for p in parts if is_color(p)), "#000")
            lens = [uss_len(p) for p in parts if not is_color(p)]
            while len(lens) < 3:
                lens.append("0")
            s.append("text-shadow: %s %s %s %s" % (lens[0], lens[1], lens[2], uss_color(col)))
        return s

    def face(self, cs):
        """(TTF path under Art/Fonts, synthetic bold) for a computed style."""
        wt = weight(cs)
        path, got = self.font_file(cs.get("font-family", "body"), wt, cs.get("font-style", "normal") == "italic")
        # A browser fakes bold when 600+ is asked of a face lighter than 600 (Archivo Black
        # ships one weight, and `.lbl` asks 700 of whatever it wraps). TextCore's <b> does too.
        return path, wt >= 600 and got < 600

    def font_file(self, fam, wt, italic):
        """(path, weight) of the face CSS font matching picks (CSS Fonts 4 §5.2): a 400-500
        target looks up to 500 then down, a lighter one looks down, a bolder one up."""
        spec = FONTS[fam]
        files = spec["files"]
        if (wt, italic) in files:
            return spec["dir"] + "/" + files[(wt, italic)], wt
        slant = [k for k in files if k[1] == italic]
        if not slant:
            self.warn("%s italic missing: upright used" % spec["name"])
            slant = [k for k in files if not k[1]]
        weights = sorted(k[0] for k in slant)
        up = [w for w in weights if w > wt]
        down = [w for w in reversed(weights) if w < wt]
        if wt in weights:
            order = [wt]
        elif 400 <= wt <= 500:
            order = [w for w in up if w <= 500] + down + [w for w in up if w > 500]
        elif wt < 400:
            order = down + up
        else:
            order = up + down
        got = order[0]
        f = files[(got, slant[0][1])]
        self.warn("font %s %d%s missing: used %s" % (spec["name"], wt, " italic" if italic else "", f))
        return spec["dir"] + "/" + f, got

    def rich(self, n):
        """The Label text for n: rich-text runs relative to its text source's style."""
        base = self.text_source(n).cs
        text = self.rich_runs(n, base)
        text = re.sub(r" *\n *", "\n", text).strip(" ")
        if self.face(base)[1]:
            text = "<b>%s</b>" % text
        ls = px(base.get("letter-spacing"), default=0) or 0
        if abs(ls) > 0.01:
            text = "<cspace=%spx>%s</cspace>" % (fmt(ls), text)
        if base.get("-gradient-text"):
            text = '<gradient="%s">%s</gradient>' % (base["-gradient-text"], text)
        if ("\n" in text or self.wrap_lines(n) > 1) and base.get("line-height", "normal") != "normal":
            text = "<line-height=%spx>%s" % (fmt(line_px(base)), text)
        return text

    def rich_runs(self, n, base):
        out = []
        for c in n.children:
            if isinstance(c, str):
                out.append(collapse(c, n.cs))
            elif c.tag == "br":
                out.append("\n")
            else:
                inner = self.rich_runs(c, base)
                out.append(self.wrap(inner, c.cs, base, n.cs))
        return "".join(out)

    def wrap(self, inner, cs, base, parent_cs):
        if cs is base:
            return inner
        if cs.get("-gradient-text") and not parent_cs.get("-gradient-text"):
            inner = '<gradient="%s">%s</gradient>' % (cs["-gradient-text"], inner)
        if cs.get("color") != parent_cs.get("color") and cs.get("color") not in (None, "transparent"):
            inner = "<color=%s>%s</color>" % (hex_color(cs["color"]), inner)
        if cs.get("font-size") != parent_cs.get("font-size"):
            inner = "<size=%s>%s</size>" % (fmt(float(cs["font-size"][:-2])), inner)
        w, pw = weight(cs), weight(parent_cs)
        if w >= 600 > pw or (cs.get("font-family") != parent_cs.get("font-family") and w > pw):
            inner = "<b>%s</b>" % inner
        if cs.get("font-family") != parent_cs.get("font-family"):
            self.warn("mixed font families inside one text run: inner run keeps the outer font")
        if cs.get("font-style") == "italic" and parent_cs.get("font-style") != "italic":
            inner = "<i>%s</i>" % inner
        ls, pls = px(cs.get("letter-spacing"), default=0) or 0, px(parent_cs.get("letter-spacing"), default=0) or 0
        if abs(ls - pls) > 0.01:
            inner = "<cspace=%spx>%s</cspace>" % (fmt(ls), inner)
            if abs(pls) > 0.01:
                inner += "<cspace=%spx>" % fmt(pls)
        return inner

    def paint_attrs(self, n):
        cs = n.cs
        a = ""
        layers = []
        if "background-image" in cs:
            layers.append(cs["background-image"])
        bg = cs.get("background")
        if bg:
            layers.append(bg)
        elif cs.get("background-color"):
            layers.append(cs["background-color"])
        if layers:
            a += ' paint="%s"' % attr(", ".join(layers))
        if cs.get("background-size") and cs["background-size"] not in ("cover", "contain", "auto"):
            a += ' tile="%s"' % attr(cs["background-size"])
        if cs.get("clip-path"):
            a += ' clip="%s"' % attr(cs["clip-path"])
            w = px(cs.get("border-top-width"), default=0) or 0
            if w:
                col = cs.get("border-top-color") or cs.get("color", "#fff")
                a += ' edge="%spx %s"' % (fmt(w), attr(col))
        if cs.get("box-shadow"):
            a += ' shadow="%s"' % attr(cs["box-shadow"])
        if cs.get("mask-image"):
            a += ' mask="%s"' % attr(cs["mask-image"])
        return a

    def emit_glyph_row(self, n, out, ind, parent, extra):
        """A text run containing non-Barlow glyphs: a row of labels and vector glyphs."""
        style = self.layout_style(n, parent, False, extra) + self.box_style(n, label=False)
        style += ["flex-direction: row", "align-items: center"]
        out.append('%s<ui:VisualElement%s style="%s">' % (ind, self.common_attrs(n), join_style(style)))
        runs = []
        for text, cs in segments(n):
            for part in re.split("([%s])" % "".join(GLYPHS), text):
                if part:
                    runs.append((part, cs))
        if runs:
            runs[0] = (runs[0][0].lstrip(), runs[0][1])
            runs[-1] = (runs[-1][0].rstrip(), runs[-1][1])
        for text, cs in runs:
            if not text:
                continue
            fs = float(cs["font-size"][:-2])
            ls = px(cs.get("letter-spacing"), default=0) or 0
            if text in GLYPHS:
                svg, k = GLYPHS[text]
                svg = svg.format(c=hex_color(cs.get("color", "#F3F4F6"))[:7])
                name = "ico_%s.svg" % hashlib.sha1(svg.encode("utf-8")).hexdigest()[:10]
                self.icons[name] = svg
                size = fs * k
                st = ["width: %spx" % fmt(size), "height: %spx" % fmt(size), "flex-shrink: 0",
                      "margin-right: %spx" % fmt(ls), "background-image: url('%s%s')" % (ICON_URL, name),
                      "-unity-background-scale-mode: scale-to-fit"]
                out.append('%s  <ui:VisualElement picking-mode="Ignore" class="fe-icon fe-glyph" style="%s" />' % (ind, join_style(st)))
            else:
                probe = Node("span", {}, n)
                probe.cs = cs
                st = self.text_style(probe, probe) + ["flex-shrink: 0"]
                out.append('%s  <ui:Label picking-mode="Ignore" text="%s" style="%s" />' % (ind, attr(text), join_style(st)))
        out.append("%s</ui:VisualElement>" % ind)

    # ── leaves ─────────────────────────────────────────────────────
    def emit_img(self, n, out, ind, parent, bleed, extra):
        cs = n.cs
        src = n.attrs.get("src", "")
        blob = src.split("/")[-1]
        file = BLOBS.get(blob)
        if file is None:
            self.warn("image `%s` not mapped" % src)
            return
        style = self.layout_style(n, parent, bleed, extra)
        style += self.box_style(n, label=False)
        style.append("background-image: url('%s%s')" % (IMAGE_URL, file))
        fit = cs.get("object-fit")
        if fit == "cover":
            style.append("background-size: cover")
            pos = split_ws(cs.get("object-position", "50% 50%"))
            if len(pos) == 1:
                pos.append("50%")
            style.append("background-position-x: %s" % bg_pos(pos[0], "x"))
            style.append("background-position-y: %s" % bg_pos(pos[1], "y"))
        elif fit == "contain":
            style.append("-unity-background-scale-mode: scale-to-fit")
        else:
            style.append("-unity-background-scale-mode: stretch-to-fill")
        if cs.get("mask-image"):
            masked = self.bake_masked(file, n)
            if masked:
                drop = ("background-image", "background-size", "background-position", "-unity-background-scale-mode")
                style = [x for x in style if not x.startswith(drop)]
                style.append("background-image: url('%sMasked/%s')" % (IMAGE_URL, masked))
                style.append("-unity-background-scale-mode: stretch-to-fill")
            else:
                self.warn("mask-image on <img> dropped (not a PNG or not a vertical linear mask)")
        out.append('%s<ui:VisualElement picking-mode="Ignore" class="fe-img" style="%s;" />' % (ind, join_style(style)[:-1]))

    def bake_masked(self, file, n):
        """UI Toolkit cannot gradient-mask an image, so bake it: crop the source the way
        object-fit: cover does, at 2x the element size, and multiply in the mask's alpha.
        Returns the file name under Art/UI/Mockup/Masked, or None."""
        cs = n.cs
        w, h = px(cs.get("width")), px(cs.get("height"))
        mask = cs.get("mask-image", "").strip()
        m = re.match(r"^linear-gradient\((.*)\)$", mask)
        if not file.endswith(".png") or w is None or h is None or not m:
            return None
        alpha = linear_alpha(m.group(1), h)
        if alpha is None:
            return None
        key = hashlib.sha1(("%s|%s|%s|%s|%s" % (file, w, h, mask, cs.get("object-position", ""))).encode()).hexdigest()[:10]
        name = "%s_%s.png" % (file[:-4], key)
        path = os.path.join(OUT_MASKED, name)
        if not os.path.exists(path):
            iw, ih, rows = pngio.read_rgba(os.path.join(IMAGES, file))
            W, H = int(round(w * 2)), int(round(h * 2))
            scale = max(W / float(iw), H / float(ih))
            pos = split_ws(cs.get("object-position", "50% 50%")) + ["50%"]
            fx = frac(bg_pos(pos[0], "x"))
            fy = frac(bg_pos(pos[1], "y"))
            ox, oy = (iw * scale - W) * fx, (ih * scale - H) * fy
            out = []
            for y in range(H):
                sy = min(ih - 1, max(0, int((y + 0.5 + oy) / scale)))
                src = rows[sy]
                a = alpha((y + 0.5) / 2.0)
                line = bytearray(W * 4)
                for x in range(W):
                    sx = min(iw - 1, max(0, int((x + 0.5 + ox) / scale)))
                    o = sx * 4
                    line[4 * x:4 * x + 3] = src[o:o + 3]
                    line[4 * x + 3] = int(src[o + 3] * a + 0.5)
                out.append(line)
            os.makedirs(OUT_MASKED, exist_ok=True)
            pngio.write(path, W, H, out, alpha=True)
        return name

    def emit_svg(self, n, out, ind, parent, extra):
        svg = n.svg
        cs = n.cs
        texts = re.findall(r"<text\b([^>]*)>([^<]*)</text>", svg)
        svg = re.sub(r"<text\b[^>]*>[^<]*</text>", "", svg)
        color = cs.get("color", "#F3F4F6")
        svg = svg.replace("currentColor", hex_color(color)[:7])
        if "xmlns=" not in svg:
            svg = svg.replace("<svg", '<svg xmlns="http://www.w3.org/2000/svg"', 1)
        # Strip layout attributes that belong to the element, not the drawing.
        head_end = svg.index(">")
        head = re.sub(r'\s(class|style|aria-label|aria-hidden)="[^"]*"', "", svg[:head_end])
        # Unity's importer (PreserveViewport) needs the root's width/height; the mockups often
        # size an <svg> only through CSS, so give it its viewBox size.
        vbm = re.search(r'viewBox="([^"]+)"', head)
        if vbm and not re.search(r'\swidth="', head):
            vw, vh = vbm.group(1).split()[2:4]
            head = head.replace("<svg", '<svg width="%s" height="%s"' % (vw, vh), 1)
        svg = head + svg[head_end:]
        w = px(n.attrs.get("width")) or px(cs.get("width"))
        h = px(n.attrs.get("height")) or px(cs.get("height"))
        if (w is None or h is None) and "viewBox" in svg:
            vb = re.search(r'viewBox="([^"]+)"', svg).group(1).split()
            w = w or float(vb[2])
            h = h or float(vb[3])
        digest = hashlib.sha1(svg.encode("utf-8")).hexdigest()[:10]
        name = "ico_%s.svg" % digest
        self.icons[name] = svg
        style = self.layout_style(n, parent, False, extra)
        style = [x for x in style if not x.startswith(("width:", "height:"))]
        if cs.get("width") is None or not str(cs.get("width")).endswith("%"):
            style.append("width: %spx" % fmt(w or 24))
            style.append("height: %spx" % fmt(h or 24))
        else:
            style.append("width: %s" % uss_len(cs["width"]))
            style.append("height: %spx" % fmt(h or 24))
        style += self.transform_style(n)
        if cs.get("opacity"):
            style.append("opacity: %s" % cs["opacity"])
        style.append("background-image: url('%s%s')" % (ICON_URL, name))
        style.append("-unity-background-scale-mode: scale-to-fit")
        f = self.filter_style(n, cs.get("filter"))
        if f:
            style.append(f)
        if not texts:
            out.append('%s<ui:VisualElement picking-mode="Ignore" class="fe-icon" style="%s;" />' % (ind, join_style(style)[:-1]))
            return
        out.append('%s<ui:VisualElement picking-mode="Ignore" class="fe-icon" style="%s;">' % (ind, join_style(style)[:-1]))
        vb = re.search(r'viewBox="([^"]+)"', n.svg)
        vbw, vbh = (float(v) for v in vb.group(1).split()[2:4]) if vb else ((w or 24), (h or 24))
        k = (w or vbw) / vbw
        for attrs_text, content in texts:
            a = dict(re.findall(r'([\w-]+)="([^"]*)"', attrs_text))
            fs = float(a.get("font-size", "16")) * k
            x, y = float(a.get("x", "0")) * k, float(a.get("y", "0")) * k
            anchor = a.get("text-anchor", "start")
            fam = family_of(a.get("font-family", "Barlow"))
            wt = int(a.get("font-weight", "400")) if a.get("font-weight", "400").isdigit() else 700
            italic = a.get("font-style") == "italic"
            box_w = fs * 4
            left = {"middle": x - box_w / 2, "end": x - box_w}.get(anchor, x)
            align = {"middle": "center", "end": "right"}.get(anchor, "left")
            # y is the baseline; TextCore's content area starts one (TextCore) ascent above it.
            a_tc, d_tc = FONTS[fam]["tc"]
            st = ["position: absolute", "left: %spx" % fmt(left), "top: %spx" % fmt(y - fs * a_tc),
                  "width: %spx" % fmt(box_w), "height: %spx" % fmt(fs * (a_tc + d_tc)),
                  "-unity-font-definition: url('%s%s.ttf')" % (FONT_URL, self.font_file(fam, wt, italic)[0]),
                  "font-size: %spx" % fmt(fs), "color: %s" % uss_color(a.get("fill", "#fff")),
                  "-unity-text-align: upper-%s" % align, "white-space: nowrap"]
            out.append('%s  <ui:Label picking-mode="Ignore" text="%s" style="%s" />' % (ind, attr(content.strip()), join_style(st)))
        out.append("%s</ui:VisualElement>" % ind)

    def emit_input(self, n, out, ind, parent, extra):
        t = n.attrs.get("type", "text")
        style = self.layout_style(n, parent, False, extra)
        cls = n.classes
        if t == "checkbox":
            on = "true" if "checked" in n.attrs else "false"
            self.btn += 1
            # The knob is a ::after the parser drops; each page's size lives in Kit.uss.
            variant = ' class="kit-switch--%s"' % self.prefix.lower() if self.prefix else ""
            out.append('%s<fe:KitSwitch name="switch-%d"%s value="%s" style="%s;" />'
                       % (ind, self.btn, variant, on, join_style(style + ["flex-shrink: 0"])[:-1]))
        elif t == "range":
            m = re.search(r"--v\s*:\s*([\d.]+)%", n.attrs.get("style", ""))
            v = float(m.group(1)) / 100 if m else float(n.attrs.get("value", "50")) / 100
            self.btn += 1
            if not any(x.startswith("width:") for x in style):
                style.append("width: 100%")
            if not any(x.startswith("height:") for x in style):
                style.append("height: %s" % uss_len(n.cs.get("height", "6px")))
            out.append('%s<fe:KitSlider name="slider-%d" value="%s" style="%s;" />' % (ind, self.btn, fmt(v), join_style(style)[:-1]))
        else:
            self.warn("input type=%s dropped" % t)


# ════════════════════════════════════════════════════════════════════════════
# Value helpers
# ════════════════════════════════════════════════════════════════════════════
COLOR_RE = re.compile(r"^(#[0-9a-fA-F]{3,8}|rgba?\([^)]*\)|transparent|white|black)$")


def is_color(v):
    return bool(COLOR_RE.match(v.strip()))


def uss_color(v):
    v = v.strip()
    if v == "transparent":
        return "rgba(0, 0, 0, 0)"
    m = re.match(r"^#([0-9a-fA-F]{8})$", v)
    if m:
        h = m.group(1)
        return "rgba(%d, %d, %d, %s)" % (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), fmt(int(h[6:8], 16) / 255))
    m = re.match(r"^rgba?\(([^)]*)\)$", v)
    if m:
        p = [x for x in re.split(r"[\s,/]+", m.group(1)) if x]
        if len(p) == 3:
            return "rgb(%s, %s, %s)" % tuple(p)
        a = p[3]
        if a.startswith("."):
            a = "0" + a
        return "rgba(%s, %s, %s, %s)" % (p[0], p[1], p[2], a)
    return v


def hex_color(v):
    v = v.strip()
    if v.startswith("#"):
        h = v[1:]
        if len(h) in (3, 4):
            h = "".join(ch * 2 for ch in h)
        return "#" + h.upper()
    m = re.match(r"^rgba?\(([^)]*)\)$", v)
    if m:
        p = [x for x in re.split(r"[\s,/]+", m.group(1)) if x]
        r, g, b = (int(float(x)) for x in p[:3])
        a = float(p[3]) if len(p) > 3 else 1.0
        return "#%02X%02X%02X%02X" % (r, g, b, int(round(a * 255)))
    return "#FFFFFF"


def uss_len(v):
    v = str(v).strip()
    if v in ("0", "auto"):
        return v
    if re.match(r"^-?[\d.]+$", v):
        return v + "px"
    if v.startswith("."):
        return "0" + v
    if v.startswith("-."):
        return "-0" + v[1:]
    return v


def bg_pos(v, axis):
    v = v.strip()
    kw = {"left": "0%", "top": "0%", "center": "50%", "right": "100%", "bottom": "100%"}
    v = kw.get(v, v)
    if v == "0":
        v = "0%"
    return v


def align(v):
    return {"start": "flex-start", "end": "flex-end", "baseline": "flex-end", "normal": "stretch",
            "flex-start": "flex-start", "flex-end": "flex-end", "center": "center", "stretch": "stretch"}.get(v, v)


def justify(v):
    return {"start": "flex-start", "end": "flex-end", "space-evenly": "space-around"}.get(v, v)


def css_metrics(cs):
    """(ascent, descent) in em of the font the browser laid this style out with (FONTS)."""
    return FONTS[cs.get("font-family", "body")]["css"]


def line_px(cs):
    fs = float(cs["font-size"][:-2])
    lh = cs.get("line-height", "normal")
    if lh == "normal":
        return fs * sum(css_metrics(cs))
    if lh.endswith("px"):
        return float(lh[:-2])
    return fs * float(lh)


def strut_box(block_cs, span_cs):
    """(height, glyph-top) of one line: the block's strut and one inline run on a shared
    baseline. Heights in px; glyph-top is where the run's content area starts."""
    S, Ls = float(block_cs["font-size"][:-2]), line_px(block_cs)
    f, l = float(span_cs["font-size"][:-2]), line_px(span_cs)
    (A, D), (a, d) = css_metrics(block_cs), css_metrics(span_cs)
    base = (Ls - S * (A + D)) / 2 + S * A                          # strut baseline from its top
    hl = (l - f * (a + d)) / 2                                     # span half-leading
    top = min(-base, -(hl + f * a))
    bottom = max(Ls - base, hl + f * d)
    return bottom - top, (-f * a) - top


def frac(v):
    """'30%' -> 0.3; anything else -> 0.5 (centre)."""
    v = v.strip()
    return float(v[:-1]) / 100.0 if v.endswith("%") else 0.5


def linear_alpha(args, h):
    """Alpha of a vertical CSS mask gradient (default, `180deg` or `to bottom`) as f(y px);
    None for any other direction."""
    parts = [p.strip() for p in split_top(args, ",")]
    if parts and (parts[0].endswith("deg") or parts[0].startswith("to ")):
        if parts[0] not in ("180deg", "to bottom"):
            return None
        parts = parts[1:]
    stops = []
    for p in parts:
        t = split_ws(p)
        a = 1.0
        mm = re.match(r"rgba\(([^)]*)\)", t[0])
        if mm:
            a = float(re.split(r"[\s,/]+", mm.group(1).strip())[3])
        elif t[0] == "transparent":
            a = 0.0
        positions = [float(x[:-1]) / 100.0 if x.endswith("%") else float(x.replace("px", "")) / h for x in t[1:]]
        for q in positions or [None]:
            stops.append([q, a])
    if not stops:
        return None
    if stops[0][0] is None:
        stops[0][0] = 0.0
    if stops[-1][0] is None:
        stops[-1][0] = 1.0
    for i in range(1, len(stops) - 1):
        if stops[i][0] is None:
            stops[i][0] = (stops[i - 1][0] + stops[i + 1][0]) / 2.0

    def f(y):
        t = y / h
        if t <= stops[0][0]:
            return stops[0][1]
        for i in range(1, len(stops)):
            if t <= stops[i][0]:
                span = stops[i][0] - stops[i - 1][0]
                k = (t - stops[i - 1][0]) / span if span > 0 else 1.0
                return stops[i - 1][1] + (stops[i][1] - stops[i - 1][1]) * k
        return stops[-1][1]
    return f


def weight(cs):
    w = cs.get("font-weight", "400")
    if w == "bold":
        return 700
    if w == "normal":
        return 400
    try:
        return int(w)
    except ValueError:
        return 400


def collapse(text, cs):
    t = re.sub(r"\s+", " ", text)
    tt = cs.get("text-transform", "none")
    if tt == "uppercase":
        t = t.upper()
    elif tt == "lowercase":
        t = t.lower()
    return t


def attr(s):
    return html.escape(s, quote=True).replace("\n", "&#10;")


def join_style(items):
    """'k: v' items -> one inline style string; a later duplicate key wins."""
    seen = {}
    for it in items:
        k = it.split(":", 1)[0].strip()
        seen[k] = it
    return "; ".join(seen.values()) + ";"


# ════════════════════════════════════════════════════════════════════════════
# Fonts.uss and the driver
# ════════════════════════════════════════════════════════════════════════════
def write_if_changed(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as f:
            if f.read() == text:
                return False
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return True


def convert_screen(version, name, kit_rules, icons, kit_text=""):
    page = PAGES[version]
    path = os.path.join(MOCKUPS, version, name + ".dc.html")
    with open(path, encoding="utf-8") as f:
        text = f.read()
    p = Parser()
    p.feed(text)
    scr = find_scr(p.root)
    if scr is None:
        raise SystemExit("%s/%s: no .scr root" % (version, name))
    rules = kit_rules + parse_css(page_styles(text), 100000)
    base = dict(BASE)
    body = [r for r in rules if r.sel[-1][1]["tag"] == "body"]
    for r in body:
        for k, v in r.decls.items():
            if k == "font-family":
                base[k] = family_of(v)
            elif k == "color":
                base[k] = v
    compute_styles(scr, rules, base, page["prefix"])
    report = []
    css_text = (kit_text or "") + page_styles(text)
    apply_box_sizing(scr, bool(re.search(r"\*\s*\{[^}]*box-sizing\s*:\s*border-box", css_text)), report)
    inherit_clips(scr)
    apply_hooks(name, scr, page["hooks"])
    conv = Converter(name, report, icons, page["prefix"])
    lines = conv.convert(scr)
    header = ('<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:fe="SlapMania.UI.FrontEnd" editor-extension-mode="False">\n'
              '  <!-- GENERATED by Tools/mockup2uxml from Docs/Reference/Mockups/%s/%s.dc.html.\n'
              '       Re-run the tool rather than editing by hand; see M10 §4. -->\n'
              '  <Style src="project://database/Assets/_Project/Prefabs/UI/Styles/Kit.uss" />\n' % (version, name))
    body_text = "\n".join("  " + l for l in lines)
    return header + body_text + "\n</ui:UXML>\n", report


def prune(folder, prefix, suffix, keep):
    """Delete generated `prefix*suffix` files (and their .meta) in folder not in keep."""
    if not os.path.isdir(folder):
        return 0
    n = 0
    for fname in os.listdir(folder):
        if fname.startswith(prefix) and fname.endswith(suffix) and fname not in keep:
            os.remove(os.path.join(folder, fname))
            meta = os.path.join(folder, fname + ".meta")
            if os.path.exists(meta):
                os.remove(meta)
            n += 1
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--page", choices=sorted(PAGES), help="convert one canvas page only")
    ap.add_argument("--out", help="write into this folder instead of Assets/ (dry run)")
    ap.add_argument("screens", nargs="*", help="only these screens (of --page, V2 by default)")
    args = ap.parse_args()
    pages = [args.page] if args.page else (["V2"] if args.screens else list(PAGES))
    jobs = [(v, n) for v in pages for n in (args.screens or PAGES[v]["screens"])]
    full = not args.page and not args.screens
    if args.out:
        global OUT_SCREENS, OUT_ICONS, REPORT
        OUT_SCREENS = os.path.join(args.out, "Screens")
        OUT_ICONS = os.path.join(args.out, "Icons")
        REPORT = os.path.join(args.out, "CONVERSION-REPORT.md")
    if args.list:
        print("\n".join("%s/%s" % j for j in jobs))
        return
    kits = {}
    for v in pages:
        kits[v] = ([], "")
        if PAGES[v]["kit"]:
            with open(os.path.join(MOCKUPS, v, PAGES[v]["kit"]), encoding="utf-8") as f:
                kit_text = f.read()
            kits[v] = (parse_css(kit_text), kit_text)

    def uxml_path(v, name):
        return os.path.join(OUT_SCREENS, PAGES[v]["out"], name + ".uxml")

    icons, reports, changed = {}, {}, []
    for v, name in jobs:
        uxml, report = convert_screen(v, name, kits[v][0], icons, kits[v][1])
        if write_if_changed(uxml_path(v, name), uxml):
            changed.append(name + ".uxml")
        reports[(v, name)] = report
    for fname, svg in icons.items():
        if write_if_changed(os.path.join(OUT_ICONS, fname), svg):
            changed.append(fname)
    if full:
        # A full run owns these folders: drop generated files nothing references any more
        # (an icon whose SVG changed gets a new hash; the old one would linger forever).
        # Pages share icons, so only a run over every page may prune.
        removed = prune(OUT_ICONS, "ico_", ".svg", set(icons))
        used = set()
        for v, name in jobs:
            with open(uxml_path(v, name), encoding="utf-8") as f:
                used.update(re.findall(r"Mockup/Masked/([^')]+)", f.read()))
        removed += prune(OUT_MASKED, "", ".png", used)
        if removed:
            print("removed %d stale generated files" % removed)
        lines = ["# Mockup → UXML conversion report", "",
                 "Generated by `Tools/mockup2uxml`. Each line is a CSS feature the converter could not",
                 "express exactly in UI Toolkit, and what it did instead. Empty sections convert 1:1.", ""]
        for v in pages:
            lines += ["# %s page" % v, ""]
            for name in PAGES[v]["screens"]:
                lines.append("## %s" % (PAGES[v]["prefix"] + name))
                lines.append("")
                rep = reports[(v, name)]
                lines += ["- %s" % r for r in rep] if rep else ["- (exact)"]
                lines.append("")
        write_if_changed(REPORT, "\n".join(lines))
    print("converted %d screens, %d icons; %d files changed" % (len(jobs), len(icons), len(changed)))
    for v, name in jobs:
        if reports[(v, name)]:
            print("  %-16s %d approximations" % (PAGES[v]["prefix"] + name, len(reports[(v, name)])))
            if not full:
                for r in reports[(v, name)]:
                    print("      - " + r)


if __name__ == "__main__":
    main()
