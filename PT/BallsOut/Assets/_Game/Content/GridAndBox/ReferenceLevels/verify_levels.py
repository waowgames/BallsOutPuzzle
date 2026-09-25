"""Solvability and dead-end check for the generated reference levels.

Run from the project Assets directory:
    python _Game/Content/GridAndBox/ReferenceLevels/verify_levels.py [first] [last] [--probes N]

The ball flow is a line-by-line port of BallSimulationSystem / BallCollectionSystem
(sinkReachRows = 2, flat reservoirs). A player action is one box step (or a tap that
wakes a box) followed by letting the balls settle, so a found solution can always be
played by moving slowly. The dead-end probe plays random sequences of actions and
checks that every visited position can still be won.
"""

from pathlib import Path
import heapq
import random
import re
import sys

ROOT = Path(__file__).resolve().parents[4]
CONTENT = ROOT / "_Game" / "Content" / "GridAndBox"
OUTPUT = Path(__file__).resolve().parent
R = 4                 # LevelDefinition.MicroResolution
REACH = 2             # BallCollectionSystem collection reach with sinkReachRows = 2
AXES = {0: ((1, 0), (-1, 0), (0, 1), (0, -1)), 1: ((1, 0), (-1, 0)), 2: ((0, 1), (0, -1))}


def guid_of(path):
    return re.search(r"^guid: ([0-9a-f]{32})$", path.with_suffix(path.suffix + ".meta").read_text(), re.M).group(1)


def load_catalog():
    shapes, colors = {}, {}
    for folder in (CONTENT, OUTPUT):
        for path in folder.glob("Shape_*.asset"):
            cells = [(int(x), int(y)) for x, y in re.findall(r"- \{x: (-?\d+), y: (-?\d+)\}", path.read_text())]
            shapes[guid_of(path)] = cells
        for path in folder.glob("Color_*.asset"):
            colors[guid_of(path)] = re.search(r"^  id: (\S+)", path.read_text(), re.M).group(1)
    return shapes, colors


def field(text, name):
    return re.search(rf"^  {name}: (.*)$", text, re.M).group(1).strip()


def mask(text, name):
    block = re.search(rf"^  {name}:\n    defaultKind: (\d)\n    overrides:(.*?)(?=^  \w)", text, re.M | re.S)
    cells = {(int(x), int(y)): int(k) for x, y, k in
             re.findall(r"cell: \{x: (\d+), y: (\d+)\}\n      kind: (\d)", block.group(2))}
    return int(block.group(1)), cells


def capacity(offsets, slots, dense):
    cell_set = set(offsets)
    seam = slots // 2 if dense else 0
    seams = sum((x + 1, y) in cell_set for x, y in offsets) + sum((x, y + 1) in cell_set for x, y in offsets)
    corners = sum((x + 1, y) in cell_set and (x, y + 1) in cell_set and (x + 1, y + 1) in cell_set
                  for x, y in offsets)
    return slots * slots * len(offsets) + slots * seam * seams + seam * seam * corners


class Level:
    def __init__(self, path, shapes, colors):
        text = path.read_text(encoding="utf-8")
        self.name = path.stem
        self.width = int(field(text, "macroGridWidth"))
        self.lower = int(field(text, "lowerGridHeight"))
        self.upper = int(field(text, "ballAreaMacroHeight"))
        if int(field(text, "hopperMicroRows")) != 0:
            raise ValueError("funnels are not modelled")
        slots = int(field(text, "boxSlotsPerSide"))
        dense = field(text, "denseBoxFill") == "1"
        layers = int(field(text, "fillLayers"))
        self.dividers = [int(v) for v in re.findall(r"\d+", field(text, "reservoirDividerColumns"))]
        lower_default, lower_over = mask(text, "lowerGridMask")
        upper_default, upper_over = mask(text, "ballAreaMask")
        self.usable = set()
        for y in range(self.lower + self.upper):
            for x in range(self.width):
                if y < self.lower:
                    kind = lower_over.get((x, y), lower_default)
                else:
                    kind = upper_over.get((x, y - self.lower), upper_default)
                if kind == 1:
                    self.usable.add((x, y))
        self.color_names = []
        self.boxes = []
        section = text.split("\n  boxes:\n", 1)[1].split("\n  balls:\n", 1)[0]
        for chunk in section.split("  - id: ")[1:]:
            shape = shapes[re.search(r"shape: \{fileID: \d+, guid: (\w+)", chunk).group(1)]
            color = self.color_index(colors[re.search(r"color: \{fileID: \d+, guid: (\w+)", chunk).group(1)])
            x, y = map(int, re.search(r"startingMacroOrigin: \{x: (-?\d+), y: (-?\d+)\}", chunk).groups())
            ice = int(re.search(r"iceCount: (\d+)", chunk).group(1))
            fill = int(re.search(r"initialFillCount: (\d+)", chunk).group(1))
            axis = re.search(r"moveAxis: (\d+)", chunk)
            inner = re.search(r"innerColor: \{fileID: \d+, guid: (\w+)", chunk)
            layer_colors = [self.color_index(colors[inner.group(1)]), color] if inner else [color]
            self.boxes.append(dict(id=chunk.split("\n", 1)[0], cells=shape, colors=layer_colors, x=x, y=y, ice=ice,
                                   fill=fill, cap=capacity(shape, slots, dense) * layers,
                                   axis=int(axis.group(1)) if axis else 0))
        self.balls = {}
        ball_text = text.split("\n  balls:\n", 1)[1].split("\n  palette:", 1)[0]
        for guid, x, y in re.findall(r"color: \{fileID: \d+, guid: (\w+), type: 2\}\n    cell: \{x: (\d+), y: (\d+)\}",
                                     ball_text):
            self.balls[(int(x), int(y))] = self.color_index(colors[guid])

    def color_index(self, name):
        if name not in self.color_names:
            self.color_names.append(name)
        return self.color_names.index(name) + 1


class Board:
    """Static geometry plus the ball-flow port. Mutable state lives in State."""

    def __init__(self, level):
        self.level = level
        self.W = level.width * R
        self.H = (level.lower + level.upper) * R
        self.first = level.lower * R
        W = self.W
        self.is_ball = [False] * (W * self.H)
        for y in range(self.first, self.H):
            for x in range(W):
                self.is_ball[y * W + x] = (x // R, y // R) in level.usable
        # Destination macro index (box lookup) for any micro cell, or -1.
        self.macro_of = {}
        # Per ball cell: (down, left, right) indices, -1 outside, plus travel legality.
        self.nb = {}
        for y in range(self.first, self.H):
            for x in range(W):
                if not self.is_ball[y * W + x]:
                    continue
                odd = y & 1
                cells = ((x, y - 2), (x + odd - 1, y - 1), (x + odd, y - 1))
                entry = []
                for cx, cy in cells:
                    index = cy * W + cx if 0 <= cx < W and 0 <= cy < self.H else -1
                    entry.append((index, self.travel(x, y, cx, cy)))
                    if index >= 0 and cy < self.first:
                        self.macro_of[index] = (cy // R) * level.width + cx // R
                self.nb[y * W + x] = entry
        for x in range(W):
            index = (self.first - 1) * W + x
            self.macro_of[index] = (level.lower - 1) * level.width + x // R

    def travel(self, fx, fy, tx, ty):
        if not self.level.dividers or fy < self.first or ty < self.first:
            return True
        for column in self.level.dividers:
            boundary = column * R
            if (fx < boundary) != (tx < boundary):
                return False
        return True


class State:
    __slots__ = ("balls", "boxes", "occ", "left")

    def copy(self):
        other = State()
        other.balls = self.balls  # copy-on-write: settle() replaces it before mutating
        other.boxes = [list(b) for b in self.boxes]
        other.occ = list(self.occ)
        other.left = self.left
        return other

    def key(self):
        return (self.balls, tuple(tuple(b) for b in self.boxes))


# box record: [x, y, alive, woke, fill, ice, layer]; nested boxes collect colors[layer] in turn
X, Y, ALIVE, WOKE, FILL, ICE, LAYER = range(7)


class Game:
    def __init__(self, level):
        self.level = level
        self.board = Board(level)
        self.box_cells = [b["cells"] for b in level.boxes]
        self.box_colors = [b["colors"] for b in level.boxes]
        self.box_cap = [b["cap"] for b in level.boxes]
        self.box_axis = [b["axis"] for b in level.boxes]
        self.distances = {}

    def initial(self):
        s = State()
        balls = bytearray(self.board.W * self.board.H)
        for (x, y), color in self.level.balls.items():
            balls[y * self.board.W + x] = color
        s.balls = bytes(balls)
        s.boxes = [[b["x"], b["y"], True, False, b["fill"], b["ice"], 0] for b in self.level.boxes]
        s.occ = [-1] * (self.level.width * self.level.lower)
        for i, b in enumerate(self.level.boxes):
            for dx, dy in b["cells"]:
                s.occ[(b["y"] + dy) * self.level.width + b["x"] + dx] = i
        s.left = len(self.level.balls)
        self.settle(s)
        return s

    def can_collect(self, s, i, color):
        b = s.boxes[i]
        return (b[ALIVE] and b[WOKE] and b[ICE] == 0 and b[FILL] < self.box_cap[i]
                and self.box_colors[i][b[LAYER]] == color)

    def collect(self, s, balls, cell, i):
        balls[cell] = 0
        s.left -= 1
        b = s.boxes[i]
        b[FILL] += 1
        if b[FILL] == self.box_cap[i] and b[LAYER] + 1 < len(self.box_colors[i]):
            # Inner layer done: the box empties and goes on in its outer color. No ice cracks.
            b[LAYER] += 1
            b[FILL] = 0
        elif b[FILL] == self.box_cap[i]:
            # Completion releases the footprint and chips every other frozen box.
            b[ALIVE] = False
            for dx, dy in self.box_cells[i]:
                s.occ[(b[Y] + dy) * self.level.width + b[X] + dx] = -1
            for other in s.boxes:
                if other[ALIVE] and other[ICE] > 0:
                    other[ICE] -= 1

    def box_at(self, s, index):
        macro = self.board.macro_of.get(index)
        return -1 if macro is None or macro < 0 else s.occ[macro]

    def may_collect(self, s):
        """Cheap test: can any resting ball enter a box right now?"""
        board, W = self.board, self.board.W
        balls = s.balls
        width = self.level.width
        top = (self.level.lower - 1) * width
        for mx in range(width):
            i = s.occ[top + mx]
            if i < 0 or not (s.boxes[i][WOKE] and s.boxes[i][ICE] == 0):
                continue
            color = self.box_colors[i][s.boxes[i][LAYER]]
            for row, lo, hi in ((0, mx * R - 1, mx * R + R), (1, mx * R, mx * R + R - 1)):
                y = board.first + row
                for x in range(max(0, lo), min(W - 1, hi) + 1):
                    if balls[y * W + x] == color:
                        return True
        return False

    def settle(self, s, limit=4000):
        board = self.board
        W, H, first = board.W, board.H, board.first
        is_ball, nb = board.is_ball, board.nb
        balls = bytearray(s.balls)
        for _ in range(limit):
            changed = False
            for y in range(first, H):
                row = y - first
                base = y * W
                for x in range(W):
                    c = base + x
                    color = balls[c]
                    if not color:
                        continue
                    (d, d_ok), (l, l_ok), (r, r_ok) = nb[c]

                    def into_box(dest):
                        if row >= REACH or dest < 0:
                            return -1
                        i = self.box_at(s, dest)
                        return i if i >= 0 and self.can_collect(s, i, color) else -1

                    def empty(dest):
                        return dest >= 0 and is_ball[dest] and not balls[dest]

                    i = into_box(d)
                    if i >= 0:
                        self.collect(s, balls, c, i)
                        changed = True
                        continue

                    def enter_ok(dest, ok):
                        return ok and (empty(dest) or into_box(dest) >= 0)

                    if empty(l) and empty(r) and enter_ok(d, d_ok):
                        self.enter(s, balls, c, d, color, into_box(d))
                        changed = True
                        continue
                    can_left = enter_ok(l, l_ok)
                    can_right = enter_ok(r, r_ok)
                    if can_right and ((r // W) & 1) == 1 and empty(r):
                        # DefersTo: an odd-row hole belongs to the ball at its upper-right.
                        if x + 1 < W and balls[c + 1]:
                            can_right = False
                    if not can_left and not can_right:
                        if row < REACH:
                            i = self.box_at(s, (first - 1) * W + x)
                            if i >= 0 and self.can_collect(s, i, color):
                                self.collect(s, balls, c, i)
                                changed = True
                        continue
                    target = (r if ((r // W) & 1) == 0 else l) if can_left and can_right else (l if can_left else r)
                    self.enter(s, balls, c, target, color, into_box(target))
                    changed = True
            if not changed:
                s.balls = bytes(balls)
                return
        raise RuntimeError("balls did not settle")

    def enter(self, s, balls, c, dest, color, box):
        if box >= 0:
            self.collect(s, balls, c, box)
        else:
            balls[dest] = color
            balls[c] = 0

    def can_place(self, s, i, x, y):
        width, lower = self.level.width, self.level.lower
        for dx, dy in self.box_cells[i]:
            cx, cy = x + dx, y + dy
            if cy < 0 or cy >= lower or (cx, cy) not in self.level.usable:
                return False
            other = s.occ[cy * width + cx]
            if other >= 0 and other != i:
                return False
        return True

    def moves(self, s):
        """Yield (label, next_state) for every player action from a settled state."""
        width, lower = self.level.width, self.level.lower
        for i, b in enumerate(s.boxes):
            if not b[ALIVE] or b[ICE] > 0:
                continue
            if not b[WOKE] and any(b[Y] + dy == lower - 1 for _, dy in self.box_cells[i]):
                n = s.copy()
                n.boxes[i][WOKE] = True
                if self.may_collect(n):
                    self.settle(n)
                yield (i, "tap"), n
            for dx, dy in AXES[self.box_axis[i]]:
                if not self.can_place(s, i, b[X] + dx, b[Y] + dy):
                    continue
                n = s.copy()
                nb = n.boxes[i]
                for cx, cy in self.box_cells[i]:
                    n.occ[(nb[Y] + cy) * width + nb[X] + cx] = -1
                nb[X] += dx
                nb[Y] += dy
                nb[WOKE] = True
                for cx, cy in self.box_cells[i]:
                    n.occ[(nb[Y] + cy) * width + nb[X] + cx] = i
                if self.may_collect(n):
                    self.settle(n)
                yield (i, (dx, dy)), n

    def collects_at(self, s, i, x, y):
        """Would box i, awake at origin (x, y), take a ball from the resting pile?"""
        board, W, lower = self.board, self.board.W, self.level.lower
        b = s.boxes[i]
        if b[ICE] > 0:
            return False
        color = self.box_colors[i][b[LAYER]]
        for dx, dy in self.box_cells[i]:
            if y + dy != lower - 1:
                continue
            mx = x + dx
            for row, lo, hi in ((0, mx * R - 1, mx * R + R), (1, mx * R, mx * R + R - 1)):
                base = (board.first + row) * W
                for cx in range(max(0, lo), min(W - 1, hi) + 1):
                    if s.balls[base + cx] == color:
                        return True
        return False

    def relocations(self, s):
        """Yield (label, next_state): one box carried to any spot it can reach in one drag
        without passing under a matching ball, or a tap on a sleeping top-row box."""
        width, lower = self.level.width, self.level.lower
        for i, b in enumerate(s.boxes):
            if not b[ALIVE] or b[ICE] > 0:
                continue
            if not b[WOKE] and any(b[Y] + dy == lower - 1 for _, dy in self.box_cells[i]):
                n = s.copy()
                n.boxes[i][WOKE] = True
                if self.may_collect(n):
                    self.settle(n)
                yield (i, "tap"), n
            start = (b[X], b[Y])
            if not b[WOKE] and self.collects_at(s, i, *start):
                continue  # grabbing it wakes it where it stands; the tap covers that
            reached = {start}
            frontier = [start]
            while frontier:
                x, y = frontier.pop()
                for dx, dy in AXES[self.box_axis[i]]:
                    target = (x + dx, y + dy)
                    if target in reached or not self.can_place(s, i, *target):
                        continue
                    reached.add(target)
                    # A spot that collects ends the drag: the balls start flowing there.
                    if not self.collects_at(s, i, *target):
                        frontier.append(target)
            for target in reached:
                if target == start:
                    continue
                n = s.copy()
                nb = n.boxes[i]
                for cx, cy in self.box_cells[i]:
                    n.occ[(nb[Y] + cy) * width + nb[X] + cx] = -1
                nb[X], nb[Y] = target
                nb[WOKE] = True
                for cx, cy in self.box_cells[i]:
                    n.occ[(nb[Y] + cy) * width + nb[X] + cx] = i
                if self.may_collect(n):
                    self.settle(n)
                yield (i, ("to",) + target), n

    def estimate(self, s):
        """Box steps still needed: each live box to the nearest top-row spot showing its color."""
        board, W, width, lower = self.board, self.board.W, self.level.width, self.level.lower
        exposed = []
        for mx in range(width):
            colors = set()
            for row, lo, hi in ((0, mx * R - 1, mx * R + R), (1, mx * R, mx * R + R - 1)):
                y = board.first + row
                for x in range(max(0, lo), min(W - 1, hi) + 1):
                    if s.balls[y * W + x]:
                        colors.add(s.balls[y * W + x])
            exposed.append(colors)
        total = 0
        for i, b in enumerate(s.boxes):
            if not b[ALIVE]:
                continue
            color = self.box_colors[i][b[LAYER]]
            best = 4
            for tx in range(width):
                if color not in exposed[tx]:
                    continue
                for dx, dy in self.box_cells[i]:
                    step = self.distance(i, (b[X], b[Y]), (tx - dx, lower - 1 - dy))
                    if step is not None:
                        best = min(best, step)
            total += best
        return total

    def distance(self, i, start, goal):
        """Steps for box i between origins on the empty board (walls and axis lock only)."""
        key = (tuple(self.box_cells[i]), self.box_axis[i])
        table = self.distances.setdefault(key, {})
        if start not in table:
            empty = State()
            empty.occ = [-1] * (self.level.width * self.level.lower)
            reach = {start: 0}
            queue = [start]
            for x, y in queue:
                for dx, dy in AXES[self.box_axis[i]]:
                    target = (x + dx, y + dy)
                    if target not in reach and self.can_place(empty, i, *target):
                        reach[target] = reach[(x, y)] + 1
                        queue.append(target)
            table[start] = reach
        return table[start].get(goal)

    def solve_any(self, start, budget=240000):
        """Try a few search orders; the first one that finishes (found or exhausted) decides."""
        for weight, limit in ((0, budget // 4), (8, budget // 4), (30, budget // 2)):
            path, expanded, exhausted = self.solve(start, limit, weight)
            if path is not None or exhausted:
                return path, expanded, exhausted
        return None, expanded, False

    def solve(self, start, limit=200000, weight=8):
        """Weighted best-first search over drags (see relocations). Every single-step
        sequence is also a sequence of drags, so an exhausted search proves a dead end.
        Returns (path or None, expanded, exhausted)."""
        seen = {start.key(): None}
        heap = [(start.left + weight * self.estimate(start), 0, 0, start)]
        counter = 1
        parents = {}
        expanded = 0
        while heap:
            _, depth, _, s = heapq.heappop(heap)
            if s.left == 0:
                path = []
                k = s.key()
                while parents.get(k):
                    k, action = parents[k]
                    path.append(action)
                return path[::-1], expanded, True
            expanded += 1
            if expanded > limit:
                return None, expanded, False
            for action, n in self.relocations(s):
                k = n.key()
                if k in seen:
                    continue
                seen[k] = None
                parents[k] = (s.key(), action)
                counter += 1
                heapq.heappush(heap, (n.left + weight * self.estimate(n), depth + 1, counter, n))
        return None, expanded, True

    def describe(self, action):
        i, move = action
        name = self.level.boxes[i]["id"]
        if move == "tap":
            return f"tap {name}"
        if move[0] == "to":
            return f"{name}->({move[1]},{move[2]})"
        return f"{name} {dict((((1, 0), 'R'), ((-1, 0), 'L'), ((0, 1), 'U'), ((0, -1), 'D')))[move]}"


def probe(game, start, rounds, rng, limit):
    """Random play-outs; every position reached must still be winnable."""
    stuck, unknown = [], 0
    for _ in range(rounds):
        s = start
        history = []
        for _ in range(rng.randint(3, 40)):
            options = list(game.relocations(s))
            if not options:
                break
            # Favour actions that collect, like a player who is making progress.
            progress = [o for o in options if o[1].left < s.left]
            action, s = rng.choice(progress if progress and rng.random() < 0.6 else options)
            history.append(action)
            if s.left == 0:
                break
        if s.left == 0:
            continue
        path, _, exhausted = game.solve_any(s, limit)
        if path is None and not exhausted:
            path, _, exhausted = game.solve_any(s, limit * 10)
        if path is None and exhausted:
            stuck.append(history)
        elif path is None:
            unknown += 1
    return stuck, unknown


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    rounds = 0
    if "--probes" in sys.argv:
        rounds = int(sys.argv[sys.argv.index("--probes") + 1])
        args = [a for a in args if a != str(rounds)]
    first = int(args[0]) if args else 16
    last = int(args[1]) if len(args) > 1 else 99
    shapes, colors = load_catalog()
    failures = 0
    for path in sorted(OUTPUT.glob("Level_*_Reference.asset")):
        number = int(path.stem.split("_")[1])
        if not first <= number <= last:
            continue
        game = Game(Level(path, shapes, colors))
        start = game.initial()
        path_found, expanded, exhausted = game.solve_any(start)
        if path_found is None:
            failures += 1
            print(f"{path.stem}: {'UNSOLVABLE' if exhausted else 'no solution within limit'} ({expanded} states)")
            continue
        line = f"{path.stem}: solved in {len(path_found)} actions ({expanded} states searched)"
        if rounds:
            stuck, unknown = probe(game, start, rounds, random.Random(number), 60000)
            line += f"; dead-end probes: {len(stuck)} stuck / {unknown} unknown of {rounds}"
            if stuck:
                failures += 1
                line += "\n    e.g. " + ", ".join(game.describe(a) for a in stuck[0])
        print(line)
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
