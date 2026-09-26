using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Shared authoring/runtime validation; capacity math uses the same shape rule as gameplay.
    public static class LevelValidator
    {
        public static void Validate(LevelDefinition level, List<string> errors)
        {
            if (level == null) { errors.Add("Assign a LevelDefinition."); return; }
            if (level.macroGridWidth <= 0 || level.lowerGridHeight <= 0 || level.ballAreaMacroHeight <= 0 || level.fillLayers < 0 ||
                (long)level.macroGridWidth * ((long)level.lowerGridHeight + level.ballAreaMacroHeight) * LevelDefinition.MicroResolution * LevelDefinition.MicroResolution > int.MaxValue ||
                level.macroCellSize <= 0f || float.IsNaN(level.macroCellSize) || float.IsInfinity(level.macroCellSize))
            { errors.Add("Grid dimensions and finite cell size must be positive and fit the micro-grid index range."); return; }
            if (level.lowerGridMask == null || level.ballAreaMask == null || level.boxes == null || level.balls == null)
            { errors.Add("Masks and spawn lists must be assigned."); return; }
            if (level.hopperMicroRows < 0 || level.hopperMicroRows > (level.ballAreaMacroHeight - 1) * LevelDefinition.MicroResolution ||
                (level.hopperMicroRows > 0 && (level.hopperMicroRows < 4 || level.macroGridWidth < 3)))
            { errors.Add("Use 0 hopper rows, or at least 4 with a width of 3+ and one full reservoir row below the funnel."); return; }
            ValidateMask(level.lowerGridMask, level.macroGridWidth, level.lowerGridHeight, "Lower", errors);
            ValidateMask(level.ballAreaMask, level.macroGridWidth, level.ballAreaMacroHeight, "Ball area", errors);
            if (level.reservoirDividerColumns != null)
            {
                var dividers = new HashSet<int>();
                foreach (int column in level.reservoirDividerColumns)
                    if (column <= 0 || column >= level.macroGridWidth || !dividers.Add(column))
                        errors.Add($"Invalid or duplicate reservoir divider at column {column}.");
                if (level.hopperMicroRows > 0 && dividers.Count > 0)
                    errors.Add("Reservoir dividers cannot be combined with a funnel.");
            }
            if (level.hopperMicroRows > 0)
                for (int y = (level.HopperStartRow - 1) / LevelDefinition.MicroResolution; y < level.ballAreaMacroHeight; y++)
                    for (int x = 0; x < level.macroGridWidth; x++)
                        if (level.ballAreaMask.Get(new Vector2Int(x, y), level.macroGridWidth, level.ballAreaMacroHeight) != CellKind.Usable)
                        { errors.Add("Keep the funnel band and its entrance usable; use ball-area cutouts below it or on flat-reservoir levels."); return; }
            if (errors.Count > 0) return;
            var occupied = new HashSet<Vector2Int>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var capacities = new Dictionary<BallColorDefinition, long>();
            var ballCounts = new Dictionary<BallColorDefinition, long>();
            if (level.boxes.Count == 0 || level.balls.Count == 0) errors.Add("A playable level must contain boxes and balls.");
            for (int i = 0; i < level.boxes.Count; i++)
            {
                BoxSpawnData box = level.boxes[i];
                if (box == null || box.shape == null || box.shape.CellCount == 0 || box.color == null)
                { errors.Add($"Box {i}: assign a nonempty shape and color."); continue; }
                if (string.IsNullOrWhiteSpace(box.id) || !ids.Add(box.id)) errors.Add($"Box {i}: ID must be nonempty and unique.");
                long capacity = level.BoxCapacity(box.shape);
                if (box.initialFillCount < 0 || box.initialFillCount >= capacity)
                    errors.Add($"Box {i}: initial fill must be between 0 and capacity - 1.");
                if (!Enum.IsDefined(typeof(BoxMoveAxis), box.moveAxis)) errors.Add($"Box {i}: invalid move axis.");
                if (box.innerColor == box.color) errors.Add($"Box {i}: inner color must differ from the outer color.");
                // A nested box takes a full load of its inner color, then a full load of its outer color.
                if (box.innerColor != null)
                {
                    AddCount(capacities, box.innerColor, capacity - box.initialFillCount);
                    AddCount(capacities, box.color, capacity);
                }
                else AddCount(capacities, box.color, capacity - box.initialFillCount);
                var shape = new HashSet<Vector2Int>();
                foreach (var offset in box.shape.Cells)
                {
                    if (!shape.Add(offset)) errors.Add($"Box {i}: duplicate shape cell {offset}.");
                    Vector2Int cell = box.startingMacroOrigin + offset;
                    if (level.GetCell(cell) != CellKind.Usable || cell.y >= level.lowerGridHeight)
                        errors.Add($"Box {i}: starting cell {cell} must be usable lower-grid space.");
                    if (!occupied.Add(cell)) errors.Add($"Box {i}: overlap at {cell}.");
                }
                var reached = new HashSet<Vector2Int>();
                Visit(box.shape.Cells[0], shape, reached);
                if (reached.Count != shape.Count) errors.Add($"Box {i}: shape cells must be edge-connected.");
            }
            ValidateIce(level, errors);
            ValidateLocks(level, errors);
            occupied.Clear();
            for (int i = 0; i < level.balls.Count; i++)
            {
                BallSpawnData ball = level.balls[i];
                if (ball == null) { errors.Add($"Ball {i}: missing spawn data."); continue; }
                if (ball.color == null) errors.Add($"Ball {i}: assign a color.");
                else AddCount(ballCounts, ball.color, 1);
                if (!level.IsBallMicroCell(ball.cell))
                    errors.Add($"Ball {i}: {ball.cell} is outside the usable ball area.");
                if (!occupied.Add(ball.cell)) errors.Add($"Ball {i}: duplicate cell {ball.cell}.");
                if (ball.specialType != BallSpecialType.Normal) errors.Add($"Ball {i}: key balls require Phase 13.");
            }
            var colors = new HashSet<BallColorDefinition>(capacities.Keys);
            colors.UnionWith(ballCounts.Keys);
            var colorIds = new Dictionary<string, BallColorDefinition>(StringComparer.Ordinal);
            foreach (BallColorDefinition color in colors)
            {
                capacities.TryGetValue(color, out long capacity);
                ballCounts.TryGetValue(color, out long count);
                if (count != capacity)
                    errors.Add($"Color '{color.name}': {count} balls but {capacity} box capacity (difference {count - capacity:+0;-0;0}). Counts must match exactly across all same-color boxes.");
                if (string.IsNullOrWhiteSpace(color.id)) errors.Add($"Color '{color.name}': assign a stable nonempty ID.");
                else if (colorIds.TryGetValue(color.id, out var other))
                    errors.Add($"Colors '{color.name}' and '{other.name}' share ID '{color.id}'. Reuse one color asset for matching gameplay colors.");
                else colorIds.Add(color.id, color);
            }
        }

        // Each completion lowers every other frozen box by one, so thaw greedily
        // from the lowest counts; a count that outruns available completions never breaks.
        private static void ValidateIce(LevelDefinition level, List<string> errors)
        {
            var counts = new List<int>();
            int completable = 0;
            for (int i = 0; i < level.boxes.Count; i++)
            {
                BoxSpawnData box = level.boxes[i];
                if (box == null) continue;
                if (box.iceCount < 0) errors.Add($"Box {i}: ice count cannot be negative.");
                else if (box.iceCount == 0) completable++;
                else counts.Add(box.iceCount);
            }
            counts.Sort();
            foreach (int count in counts)
            {
                if (count > completable)
                {
                    errors.Add($"Ice count {count} can never reach 0: only {completable} boxes can complete before it.");
                    return;
                }
                completable++;
            }
        }

        // Every padlock needs at least one key box, every key a padlock, and no chain of
        // locked key boxes may loop back on itself (none of them could ever open).
        private static void ValidateLocks(LevelDefinition level, List<string> errors)
        {
            var locks = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < level.boxes.Count; i++)
            {
                BoxSpawnData box = level.boxes[i];
                if (box == null || !box.startsLocked) continue;
                if (string.IsNullOrWhiteSpace(box.lockId)) errors.Add($"Box {i}: a locked box needs a lockId.");
                else if (locks.ContainsKey(box.lockId)) errors.Add($"Box {i}: lockId '{box.lockId}' is already used by another box.");
                else locks.Add(box.lockId, i);
            }
            var keys = new Dictionary<int, List<int>>();
            for (int i = 0; i < level.boxes.Count; i++)
            {
                BoxSpawnData box = level.boxes[i];
                if (box == null || string.IsNullOrEmpty(box.keyId)) continue;
                if (!locks.TryGetValue(box.keyId, out int target))
                    errors.Add($"Box {i}: keyId '{box.keyId}' matches no locked box.");
                else if (target == i) errors.Add($"Box {i}: a box cannot carry the key to its own lock.");
                else
                {
                    if (!keys.TryGetValue(target, out var list)) keys.Add(target, list = new List<int>());
                    list.Add(i);
                }
            }
            foreach (var pair in locks)
                if (!keys.ContainsKey(pair.Value)) errors.Add($"Box {pair.Value}: lock '{pair.Key}' has no key box.");
            // 0 = unvisited, 1 = on the current path, 2 = can open.
            var state = new int[level.boxes.Count];
            foreach (int start in locks.Values)
                if (!Opens(start)) { errors.Add($"Box {start}: its lock waits on a key box that is itself waiting on this lock."); return; }

            bool Opens(int box)
            {
                if (state[box] == 2) return true;
                if (state[box] == 1) return false;
                state[box] = 1;
                if (keys.TryGetValue(box, out var sources))
                    foreach (int source in sources)
                        if (level.boxes[source].startsLocked && !Opens(source)) return false;
                state[box] = 2;
                return true;
            }
        }

        private static void AddCount(Dictionary<BallColorDefinition, long> counts, BallColorDefinition color, long amount)
        {
            counts.TryGetValue(color, out long current);
            counts[color] = current + amount;
        }

        private static void ValidateMask(CellMask mask, int width, int height, string name, List<string> errors)
        {
            if (mask.overrides == null) { errors.Add(name + ": overrides list is missing."); return; }
            if (!Enum.IsDefined(typeof(CellKind), mask.defaultKind)) errors.Add(name + ": invalid default cell kind.");
            var cells = new HashSet<Vector2Int>();
            foreach (var item in mask.overrides)
            {
                if (item.cell.x < 0 || item.cell.y < 0 || item.cell.x >= width || item.cell.y >= height)
                    errors.Add($"{name}: override {item.cell} is outside its local mask bounds.");
                if (!cells.Add(item.cell)) errors.Add($"{name}: duplicate override {item.cell}.");
                if (!Enum.IsDefined(typeof(CellKind), item.kind)) errors.Add($"{name}: invalid cell kind at {item.cell}.");
            }
        }

        private static void Visit(Vector2Int start, HashSet<Vector2Int> shape, HashSet<Vector2Int> reached)
        {
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(start);
            reached.Add(start);
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue();
                foreach (var direction in directions)
                {
                    Vector2Int next = cell + direction;
                    if (shape.Contains(next) && reached.Add(next)) pending.Enqueue(next);
                }
            }
        }
    }
}
