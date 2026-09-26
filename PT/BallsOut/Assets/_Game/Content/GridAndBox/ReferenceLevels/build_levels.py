"""Author the Sand Blocks reference layouts (levels 1-28) as Unity level assets.

Run from the project Assets directory: python _Game/Content/GridAndBox/ReferenceLevels/build_levels.py
The source videos/images are visual references; the layouts use Balls Out's grid rules.
"""

from collections import Counter
from pathlib import Path
import math
import re
import uuid


ROOT = Path(__file__).resolve().parents[4]
CONTENT = ROOT / "_Game" / "Content" / "GridAndBox"
OUTPUT = Path(__file__).resolve().parent
MATERIALS = ROOT / "_ART" / "GridAndBox" / "Materials"
NAMESPACE = uuid.UUID("76e91398-e9d0-40f6-a5e2-c44503689ca0")
# Balls per box cell edge (LevelDefinition.boxSlotsPerSide): 4 -> 16 per cell, 3 -> 9, 2 -> 4.
# 3 is the ceiling: at 4 the balls in a box end up smaller than the ones in the reservoir.
DEFAULT_SLOTS = 3
# Ball layers stacked in each box (LevelDefinition.fillLayers). A second layer doubles the
# balls without shrinking them; levels opt in with fill_layers=2 when the board stays compact.
DEFAULT_FILL_LAYERS = 1
# The reservoir is sized so balls fill at most this share of it; the rest is room to flow.
# A packed reservoir cannot move at all, which traps colours behind each other.
MAX_RESERVOIR_FILL = 0.75
# Dense fill also packs the seams between cells, so a full box is covered wall to wall
# instead of showing empty gutters between its cells.
DEFAULT_DENSE = True


def guid(name):
    return uuid.uuid5(NAMESPACE, name).hex


def asset_guid(path):
    return re.search(r"^guid: ([0-9a-f]{32})$", path.with_suffix(path.suffix + ".meta").read_text(), re.M).group(1)


def reference(value):
    return f"{{fileID: 11400000, guid: {value}, type: 2}}"


def write_asset(path, body):
    path.write_text(body, encoding="utf-8", newline="\n")
    meta = path.with_suffix(path.suffix + ".meta")
    if not meta.exists():
        meta.write_text(
            f"fileFormatVersion: 2\nguid: {guid(path.name)}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
            encoding="utf-8", newline="\n")


def write_material(name, rgb, box):
    source = MATERIALS / ("Box_Lilac.mat" if box else "Lilac.mat")
    target = OUTPUT / f"{name}.mat"
    text = source.read_text(encoding="utf-8")
    text = re.sub(r"(?m)^  m_Name: .*", f"  m_Name: {name}", text, count=1)
    color = f"{{r: {rgb[0]}, g: {rgb[1]}, b: {rgb[2]}, a: 1}}"
    text = re.sub(r"(?m)(    - _(?:BaseColor|Color): )\{[^}]+\}", lambda match: match.group(1) + color, text)
    if box:
        inner = f"{{r: {rgb[0] * 0.4:.4f}, g: {rgb[1] * 0.4:.4f}, b: {rgb[2] * 0.4:.4f}, a: 1}}"
        text = re.sub(r"(?m)(    - _InnerColor: )\{[^}]+\}", lambda match: match.group(1) + inner, text)
    target.write_text(text, encoding="utf-8", newline="\n")
    meta = target.with_suffix(".mat.meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid(name)}\nNativeFormatImporter:\n"
                        "  externalObjects: {}\n  mainObjectFileID: 2100000\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
                        encoding="utf-8", newline="\n")
    return asset_guid(target)


def new_color(name, rgb):
    ball = write_material(f"Ball_{name}", rgb, False)
    box = write_material(f"Box_{name}", rgb, True)
    path = OUTPUT / f"Color_{name}.asset"
    body = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 55425f6c35c259a499f3b91c4854fbed, type: 3}}
  m_Name: Color_{name}
  m_EditorClassIdentifier: 
  id: {name.lower()}
  displayColor: {{r: {rgb[0]}, g: {rgb[1]}, b: {rgb[2]}, a: 1}}
  ballMaterial: {{fileID: 2100000, guid: {ball}, type: 2}}
  boxMaterial: {{fileID: 2100000, guid: {box}, type: 2}}
  ballPrefabOverride: {{fileID: 0}}
"""
    write_asset(path, body)
    return asset_guid(path)


SHAPE_CELLS = {
    "Q": [(0, 0), (1, 0), (0, 1), (1, 1)],
    "H3": [(0, 0), (1, 0), (2, 0)],
    "H4": [(0, 0), (1, 0), (2, 0), (3, 0)],
    "V4": [(0, 0), (0, 1), (0, 2), (0, 3)],
    "R8": [(x, y) for y in range(2) for x in range(4)],
    "L3A": [(0, 0), (0, 1), (1, 1)],
    "L3B": [(1, 0), (0, 1), (1, 1)],
    "L3C": [(0, 0), (1, 0), (0, 1)],
    "L3D": [(0, 0), (1, 0), (1, 1)],
    "L4A": [(0, 0), (0, 1), (0, 2), (1, 2)],
    "L4B": [(1, 0), (1, 1), (1, 2), (0, 2)],
    "T4": [(1, 0), (0, 1), (1, 1), (2, 1)],
    "R6": [(x, y) for y in range(2) for x in range(3)],
    "X5": [(1, 0), (0, 1), (1, 1), (2, 1), (1, 2)],
    "TD": [(0, 1), (1, 1), (2, 1), (1, 0)],
}

AXIS = {None: 0, "H": 1, "V": 2}


def box(shape, color, x, y, ice=0, fill=0, axis=None, inner=None, lock=None, key=None):
    """axis: None (free), "H" or "V". inner: color a nested box collects before its own.
    lock: padlock name; the box waits until every box with key=<that name> completes."""
    return (shape, color, x, y, ice, fill, axis, inner, lock, key)


LEVELS = [
    dict(n=1, width=4, lower=4, dense=True, fill_layers=2,
         boxes=[box("Q", "B", 0, 0, fill=37), box("Q", "R", 2, 0)],
         dividers=[2], chamber_colors=["B", "R"],
         art=["BBRR", "BBRR", "BBRR"]),
    dict(n=2, width=5, lower=6, fill_layers=2,
         boxes=[box("L3A", "R", 0, 2), box("L3B", "B", 3, 1), box("T4", "G", 1, 0)],
         art=["BBBBB", "GGRRR"]),
    dict(n=3, width=5, lower=6, fill_layers=2,
         boxes=[box("V2", "R", 2, 3), box("H3", "Y", 0, 1),
                box("H3", "G", 0, 0), box("V2", "B", 4, 0)],
         art=["GGGGG", "YYYBB", "BBBRR", "RRRRR"]),
    dict(n=4, width=6, lower=6,
         boxes=[box("Q", "R", 0, 0), box("Q", "R", 0, 2),
                box("Q", "Y", 2, 0), box("Q", "Y", 2, 2),
                box("Q", "B", 4, 0), box("Q", "G", 4, 2)],
         art=["RRRRRR", "BBBBBB", "GGGYYY"]),
    # V3 boxes fill the lower grid's height, so they can never pass each other:
    # chambers follow the box order and the puzzle is shifting them into place.
    dict(n=5, width=7, lower=3, fill_layers=2,
         boxes=[box("V3", "C", 0, 0), box("V3", "Y", 1, 0),
                box("V3", "R", 2, 0), box("V3", "U", 3, 0),
                box("V3", "B", 4, 0), box("V3", "P", 5, 0)],
         upper_outside_columns=[0],
         dividers=[2, 3, 4, 5, 6], chamber_colors=["C", "Y", "R", "U", "B", "P"],
         art=["0CYRUBP"]),
    dict(n=6, width=4, lower=7,
         boxes=[box("L3A", "Y", 0, 4), box("H2", "B", 2, 4),
                box("H3", "R", 1, 3), box("V2", "Y", 0, 1),
                box("S", "B", 1, 2), box("S", "B", 2, 2), box("S", "Y", 3, 2),
                box("S", "Y", 1, 1), box("S", "Y", 2, 1),
                box("V2", "B", 3, 0), box("H3", "R", 0, 0)],
         art=["RRRR", "YYYY", "BBBB", "YYYY"]),
    dict(n=7, width=6, lower=6, fill_layers=2,
         boxes=[box("Q", "R", 4, 4), box("L3D", "R", 4, 2),
                box("H3", "R", 1, 0), box("T4", "B", 1, 2),
                box("L3A", "B", 0, 3), box("L3B", "B", 4, 0),
                box("H3", "B", 0, 1)],
         art=["BBBBBB", "BBRRBB", "BRRBBB", "BBBBBB"]),
    dict(n=8, width=6, lower=4, dense=True, fill_layers=2,
         boxes=[box("Q", "B", 0, 2, fill=75), box("Q", "B", 4, 2),
                box("H2", "R", 0, 0, ice=1), box("H2", "R", 2, 0, ice=2),
                box("H2", "B", 4, 0, ice=3), box("H2", "B", 2, 1, ice=4)],
         art=["RRRRRR", "RRRRRB", "RBBBBB"]),
    dict(n=9, width=6, lower=5, fill_layers=2,
         boxes=[box("H2", "W", 0, 3), box("H2", "W", 4, 3),
                box("L4A", "Y", 0, 0), box("L4B", "Y", 4, 0),
                box("V2", "Y", 1, 0), box("V2", "Y", 4, 0),
                box("S", "C", 2, 0), box("S", "C", 3, 0),
                box("S", "C", 2, 1), box("S", "C", 3, 1)],
         art=["WWYYWW", "WWYYWW", "CCYYCC", "CCYYCC"]),
    dict(n=10, width=7, lower=6, fill_layers=2,
         boxes=[box("S", "R", 0, y) for y in range(1, 5)] + [box("S", "B", 0, 0)] +
               [box("S", "B", 2, y, ice=3 if y == 4 else 0) for y in range(5)] +
               [box("S", "G", 4, y, ice=6 if y == 4 else 0) for y in range(5)] +
               [box("S", "Y", 6, y, ice=10 if y == 4 else 0) for y in range(5)],
         lower_outside=[(x, y) for x in (1, 3, 5) for y in range(5)],
         art=["RYYYYYY", "RYYYYYY", "RBGBGBG"]),
    dict(n=11, width=6, lower=7,
         boxes=[box("S", "B", 0, 6, ice=8), box("H2", "B", 1, 6),
                box("H2", "R", 3, 6), box("S", "R", 5, 6, ice=8),
                box("S", "R", 0, 5, ice=8), box("H2", "R", 1, 5),
                box("H2", "B", 3, 5), box("S", "B", 5, 5, ice=8),
                box("H2", "B", 0, 4), box("H2", "R", 2, 4, ice=3),
                box("H2", "R", 4, 4),
                box("H2", "R", 0, 2), box("H2", "B", 2, 2), box("H2", "R", 4, 2),
                box("H2", "B", 0, 1, ice=5), box("H2", "R", 2, 1),
                box("H2", "B", 4, 1, ice=8), box("H2", "B", 0, 0),
                box("H2", "R", 2, 0, ice=5), box("H2", "B", 4, 0)],
         art=["BBBBBB", "BBRRRB", "RRRRRR", "RRRRRR", "RBBBBR", "BBBBBB"]),
    dict(n=12, width=8, lower=5,
         boxes=[box("H2", "B", x, 4) for x in (0, 2, 4, 6)] +
               [box("H2", "Y", 0, 3), box("S", "G", 2, 3),
                box("H2", "Y", 3, 3), box("S", "G", 5, 3),
                box("H2", "G", 0, 2), box("H2", "Y", 2, 2),
                box("H2", "G", 4, 2), box("H2", "Y", 6, 2),
                box("R8", "R", 2, 0)],
         lower_outside=[(x, y) for x in (0, 1, 6, 7) for y in (0, 1)],
         art=["RRRRRRRR", "GGGGGGGG", "YBBYYBBY", "YBBYYBBY"]),
    dict(n=13, slots=2, width=5, lower=8,
         boxes=[box("Q", "R", 0, 0), box("Q", "R", 3, 0),
                box("V2", "B", 2, 0), box("H3", "B", 0, 2),
                box("H2", "R", 3, 2), box("H3", "R", 0, 3),
                box("H2", "Y", 3, 3),
                box("S", "B", 0, 4), box("S", "Y", 1, 4),
                box("S", "B", 2, 4), box("S", "R", 3, 4), box("S", "Y", 4, 4),
                box("S", "Y", 0, 5), box("S", "B", 1, 5),
                box("S", "Y", 2, 5), box("S", "R", 3, 5),
                box("V3", "Y", 4, 5), box("V2", "B", 2, 6)],
         art=["YYYYY", "RYYYY", "RYBYY", "YYBYY", "RRBRR", "YYYYY"]),
    dict(n=14, slots=2, width=6, lower=10,
         boxes=[box("Q", "G", 1, 6, ice=8), box("Q", "R", 3, 6, ice=10),
                box("Q", "B", 1, 3, ice=4), box("Q", "Y", 3, 3, ice=6),
                box("S", "G", 0, 9), box("S", "R", 5, 9),
                box("S", "R", 0, 8), box("S", "G", 5, 8),
                box("S", "G", 0, 7), box("S", "R", 5, 7),
                box("S", "R", 0, 6), box("S", "G", 5, 6),
                box("H2", "Y", 1, 5), box("H2", "B", 3, 5),
                box("S", "Y", 0, 4), box("S", "B", 5, 4),
                box("S", "B", 0, 3), box("S", "Y", 5, 3),
                box("S", "Y", 0, 2), box("S", "B", 5, 2),
                box("S", "Y", 0, 1), box("S", "B", 5, 1)] +
               [box("S", "Y" if x % 2 == 0 else "B", x, 0) for x in range(6)],
         art=["GGGRRR", "GGGRRR", "BBYYYY", "YYYYBB", "YBBYYY", "BBYYYY", "YYYYBB"]),
    dict(n=15, slots=2, width=6, lower=9,
         boxes=[box("Q", "B", 0, 7, ice=4), box("Q", "Y", 1, 5, ice=6),
                box("Q", "G", 3, 3, ice=7), box("Q", "R", 4, 0, ice=8),
                box("V2", "R", 2, 7), box("V2", "G", 3, 7), box("V2", "Y", 5, 7),
                box("V2", "R", 0, 5), box("V2", "B", 3, 5),
                box("V2", "B", 4, 5), box("V2", "B", 5, 5),
                box("V2", "R", 0, 3), box("V2", "G", 1, 3),
                box("V2", "Y", 2, 3), box("V2", "R", 5, 3),
                box("V2", "B", 0, 1), box("V2", "G", 1, 1),
                box("V2", "Y", 2, 1), box("V2", "R", 3, 1),
                box("S", "B", 0, 0), box("S", "G", 1, 0),
                box("S", "Y", 2, 0), box("S", "R", 3, 0)],
         art=["BBYGRR", "BBYGGR", "RRYGGR", "RRYGGG", "YYYGBB",
              "GGGGBR", "GGYYBR", "GGYYBR", "GGGGGB"]),
    # From here on, "H"/"V" boxes only slide along one axis (arrow on the lid) and
    # layered chambers list their bands bottom to top. Balls never enter the lower grid,
    # so a sideways-only box must start in the top row to ever collect.
    dict(n=16, width=7, lower=4, fill_layers=2,
         boxes=[box("R6", "B", 4, 2, axis="H"),
                box("R6", "R", 0, 0, axis="V"), box("R6", "R", 4, 0, axis="V")],
         upper_outside_columns=[3], dividers=[3, 4],
         layers=[[("B", 77), ("R", 154)], [], [("R", 154), ("B", None)]]),
    dict(n=17, width=6, lower=6,
         boxes=[box("V3", "B", 0, 3), box("V3", "Y", 1, 3), box("V3", "B", 2, 3),
                box("V2", "G", 3, 4), box("V2", "G", 4, 4),
                box("V3", "B", 0, 0), box("V3", "Y", 1, 0), box("V3", "B", 2, 0),
                box("V2", "B", 3, 1), box("V2", "G", 4, 0), box("S", "B", 5, 1)],
         lower_outside=[(5, 0), (5, 2)], dividers=[1, 2, 3, 5],
         layers=[[("B", None)], [("Y", None)], [("B", None)], [("G", None)], [("B", None)]]),
    dict(n=18, width=6, lower=5,
         boxes=[box("H2", "R", 0, 4, axis="H"), box("H2", "G", 2, 4),
                box("S", "Y", 0, 3, ice=3), box("S", "Y", 1, 3, ice=3), box("H2", "B", 2, 3),
                box("H2", "Y", 0, 2, ice=2), box("H2", "Y", 2, 2),
                box("H2", "B", 0, 1), box("H2", "Y", 0, 0),
                box("V2", "G", 3, 0), box("V2", "R", 4, 0), box("V2", "B", 5, 0)],
         dividers=[2, 4],
         layers=[[("G", None), ("Y", None)], [("B", None), ("Y", None)], [("R", None)]]),
    dict(n=19, width=6, lower=6,
         boxes=[box("H2", "Y", 0, 5, axis="H"),
                box("V2", "Y", 1, 3), box("L3B", "R", 2, 3), box("L3B", "G", 4, 3),
                box("V2", "B", 0, 1), box("Q", "G", 1, 1), box("Q", "Y", 3, 1), box("V2", "B", 5, 1),
                box("H2", "Y", 0, 0), box("H2", "R", 2, 0), box("S", "B", 4, 0), box("S", "R", 5, 0)],
         art=["GGRBYY", "GGRRBY", "GGGRBY"]),
    # Wrong-color stacks under narrow chambers, joined only by the bottom corridor.
    dict(n=20, width=7, lower=5, fill_layers=2,
         boxes=[box("S", "G", 0, y) for y in range(1, 5)] +
               [box("S", "R", 2, y) for y in range(2, 5)] +
               [box("S", "B", 4, y) for y in range(3, 5)] + [box("S", "Y", 6, 0)],
         lower_outside=[(x, y) for x in (1, 3, 5) for y in range(1, 5)],
         upper_outside_columns=[1, 3, 5], dividers=[1, 2, 3, 4, 5, 6],
         layers=[[("Y", None)], [], [("B", None)], [], [("R", None)], [], [("G", None)]]),
    dict(n=21, width=6, lower=5,
         boxes=[box("H2", "R", 0, 4, axis="H"), box("H2", "B", 4, 4),
                box("S", "G", 0, 3, ice=4), box("S", "Y", 1, 3, ice=4), box("H2", "G", 4, 3, ice=4),
                box("H2", "Y", 4, 1), box("H2", "R", 4, 0),
                box("Q", "G", 0, 0), box("Q", "B", 2, 0)],
         dividers=[3],
         layers=[[("B", None), ("G", None)], [("R", None), ("Y", None)]]),
    dict(n=22, width=6, lower=5,
         boxes=[box("V2", "R", 0, 3), box("S", "B", 1, 4), box("S", "G", 2, 4),
                box("S", "W", 3, 4), box("S", "G", 4, 4), box("V2", "R", 5, 3),
                box("S", "Y", 1, 3, ice=6), box("S", "B", 4, 3, ice=7),
                box("Q", "Y", 0, 1), box("Q", "Y", 4, 1),
                box("H2", "G", 0, 0), box("L3D", "G", 2, 0), box("S", "W", 5, 0)],
         # One chamber per column: each top-row box meets its own color first.
         dividers=[1, 2, 3, 4, 5],
         layers=[[("R", 21), ("W", 9), ("Y", None)], [("B", 9), ("Y", None)],
                 [("G", 42), ("Y", None)], [("W", 9), ("Y", None)],
                 [("G", 30), ("Y", None)], [("R", 21), ("B", 9), ("Y", None)]]),
    dict(n=23, width=7, lower=4,
         boxes=[box("S", "R", 0, 3), box("S", "B", 1, 3), box("S", "B", 2, 3),
                box("V3", "Y", 3, 1, ice=9),
                box("S", "Y", 4, 3), box("S", "G", 5, 3), box("S", "G", 6, 3),
                box("S", "Y", 1, 2), box("S", "Y", 2, 2), box("S", "B", 6, 2),
                box("S", "R", 0, 1), box("S", "Y", 1, 1), box("S", "R", 2, 1),
                box("L3A", "R", 4, 1), box("S", "Y", 6, 1), box("S", "G", 5, 0)],
         dividers=[1, 2, 3, 4, 6],
         layers=[[("R", None)], [("Y", None)], [("B", None)], [("Y", None)],
                 [("G", None)], [("R", None), ("G", None)]]),
    dict(n=24, width=6, lower=6,
         boxes=[box("S", "R", 0, 5), box("H2", "Y", 2, 5, ice=5), box("S", "B", 4, 5),
                box("S", "G", 0, 4, ice=3), box("S", "G", 5, 4, ice=4),
                box("V3", "Y", 4, 1), box("H2", "B", 2, 3),
                box("L3C", "R", 0, 2), box("S", "B", 5, 0),
                box("T4", "G", 1, 0), box("H2", "R", 3, 0)],
         dividers=[2, 4],
         layers=[[("B", None), ("R", None), ("G", None)], [("Y", None)], [("B", None), ("G", None)]]),
    dict(n=25, width=6, lower=6,
         boxes=[box("V3", "Y", 1, 1, axis="V"), box("V3", "R", 5, 0, axis="V"),
                box("T4", "G", 2, 4), box("S", "U", 5, 5), box("S", "Y", 4, 4),
                box("H2", "G", 2, 2, ice=4), box("Q", "U", 3, 0, ice=5),
                box("V2", "R", 0, 3), box("S", "U", 0, 0), box("H2", "G", 1, 0)],
         # A sliding-only box can drain just its own column, so that column holds every
         # ball of its color: whichever box of that color arrives first, the rest still fit.
         upper_outside_columns=[2], dividers=[1, 2, 3, 5],
         layers=[[("G", None)], [("Y", None), ("G", None)], [],
                 [("U", 49), ("G", None)], [("R", None), ("U", None)]]),
    # Nested boxes: an inner tray of one color framed by the outer color. Level 26 introduces
    # them with two big trays. Every layer has a colour of its own, the inner colours share the
    # left chamber and the outer colours the right one: a ball pinned against a wall above
    # another colour always sits over a layer that is not waiting on it.
    dict(n=26, width=6, lower=4, fill_layers=2,
         boxes=[box("R6", "R", 0, 2, inner="B"), box("R6", "Y", 3, 0, inner="G")],
         dividers=[3],
         layers=[[("B", None), ("G", None)], [("R", None), ("Y", None)]]),
    dict(n=27, width=6, lower=5,
         boxes=[box("Q", "Y", 0, 3), box("H2", "Y", 4, 3), box("H2", "Y", 0, 1),
                box("H2", "U", 0, 0), box("Q", "Y", 2, 0, fill=2, inner="C"), box("Q", "G", 4, 0)],
         # The inner color lies on the floor: nothing that only the nested box's outer
         # layer could clear may ever sit beneath it.
         layers=[[("C", 48), ("G", None), ("Y", 70), ("U", None), ("Y", None)]],
         blobs=("U", "Y", [(0.15, 0.35), (0.5, 0.75), (0.85, 0.3), (0.35, 0.95)])),
    dict(n=28, slots=2, width=6, lower=5,
         boxes=[box("L3A", "G", 2, 3, inner="C"), box("S", "Y", 0, 4), box("S", "G", 5, 4),
                box("L3C", "R", 3, 1, inner="Y"), box("S", "Y", 0, 2),
                box("V2", "R", 0, 0), box("L4A", "R", 1, 0), box("H2", "G", 2, 0),
                box("L4B", "R", 4, 0)],
         # Each inner color fits in the two floor rows a box draws from, so it can
         # never end up buried under a color only the nested box's outer layer takes.
         dividers=[3],
         layers=[[("Y", 24), ("R", None)], [("C", 16), ("G", None)]]),
    # Levels 29-40. Mid-board sand and separate reservoirs in the reference become chambers
    # of the top reservoir; ice-shelled obstacles become frozen boxes.
    # H-shaped board: two towers joined by a three-cell bridge. The right tower is frozen
    # until five boxes are done on the left. Every column pours from its own chamber, banded
    # in the order its stack surfaces.
    dict(n=29, width=7, lower=7,
         boxes=[box("S", "U", 0, 6), box("V2", "R", 1, 5), box("S", "U", 2, 6),
                box("V2", "Y", 0, 4), box("S", "U", 1, 4), box("V2", "B", 2, 4),
                box("S", "R", 0, 3), box("S", "Y", 2, 3),
                box("S", "B", 0, 2), box("S", "Y", 1, 2),
                box("V2", "G", 0, 0), box("S", "R", 2, 1), box("S", "B", 1, 0), box("S", "G", 2, 0),
                box("S", "G", 3, 3, ice=5),
                box("T4", "Y", 4, 5, ice=5), box("S", "B", 4, 5), box("S", "B", 6, 5),
                box("S", "Y", 4, 4), box("S", "G", 6, 4),
                box("X5", "R", 4, 2, ice=5), box("S", "Y", 4, 2), box("S", "Y", 6, 2),
                box("S", "R", 4, 1), box("H3", "G", 4, 0)],
         lower_outside=[(3, y) for y in (0, 1, 5, 6)],
         upper_outside_columns=[3], dividers=[1, 2, 3, 4, 5, 6],
         layers=[[("U", 9), ("Y", 21), ("R", 9), ("B", 9), ("G", 21)],
                 [("R", 21), ("U", 9), ("Y", 9), ("B", 9), ("G", 9)],
                 [("U", 9), ("B", 21), ("Y", 9), ("R", 9), ("G", 9)],
                 [],
                 [("Y", 15), ("B", 9), ("Y", 9), ("Y", 9), ("R", 9), ("G", 11)],
                 [("Y", 15), ("R", 57), ("G", 11)],
                 [("Y", 15), ("B", 9), ("G", 9), ("Y", 9), ("G", 11)]]),
    # A frozen red floor under a green plus. The plus drinks through its one top cell;
    # the stacks beside it surface one by one, and each column's chamber is banded in that order.
    dict(n=30, width=6, lower=5,
         boxes=[box("H2", "B", 0, 4), box("X5", "G", 3, 2),
                box("H2", "Y", 0, 3), box("S", "B", 2, 3),
                box("H2", "B", 0, 2), box("S", "Y", 2, 2),
                box("H2", "Y", 0, 1), box("S", "B", 2, 1), box("H2", "Y", 4, 1),
                box("S", "R", 0, 0, ice=6), box("H2", "R", 1, 0, ice=8),
                box("H2", "R", 3, 0, ice=8), box("S", "R", 5, 0, ice=6)],
         # Red gets a chamber of its own: a ball pinned against a wall above frozen red could
         # never come loose. The plus drinks only through its top cell, so its green gets a
         # one-column chamber, and no other box is green: a green ball another box knocked out
         # of that chamber could never reach the plus. The bar that surfaces after it takes the
         # next column.
         dividers=[2, 3, 4, 5],
         layers=[[("B", 21), ("Y", 21), ("B", 21), ("Y", 21)], [("B", 9), ("Y", None), ("B", None)],
                 [("R", None)], [("G", None)], [("Y", 21)]]),
    # A shaft of small boxes between two frozen walls, one column wider than the reference so
    # a box that is still waiting on a pinned ball can step aside.
    dict(n=31, width=6, lower=7,
         boxes=[box("V2", "G", 2, 5), box("S", "R", 3, 6), box("V2", "Y", 3, 4), box("S", "B", 2, 4),
                box("Q", "G", 2, 2, fill=40), box("V2", "R", 2, 0), box("V2", "B", 3, 0),
                box("V2", "B", 0, 5, ice=6), box("V2", "R", 1, 5, ice=6), box("V3", "Y", 0, 2, ice=6),
                box("V3", "G", 1, 2, ice=6), box("H2", "R", 0, 0, ice=6),
                box("V2", "R", 5, 5, ice=3), box("V2", "Y", 5, 3, ice=3), box("S", "B", 5, 2, ice=3),
                box("V2", "B", 5, 0, ice=3)],
         dividers=[2, 5],
         layers=[[("B", 21), ("R", 21), ("Y", 33), ("G", None), ("R", 21)],
                 [("G", 21), ("R", 9), ("Y", 21), ("B", 9), ("G", None), ("R", None), ("B", 21)],
                 [("R", 21), ("Y", None), ("B", 9), ("B", None)]]),
    # Sideways-only box along the top, a frozen L and a frozen square in the shaft.
    dict(n=32, width=6, lower=7,
         boxes=[box("V2", "U", 0, 5), box("Q", "U", 2, 5), box("H2", "C", 4, 6, axis="H"),
                box("V2", "Y", 1, 4), box("S", "B", 4, 5), box("V3", "C", 5, 3),
                box("L3A", "C", 0, 2, ice=6), box("H2", "Y", 2, 4), box("S", "B", 2, 3),
                box("Q", "G", 3, 2, ice=12), box("V2", "P", 1, 1),
                box("H2", "P", 2, 0), box("S", "G", 0, 0), box("H2", "G", 4, 0), box("V2", "B", 5, 1)],
         dividers=[2, 4],
         layers=[[("U", None), ("B", None)], [("Y", None), ("P", None)], [("C", None), ("G", None)]]),
    # Six colours in three chambers; the open middle is the only room to manoeuvre.
    dict(n=33, width=6, lower=6,
         boxes=[box("H3", "O", 0, 5), box("H2", "C", 4, 5),
                box("S", "R", 0, 4), box("L3A", "G", 1, 3), box("V3", "U", 5, 2),
                box("V2", "G", 0, 2), box("S", "U", 1, 2), box("H3", "Y", 2, 2),
                box("V2", "R", 0, 0), box("V2", "U", 1, 0), box("Q", "C", 2, 0), box("Q", "Y", 4, 0)],
         dividers=[2, 4],
         layers=[[("C", None), ("U", None)], [("R", None), ("Y", None)], [("G", None), ("O", None)]]),
    # A ring of boxes around an empty well; half-filled sand blocks on either side.
    dict(n=34, width=6, lower=7,
         boxes=[box("H2", "Y", 1, 6), box("H2", "G", 3, 6),
                box("H2", "Y", 0, 5), box("H2", "R", 2, 5), box("H2", "G", 4, 5),
                box("Q", "G", 0, 3, fill=40), box("Q", "Y", 4, 3, fill=40),
                box("L3A", "B", 0, 1), box("L3D", "B", 0, 0), box("H2", "G", 2, 0),
                box("L3B", "R", 4, 1), box("L3C", "R", 4, 0)],
         # Floor balls never slide sideways, so each half pours what its own column of boxes
         # needs, in the order they surface: the top pairs, then the sand block below them.
         dividers=[3],
         layers=[[("Y", None), ("G", 29), ("G", 21), ("B", None)], [("G", 42), ("Y", 29), ("R", None)]]),
    # Column-locked boxes and interlocking L shapes; orange has its own one-column chamber.
    dict(n=35, width=6, lower=7,
         boxes=[box("H2", "U", 3, 6), box("V2", "Y", 1, 4), box("V2", "O", 2, 4, axis="V"),
                box("L3B", "G", 3, 4), box("H2", "Y", 1, 3), box("L4B", "B", 3, 1),
                box("V2", "O", 0, 2), box("V2", "O", 2, 1, axis="V"), box("L3A", "G", 0, 0),
                box("L3D", "U", 2, 0), box("V2", "Y", 5, 0)],
         upper_outside_columns=[0], dividers=[3, 4],
         layers=[[("B", None), ("G", None)], [("O", None)], [("U", None), ("Y", None)]]),
    # Super hard: tees and bars interlocked across a board with its lower corners cut away.
    # The yellow and blue bars start over each other's chambers and must cross the middle,
    # and the middle column pours orange, red, then orange again.
    dict(n=36, width=6, lower=6,
         boxes=[box("H2", "B", 0, 5), box("S", "O", 2, 5), box("S", "R", 3, 5), box("H2", "Y", 4, 5),
                box("T4", "Y", 0, 3), box("T4", "B", 3, 3, ice=3),
                box("TD", "R", 1, 1), box("V2", "B", 0, 1), box("V2", "Y", 5, 1),
                box("S", "R", 3, 1), box("S", "O", 4, 1),
                box("H2", "Y", 1, 0), box("H2", "R", 3, 0)],
         lower_outside=[(0, 0), (5, 0)],
         dividers=[2, 3, 4],
         layers=[[("Y", None)], [("O", 9), ("R", 40), ("O", None)], [("R", None)], [("B", None)]]),
    # A frozen centre block; column-locked bars on both flanks.
    dict(n=37, width=7, lower=7,
         boxes=[box("L4A", "R", 1, 4), box("L3A", "P", 3, 5), box("S", "R", 5, 6),
                box("V2", "U", 0, 2, axis="V"), box("Q", "G", 2, 2, ice=4), box("L3D", "C", 4, 2),
                box("L3B", "P", 0, 0), box("H2", "C", 2, 0), box("Q", "U", 4, 0),
                box("V2", "B", 6, 0, axis="V"), box("S", "Y", 5, 4)],
         # One chamber per column, banded in the order boxes surface over it: the top row
         # first, then the yellow single, the cyan L (drinking through its one top cell), the
         # thawed square, and last the three pieces from the bottom row.
         dividers=[1, 2, 3, 4, 5, 6],
         layers=[[("U", 21), ("P", 16)], [("R", 22), ("P", None)], [("R", None), ("G", 24), ("C", 10)],
                 [("P", 16), ("G", None), ("C", None)], [("P", 17), ("U", 24)],
                 [("R", 9), ("Y", None), ("C", 33), ("U", None)], [("B", None)]]),
    # Stacks of single cells around a big frozen square.
    dict(n=38, width=6, lower=5, fill_layers=2,
         boxes=[box("S", "G", 0, 4), box("Q", "B", 2, 3, ice=10),
                box("S", "G", 0, 3), box("S", "R", 1, 3), box("H2", "R", 4, 3),
                box("S", "R", 0, 2), box("S", "G", 1, 2), box("H2", "G", 4, 2),
                box("S", "G", 0, 1), box("S", "R", 1, 1), box("H2", "Y", 2, 1), box("H2", "R", 4, 1),
                box("S", "R", 0, 0), box("S", "G", 1, 0), box("H2", "Y", 2, 0), box("H2", "G", 4, 0)],
         # One chamber per stack, banded in the order its boxes surface. The yellow bars wait
         # under the frozen square until ten boxes are done, then trade places with it.
         dividers=[1, 2, 4],
         layers=[[("G", 36), ("R", 18), ("G", 18), ("R", 18)], [("R", 18), ("G", 18), ("R", 18), ("G", 18)],
                 [("Y", None), ("B", None)], [("R", 42), ("G", 42), ("R", 42), ("G", 42)]]),
    # Frozen shelves everywhere; the only way up is the open corner and the side pocket.
    dict(n=39, width=7, lower=8,
         boxes=[box("H2", "P", 0, 7, ice=4), box("H2", "R", 2, 7, ice=4),
                box("H2", "B", 0, 6, ice=4), box("H2", "Y", 2, 6, ice=4),
                box("H4", "R", 0, 5, ice=10),
                box("S", "W", 0, 4), box("S", "K", 1, 4), box("V2", "B", 2, 3), box("V2", "B", 3, 3),
                box("Q", "P", 4, 3), box("V2", "Y", 6, 3),
                box("S", "K", 0, 3), box("S", "W", 1, 3),
                box("S", "R", 0, 2, ice=7), box("H3", "K", 3, 2, ice=6),
                box("H3", "Y", 3, 1, ice=6), box("S", "B", 0, 0, ice=6), box("H2", "W", 4, 0, ice=6)],
         lower_outside=[(6, y) for y in (0, 1, 2, 5, 6, 7)],
         # At first only the open corner reaches the reservoir, so its chamber pours for the
         # boxes that can climb there; the frozen shelves' colours wait in the other two.
         upper_outside_columns=[6], dividers=[2, 4],
         layers=[[("P", 21), ("B", 21), ("R", 22), ("R", 9), ("B", 9), ("W", 21)],
                 [("R", 21), ("Y", 21), ("R", None), ("K", 33), ("Y", None)],
                 [("P", None), ("Y", 21), ("B", None), ("W", None), ("K", None)]]),
    # Padlock: the big red box opens once all four key boxes (yellow, blue, green, pink) are done.
    dict(n=40, width=6, lower=4, fill_layers=2,
         boxes=[box("S", "R", 2, 3), box("H3", "Y", 3, 3, key="red"),
                box("H3", "B", 0, 2, key="red"), box("S", "R", 3, 2),
                box("V2", "G", 0, 0, key="red"), box("R8", "R", 1, 0, lock="red"),
                box("V2", "P", 5, 0, key="red")],
         # Each key colour pours from its own chamber over its own column; the red that only the
         # padlocked box can finish sits apart in the middle, so no key ball is ever pinned
         # beneath red. Pink rides on top of green: the green box can step aside to let it through.
         dividers=[1, 2, 5],
         layers=[[("G", None), ("P", None)], [("B", None)], [("R", None)], [("Y", None)]]),
]


def prepare_palette():
    colors = {
        "R": new_color("ReferenceRed", (0.98, 0.18, 0.20)),
        "B": new_color("ReferenceBlue", (0.10, 0.38, 0.96)),
        "P": new_color("ReferencePink", (0.95, 0.19, 0.79)),
        "W": new_color("ReferenceWhite", (0.91, 0.96, 1.0)),
        "O": new_color("ReferenceOrange", (1.0, 0.52, 0.08)),
        "K": new_color("ReferenceBlack", (0.2, 0.2, 0.26)),
    }
    for key, name in (("G", "Emerald"), ("Y", "Saffron"),
                      ("C", "Aqua"), ("U", "Lilac")):
        colors[key] = asset_guid(CONTENT / f"Color_{name}.asset")
    return colors


def prepare_shapes():
    shapes = {}
    for key, name in (("S", "1x1"), ("H2", "2x1"),
                      ("V2", "1x2"), ("V3", "1x3")):
        shapes[key] = (asset_guid(CONTENT / f"Shape_{name}.asset"),
                       [(0, y) for y in range(int(name[-1]))] if name.startswith("1x")
                       else [(x, 0) for x in range(int(name[0]))])
    for key, cells in SHAPE_CELLS.items():
        path = OUTPUT / f"Shape_{key}.asset"
        body = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: cb4966f5afa37b547a073337c219324e, type: 3}
  m_Name: Shape_""" + key + "\n  m_EditorClassIdentifier: \n  cells:\n"
        body += "".join(f"  - {{x: {x}, y: {y}}}\n" for x, y in cells)
        write_asset(path, body)
        shapes[key] = (asset_guid(path), cells)
    return shapes


def mask_lines(outside, width, height):
    positions = set(outside)
    if any(x < 0 or y < 0 or x >= width or y >= height for x, y in positions):
        raise ValueError("Mask cell outside grid")
    if not positions:
        return "    overrides: []\n"
    return "    overrides:\n" + "".join(
        f"    - cell: {{x: {x}, y: {y}}}\n      kind: 0\n"
        for x, y in sorted(positions, key=lambda item: (item[1], item[0])))


def picture_color(number, x, y, width, height, fallback):
    nx = (x + 0.5) / (width * 4)
    ny = (y + 0.5) / height
    if number == 7:
        for cx, cy, rx, ry in ((0.68, 0.68, 0.31, 0.40), (0.22, 0.38, 0.19, 0.24)):
            u, v = (nx - cx) / rx, (ny - cy) / ry
            if (u * u + v * v - 1) ** 3 - u * u * v ** 3 <= 0:
                return "R"
        return "B"
    if number == 13:
        if 0.22 < ny < 0.76 and abs(nx - 0.42) < (0.76 - ny) * 0.48:
            return "B"
        if 0.17 < ny <= 0.26 and 0.18 < nx < 0.73:
            return "B"
        if (0.09 < ny < 0.17 or 0.41 < ny < 0.48 or 0.77 < ny < 0.85) and (nx < 0.4 or nx > 0.57):
            return "R"
        return "Y"
    return fallback


def capacity(offsets, slots, dense):
    """Mirror of BoxShapeDefinition.FillSlotsPerLayer."""
    cell_set = set(offsets)
    seam = slots // 2 if dense else 0
    seams = sum((dx + 1, dy) in cell_set for dx, dy in offsets) + \
            sum((dx, dy + 1) in cell_set for dx, dy in offsets)
    corners = sum((dx + 1, dy) in cell_set and (dx, dy + 1) in cell_set and
                  (dx + 1, dy + 1) in cell_set for dx, dy in offsets)
    return slots * slots * len(offsets) + slots * seam * seams + seam * seam * corners


def scaled_prefill(prefill, offsets, slots, dense, layers=1):
    # Prefill is authored against the 16-per-cell layout; keep the same share.
    return round(prefill * capacity(offsets, slots, dense) * layers / capacity(offsets, 4, dense))


def upper_outside(level, upper):
    return {(x, y) for x in level.get("upper_outside_columns", ()) for y in range(upper)}


def resolve_layers(level, needed):
    """Chamber layers, bottom to top, as (color, count). A None count takes an even
    share of whatever that color still needs after the explicit counts."""
    fixed, open_layers = Counter(), Counter()
    for chamber in level["layers"]:
        for color, count in chamber:
            if count is None:
                open_layers[color] += 1
            else:
                fixed[color] += count
    given = Counter()
    resolved = []
    for chamber in level["layers"]:
        layers = []
        for color, count in chamber:
            if count is None:
                share, extra = divmod(needed[color] - fixed[color], open_layers[color])
                count = share + (1 if given[color] < extra else 0)
                given[color] += 1
            if count < 0:
                raise ValueError(f"Level {level['n']}: layer counts exceed the {color} boxes")
            layers.append((color, count))
        resolved.append(layers)
    totals = Counter()
    for chamber in resolved:
        for color, count in chamber:
            totals[color] += count
    if +totals != +needed:
        raise ValueError(f"Level {level['n']}: layers hold {dict(totals)}, boxes need {dict(needed)}")
    return resolved


def reservoir_height(level, needed):
    """Smallest reservoir, in macro rows, that keeps every chamber under MAX_RESERVOIR_FILL."""
    outside_columns = set(level.get("upper_outside_columns", ()))
    boundaries = [0] + level.get("dividers", []) + [level["width"]]
    if "layers" in level:
        quotas = [sum(count for _, count in chamber) for chamber in resolve_layers(level, needed)]
        floor_rows = 0
        for color, first, last in level.get("floor", ()):
            # A stripe must fit its own columns within the fill limit too.
            share = sum(count for chamber in resolve_layers(level, needed) for band, count in chamber if band == color)
            floor_rows = max(floor_rows, math.ceil(share / (4 * (last - first) * MAX_RESERVOIR_FILL) / 4))
    else:
        palettes = level.get("chamber_colors") or ["".join(needed)]
        quotas = [sum(needed.get(color, 0) for color in palette) for palette in palettes]
        floor_rows = 0
    rows = max(2, floor_rows)
    for chamber, quota in enumerate(quotas):
        first, last = boundaries[chamber:chamber + 2]
        columns = 4 * sum(x not in outside_columns for x in range(first, last))
        if columns == 0:
            if quota:
                raise ValueError(f"Level {level['n']}: chamber {chamber} has balls but no columns")
            continue
        rows = max(rows, math.ceil(quota / (columns * MAX_RESERVOIR_FILL) / 4))
    return rows + level.get("extra_rows", 0)


def make_layered_balls(level, needed):
    """Each chamber fills bottom-up, row by row, one color band after another."""
    width, lower, upper = level["width"], level["lower"], level["upper"]
    outside = upper_outside(level, upper)
    boundaries = [0] + level.get("dividers", []) + [width]
    layers = resolve_layers(level, needed)
    if len(layers) != len(boundaries) - 1:
        raise ValueError(f"Level {level['n']}: layer list count differs from chamber count")
    balls = []
    used = set()
    # Floor stripes: side-by-side columns of one color each, poured before any layer.
    for color, first, last in level.get("floor", ()):
        sites = [(x, y) for y in range(lower * 4, (lower + upper) * 4) for x in range(first * 4, last * 4)
                 if (x // 4, y // 4 - lower) not in outside]
        for chamber in layers:
            for index, (band, count) in enumerate(chamber):
                if band == color:
                    chamber[index] = (band, 0)
                    balls.extend(zip(sites[:count], [color] * count))
                    used.update(sites[:count])
    for chamber, bands in enumerate(layers):
        first, last = boundaries[chamber:chamber + 2]
        sites = [(x, y) for y in range(lower * 4, (lower + upper) * 4) for x in range(first * 4, last * 4)
                 if (x // 4, y // 4 - lower) not in outside and (x, y) not in used]
        colors = [color for color, count in bands for _ in range(count)]
        if len(colors) > len(sites):
            raise ValueError(f"Level {level['n']}: chamber {chamber} needs {len(colors)} balls, has {len(sites)} positions")
        balls.extend(zip(sites, colors))
    if "blobs" in level:
        balls = gather_blobs(balls, *level["blobs"])
    balls.sort(key=lambda item: (item[0][1], item[0][0]))
    return balls


def gather_blobs(balls, spot, ground, centers):
    """Regroup `spot` balls into round blobs inside the region they share with `ground`.
    Counts and the region itself stay unchanged, so nothing moves under another band."""
    region = [cell for cell, color in balls if color in (spot, ground)]
    count = sum(color == spot for _, color in balls)
    xs = [x for x, _ in region]
    ys = [y for _, y in region]
    anchors = [(min(xs) + u * (max(xs) - min(xs)), min(ys) + v * (max(ys) - min(ys))) for u, v in centers]
    ranked = sorted(region, key=lambda cell: (min((cell[0] - ax) ** 2 + ((cell[1] - ay) * 0.87) ** 2
                                                  for ax, ay in anchors), cell[1], cell[0]))
    spots = set(ranked[:count])
    return [(cell, spot if cell in spots else ground) if color in (spot, ground) else (cell, color)
            for cell, color in balls]


def make_balls(level, needed):
    width, lower, upper = level["width"], level["lower"], level["upper"]
    outside = upper_outside(level, upper)
    art = level["art"]
    if any(len(row) != width for row in art):
        raise ValueError(f"Level {level['n']}: artwork row width differs from grid")
    available = [(x, y) for y in range(lower * 4, (lower + upper) * 4)
                 for x in range(width * 4) if (x // 4, y // 4 - lower) not in outside]
    boundaries = [0] + level.get("dividers", []) + [width]
    chamber_colors = level.get("chamber_colors")
    if chamber_colors is None:
        chamber_colors = ["".join(needed)]
    if len(chamber_colors) != len(boundaries) - 1:
        raise ValueError(f"Level {level['n']}: chamber palette count differs from divider count")
    cells = []
    allowed = {}
    filled_heights = {}
    for chamber, palette in enumerate(chamber_colors):
        first, last = boundaries[chamber:chamber + 2]
        quota = sum(needed.get(color, 0) for color in palette)
        sites = [cell for cell in available if first <= cell[0] // 4 < last]
        if quota > len(sites):
            raise ValueError(f"Level {level['n']}: chamber {chamber} needs {quota} balls, has {len(sites)} positions")
        selected = sites[:quota]
        cells.extend(selected)
        for cell in selected:
            allowed[cell] = palette
        filled_heights[chamber] = max((y for _, y in selected), default=lower * 4 - 1) - lower * 4 + 1
    if len(cells) != sum(needed.values()):
        raise ValueError(f"Level {level['n']}: chamber colors must partition the ball palette")
    cells.sort(key=lambda cell: (cell[1], cell[0]))
    desired = {}
    for x, y in cells:
        chamber = next(index for index in range(len(chamber_colors))
                       if boundaries[index] <= x // 4 < boundaries[index + 1])
        local_y = y - lower * 4
        row = len(art) - 1 - min(len(art) - 1, local_y * len(art) // filled_heights[chamber])
        desired[(x, y)] = picture_color(level["n"], x, local_y, width,
                                         filled_heights[chamber], art[row][x // 4])
    placed = {}
    remaining = needed.copy()
    for color in sorted(needed, key=lambda key: sum(value == key for value in desired.values())):
        for cell in cells:
            if remaining[color] == 0:
                break
            if desired[cell] == color and cell not in placed and color in allowed[cell]:
                placed[cell] = color
                remaining[color] -= 1
    centers = {}
    for color in needed:
        matches = [cell for cell, preferred in desired.items() if preferred == color]
        centers[color] = (sum(x for x, _ in matches) / len(matches),
                          sum(y for _, y in matches) / len(matches)) if matches else (width * 2, lower * 4)
    for cell in cells:
        if cell in placed:
            continue
        color = min((key for key, count in remaining.items() if count > 0 and key in allowed[cell]),
                    key=lambda key: abs(cell[0] - centers[key][0]) + abs(cell[1] - centers[key][1]))
        placed[cell] = color
        remaining[color] -= 1
    if any(remaining.values()):
        raise ValueError(f"Level {level['n']}: unmatched ball counts {remaining}")
    return [(cell, placed[cell]) for cell in cells]


def build_level(level, colors, shapes):
    n, width, lower = (level[key] for key in ("n", "width", "lower"))
    dense = bool(level.get("dense", DEFAULT_DENSE))
    slots = level.get("slots", DEFAULT_SLOTS)
    fill_layers = level.get("fill_layers", DEFAULT_FILL_LAYERS)
    lower_outside = set(level.get("lower_outside", ()))
    occupied = set()
    needed = Counter()
    ids = Counter()
    locks = {entry[8] for entry in level["boxes"] if entry[8]}
    keys = {entry[9] for entry in level["boxes"] if entry[9]}
    if locks != keys:
        raise ValueError(f"Level {n}: every lock needs a key box and every key a lock ({locks} vs {keys})")
    for shape, color, x, y, ice, prefill, axis, inner, lock, key in level["boxes"]:
        offsets = shapes[shape][1]
        for dx, dy in offsets:
            cell = (x + dx, y + dy)
            if cell in occupied or cell in lower_outside or not (0 <= cell[0] < width and 0 <= cell[1] < lower):
                raise ValueError(f"Level {n}: invalid or overlapping box cell {cell}")
            occupied.add(cell)
        full = capacity(offsets, slots, dense) * fill_layers
        prefill = scaled_prefill(prefill, offsets, slots, dense, fill_layers)
        if not 0 <= prefill < full:
            raise ValueError(f"Level {n}: invalid prefill on {shape}")
        if axis == "H" and not any(y + dy == lower - 1 for _, dy in offsets):
            raise ValueError(f"Level {n}: a sideways-only box must start touching the reservoir")
        if inner is not None:
            # A nested box takes a full load of its inner color, then a full load of its own.
            needed[inner] += full - prefill
            needed[color] += full
        else:
            needed[color] += full - prefill
    upper = level["upper"] = reservoir_height(level, needed)
    balls = make_layered_balls(level, needed) if "layers" in level else make_balls(level, needed)
    path = OUTPUT / f"Level_{n:02d}_Reference.asset"
    body = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: 0927cc5767a13394f9fb22eaf1fb6481, type: 3}}
  m_Name: Level_{n:02d}_Reference
  m_EditorClassIdentifier: 
  levelPrefab: {{fileID: 1903656605721246596, guid: 41b36d7bcac348d4baa631aacfacb93c, type: 3}}
  timeLimitSeconds: 240
  macroGridWidth: {width}
  lowerGridHeight: {lower}
  ballAreaMacroHeight: {upper}
  hopperMicroRows: 0
  macroCellSize: 1
  denseBoxFill: {int(dense)}
  fillLayers: {fill_layers}
  boxSlotsPerSide: {slots}
  reservoirDividerColumns: {level.get('dividers', [])}
  lowerGridMask:
    defaultKind: 1
"""
    body += mask_lines(lower_outside, width, lower)
    body += "  ballAreaMask:\n    defaultKind: 1\n"
    body += mask_lines(upper_outside(level, upper), width, upper)
    body += "  boxes:\n"
    for shape, color, x, y, ice, prefill, axis, inner, lock, key in level["boxes"]:
        prefill = scaled_prefill(prefill, shapes[shape][1], slots, dense, fill_layers)
        ids[color] += 1
        body += f"""  - id: {color}_{ids[color]}
    shape: {reference(shapes[shape][0])}
    color: {reference(colors[color])}
    startingMacroOrigin: {{x: {x}, y: {y}}}
    startsLocked: {int(lock is not None)}
    lockId: '{lock or ""}'
    keyId: '{key or ""}'
    iceCount: {ice}
    initialFillCount: {prefill}
"""
        if axis is not None:
            body += f"    moveAxis: {AXIS[axis]}\n"
        if inner is not None:
            body += f"    innerColor: {reference(colors[inner])}\n"
    body += "  balls:\n"
    for (x, y), color in balls:
        body += f"""  - color: {reference(colors[color])}
    cell: {{x: {x}, y: {y}}}
    specialType: 0
    keyId: ''
"""
    body += "  palette:\n" + "".join(f"  - {reference(colors[color])}\n" for color in needed)
    write_asset(path, body)
    return path, len(level["boxes"]), len(balls), upper


def main():
    folder_meta = OUTPUT.with_suffix(".meta")
    if not folder_meta.exists():
        folder_meta.write_text(f"fileFormatVersion: 2\nguid: {guid('ReferenceLevels folder')}\n"
                               "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
                               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
                               encoding="utf-8", newline="\n")
    colors = prepare_palette()
    shapes = prepare_shapes()
    results = [build_level(level, colors, shapes) for level in LEVELS]
    config = ROOT / "Scripts" / "LevelConfig.asset"
    text = config.read_text(encoding="utf-8")
    prefix, suffix = text.split("  levels:\n", 1)
    _, after = suffix.split("  enableLooping:", 1)
    text = prefix + "  levels:\n" + "".join(
        f"  - {reference(asset_guid(path))}\n" for path, *_ in results)
    text += "  enableLooping:" + after
    config.write_text(text, encoding="utf-8", newline="\n")
    for path, boxes, balls, upper in results:
        print(f"{path.name}: {boxes} boxes, {balls} balls, reservoir {upper} rows")


if __name__ == "__main__":
    main()
