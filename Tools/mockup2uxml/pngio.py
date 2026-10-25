"""Minimal PNG read/write (8-bit, non-interlaced) on zlib - stdlib only (ADR-0007)."""
import struct
import zlib


def read_rgba(path):
    """(width, height, rows) with each row a bytearray of RGBA."""
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("%s is not a PNG" % path)
    pos, idat = 8, b""
    w = h = ctype = None
    while pos < len(data):
        ln, = struct.unpack(">I", data[pos:pos + 4])
        tag, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if tag == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or interlace != 0:
                raise ValueError("%s: only 8-bit non-interlaced PNG is supported" % path)
        elif tag == b"IDAT":
            idat += body
        elif tag == b"IEND":
            break
    bpp = {0: 1, 2: 3, 4: 2, 6: 4}[ctype]
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev, i = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        if f:
            for x in range(stride):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                if f == 1:
                    line[x] = (line[x] + a) & 255
                elif f == 2:
                    line[x] = (line[x] + b) & 255
                elif f == 3:
                    line[x] = (line[x] + ((a + b) >> 1)) & 255
                elif f == 4:
                    p = a + b - c
                    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                    line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        if bpp == 4:
            rows.append(line)
        elif bpp == 3:
            out = bytearray(w * 4)
            out[0::4], out[1::4], out[2::4] = line[0::3], line[1::3], line[2::3]
            out[3::4] = b"\xff" * w
            rows.append(out)
        elif bpp == 1:
            out = bytearray(w * 4)
            out[0::4] = out[1::4] = out[2::4] = line
            out[3::4] = b"\xff" * w
            rows.append(out)
        else:
            out = bytearray(w * 4)
            out[0::4] = out[1::4] = out[2::4] = line[0::2]
            out[3::4] = line[1::2]
            rows.append(out)
    return w, h, rows


def write(path, w, h, rows, alpha):
    """rows: bytes per row, RGB (alpha=False) or RGBA (alpha=True)."""
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6 if alpha else 2, 0, 0, 0)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
