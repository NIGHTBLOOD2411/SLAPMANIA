#!/usr/bin/env python3
"""compare.py - measure a Unity capture of a front-end screen against its mockup render.

    python Tools/mockup2uxml/compare.py <mockup.png> <unity.png> <out.png>

Both images are 2x renders of the 430 x 932 frame (860 x 1864): the mockup from headless
Edge (`--force-device-scale-factor=2 --window-size=430,932`), the screen from Unity's Game
view at 860 x 1864. Writes a half-size triptych - mockup | Unity | difference heat map - and
prints the mean absolute difference and the share of pixels that differ by more than 48/255.
Standard library only.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pngio  # noqa: E402


def main():
    m_path, u_path, out = sys.argv[1:4]
    mw, mh, m = pngio.read_rgba(m_path)
    uw, uh, u = pngio.read_rgba(u_path)
    w, h = min(mw, uw), min(mh, uh)
    half_w, half_h = w // 2, h // 2
    total = big = 0
    tri = []
    for y in range(half_h):
        mr, ur = m[2 * y], u[2 * y]
        left = bytearray()
        mid = bytearray()
        diff = bytearray()
        for x in range(half_w):
            o = 8 * x
            d = (abs(mr[o] - ur[o]) + abs(mr[o + 1] - ur[o + 1]) + abs(mr[o + 2] - ur[o + 2])) // 3
            total += d
            if d > 48:
                big += 1
            heat = min(255, d * 3)
            left += mr[o:o + 3]
            mid += ur[o:o + 3]
            diff += bytes((heat, heat // 4, 0))
        tri.append(left + mid + diff)
    n = half_w * half_h
    pngio.write(out, half_w * 3, half_h, tri, alpha=False)
    print("%-16s mean_abs_diff=%5.1f/255  differing>48=%5.1f%%" % (os.path.basename(out), total / n, 100.0 * big / n))


if __name__ == "__main__":
    main()
