# Reference levels 1–15

These assets recreate the first fifteen Sand Blocks board compositions in the Balls Out grid system. `Level_01_Reference.asset` through `Level_15_Reference.asset` are the first fifteen entries in `Assets/Scripts/LevelConfig.asset`.

The [board images](https://sand-blocks.org/levels) and [level notes](https://levelsolve.com/sand-blocks-drop-puzzle/) were used as visual references. Each level has authored box shapes, starting cells, colors, reservoir art, and ice counts. Level 1 and level 8 also start with a partially filled receiver. The generator keeps every color's ball count equal to the remaining receiver capacity.

Run `python Assets/_Game/Content/GridAndBox/ReferenceLevels/build_levels.py` from the project root to rebuild the assets. GUIDs are stable across runs.

These are adaptations to this game's mechanics. The reference's separate stacked chambers in level 11 and independent ice slabs in later levels are represented with this project's single reservoir and frozen receiver boxes. Exact move sequences and full puzzle solvability still need an in-editor playthrough.
