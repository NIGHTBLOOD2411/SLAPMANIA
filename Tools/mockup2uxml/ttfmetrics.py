"""Horizontal advances from a TrueType file (cmap format 4/12 + hmtx) - stdlib only (ADR-0007).

The converter uses it to predict where CSS wraps a paragraph: UI Toolkit labels do not wrap
unless told to, and the converter must size a wrapped label's box itself.
"""
import struct


class Face:
    def __init__(self, path):
        d = open(path, "rb").read()
        n = struct.unpack(">H", d[4:6])[0]
        t = {}
        for i in range(n):
            tag, _, off, ln = struct.unpack(">4sIII", d[12 + 16 * i:28 + 16 * i])
            t[tag.decode("latin-1")] = (off, ln)
        self.upm = struct.unpack(">H", d[t["head"][0] + 18:t["head"][0] + 20])[0]
        n_hm = struct.unpack(">H", d[t["hhea"][0] + 34:t["hhea"][0] + 36])[0]
        o = t["hmtx"][0]
        self.adv = [struct.unpack(">H", d[o + 4 * i:o + 4 * i + 2])[0] for i in range(n_hm)]
        self.cmap = self._cmap(d, t["cmap"][0])

    @staticmethod
    def _cmap(d, base):
        n = struct.unpack(">H", d[base + 2:base + 4])[0]
        subs = []
        for i in range(n):
            pid, eid, off = struct.unpack(">HHI", d[base + 4 + 8 * i:base + 12 + 8 * i])
            fmt = struct.unpack(">H", d[base + off:base + off + 2])[0]
            subs.append((fmt, pid, eid, base + off))
        m = {}
        for fmt, pid, eid, o in sorted(subs, key=lambda s: s[0] != 12):
            if fmt == 12:
                groups = struct.unpack(">I", d[o + 12:o + 16])[0]
                for g in range(groups):
                    start, end, gid = struct.unpack(">III", d[o + 16 + 12 * g:o + 28 + 12 * g])
                    for c in range(start, end + 1):
                        m.setdefault(c, gid + c - start)
                return m
            if fmt == 4 and pid in (0, 3):
                seg2 = struct.unpack(">H", d[o + 6:o + 8])[0]
                ends = struct.unpack(">%dH" % (seg2 // 2), d[o + 14:o + 14 + seg2])
                starts = struct.unpack(">%dH" % (seg2 // 2), d[o + 16 + seg2:o + 16 + 2 * seg2])
                deltas = struct.unpack(">%dh" % (seg2 // 2), d[o + 16 + 2 * seg2:o + 16 + 3 * seg2])
                ro = o + 16 + 3 * seg2
                ranges = struct.unpack(">%dH" % (seg2 // 2), d[ro:ro + seg2])
                for s in range(seg2 // 2):
                    for c in range(starts[s], ends[s] + 1):
                        if c == 0xFFFF:
                            continue
                        if ranges[s] == 0:
                            gid = (c + deltas[s]) & 0xFFFF
                        else:
                            p = ro + 2 * s + ranges[s] + 2 * (c - starts[s])
                            gid = struct.unpack(">H", d[p:p + 2])[0]
                            if gid:
                                gid = (gid + deltas[s]) & 0xFFFF
                        m.setdefault(c, gid)
                return m
        return m

    def width(self, text, size, letter_spacing=0.0):
        """Advance width in px of `text` at `size` px (no kerning), with CSS letter-spacing
        after every character."""
        units = 0
        for ch in text:
            gid = self.cmap.get(ord(ch), 0)
            units += self.adv[min(gid, len(self.adv) - 1)]
        return units * size / self.upm + letter_spacing * len(text)
