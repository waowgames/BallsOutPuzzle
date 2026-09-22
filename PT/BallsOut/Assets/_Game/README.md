# Balls Out — Phases 1–10

Gameplay includes deterministic flow, dragging, 3D fill, completed-box removal,
win events, and level validation. No new UI, deadlock detection, tutorial gating,
or key/lock gameplay has been added. Phase 11 and later are out of scope.

## Open the prototype

Open `Assets/_Game/Prototype/Phase7.unity` and press Play. Drag the colored shapes
using mouse or touch. Blue's single-cell box starts at the ball boundary and can
collect immediately. Move other boxes up to matching columns to open corridors.
The field contains 378 balls and the five shapes total 14 macro cells of capacity.
The temporary cubes/spheres are development art, replaceable through the registry.

If prototype assets are not present, run **Tools > Balls Out > Create Phase 7
Prototype**. This creates a separate scene and assets and leaves existing scenes
intact. It does not overwrite an existing prototype.

For Phase 10 examples, open a scene under `Assets/_Game/Samples`, or use **Tools >
Balls Out > Create Phase 10 Sample Levels** to generate missing assets. Existing
levels/scenes are preserved. Save an untitled editor scene before using a builder.

| Scene folder | Board width | Balls | Demonstrates |
| --- | --- | --- | --- |
| `A_LooseSingles` | 7 | 189 | Two colors, seven 1x1 boxes, loose lower grid. |
| `B_ThreeColorsShapes` | 6 | 162 | Three colors, L + domino + single, color clusters. |
| `C_BlockedLowerGrid` | 7 | 189 | Blocked/outside lower cells and tighter movement. |
| `D_SharedColorBoxes` | 8 | 216 | Four dominoes; two boxes per color. |

These are playable development fixtures, not automated test files or a claim of
solver-proven difficulty/solvability. All per-color ball totals match box capacity.

## Use your own art and levels

1. Create assets with **Create > Balls Out > Color / Box Shape / Level / Prefab Registry**.
2. Shapes contain edge-connected integer cell offsets in a stable order. Rotation
   is disabled. The runtime origin is the center of local cell `(0, 0)`.
3. Set board width, lower/upper heights and cell size on the level. Each region's
   mask defaults to usable; add local overrides for blocked/outside cells.
4. Define all starting boxes in the lower grid, with unique IDs, shapes and colors.
5. Enter explicit ball cells or assign a palette and click **Generate Dense Ball
   Field From Palette**. This fills usable space with macro-column color stripes
   and supports Undo. Click **Validate Level (Including Color Capacity)** for
   authoring errors, or **Tools > Balls Out > Validate All Levels** for all assets.
6. Assign prefabs/materials in the registry. Ball prefabs may be overridden per
   color. Box visuals are selected by shape, with configurable local scale/offset.
   Floor scale is multiplied by macro cell size. Ball and box visual scales are
   explicit model-unit scales; adjust them when changing model dimensions.
   Set registry `fillSpacing` (X spacing, layer height, Z spacing) and `fillOffset`
   in macro-cell units. Each box registry entry can add a model-specific
   `fillOffset` in local world units. No manually placed fill sockets are needed.
7. Add `BallBoxLevelRuntime` to a level root and assign the level and registry.
   `BoardDragInput` is added automatically; assign its camera or tag a camera
   MainCamera. Use a top-down camera aimed at the XZ board plane.
8. For existing progression, assign the runtime prefab to the inherited
   `LevelDefinition.LevelPrefab` and add the level to the existing `LevelConfig`.
   The existing `LevelContentLoader` instantiates it. An unassigned runtime level
   resolves the current manager's `LevelDefinition`. Enable `waitForLevelStart`
   if gameplay must wait for the existing `GameEvents.OnLevelStarted` event.
   A win completes the existing manager only when it is playing this same level.

## Coordinates and deterministic movement

- Local X is board width, local Z is board height, local Y is visual height.
  Gravity is negative Z. The origin is the bottom-left board corner.
- Macro cells are centered at `(x + 0.5, z + 0.5) * cellSize`.
- One macro cell maps to exactly 3x3 micro cells. Micro coordinates are global:
  the first ball row is `lowerGridHeight * 3`. Masks use their region's local coordinates.
- Odd-r convention: odd global rows sit half a micro pitch to the right of even
  rows. Offsets are centered at -0.25/+0.25 pitch, keeping all 21 site centers
  inside a seven-column board and in their corresponding macro cells.
  Use ball radius at most one quarter of the micro pitch if all mesh geometry
  must stay inside its logical macro footprint at column edges.
- The true vertical odd-r destination is two rows below. It is attempted only
  when both intervening diagonal sites are empty, preventing tunneling. Otherwise
  parity-derived lower-left/right sites are used, with a persistent left/right
  alternator when both are legal. The same initial state and box moves at the
  same simulation ticks produce the same results.
- Simulation visits cells bottom-to-top, left-to-right. Balls move at most once
  per tick. No RNG, Rigidbody authority, per-ball Update, or per-tick allocations.
  Stable boards sleep until board occupancy changes. Catch-up is bounded to four
  ticks per frame and preserves the time remainder.
- Drags advance through validated cardinal neighbors. Each full shape must fit.
  A step finishes its interpolation before another starts; turns cannot cut corners.
  The previous footprint is reserved during transit. Ball animation source cells
  are also reserved against box movement, even with missing visual prefabs.
- Dropping at an invalid/unreached pointer target settles at the latest accepted
  origin. It does not return across a route that may now contain flowing balls.
- Every box macro cell maps to nine intake sites. Collection checks a downward
  destination before the ball-area mask, so boxes touching the lower boundary can
  receive balls without creating an empty entry channel in the initial field.
  Empty lower-grid cells never receive balls. Wrong colors and full boxes block
  entry; diagonal alternatives remain available.
- A box in transit blocks intake during its short step animation. Other boxes
  continue collecting throughout the drag; simulation is never paused by input.

## Events and scope boundary

`BallBoxLevelRuntime` exposes `OnLevelStarted`, `OnBoxFillChanged`,
`OnBallCollected`, `OnBoxCompleted`, `OnBoxRemoved`, and `OnLevelWon`;
`BallSimulationSystem` exposes `OnStabilityChanged`;
`BoxMovementSystem` exposes `OnBoxMoved` and `OnDragEnded`.

Color matching uses color asset identity, not display RGB. Reuse the same color
asset for matching balls/boxes. Multiple boxes may share a color.

Capacity is always `shape.CellCount * 27`. Within each shape cell, slots 0–8 fill
the bottom layer, 9–17 the middle, and 18–26 the top; rows run back-to-front and
columns left-to-right. The stable shape-cell array determines the next cell.
Collected balls reuse their original pooled visual and become children of a
mathematically positioned fill root, so partial fills and arrivals move with boxes.

`OnBoxCompleted` fires once when capacity is reached; the box stops moving and
accepting balls. After the final incoming ball lands, the completion animation
runs. Add optional `BoxCompletionAnimation` to a visual prefab to configure its
duration, closed/lid visual and Inspector start/finish UnityEvents (for an Animator,
particles, etc.). Without this hook a short shrink uses registry completion duration.
Hooks must not destroy/reparent the logical root or disable the level runtime.
The system owns occupancy and pooled-ball cleanup.

After completion, footprint cells are released, fill visuals return to the pool,
and the box is deactivated. `OnBoxRemoved` then fires. Hidden box roots are retained
until level unload; there is no per-collection Instantiate/Destroy. Pool return
restores parent, rotation and scale, including after completion shrink.

Fill/completion deadlines use the fixed simulation clock, while visual transforms
interpolate each frame. Stable ball fields keep ticking while these animations are
pending, so completion cannot stall and frame rate cannot change occupancy-release
ordering. Missing art follows the same logical timing. `OnLevelWon` fires once only
after no active balls, no remaining boxes and no pending fill/movement remain.

Validation checks dimensions, masks, enum values, connected/unique shape cells,
starting placement, box IDs, ball placement/duplicates, color IDs, and **exact**
per-color capacity sums (including multiple boxes of the same color). Invalid
levels are rejected before replacing the current runtime. Locks/key balls are
explicitly rejected until their later phase; no unlocking behavior is implemented.

## Phase changelog

| Phase | Added / integrated |
| --- | --- |
| 1 | `Scripts/Level`: color, shape, level, masks, spawn data, enums, validation guards. `Core/PrefabRegistry`. Existing `LevelData` inherited; gameplay events avoid unused interfaces. |
| 2 | `Grid/BoardGrid`, `Core/BoardPresentation`: variable dimensions, masks, occupancy, coordinate conversion, board visuals and editor gizmos. |
| 3 | `Grid/BallMicroGrid`, `Balls/BallState`, `Pooling/BallPool`: odd-r positions, dense data spawning, safe occupancy and pooled visuals. |
| 4 | `Simulation/BallSimulationSystem`: fixed ticks, guarded downward flow, deterministic diagonal choices, centralized interpolation and stability events. |
| 5 | `Boxes/BoxController`, `Boxes/BoxMovementSystem`, `Input/BoardDragInput`: data footprints, mouse/touch dragging, cardinal path validation and valid-origin snapping. |
| 6 | Shared macro/micro collision checks and transit reservations; boxes cross into cleared upper space while balls remain inside their mask. |
| 7 | `Balls/BallCollectionSystem`, `Core/BallBoxLevelRuntime`: footprint intake, color/capacity gating, multiple collectors, events and existing level-loader integration. `Editor` authoring tools and a separate prototype scene. |
| 8 | `Boxes/BoxFillSystem`: generated 27-slot fill per cell, centralized arrival interpolation, retained fills and pooled visual reuse. Extended `BoxController`, `PrefabRegistry`, `BallCollectionSystem` and `BallPool`. |
| 9 | `Boxes/BoxCompletionSystem`, optional `Boxes/BoxCompletionAnimation`: completion events, animation hooks, occupancy release, pool cleanup and win/progression integration. Extended `BoardGrid`, `BoxMovementSystem`, `BallSimulationSystem`, and `BallBoxLevelRuntime`. |
| 10 | Expanded `LevelValidator` and `LevelDefinitionEditor`; new `Editor/Phase10SampleBuilder` and four balanced level/prefab/scene sets under `Samples`. Reused existing prototype art and scene builder. |

The original template scripts and scenes are unchanged. No Unity test files were
created. No phase after 10 is implemented.

## Build status

Unity 6000.0.68f1 completed compilation and sample-scene generation with exit code
0. All five level assets (the original prototype and A–D) passed the shared level
validator, including exact per-color capacity checks. No automated test files
were created or run. Interactive drag feel, rendered appearance, and device
performance still require an in-editor/device play session; compilation and data
validation do not establish those results.
