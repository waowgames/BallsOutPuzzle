"""Levels 87-186: seeded layouts built from every mechanic, emitted in build_levels.py's level format.

build_levels.py appends these to LEVELS. Every level is generated from its own seed, so a run always
writes the same boards. Solvability comes from construction rather than a search: the generator
plays each board one box at a time and only keeps boards where that play never stalls.

- The box whose colour comes next is dragged through free cells only: every other box stays where it
  started, frozen and padlocked boxes stay shut and stones stand until enough boxes are done. Nobody
  has to be pushed aside, so no box can be trapped behind another.
- Balls on the reservoir floor never roll sideways, so the box must be able to stand under every
  column its balls can rest on: two columns either side of a conveyor's gate, or every column of its
  chamber.
- A conveyor pours the boxes' colours in exactly that order, whole box by whole box, so an earlier
  colour always lies under a later one. Its gate stays two columns clear of the side walls, so the
  pile never leans on a wall, where a ball held up by the wall could get a later colour under it.
  Chamber levels give every colour a chamber of its own.
- Chained boxes move only within reach of their unfinished partner; column-locked boxes appear only
  over chambers (a conveyor's pile spreads wider than one column).
"""

from collections import Counter
import random

SHAPES = {
    "S": [(0, 0)],
    "H2": [(0, 0), (1, 0)],
    "V2": [(0, 0), (0, 1)],
    "H3": [(0, 0), (1, 0), (2, 0)],
    "V3": [(0, 0), (0, 1), (0, 2)],
    "H4": [(0, 0), (1, 0), (2, 0), (3, 0)],
    "Q": [(0, 0), (1, 0), (0, 1), (1, 1)],
    "R6": [(x, y) for y in range(2) for x in range(3)],
    "L3A": [(0, 0), (0, 1), (1, 1)],
    "L3B": [(1, 0), (0, 1), (1, 1)],
    "L3C": [(0, 0), (1, 0), (0, 1)],
    "L3D": [(0, 0), (1, 0), (1, 1)],
    "L4A": [(0, 0), (0, 1), (0, 2), (1, 2)],
    "L4B": [(1, 0), (1, 1), (1, 2), (0, 2)],
    "T4": [(1, 0), (0, 1), (1, 1), (2, 1)],
    "TD": [(0, 1), (1, 1), (2, 1), (1, 0)],
    "X5": [(1, 0), (0, 1), (1, 1), (2, 1), (1, 2)],
}
MIRROR = {"L3A": "L3B", "L3B": "L3A", "L3C": "L3D", "L3D": "L3C", "L4A": "L4B", "L4B": "L4A"}
SINGLE_COLUMN = {"S", "V2", "V3"}
COLORS = ["R", "B", "G", "Y", "P", "O", "C", "U", "W", "K"]
LOCK_NAMES = {"R": "red", "B": "blue", "G": "green", "Y": "yellow", "P": "pink", "O": "orange",
              "C": "cyan", "U": "lilac", "W": "white", "K": "black"}
# Shape weights per difficulty: harder boards lean on bigger, awkward pieces.
SHAPE_WEIGHTS = {
    0: {"S": 5, "H2": 7, "V2": 6, "H3": 3, "V3": 2, "Q": 5, "R6": 1, "L3A": 1, "L3B": 1, "L3C": 1, "L3D": 1,
        "T4": 1, "TD": 1},
    1: {"S": 4, "H2": 6, "V2": 5, "H3": 3, "V3": 2, "Q": 4, "R6": 2, "L3A": 2, "L3B": 2, "L3C": 2, "L3D": 2,
        "L4A": 1, "L4B": 1, "T4": 2, "TD": 2, "H4": 1, "X5": 1},
    2: {"S": 4, "H2": 5, "V2": 5, "H3": 3, "V3": 3, "Q": 3, "R6": 2, "L3A": 2, "L3B": 2, "L3C": 2, "L3D": 2,
        "L4A": 2, "L4B": 2, "T4": 2, "TD": 2, "H4": 1, "X5": 1},
}
MECHANICS = ("ice", "nested", "lock", "chain", "axis", "stone")
MIN_FREE_SHARE = 0.36
MAX_CHAMBER_ROWS = 7


def capacity(shape):
    """Balls a box takes with 3 slots per side, dense fill and two layers (mirrors build_levels.capacity)."""
    cells = set(SHAPES[shape])
    seams = sum((x + 1, y) in cells for x, y in cells) + sum((x, y + 1) in cells for x, y in cells)
    corners = sum((x + 1, y) in cells and (x, y + 1) in cells and (x + 1, y + 1) in cells for x, y in cells)
    return 2 * (9 * len(cells) + 3 * seams + corners)


def extent(shape):
    cells = SHAPES[shape]
    return max(x for x, _ in cells) + 1, max(y for _, y in cells) + 1


def footprint(shape, x, y):
    return {(x + dx, y + dy) for dx, dy in SHAPES[shape]}


class Box:
    def __init__(self, shape, x, y):
        self.shape, self.x, self.y = shape, x, y
        self.color = None
        self.ice = 0
        self.axis = None
        self.inner = None
        self.lock = None
        self.key = None
        self.link = None
        self.partner = None

    @property
    def cells(self):
        return footprint(self.shape, self.x, self.y)

    def top(self, lower):
        return any(cy == lower - 1 for _, cy in self.cells)

    def needs(self):
        full = capacity(self.shape)
        return [(self.inner, full), (self.color, full)] if self.inner else [(self.color, full)]

    def entry(self):
        return (self.shape, self.color, self.x, self.y, self.ice, 0, self.axis, self.inner,
                self.lock, self.key, self.link)


class Plan:
    """A board in progress: boxes, stones and the reservoir the boxes collect from."""

    def __init__(self, rng, n, difficulty, kind, width, lower):
        self.rng, self.n, self.difficulty, self.kind = rng, n, difficulty, kind
        self.width, self.lower = width, lower
        self.boxes = []
        self.obstacles = []  # (x, y, w, h, count)
        self.gate = None
        self.chambers = None  # [(first column, last column exclusive, [colors])]

    # --- layout -------------------------------------------------------------------------------------
    def occupied(self):
        cells = set()
        for b in self.boxes:
            cells |= b.cells
        for ox, oy, ow, oh, _ in self.obstacles:
            cells |= {(ox + dx, oy + dy) for dx in range(ow) for dy in range(oh)}
        return cells

    def fits(self, cells, taken):
        return all(0 <= x < self.width and 0 <= y < self.lower and (x, y) not in taken for x, y in cells)

    def place(self, shape, x, y, symmetric, taken):
        cells = footprint(shape, x, y)
        if not self.fits(cells, taken):
            return 0
        placed = [(shape, x, y, cells)]
        if symmetric:
            w, _ = extent(shape)
            mirror = MIRROR.get(shape, shape)
            mx = self.width - x - w
            mcells = footprint(mirror, mx, y)
            if mcells != cells:
                if mcells & cells or not self.fits(mcells, taken):
                    return 0
                placed.append((mirror, mx, y, mcells))
        for s, px, py, c in placed:
            self.boxes.append(Box(s, px, py))
            taken |= c
        return sum(len(c) for *_, c in placed)

    def layout(self, share, symmetric):
        rng, weights = self.rng, SHAPE_WEIGHTS[self.difficulty]
        names = list(weights)
        taken = set()
        target = round(self.width * self.lower * share)
        # The top row starts with a few boxes, so the first colours have somewhere to go.
        for _ in range(40):
            if sum(b.top(self.lower) for b in self.boxes) >= (3 if self.width > 6 else 2):
                break
            shape = rng.choice(["S", "H2", "H2", "V2", "Q", "H3", "L3A", "L3C"])
            w, h = extent(shape)
            self.place(shape, rng.randint(0, self.width - w), self.lower - h, symmetric, taken)
        for _ in range(600):
            if len(taken) >= target:
                break
            shape = rng.choices(names, [weights[s] for s in names])[0]
            w, h = extent(shape)
            if w > self.width or h > self.lower:
                continue
            if len(taken) + len(SHAPES[shape]) * (2 if symmetric else 1) > target + 2:
                continue
            self.place(shape, rng.randint(0, self.width - w), rng.randint(0, self.lower - h), symmetric, taken)
        self.boxes.sort(key=lambda b: (-b.y, b.x))

    def add_stones(self, count):
        rng = self.rng
        sizes = [(1, 1), (1, 1), (2, 1), (1, 2), (2, 2), (3, 1)]
        for _ in range(count * 30):
            if len(self.obstacles) >= count:
                break
            w, h = rng.choice(sizes)
            if self.lower - 1 - h < 1 or w > self.width:
                continue
            x = rng.randint(0, self.width - w)
            y = rng.randint(1, self.lower - 1 - h)  # never on the top row
            cells = {(x + dx, y + dy) for dx in range(w) for dy in range(h)}
            if cells & self.occupied():
                continue
            if self.width * self.lower - len(self.occupied()) - len(cells) < self.width * self.lower * MIN_FREE_SHARE:
                continue
            limit = max(1, min(len(self.boxes) // 2, 2 + self.difficulty * 2))
            self.obstacles.append((x, y, w, h, rng.randint(1, limit)))

    # --- colours and mechanics ----------------------------------------------------------------------
    def paint(self, palette):
        rng = self.rng
        order = list(range(len(self.boxes)))
        rng.shuffle(order)
        # Every colour gets at least one box, the rest are spread so no colour dominates.
        for slot, index in enumerate(order):
            if slot < len(palette):
                self.boxes[index].color = palette[slot]
            else:
                counts = Counter(b.color for b in self.boxes if b.color)
                least = min(counts[c] for c in palette)
                self.boxes[index].color = rng.choice([c for c in palette if counts[c] <= least + 1])

    def add_ice(self, count):
        rng = self.rng
        candidates = [b for b in self.boxes if not b.top(self.lower) and not b.lock]
        rng.shuffle(candidates)
        for b in candidates[:count]:
            b.ice = rng.randint(1, max(1, min(len(self.boxes) // 2, 2 + 2 * self.difficulty)))

    def add_nested(self, count, palette):
        rng = self.rng
        candidates = [b for b in self.boxes if len(SHAPES[b.shape]) >= 2 and not b.inner and b.axis is None]
        rng.shuffle(candidates)
        for b in candidates[:count]:
            b.inner = rng.choice([c for c in palette if c != b.color])

    def add_lock(self, locks):
        rng = self.rng
        for _ in range(locks):
            candidates = [b for b in self.boxes if not b.top(self.lower) and not b.lock and not b.key
                          and len(SHAPES[b.shape]) >= 2]
            if not candidates:
                return
            candidates.sort(key=lambda b: -len(SHAPES[b.shape]))
            target = rng.choice(candidates[:3])
            name = LOCK_NAMES[target.color]
            if any(b.lock == name for b in self.boxes):
                name += " " + str(sum(b.lock is not None for b in self.boxes) + 1)
            keys = [b for b in self.boxes if b is not target and not b.lock and not b.key]
            if not keys:
                return
            rng.shuffle(keys)
            # Keys near the top first, so the padlock opens partway through the level.
            keys.sort(key=lambda b: -b.y)
            chosen = keys[:rng.randint(1, min(3, len(keys)))]
            target.lock = name
            for b in chosen:
                b.key = name

    def add_chains(self, pairs):
        rng = self.rng
        made = 0
        order = self.boxes[:]
        rng.shuffle(order)
        for a in order:
            if made >= pairs:
                return
            if a.link or a.lock:
                continue
            for b in order:
                if b is a or b.link or b.lock:
                    continue
                gap = chain_gap(a, b)
                if gap is None:
                    continue
                length = max(1, gap)
                if length > 3 or (a.ice and b.ice):
                    continue
                length = max(length, rng.choice([1, 1, 2, 2, 3]))
                name = "abcdef"[made]
                a.link = b.link = name
                a.partner, b.partner = b, a
                self.chain_lengths[name] = length
                made += 1
                break

    def add_axis(self, count, rail_columns=None):
        """Rails: a column-locked box first (one that has to rise to the top), then sideways-only boxes
        on the top row."""
        rng = self.rng
        made = 0
        taken = self.occupied()
        column = [b for b in self.boxes if b.shape in SINGLE_COLUMN and not b.link and not b.inner and b.axis is None
                  and not b.top(self.lower) and (rail_columns is None or b.x in rail_columns)
                  and all((b.x, y) not in taken for y in range(max(cy for _, cy in b.cells) + 1, self.lower))]
        if column and rng.random() < 0.7:
            rng.choice(column).axis = "V"
            made += 1
        top = [b for b in self.boxes if b.top(self.lower) and not b.link and not b.inner and b.axis is None]
        rng.shuffle(top)
        for b in top[:count - made]:
            b.axis = "H"

    def roomy(self):
        return self.width * self.lower - len(self.occupied()) >= self.width * self.lower * MIN_FREE_SHARE

    # --- reachability ---------------------------------------------------------------------------------
    def standing(self, completions):
        cells = set()
        for ox, oy, ow, oh, count in self.obstacles:
            if completions < count:
                cells |= {(ox + dx, oy + dy) for dx in range(ow) for dy in range(oh)}
        return cells

    def gated(self, b, done):
        if b.ice > len(done):
            return True
        return bool(b.lock) and any(k.key == b.lock and k not in done for k in self.boxes)

    def sweep(self, color):
        """Columns a box must be able to stand under to take every ball of its colour.
        Balls resting on the reservoir floor never roll sideways, so the box has to reach all of them:
        the conveyor's pile spreads up to two columns either side of the gate (three rows of reservoir,
        half a column per row), and a chamber leaves balls on the floor of every one of its columns."""
        if self.kind == "conveyor":
            return {c for c in range(self.gate - 2, self.gate + 3) if 0 <= c < self.width}
        return {c for first, last, colors in self.chambers if color in colors for c in range(first, last)}

    def covered(self, b, walls, partner):
        """Top-row columns box b can stand under, dragged through free cells only. A chained box has to
        stay within its chain's reach of an unfinished partner, which does not move."""
        moves = {None: ((1, 0), (-1, 0), (0, 1), (0, -1)), "H": ((1, 0), (-1, 0)), "V": ((0, 1), (0, -1))}[b.axis]
        slack = self.chain_lengths.get(b.link) if partner else None
        start = (b.x, b.y)
        seen = {start}
        queue = [start]
        columns = set()
        for x, y in queue:
            columns |= {cx for cx, cy in footprint(b.shape, x, y) if cy == self.lower - 1}
            for dx, dy in moves:
                nxt = (x + dx, y + dy)
                if nxt in seen:
                    continue
                cells = footprint(b.shape, *nxt)
                if not self.fits(cells, walls):
                    continue
                if partner and not within(cells, partner, slack):
                    continue
                seen.add(nxt)
                queue.append(nxt)
        return columns

    def serve(self):
        """Constructive play, one box at a time: the box whose colour comes next is dragged through
        free cells only (every other box stays where it started, stones stand until enough boxes are
        done) under every column its balls can rest on, and is filled before the next one moves.
        Returns the serving order, or None when the board stalls."""
        rng = self.rng
        order = []
        spread = {0: 1.5, 1: 4.0, 2: 6.0}[self.difficulty]
        while len(order) < len(self.boxes):
            done = set(order)
            stones = self.standing(len(done))
            options = []
            for b in self.boxes:
                if b in done or self.gated(b, done):
                    continue
                walls = set(stones)
                for other in self.boxes:
                    if other is not b and other not in done:
                        walls |= other.cells
                partner = b.partner.cells if b.partner is not None and b.partner not in done else None
                columns = self.covered(b, walls, partner)
                if all(self.sweep(color) <= columns for color, _ in b.needs()):
                    # Easy boards serve the top of the board first; harder ones jump around more.
                    options.append((-b.y + rng.random() * spread - 2 * (b.key is not None), b))
            if not options:
                return None
            order.append(min(options, key=lambda item: item[0])[1])
        return order

    def conveyor_queue(self):
        """The belt pours the boxes' colours in serving order: a box's balls all come before the next
        box's, so an earlier colour always lies under a later one and is never buried."""
        order = self.serve()
        if order is None:
            return None
        merged = []
        for b in order:
            for color, count in b.needs():
                if merged and merged[-1][0] == color:
                    merged[-1] = (color, merged[-1][1] + count)
                else:
                    merged.append((color, count))
        return merged

    def allot_chambers(self, needed):
        """One chamber per colour, sized by how many balls it holds; left to right in a random order."""
        colors = list(needed)
        if len(colors) > self.width:
            return None
        columns = {c: 1 for c in colors}
        for _ in range(self.width - len(colors)):
            widest = max(colors, key=lambda c: needed[c] / columns[c])
            columns[widest] += 1
        self.rng.shuffle(colors)
        chambers, first = [], 0
        for c in colors:
            chambers.append([first, first + columns[c], [c]])
            first += columns[c]
        return chambers


def within(cells, other, slack):
    """Chain reach: at most `slack` free cells between the two boxes on either axis."""
    ax0, ax1 = min(x for x, _ in cells), max(x for x, _ in cells)
    ay0, ay1 = min(y for _, y in cells), max(y for _, y in cells)
    bx0, bx1 = min(x for x, _ in other), max(x for x, _ in other)
    by0, by1 = min(y for _, y in other), max(y for _, y in other)
    return max(bx0 - ax1, ax0 - bx1) - 1 <= slack and max(by0 - ay1, ay0 - by1) - 1 <= slack


def chain_gap(a, b):
    """Free cells between two boxes on the wider axis, or None when they overlap on neither."""
    ac, bc = a.cells, b.cells
    ax0, ax1 = min(x for x, _ in ac), max(x for x, _ in ac)
    ay0, ay1 = min(y for _, y in ac), max(y for _, y in ac)
    bx0, bx1 = min(x for x, _ in bc), max(x for x, _ in bc)
    by0, by1 = min(y for _, y in bc), max(y for _, y in bc)
    gap_x = max(bx0 - ax1, ax0 - bx1) - 1
    gap_y = max(by0 - ay1, ay0 - by1) - 1
    return max(gap_x, gap_y, 0) if gap_x <= 3 and gap_y <= 3 else None


def plan_level(n, index, difficulty, kind, rng):
    stage = index / 99  # 0 at level 87, 1 at level 186
    if kind == "conveyor":
        width = rng.choice([6, 7, 7, 8] if difficulty == 0 else [7, 7, 8])
        lower = rng.choice([5, 6] if difficulty == 0 else [5, 6, 6])
    else:
        width = rng.choice([6, 7, 7, 8])
        lower = rng.choice([5, 5, 6])
    plan = Plan(rng, n, difficulty, kind, width, lower)
    plan.chain_lengths = {}
    share = rng.uniform(0.44, 0.52) + 0.03 * difficulty + 0.03 * stage
    plan.layout(min(share, 0.6), symmetric=rng.random() < 0.55)
    if len(plan.boxes) < 5:
        return None
    if kind == "conveyor":
        # The pile under the gate spreads at most two columns either way (three rows of reservoir, half
        # a column per row). Kept off the side walls: a ball resting against a wall is held up by it,
        # a later colour fills the gap underneath, and the earlier ball ends up buried on top.
        plan.gate = rng.randint(2, width - 3)

    colors = rng.sample(COLORS, min(len(plan.boxes) - 1, rng.choice([4, 5, 5, 6] if difficulty else [4, 4, 5, 5])))
    plan.paint(colors)

    # Mechanics: a couple per level early on, up to four on the very hard boards.
    count = {0: rng.choice([1, 2, 2]), 1: rng.choice([2, 3]), 2: rng.choice([3, 4])}[difficulty]
    if stage > 0.5 and difficulty == 0:
        count = min(3, count + 1)
    featured = rng.sample(MECHANICS, count)
    if "stone" in featured:
        plan.add_stones(rng.randint(1, 1 + difficulty))
    if "ice" in featured:
        plan.add_ice(rng.randint(1, 1 + difficulty))
    if "lock" in featured:
        plan.add_lock(2 if difficulty == 2 and rng.random() < 0.5 else 1)
    if "nested" in featured:
        plan.add_nested(rng.randint(1, 1 + (difficulty > 0)), colors)
    if "chain" in featured:
        plan.add_chains(rng.randint(1, 1 + (difficulty > 0)))
    if "axis" in featured:
        # A column-locked box could never follow the conveyor's pile sideways, so belts get
        # sideways-only boxes alone.
        plan.add_axis(rng.randint(1, 2), set() if kind == "conveyor" else None)
    if not plan.roomy():
        return None

    needed = Counter()
    for b in plan.boxes:
        for color, amount in b.needs():
            needed[color] += amount
    level = dict(n=n, width=width, lower=lower, fill_layers=2,
                 boxes=[b.entry() for b in plan.boxes], layers=[[]])
    if difficulty:
        level["difficulty"] = difficulty
    if plan.obstacles:
        level["obstacles"] = list(plan.obstacles)
    if plan.chain_lengths:
        level["chains"] = dict(plan.chain_lengths)

    if kind == "conveyor":
        queue = plan.conveyor_queue()
        if queue is None:
            return None
        total = sum(count for _, count in queue)
        runs = 4 if difficulty == 2 or total > 620 else 3 if total > 260 else 2
        level["conveyor"] = (plan.gate, queue, runs)
        return level, total

    chambers = plan.allot_chambers(needed)
    if chambers is None:
        return None
    plan.chambers = chambers
    # Every colour keeps a chamber of its own: two colours sharing one would rest on each other's floor,
    # and a box could not finish before the other colour's box has cleared its way.
    if plan.serve() is None:
        return None
    level["boxes"] = [b.entry() for b in plan.boxes]
    level["dividers"] = [first for first, _, _ in chambers[1:]]
    level["layers"] = [[(c, None) for c in cs] for _, _, cs in chambers]
    merged = chambers
    # Chamber height: every chamber holds at most three quarters of its rows (build_levels.reservoir_height).
    rows = max(-(-sum(needed[c] for c in cs) // (12 * (last - first))) for first, last, cs in merged)
    tube = None
    if rng.random() < 0.45 or rows > MAX_CHAMBER_ROWS:
        # A tube over the fullest single-colour chamber pours part of that colour from above.
        options = [(sum(needed[c] for c in cs) / (last - first), first, last, cs[0])
                   for first, last, cs in merged if len(cs) == 1]
        if options:
            _, first, last, color = max(options)
            tube = ((first + last - 1) // 2, color, needed[color] * rng.choice([2, 3, 4]) // 7)
    if tube:
        column, color, amount = tube
        level["feeders"] = [(column, [(color, amount)])]
    total = sum(needed.values())
    return level, total


def difficulty_of(index):
    step = index % 10
    return 2 if step == 9 else 1 if step in (3, 6, 8) else 0


def kind_of(index):
    return "chamber" if index % 10 in (1, 5, 8) else "conveyor"


def generated_levels(first=87, count=100):
    levels = []
    for index in range(count):
        n = first + index
        difficulty, kind = difficulty_of(index), kind_of(index)
        # Ball budgets: plenty of balls, fewer on the timed hard boards so the clock stays fair.
        low, high = {0: (480, 760), 1: (440, 660), 2: (380, 560)}[difficulty]
        if index < 10:
            high -= 80
        for attempt in range(40000):
            rng = random.Random(n * 7919 + attempt)
            result = plan_level(n, index, difficulty, kind, rng)
            if result is None:
                continue
            level, total = result
            if not low <= total <= high:
                continue
            if kind == "chamber":
                rows = reservoir_rows(level)
                if rows > MAX_CHAMBER_ROWS:
                    continue
            levels.append(level)
            break
        else:
            raise RuntimeError(f"Level {n}: no layout found")
    return levels


def reservoir_rows(level):
    needed = Counter()
    for shape, color, *_rest in level["boxes"]:
        inner = _rest[5]
        needed[color] += capacity(shape)
        if inner:
            needed[inner] += capacity(shape)
    for _, queue in level.get("feeders", ()):
        for color, amount in queue:
            needed[color] -= amount
    bounds = [0] + level["dividers"] + [level["width"]]
    rows = 2
    for (first, last), chamber in zip(zip(bounds, bounds[1:]), level["layers"]):
        quota = sum(needed[c] for c, _ in chamber)
        rows = max(rows, -(-quota // (4 * (last - first) * 3)))  # ceil(quota / (cols*4*0.75) / 4)
    return rows
