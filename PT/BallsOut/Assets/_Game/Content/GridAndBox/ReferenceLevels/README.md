# Reference levels 1–66

Levels 1–40 recreate the first forty Sand Blocks board compositions in the Balls Out grid system; levels 41–56 follow the reference boards' mechanics and difficulty without copying their layouts, and levels 57–66 introduce chained boxes. `Level_01_Reference.asset` through `Level_66_Reference.asset` are the first sixty-six entries in `Assets/Scripts/LevelConfig.asset`.

The [board images](https://sand-blocks.org/levels) and [level notes](https://levelsolve.com/sand-blocks-drop-puzzle/) were used as visual references. Each level has authored box shapes, starting cells, colors, reservoir art, and ice counts. Level 1 and level 8 also start with a partially filled receiver. The generator keeps every color's ball count equal to the remaining receiver capacity.

Run `python Assets/_Game/Content/GridAndBox/ReferenceLevels/build_levels.py` from the project root to rebuild the assets. GUIDs are stable across runs.

These are adaptations to this game's mechanics. The reference's separate stacked chambers in level 11 and independent ice slabs in later levels are represented with this project's single reservoir and frozen receiver boxes. Sand blocks that sit mid-board in the reference (levels 21–40) become chambers of the top reservoir or partly filled boxes. Level 29's two reservoirs become two chambers over an H-shaped board, and level 30's stepped pit becomes a flat board.

## Levels 16–40

- **Axis-locked boxes** (`moveAxis`, from level 16): a double-headed arrow on the lid shows the only axis the box can slide along. Balls never enter the lower grid, so a sideways-only box must start in the top row, and a column-locked box's column holds every ball of its color.
- **Nested boxes** (`innerColor`, from level 26): an inner tray framed by the outer color. The box collects a full load of the inner color, the tray and its balls pop away, and then it collects a full load of the outer color. Both loads count toward their colors' capacities; only the final completion cracks ice. An inner-color ball must never end up under balls that only the nested box's outer layer could take. Balls against a wall or divider can only drop straight down, so they get pinned above whatever slides in beneath them. Levels 27–28 keep every inner-color ball in the two floor rows a box draws from. Level 26 introduces the mechanic with two big trays whose colors cross over (a red box with a blue tray, a blue box with a red tray): a pinned ball always sits above a color the other box can take.
- **Padlocks and keys** (`startsLocked` + `lockId`, `keyId`, from level 40): a padlocked box cannot move or collect. Every box whose `keyId` names that lock carries a gold key on its lid. When it completes, the key flies into the padlock and the counter drops. The last key pops the shackle and frees the box. Build with `box(..., lock="name")` and `box(..., key="name")`.
- Layered chambers (`layers=`) fill each chamber bottom to top, one color band after another.

## Levels 41–56

- Two colour bands in one narrow chamber mix where they meet: balls pinned against a wall drop behind the next band, and a box waiting on them can block the board. Most of these levels give each colour a chamber of its own, so the puzzle is getting every box under its chamber in a workable order. Every other level from 42 on shares a wider chamber between colours laid out in a pattern instead of bands (`mix={chamber: "stripes" | "checker" | "diagonal" | "zigzag"}`), which keeps each colour reachable from the floor as the pile drains.
- **Feeder tubes** (`feeders`, from level 50): a tube on the reservoir's top edge holds a queue of balls, first out first. Whenever a site in the top ball row under the tube is free, the next ball drops into it, so the tube keeps its chamber topped up as the pile drains. The tube shows the next balls in line and a counter of everything still inside. Build with `feeders=[(column, [(color, count), ...])]`; a `None` count takes its share like a layer band. Tubes need a gap of one column between them and cannot be combined with a funnel.
- `difficulty=1` (Hard) or `difficulty=2` (Very Hard) tags a level for the difficulty presentation. Hard: 29, 43, 44, 48, 54, 56. Very Hard: 34, 49.

## Levels 57–66

- **Chained boxes** (`links`, from level 57): two boxes joined by a steel chain. Their footprints can drift at most `length` free cells apart on either axis, diagonals included (1 by default). Dragging one further tows the other along, and the towed box collects like one the player picked up. A frozen, padlocked, completing or wrongly axis-locked partner cannot follow, so the chain holds the dragged box back: it twangs, and a padlock shakes. When either box completes, the chain snaps and the other box is free. Each box holds at most one chain. Build with `box(..., link="name")` on both boxes; `chains={"name": 2}` changes a chain's length. The chain runs between swivel posts on the two lids (beside the key or padlock when the box carries one).
- Chains mix with the earlier mechanics: 58 ties a single to a frozen bar, 59 and 62 chain nested boxes, 60 and 64 chain key boxes, 63, 65 and 66 add feeder tubes. Level 61 uses only a nested box and ice.
- Hard: 60, 62, 64, 65. Very Hard: 66.
- `verify_levels.py` does not model chains yet, so it cannot vouch for levels 57–66 except 61.

## Solvability check

`python Assets/_Game/Content/GridAndBox/ReferenceLevels/verify_levels.py 16 40 --probes 60` loads the generated assets and runs a line-by-line port of the ball simulation (`BallSimulationSystem`, `BallCollectionSystem`, `sinkReachRows = 2`), including ice, nested layers, padlocks and feeder tubes. It searches for a win, then plays random drags and checks that each position reached can still be won. An exhausted search proves a dead end. A search that runs out of budget is reported as unknown. The check models moving slowly (the balls settle after every drag), so fast drags that collect in passing are not covered.

All of levels 16–28 are solved, and none of the random play-outs reached a dead end. Levels 1–13 are also solved. Level 14 is too large for the search budget, and level 15 is solved, but most of its probes are unknown.
