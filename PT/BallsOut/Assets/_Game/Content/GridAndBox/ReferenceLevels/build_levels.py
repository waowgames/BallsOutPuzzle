"""Author the first fifteen Sand Blocks reference layouts as Unity level assets.

Run from the project Assets directory: python _Game/Content/GridAndBox/ReferenceLevels/build_levels.py
The source videos/images are visual references; the layouts use Balls Out's grid rules.
"""

from collections import Counter
from pathlib import Path
import re
import uuid


ROOT = Path(__file__).resolve().parents[4]
CONTENT = ROOT / "_Game" / "Content" / "GridAndBox"
OUTPUT = Path(__file__).resolve().parent
MATERIALS = ROOT / "_ART" / "GridAndBox" / "Materials"
NAMESPACE = uuid.UUID("76e91398-e9d0-40f6-a5e2-c44503689ca0")


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
}


def box(shape, color, x, y, ice=0, fill=0):
    return (shape, color, x, y, ice, fill)


LEVELS = [
    dict(n=1, width=4, lower=4, upper=4, dense=True,
         boxes=[box("Q", "B", 0, 0, fill=37), box("Q", "R", 2, 0)],
         dividers=[2], chamber_colors=["B", "R"],
         art=["BBRR", "BBRR", "BBRR"]),
    dict(n=2, width=5, lower=6, upper=2,
         boxes=[box("L3A", "R", 0, 2), box("L3B", "B", 3, 1), box("T4", "G", 1, 0)],
         art=["BBBBB", "GGRRR"]),
    dict(n=3, width=5, lower=6, upper=2,
         boxes=[box("V2", "R", 2, 3), box("H3", "Y", 0, 1),
                box("H3", "G", 0, 0), box("V2", "B", 4, 0)],
         art=["GGGGG", "YYYBB", "BBBRR", "RRRRR"]),
    dict(n=4, width=6, lower=6, upper=4,
         boxes=[box("Q", "R", 0, 0), box("Q", "R", 0, 2),
                box("Q", "Y", 2, 0), box("Q", "Y", 2, 2),
                box("Q", "B", 4, 0), box("Q", "G", 4, 2)],
         art=["RRRRRR", "BBBBBB", "GGGYYY"]),
    dict(n=5, width=7, lower=3, upper=6,
         boxes=[box("V3", "P", 1, 0), box("V3", "R", 2, 0),
                box("V3", "B", 3, 0), box("V3", "U", 4, 0),
                box("V3", "Y", 5, 0), box("V3", "C", 6, 0)],
         upper_outside=[(0, y) for y in range(6)] + [(6, y) for y in range(6)],
         dividers=[2, 3, 4, 5], chamber_colors=["C", "Y", "RU", "B", "P"],
         art=["0CYRBP0", "0CYRBP0", "0CYUBP0", "0CYUBP0"]),
    dict(n=6, width=4, lower=7, upper=5,
         boxes=[box("L3A", "Y", 0, 4), box("H2", "B", 2, 4),
                box("H3", "R", 1, 3), box("V2", "Y", 0, 1),
                box("S", "B", 1, 2), box("S", "B", 2, 2), box("S", "Y", 3, 2),
                box("S", "Y", 1, 1), box("S", "Y", 2, 1),
                box("V2", "B", 3, 0), box("H3", "R", 0, 0)],
         art=["RRRR", "YYYY", "BBBB", "YYYY"]),
    dict(n=7, width=6, lower=6, upper=4,
         boxes=[box("Q", "R", 4, 4), box("L3D", "R", 4, 2),
                box("H3", "R", 1, 0), box("T4", "B", 1, 2),
                box("L3A", "B", 0, 3), box("L3B", "B", 4, 0),
                box("H3", "B", 0, 1)],
         art=["BBBBBB", "BBRRBB", "BRRBBB", "BBBBBB"]),
    dict(n=8, width=6, lower=4, upper=3, dense=True,
         boxes=[box("Q", "B", 0, 2, fill=75), box("Q", "B", 4, 2),
                box("H2", "R", 0, 0, ice=1), box("H2", "R", 2, 0, ice=2),
                box("H2", "B", 4, 0, ice=3), box("H2", "B", 2, 1, ice=4)],
         art=["RRRRRR", "RRRRRB", "RBBBBB"]),
    dict(n=9, width=6, lower=5, upper=4,
         boxes=[box("H2", "W", 0, 3), box("H2", "W", 4, 3),
                box("L4A", "Y", 0, 0), box("L4B", "Y", 4, 0),
                box("V2", "Y", 1, 0), box("V2", "Y", 4, 0),
                box("S", "C", 2, 0), box("S", "C", 3, 0),
                box("S", "C", 2, 1), box("S", "C", 3, 1)],
         art=["WWYYWW", "WWYYWW", "CCYYCC", "CCYYCC"]),
    dict(n=10, width=7, lower=6, upper=3,
         boxes=[box("S", "R", 0, y) for y in range(1, 5)] + [box("S", "B", 0, 0)] +
               [box("S", "B", 2, y, ice=3 if y == 4 else 0) for y in range(5)] +
               [box("S", "G", 4, y, ice=6 if y == 4 else 0) for y in range(5)] +
               [box("S", "Y", 6, y, ice=10 if y == 4 else 0) for y in range(5)],
         lower_outside=[(x, y) for x in (1, 3, 5) for y in range(5)],
         art=["RYYYYYY", "RYYYYYY", "RBGBGBG"]),
    dict(n=11, width=6, lower=7, upper=6,
         boxes=[box("S", "Y", 0, 6, ice=8), box("H2", "Y", 1, 6),
                box("H2", "G", 3, 6), box("S", "G", 5, 6, ice=8),
                box("S", "G", 0, 5, ice=8), box("H2", "G", 1, 5),
                box("H2", "Y", 3, 5), box("S", "Y", 5, 5, ice=8),
                box("H2", "Y", 0, 4), box("H2", "G", 2, 4, ice=3),
                box("H2", "G", 4, 4),
                box("H2", "R", 0, 2), box("H2", "B", 2, 2), box("H2", "R", 4, 2),
                box("H2", "B", 0, 1, ice=5), box("H2", "R", 2, 1),
                box("H2", "B", 4, 1, ice=8), box("H2", "B", 0, 0),
                box("H2", "R", 2, 0, ice=5), box("H2", "B", 4, 0)],
         art=["YYYYYY", "YYGGGY", "GGGGGG", "RRRRRR", "RBBBBR", "BBBBBB"]),
    dict(n=12, width=8, lower=5, upper=4,
         boxes=[box("H2", "B", x, 4) for x in (0, 2, 4, 6)] +
               [box("H2", "Y", 0, 3), box("S", "G", 2, 3),
                box("H2", "Y", 3, 3), box("S", "G", 5, 3),
                box("H2", "G", 0, 2), box("H2", "Y", 2, 2),
                box("H2", "G", 4, 2), box("H2", "Y", 6, 2),
                box("R8", "R", 2, 0)],
         lower_outside=[(x, y) for x in (0, 1, 6, 7) for y in (0, 1)],
         art=["RRRRRRRR", "GGGGGGGG", "YBBYYBBY", "YBBYYBBY"]),
    dict(n=13, width=5, lower=8, upper=7,
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
    dict(n=14, width=6, lower=10, upper=7,
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
    dict(n=15, width=6, lower=9, upper=9,
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
]


def prepare_palette():
    colors = {
        "R": new_color("ReferenceRed", (0.98, 0.18, 0.20)),
        "B": new_color("ReferenceBlue", (0.10, 0.38, 0.96)),
        "P": new_color("ReferencePink", (0.95, 0.19, 0.79)),
        "W": new_color("ReferenceWhite", (0.91, 0.96, 1.0)),
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


def make_balls(level, needed):
    width, lower, upper = level["width"], level["lower"], level["upper"]
    outside = set(level.get("upper_outside", ()))
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
    n, width, lower, upper = (level[key] for key in ("n", "width", "lower", "upper"))
    dense = bool(level.get("dense", False))
    lower_outside = set(level.get("lower_outside", ()))
    occupied = set()
    needed = Counter()
    ids = Counter()
    for shape, color, x, y, ice, prefill in level["boxes"]:
        offsets = shapes[shape][1]
        for dx, dy in offsets:
            cell = (x + dx, y + dy)
            if cell in occupied or cell in lower_outside or not (0 <= cell[0] < width and 0 <= cell[1] < lower):
                raise ValueError(f"Level {n}: invalid or overlapping box cell {cell}")
            occupied.add(cell)
        cell_set = set(offsets)
        seams = sum((dx + 1, dy) in cell_set for dx, dy in offsets) + \
                sum((dx, dy + 1) in cell_set for dx, dy in offsets)
        corners = sum((dx + 1, dy) in cell_set and (dx, dy + 1) in cell_set and
                      (dx + 1, dy + 1) in cell_set for dx, dy in offsets)
        capacity = 16 * len(offsets) + (8 * seams + 4 * corners if dense else 0)
        if not 0 <= prefill < capacity:
            raise ValueError(f"Level {n}: invalid prefill on {shape}")
        needed[color] += capacity - prefill
    balls = make_balls(level, needed)
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
  fillLayers: 1
  reservoirDividerColumns: {level.get('dividers', [])}
  lowerGridMask:
    defaultKind: 1
"""
    body += mask_lines(lower_outside, width, lower)
    body += "  ballAreaMask:\n    defaultKind: 1\n"
    body += mask_lines(level.get("upper_outside", ()), width, upper)
    body += "  boxes:\n"
    for shape, color, x, y, ice, prefill in level["boxes"]:
        ids[color] += 1
        body += f"""  - id: {color}_{ids[color]}
    shape: {reference(shapes[shape][0])}
    color: {reference(colors[color])}
    startingMacroOrigin: {{x: {x}, y: {y}}}
    startsLocked: 0
    lockId: ''
    iceCount: {ice}
    initialFillCount: {prefill}
"""
    body += "  balls:\n"
    for (x, y), color in balls:
        body += f"""  - color: {reference(colors[color])}
    cell: {{x: {x}, y: {y}}}
    specialType: 0
    keyId: ''
"""
    body += "  palette:\n" + "".join(f"  - {reference(colors[color])}\n" for color in needed)
    write_asset(path, body)
    return path, len(level["boxes"]), len(balls)


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
        f"  - {reference(asset_guid(path))}\n" for path, _, _ in results)
    text += "  enableLooping:" + after
    config.write_text(text, encoding="utf-8", newline="\n")
    for path, boxes, balls in results:
        print(f"{path.name}: {boxes} boxes, {balls} balls")


if __name__ == "__main__":
    main()
