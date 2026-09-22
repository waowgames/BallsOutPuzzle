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
            if (level.macroGridWidth <= 0 || level.lowerGridHeight <= 0 || level.ballAreaMacroHeight <= 0 ||
                (long)level.macroGridWidth * ((long)level.lowerGridHeight + level.ballAreaMacroHeight) * 9 > int.MaxValue ||
                level.macroCellSize <= 0f || float.IsNaN(level.macroCellSize) || float.IsInfinity(level.macroCellSize))
            { errors.Add("Grid dimensions and finite cell size must be positive and fit the micro-grid index range."); return; }
            if (level.lowerGridMask == null || level.ballAreaMask == null || level.boxes == null || level.balls == null)
            { errors.Add("Masks and spawn lists must be assigned."); return; }
            ValidateMask(level.lowerGridMask, level.macroGridWidth, level.lowerGridHeight, "Lower", errors);
            ValidateMask(level.ballAreaMask, level.macroGridWidth, level.ballAreaMacroHeight, "Ball area", errors);
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
                if (box.startsLocked) errors.Add($"Box {i}: locks require Phase 13; turn off startsLocked.");
                AddCount(capacities, box.color, (long)box.shape.CellCount * LevelDefinition.CapacityPerMacroCell);
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
            occupied.Clear();
            for (int i = 0; i < level.balls.Count; i++)
            {
                BallSpawnData ball = level.balls[i];
                if (ball == null) { errors.Add($"Ball {i}: missing spawn data."); continue; }
                Vector2Int macro = BallMicroGrid.ToMacro(ball.cell);
                if (ball.color == null) errors.Add($"Ball {i}: assign a color.");
                else AddCount(ballCounts, ball.color, 1);
                if (macro.y < level.lowerGridHeight || level.GetCell(macro) != CellKind.Usable)
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
