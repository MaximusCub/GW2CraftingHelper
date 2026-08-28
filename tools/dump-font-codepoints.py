#!/usr/bin/env python3
"""Emit the codepoint set the shipped Blish HUD text face can actually draw.

Blish HUD 1.3.0 exposes exactly one text face to modules, Menomonia, in 28
size/style variants under Content/fonts/menomonia. Every variant is a
pre-baked MonoGame.Extended BitmapFont in an uncompressed XNB, and all 28
carry the same glyph table, so the intersection printed here is equally
their union. A codepoint outside it renders as nothing AND advances zero
pixels, which is why the omission is invisible to a screenshot and to a
layout assertion alike.

Usage:
    python3 tools/dump-font-codepoints.py [FONT_DIR] > docs/font-codepoints.txt

FONT_DIR defaults to the standard Windows install path seen from WSL. Pass
the directory again after a Blish upgrade and commit the diff.

XNB layout consumed below (Texture2D/BitmapFontReader, content format 5):
    header 'XNBw', flags, int32 file length      10 bytes
    7-bit-encoded reader count, then per reader a 7-bit-length-prefixed
    type name and an int32 version
    7-bit shared-resource count, 7-bit type id
    int32 page count, then one 7-bit-length-prefixed page name each
    int32 line height, int32 region count
    per region nine int32s:
        character, page, x, y, width, height, xOffset, yOffset, xAdvance
"""

import glob
import os
import struct
import sys

DEFAULT_FONT_DIR = "/mnt/c/Blish.HUD/Content/fonts/menomonia"


def read_7bit(data, index):
    value = 0
    shift = 0
    while True:
        byte = data[index]
        index += 1
        value |= (byte & 0x7F) << shift
        shift += 7
        if not byte & 0x80:
            return value, index


def read_string(data, index):
    length, index = read_7bit(data, index)
    return data[index:index + length].decode("utf-8"), index + length


def codepoints(path):
    data = open(path, "rb").read()
    if data[:4] != b"XNBw":
        raise ValueError("%s is not a Windows XNB" % path)

    index = 10
    readers, index = read_7bit(data, index)
    for _ in range(readers):
        name, index = read_string(data, index)
        index += 4
        if "BitmapFontReader" not in name:
            # The font folder also holds each face's texture page as its
            # own Texture2D xnb. Not an error, just not a glyph table.
            return None

    _, index = read_7bit(data, index)  # shared-resource count
    _, index = read_7bit(data, index)  # type id of the root object

    pages, = struct.unpack_from("<i", data, index)
    index += 4
    for _ in range(pages):
        _, index = read_string(data, index)

    index += 4  # line height
    count, = struct.unpack_from("<i", data, index)
    index += 4

    found = set()
    for _ in range(count):
        character, = struct.unpack_from("<i", data, index)
        index += 36
        found.add(character)
    return found


def main(argv):
    font_dir = argv[1] if len(argv) > 1 else DEFAULT_FONT_DIR
    shared = None
    parsed = 0
    for face in sorted(glob.glob(os.path.join(font_dir, "*.xnb"))):
        found = codepoints(face)
        if found is None:
            continue
        parsed += 1
        shared = found if shared is None else (shared & found)

    if not parsed:
        sys.stderr.write("no bitmap-font .xnb faces under %s\n" % font_dir)
        return 1

    sys.stdout.write(HEADER % (parsed, font_dir, len(shared)))
    for value in sorted(shared):
        sys.stdout.write("%04X\n" % value)
    return 0


HEADER = """\
# Codepoints the shipped Blish HUD text face can draw.
#
# GENERATED - do not hand-edit. Regenerate after a Blish upgrade with:
#   python3 tools/dump-font-codepoints.py > docs/font-codepoints.txt
#
# Intersected over %d pre-baked Menomonia faces read from
#   %s
# All 28 carry the same glyph table, so this is equally their union.
#
# A codepoint outside this set renders as nothing and advances zero
# pixels: MonoGame's BitmapFont returns a null region, the blit is skipped,
# the advance is skipped with it, and MeasureString under-reports. So the
# defect shows up neither on screen nor in a layout assertion, which is how
# five geometric glyphs reached players (KNOWN-ISSUES #64).
#
# One codepoint per line, uppercase hex, no prefix, blank lines and #
# comments ignored. The "UI glyph escapes exist in the shipped font" step in
# .github/workflows/tests.yml is the half with teeth.
#
# %d codepoints.
"""


if __name__ == "__main__":
    sys.exit(main(sys.argv))
