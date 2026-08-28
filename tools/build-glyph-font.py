#!/usr/bin/env python3
"""Build ref/glyphs.fnt + ref/glyphs.png - the module's shipped glyph font.

Blish HUD 1.3.0 ships exactly one text face (Menomonia, 226 codepoints) and no
runtime font baking (ContentsManager.GetBitmapFont throws NotImplementedException),
so every geometric glyph the UI wants has to arrive as a BMFont we author and
package ourselves. See dev/records/2026-08-glyph-font.md for the measurements.

Regenerate with:

    python3 tools/build-glyph-font.py --fetch --out-dir ref

or, against a local checkout of https://github.com/twbs/icons:

    python3 tools/build-glyph-font.py --icons-dir ../icons/icons --out-dir ref

Both forms are byte-reproducible: the same Bootstrap Icons source SVGs and the
same GLYPHS table below always produce the same .fnt and .png.

Source artwork is Bootstrap Icons (https://github.com/twbs/icons), MIT, taken
from icons/*.svg rather than from the built webfont so the provenance of the
rasterized artwork stays a plain MIT copy question. Attribution rides in
ref/THIRD-PARTY-NOTICES.txt, which the .bhm packaging target already includes.

Python 3 standard library only - no Pillow, no cairo, no fontTools. The SVG
path rasterizer below is deliberately small: it covers exactly the subset of
SVG that Bootstrap's flattened fill-only icons use.
"""

import argparse
import math
import os
import re
import struct
import sys
import zlib

BOOTSTRAP_ICONS_RAW = "https://raw.githubusercontent.com/twbs/icons/main/icons/{0}.svg"

# The .fnt's own vertical frame. Nothing at runtime reads these as absolute
# pixels: GlyphFontDescriptor aligns BASE against the baseline of whichever
# Menomonia face the glyphs are merged into, so only the DIFFERENCE between a
# glyph's yOffset and BASE carries meaning. They are declared, not measured.
LINE_HEIGHT = 24
BASE = 18

# One row per glyph. Every number here is an optical decision, so each is
# stated in the terms the eye judges rather than in font-file terms:
#
#   ink_h      height of the rendered ink in pixels. The SVG is scaled
#              uniformly so its ink bounding box comes out this tall, which is
#              what keeps a pair like the two carets identically sized.
#   rise       height of the ink's CENTRE above the baseline. Menomonia Bold 20
#              - the face these glyphs are merged into for column headers -
#              puts cap ink between 4 and 21 px down the line box, so a rise of
#              8 or 9 lands a glyph on the cap centre (Services/TypeRampMetrics).
#   advance    pen movement. The composite font inherits Blish's
#              LetterSpacing = -1, so an advance of N leaves N-1 px of pen
#              travel; the values below already pay for that.
#
# Codepoints are BMP private use starting at U+E100. U+E000 is skipped because
# Menomonia already defines it, and the merge must not shadow a real glyph.
#
# THIS TABLE IS DELIBERATELY SHORT, on two separate grounds.
#
# A codepoint nothing draws is a vocabulary that rots, so only glyphs with a
# live call site ship; Services/UiGlyphs and the "UI glyph escapes" CI step
# assert this table and the module's constants agree in both directions.
#
# And only SOLID FILL artwork survives the trip. Bootstrap draws on a 16px
# grid with roughly 1-unit strokes, so a stroked icon rendered to the 6-10px
# of ink this UI has room for lands at well under one pixel of coverage: x-lg
# at 8px measures a 0.66px diagonal, which is a paler mark than Menomonia's
# own solid U+00D7, and check-lg, the chevrons, plus-lg and dash-lg all fail
# the same way. The two carets below are flattened fills and stay at full
# coverage all the way down - see the --preview output.
#
# The wider affordance shortlist this font was scoped against is recorded in
# dev/records/2026-08-glyph-font.md, along with which entries the stroke-weight
# measurement disqualified. Adding a glyph is a row here plus a constant in
# UiGlyphs; do that when a seat exists and the artwork survives --preview.
GLYPHS = [
    # codepoint, bootstrap icon, ink_h, rise, advance, role
    (0xE100, "caret-up-fill",         6,    8,       9, "sort ascending"),
    (0xE101, "caret-down-fill",       6,    8,       9, "sort descending"),
]

ATLAS_PADDING = 1
SUPERSAMPLE = 16
CURVE_STEPS = 24


# ---------------------------------------------------------------------------
# SVG path parsing and flattening
# ---------------------------------------------------------------------------

_NUMBER = re.compile(r"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?")
_COMMAND = re.compile(r"([MmZzLlHhVvCcSsQqTtAa])")


def _tokenize(d):
    """Split a path 'd' attribute into (command, [numbers]) pairs."""
    parts = [p for p in _COMMAND.split(d) if p.strip()]
    out = []
    i = 0
    while i < len(parts):
        cmd = parts[i]
        if not _COMMAND.fullmatch(cmd):
            raise ValueError("path does not start with a command: " + d[:40])
        args = []
        if i + 1 < len(parts) and not _COMMAND.fullmatch(parts[i + 1]):
            args = [float(n) for n in _NUMBER.findall(parts[i + 1])]
            i += 1
        out.append((cmd, args))
        i += 1
    return out


def _cubic(p0, p1, p2, p3, steps):
    pts = []
    for s in range(1, steps + 1):
        t = s / steps
        u = 1.0 - t
        x = u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0]
        y = u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]
        pts.append((x, y))
    return pts


def _quadratic(p0, p1, p2, steps):
    pts = []
    for s in range(1, steps + 1):
        t = s / steps
        u = 1.0 - t
        x = u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0]
        y = u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]
        pts.append((x, y))
    return pts


def _arc(p0, rx, ry, rotation, large_arc, sweep, p1, steps):
    """Endpoint-parameterised elliptical arc, per SVG 1.1 F.6.5."""
    if p0 == p1:
        return []
    if rx == 0 or ry == 0:
        return [p1]

    rx, ry = abs(rx), abs(ry)
    phi = math.radians(rotation)
    cos_p, sin_p = math.cos(phi), math.sin(phi)

    dx2 = (p0[0] - p1[0]) / 2.0
    dy2 = (p0[1] - p1[1]) / 2.0
    x1p = cos_p * dx2 + sin_p * dy2
    y1p = -sin_p * dx2 + cos_p * dy2

    # F.6.6: scale the radii up if they cannot span the chord.
    lam = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry)
    if lam > 1:
        scale = math.sqrt(lam)
        rx *= scale
        ry *= scale

    num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p
    den = rx * rx * y1p * y1p + ry * ry * x1p * x1p
    coef = math.sqrt(max(0.0, num / den))
    if large_arc == sweep:
        coef = -coef
    cxp = coef * rx * y1p / ry
    cyp = -coef * ry * x1p / rx

    cx = cos_p * cxp - sin_p * cyp + (p0[0] + p1[0]) / 2.0
    cy = sin_p * cxp + cos_p * cyp + (p0[1] + p1[1]) / 2.0

    def angle(ux, uy, vx, vy):
        dot = ux * vx + uy * vy
        mag = math.hypot(ux, uy) * math.hypot(vx, vy)
        if mag == 0:
            return 0.0
        a = math.acos(max(-1.0, min(1.0, dot / mag)))
        return -a if ux * vy - uy * vx < 0 else a

    theta1 = angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry)
    delta = angle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry)
    if not sweep and delta > 0:
        delta -= 2 * math.pi
    elif sweep and delta < 0:
        delta += 2 * math.pi

    segments = max(steps, int(steps * abs(delta) / math.pi))
    pts = []
    for s in range(1, segments + 1):
        t = theta1 + delta * (s / segments)
        x = cos_p * rx * math.cos(t) - sin_p * ry * math.sin(t) + cx
        y = sin_p * rx * math.cos(t) + cos_p * ry * math.sin(t) + cy
        pts.append((x, y))
    return pts


def flatten_path(d):
    """Flatten a path 'd' attribute into a list of closed polygons."""
    polys = []
    current = []
    pos = (0.0, 0.0)
    start = (0.0, 0.0)
    prev_cubic_ctrl = None
    prev_quad_ctrl = None

    def close():
        if len(current) > 2:
            polys.append(list(current))

    for cmd, args in _tokenize(d):
        rel = cmd.islower()
        up = cmd.upper()

        if up == "Z":
            close()
            current = []
            pos = start
            prev_cubic_ctrl = prev_quad_ctrl = None
            continue

        arity = {"M": 2, "L": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "T": 2, "A": 7}[up]
        if arity == 0 or len(args) % arity != 0:
            raise ValueError("bad argument count for %s: %r" % (cmd, args))

        for i in range(0, len(args), arity):
            chunk = args[i:i + arity]

            if up == "M":
                close()
                current = []
                pos = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                start = pos
                current.append(pos)
                # A second coordinate pair after M is an implicit L.
                up = "L"
                prev_cubic_ctrl = prev_quad_ctrl = None
                continue

            if up == "L":
                pos = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                current.append(pos)
                prev_cubic_ctrl = prev_quad_ctrl = None
            elif up == "H":
                pos = (chunk[0] + pos[0], pos[1]) if rel else (chunk[0], pos[1])
                current.append(pos)
                prev_cubic_ctrl = prev_quad_ctrl = None
            elif up == "V":
                pos = (pos[0], chunk[0] + pos[1]) if rel else (pos[0], chunk[0])
                current.append(pos)
                prev_cubic_ctrl = prev_quad_ctrl = None
            elif up in ("C", "S"):
                if up == "C":
                    c1 = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                    c2 = (chunk[2] + pos[0], chunk[3] + pos[1]) if rel else (chunk[2], chunk[3])
                    end = (chunk[4] + pos[0], chunk[5] + pos[1]) if rel else (chunk[4], chunk[5])
                else:
                    c1 = pos if prev_cubic_ctrl is None else (
                        2 * pos[0] - prev_cubic_ctrl[0], 2 * pos[1] - prev_cubic_ctrl[1])
                    c2 = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                    end = (chunk[2] + pos[0], chunk[3] + pos[1]) if rel else (chunk[2], chunk[3])
                current.extend(_cubic(pos, c1, c2, end, CURVE_STEPS))
                pos = end
                prev_cubic_ctrl = c2
                prev_quad_ctrl = None
            elif up in ("Q", "T"):
                if up == "Q":
                    c1 = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                    end = (chunk[2] + pos[0], chunk[3] + pos[1]) if rel else (chunk[2], chunk[3])
                else:
                    c1 = pos if prev_quad_ctrl is None else (
                        2 * pos[0] - prev_quad_ctrl[0], 2 * pos[1] - prev_quad_ctrl[1])
                    end = (chunk[0] + pos[0], chunk[1] + pos[1]) if rel else (chunk[0], chunk[1])
                current.extend(_quadratic(pos, c1, end, CURVE_STEPS))
                pos = end
                prev_quad_ctrl = c1
                prev_cubic_ctrl = None
            elif up == "A":
                end = (chunk[5] + pos[0], chunk[6] + pos[1]) if rel else (chunk[5], chunk[6])
                current.extend(_arc(pos, chunk[0], chunk[1], chunk[2],
                                    bool(chunk[3]), bool(chunk[4]), end, CURVE_STEPS))
                pos = end
                prev_cubic_ctrl = prev_quad_ctrl = None

    close()
    return polys


def circle_polygon(cx, cy, r, steps=192):
    return [[(cx + r * math.cos(2 * math.pi * i / steps),
              cy + r * math.sin(2 * math.pi * i / steps)) for i in range(steps)]]


def parse_svg(text):
    """Return (polygons, even_odd) for a Bootstrap Icons SVG."""
    view = re.search(r'viewBox="([^"]+)"', text)
    if not view:
        raise ValueError("no viewBox")
    vb = [float(n) for n in _NUMBER.findall(view.group(1))]
    if vb[:2] != [0.0, 0.0]:
        raise ValueError("only origin-anchored viewBoxes are supported: %r" % (vb,))

    polys = []
    even_odd = False
    for match in re.finditer(r"<path\b([^>]*)>", text):
        attrs = match.group(1)
        rule = re.search(r'fill-rule="([^"]+)"', attrs)
        if rule and rule.group(1) == "evenodd":
            even_odd = True
        d = re.search(r'\sd="([^"]+)"', attrs)
        if d:
            polys.extend(flatten_path(d.group(1)))

    for match in re.finditer(r"<circle\b([^>]*)>", text):
        attrs = match.group(1)

        def attr(name):
            m = re.search(r'\s%s="([^"]+)"' % name, attrs)
            return float(m.group(1)) if m else 0.0

        polys.extend(circle_polygon(attr("cx"), attr("cy"), attr("r")))

    if not polys:
        raise ValueError("no fillable geometry found")
    return polys, even_odd, (vb[2], vb[3])


# ---------------------------------------------------------------------------
# Rasterization
# ---------------------------------------------------------------------------

def bounds(polys):
    xs = [p[0] for poly in polys for p in poly]
    ys = [p[1] for poly in polys for p in poly]
    return min(xs), min(ys), max(xs), max(ys)


def rasterize(polys, width, height, even_odd):
    """Scanline-fill polygons into a width x height coverage map (0..255).

    Samples SUPERSAMPLE x SUPERSAMPLE points per output pixel and averages,
    which is what gives these glyphs an antialiased edge that sits beside
    Menomonia's own antialiased text without looking cut out.
    """
    sw, sh = width * SUPERSAMPLE, height * SUPERSAMPLE
    edges = []
    for poly in polys:
        for i in range(len(poly)):
            x0, y0 = poly[i]
            x1, y1 = poly[(i + 1) % len(poly)]
            if y0 != y1:
                edges.append((x0, y0, x1, y1))

    counts = [0] * (width * height)
    for sy in range(sh):
        y = (sy + 0.5) / SUPERSAMPLE
        crossings = []
        for x0, y0, x1, y1 in edges:
            if (y0 <= y < y1) or (y1 <= y < y0):
                t = (y - y0) / (y1 - y0)
                crossings.append((x0 + t * (x1 - x0), 1 if y1 > y0 else -1))
        if not crossings:
            continue
        crossings.sort()

        spans = []
        if even_odd:
            for i in range(0, len(crossings) - 1, 2):
                spans.append((crossings[i][0], crossings[i + 1][0]))
        else:
            winding = 0
            span_start = None
            for x, direction in crossings:
                was_inside = winding != 0
                winding += direction
                if not was_inside and winding != 0:
                    span_start = x
                elif was_inside and winding == 0 and span_start is not None:
                    spans.append((span_start, x))
                    span_start = None

        row = (sy // SUPERSAMPLE) * width
        for xa, xb in spans:
            sxa = max(0, int(math.ceil(xa * SUPERSAMPLE - 0.5)))
            sxb = min(sw, int(math.ceil(xb * SUPERSAMPLE - 0.5)))
            for sx in range(sxa, sxb):
                counts[row + sx // SUPERSAMPLE] += 1

    per_pixel = SUPERSAMPLE * SUPERSAMPLE
    return [min(255, round(c * 255 / per_pixel)) for c in counts]


def render_glyph(svg_text, ink_height):
    """Rasterize one icon to exactly ink_height pixels of ink, uniformly scaled."""
    polys, even_odd, _ = parse_svg(svg_text)
    minx, miny, maxx, maxy = bounds(polys)
    scale = ink_height / (maxy - miny)
    width = max(1, int(round((maxx - minx) * scale)))
    height = max(1, int(round(ink_height)))
    placed = [[((x - minx) * scale, (y - miny) * scale) for x, y in poly] for poly in polys]
    return width, height, rasterize(placed, width, height, even_odd)


# ---------------------------------------------------------------------------
# Atlas, PNG and .fnt emission
# ---------------------------------------------------------------------------

def write_png(path, width, height, pixels):
    """8-bit RGBA PNG, white ink with coverage in the alpha channel, so the
    atlas takes whatever colour the caller draws it in.

    RGBA rather than the smaller greyscale+alpha because Blish loads module
    textures through TextureUtil.FromStreamPremultiplied, which calls
    Texture2D.FromStream and then GetData<Color> - a path that wants a
    straightforwardly 32-bit source. The whole page is 141 bytes either way.
    """
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter type 0 (None) - keeps the writer trivial
        for a in pixels[y * width:(y + 1) * width]:
            raw.append(255)
            raw.append(255)
            raw.append(255)
            raw.append(a)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as handle:
        handle.write(png)


def load_svg(icon, icons_dir, fetch):
    if icons_dir:
        with open(os.path.join(icons_dir, icon + ".svg"), "r", encoding="utf-8") as handle:
            return handle.read()
    if not fetch:
        raise SystemExit("pass --icons-dir or --fetch")
    from urllib.request import urlopen
    with urlopen(BOOTSTRAP_ICONS_RAW.format(icon), timeout=30) as response:
        return response.read().decode("utf-8")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--icons-dir", help="path to a twbs/icons checkout's icons/ folder")
    parser.add_argument("--fetch", action="store_true", help="download the SVGs instead")
    parser.add_argument("--out-dir", default="ref")
    parser.add_argument("--preview", action="store_true", help="print each glyph as ASCII art")
    args = parser.parse_args(argv)

    rendered = []
    for codepoint, icon, ink_h, rise, advance, role in GLYPHS:
        width, height, coverage = render_glyph(load_svg(icon, args.icons_dir, args.fetch), ink_h)
        rendered.append({
            "codepoint": codepoint,
            "icon": icon,
            "role": role,
            "w": width,
            "h": height,
            "coverage": coverage,
            "xoffset": 0,
            "yoffset": BASE - rise - height // 2,
            "xadvance": advance,
        })
        if args.preview:
            print("U+%04X %s (%s) %dx%d" % (codepoint, icon, role, width, height))
            ramp = " .:-=+*#%@"
            for y in range(height):
                print("  " + "".join(
                    ramp[min(len(ramp) - 1, coverage[y * width + x] * len(ramp) // 256)]
                    for x in range(width)))

    # One row, left to right: thirteen glyphs under 12px tall pack into a strip
    # narrower than a single 128px page, and a strip keeps the .fnt's x/y
    # trivially eyeballable against the PNG during review.
    page_h = max(g["h"] for g in rendered) + 2 * ATLAS_PADDING
    page_w = sum(g["w"] + ATLAS_PADDING for g in rendered) + ATLAS_PADDING
    page = [0] * (page_w * page_h)

    pen = ATLAS_PADDING
    for glyph in rendered:
        glyph["x"] = pen
        glyph["y"] = ATLAS_PADDING
        for y in range(glyph["h"]):
            dest = (ATLAS_PADDING + y) * page_w + pen
            page[dest:dest + glyph["w"]] = glyph["coverage"][y * glyph["w"]:(y + 1) * glyph["w"]]
        pen += glyph["w"] + ATLAS_PADDING

    os.makedirs(args.out_dir, exist_ok=True)
    png_name = "glyphs_0.png"
    write_png(os.path.join(args.out_dir, png_name), page_w, page_h, page)

    # BMFont text format. Strictly key=value lines: the module's parser and
    # every other BMFont reader is line-oriented, and a stray comment line
    # breaks them - so the provenance note lives in THIRD-PARTY-NOTICES.txt,
    # not here.
    lines = [
        'info face="GwchGlyphs" size=%d bold=0 italic=0 charset="" unicode=1 '
        'stretchH=100 smooth=1 aa=1 padding=0,0,0,0 spacing=0,0 outline=0' % LINE_HEIGHT,
        "common lineHeight=%d base=%d scaleW=%d scaleH=%d pages=1 packed=0 "
        "alphaChnl=1 redChnl=0 greenChnl=0 blueChnl=0" % (LINE_HEIGHT, BASE, page_w, page_h),
        'page id=0 file="%s"' % png_name,
        "chars count=%d" % len(rendered),
    ]
    for glyph in sorted(rendered, key=lambda g: g["codepoint"]):
        lines.append(
            "char id=%d x=%d y=%d width=%d height=%d xoffset=%d yoffset=%d "
            "xadvance=%d page=0 chnl=15"
            % (glyph["codepoint"], glyph["x"], glyph["y"], glyph["w"], glyph["h"],
               glyph["xoffset"], glyph["yoffset"], glyph["xadvance"]))
    lines.append("kernings count=0")

    with open(os.path.join(args.out_dir, "glyphs.fnt"), "w", encoding="ascii", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")

    print("wrote %s/glyphs.fnt and %s/%s (%dx%d, %d glyphs)"
          % (args.out_dir, args.out_dir, png_name, page_w, page_h, len(rendered)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
