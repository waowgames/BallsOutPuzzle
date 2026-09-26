# Reference levels 1–40

These assets recreate the first forty Sand Blocks board compositions in the Balls Out grid system. `Level_01_Reference.asset` through `Level_40_Reference.asset` are the first forty entries in `Assets/Scripts/LevelConfig.asset`.

The [board images](https://sand-blocks.org/levels) and [level notes](https://levelsolve.com/sand-blocks-drop-puzzle/) were used as visual references. Each level has authored box shapes, starting cells, colors, reservoir art, and ice counts. Level 1 and level 8 also start with a partially filled receiver. The generator keeps every color's ball count equal to the remaining receiver capacity.

Run `python Assets/_Game/Content/GridAndBox/ReferenceLevels/build_levels.py` from the project root to rebuild the assets. GUIDs are stable across runs.

These are adaptations to this game's mechanics. The reference's separate stacked chambers in level 11 and independent ice slabs in later levels are represented with this project's single reservoir and frozen receiver boxes. Sand blocks that sit mid-board in the reference (levels 21–40) become chambers of the top reservoir or partly filled boxes. Level 29's two reservoirs become two chambers over an H-shaped board, and level 30's stepped pit becomes a flat board.

## Levels 16–40

- **Axis-locked boxes** (`moveAxis`, from level 16): a double-headed arrow on the lid shows the only axis the box can slide along. Balls never enter the lower grid, so a sideways-only box must start in the top row, and a column-locked box's column holds every ball of its color.
- **Nested boxes** (`innerColor`, from level 26): an inner tray framed by the outer color. The box collects a full load of the inner color, the tray and its balls pop away, and then it collects a full load of the outer color. Both loads count toward their colors' capacities; only the final completion cracks ice. An inner-color ball must never end up under balls that only the nested box's outer layer could take. Balls against a wall or divider can only drop straight down, so they get pinned above whatever slides in beneath them. Levels 27–28 keep every inner-color ball in the two floor rows a box draws from. Level 26 introduces the mechanic with two big trays whose colors cross over (a red box with a blue tray, a blue box with a red tray): a pinned ball always sits above a color the other box can take.
- **Padlocks and keys** (`startsLocked` + `lockId`, `keyId`, from level 40): a padlocked box cannot move or collect. Every box whose `keyId` names that lock carries a gold key on its lid. When it completes, the key flies into the padlock and the counter drops. The last key pops the shackle and frees the box. Build with `box(..., lock="name")` and `box(..., key="name")`.
- Layered chambers (`layers=`) fill each chamber bottom to top, one color band after another.

## Solvability check

`python Assets/_Game/Content/GridAndBox/ReferenceLevels/verify_levels.py 16 40 --probes 60` loads the generated assets and runs a line-by-line port of the ball simulation (`BallSimulationSystem`, `BallCollectionSystem`, `sinkReachRows = 2`), including ice, nested layers and padlocks. It searches for a win, then plays random drags and checks that each position reached can still be won. An exhausted search proves a dead end. A search that runs out of budget is reported as unknown. The check models moving slowly (the balls settle after every drag), so fast drags that collect in passing are not covered.

All of levels 16–28 are solved, and none of the random play-outs reached a dead end. Levels 1–13 are also solved. Level 14 is too large for the search budget, and level 15 is solved, but most of its probes are unknown.
