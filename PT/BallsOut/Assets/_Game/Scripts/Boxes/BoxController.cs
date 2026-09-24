using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxController : MonoBehaviour
    {
        public string Id { get; private set; }
        public BoxShapeDefinition Shape { get; private set; }
        public BallColorDefinition Color { get; private set; }
        public Vector2Int Origin { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsInTransit { get; internal set; }
        public int CurrentFill { get; private set; }
        public int Capacity { get; private set; }
        public bool IsCompleting { get; internal set; }
        public bool IsRemoved { get; internal set; }
        public bool CanMove => IsPlaced && !IsCompleting && !IsRemoved && CurrentFill < Capacity;
        public Transform FillRoot { get; private set; }
        public Vector3 FillSpacing { get; private set; }
        public BoxCompletionAnimation CompletionAnimation { get; private set; }
        private BoxFillLabel fillLabel;
        internal List<BallState> CollectedBalls { get; private set; }
        internal Vector3[] FillSlots { get; private set; }
        internal int PendingFillAnimations;
        public event Action<BoxController> OnBoxFillChanged;

        internal void Initialize(BoxSpawnData spawn, PrefabRegistry registry, float cellSize, bool denseFill)
        {
            Id = spawn.id;
            Shape = spawn.shape;
            Color = spawn.color;
            Capacity = Shape.FillSlotsPerLayer(denseFill) * LevelDefinition.FillLayers;
            CollectedBalls = new List<BallState>(Capacity);
            FillRoot = new GameObject("Fill").transform;
            FillRoot.SetParent(transform, false);
            FillRoot.localPosition = (registry != null ? registry.fillOffset : new Vector3(0f, 0.085f, 0f)) * cellSize;
            int resolution = LevelDefinition.MicroResolution;
            int seam = denseFill ? 2 : 0;
            int stride = resolution + seam;
            float slotSpacing = cellSize * (denseFill ? 0.168f : 0.68f / resolution);
            FillSpacing = new Vector3(slotSpacing, cellSize / LevelDefinition.MicroResolution * 0.9f, slotSpacing);
            FillSlots = new Vector3[Shape.FillSlotsPerLayer(denseFill)];
            var cells = new HashSet<Vector2Int>(Shape.Cells);
            int slot = 0;
            foreach (Vector2Int cell in Shape.Cells)
            {
                int minX = cell.x, maxX = cell.x, minZ = cell.y, maxZ = cell.y;
                while (cells.Contains(new Vector2Int(minX - 1, cell.y))) minX--;
                while (cells.Contains(new Vector2Int(maxX + 1, cell.y))) maxX++;
                while (cells.Contains(new Vector2Int(cell.x, minZ - 1))) minZ--;
                while (cells.Contains(new Vector2Int(cell.x, maxZ + 1))) maxZ++;
                int columns = resolution * (maxX - minX + 1) + seam * (maxX - minX);
                int rows = resolution * (maxZ - minZ + 1) + seam * (maxZ - minZ);
                float startX = (minX + maxX) * cellSize * 0.5f - (columns - 1) * slotSpacing * 0.5f;
                float startZ = (minZ + maxZ) * cellSize * 0.5f - (rows - 1) * slotSpacing * 0.5f + 0.01f * cellSize;
                int startColumn = (cell.x - minX) * stride;
                int startRow = (cell.y - minZ) * stride;
                bool right = seam != 0 && cells.Contains(cell + Vector2Int.right);
                bool up = seam != 0 && cells.Contains(cell + Vector2Int.up);
                bool diagonal = right && up && cells.Contains(cell + Vector2Int.one);
                for (int row = 0; row < resolution + (up ? seam : 0); row++)
                    for (int column = 0; column < resolution + (right ? seam : 0); column++)
                    {
                        if (row >= resolution && column >= resolution && !diagonal) continue;
                        FillSlots[slot++] = new Vector3(
                            startX + (startColumn + column) * slotSpacing,
                            0f,
                            startZ + (startRow + row) * slotSpacing);
                    }
            }
            // The game camera looks down with screen up along local +Z.
            Array.Sort(FillSlots, (a, b) =>
            {
                int rowOrder = a.z.CompareTo(b.z);
                return rowOrder != 0 ? rowOrder : a.x.CompareTo(b.x);
            });
            if (registry != null && registry.TryGetBox(Shape, out var entry) && entry.prefab != null)
            {
                GameObject visual = Instantiate(entry.prefab, transform);
                visual.transform.localPosition = entry.localOffset;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = entry.localScale == Vector3.zero ? Vector3.one : entry.localScale;
                PrefabRegistry.ApplyMaterial(visual, Color.boxMaterial);
                FillRoot.localPosition += entry.fillOffset;
                CompletionAnimation = visual.GetComponentInChildren<BoxCompletionAnimation>(true);
                fillLabel = BoxFillLabel.Create(this, visual, cellSize);
                fillLabel.SetFill(CurrentFill, Capacity);
            }
            // Hit proxies follow the data footprint, never mesh bounds.
            foreach (Vector2Int cell in Shape.Cells)
            {
                var hit = gameObject.AddComponent<BoxCollider>();
                hit.center = new Vector3(cell.x * cellSize, cellSize * 0.2f, cell.y * cellSize);
                hit.size = new Vector3(cellSize * 0.95f, cellSize * 0.4f, cellSize * 0.95f);
            }
        }

        internal void SetOrigin(Vector2Int origin) { Origin = origin; IsPlaced = true; }
        internal void ClearPlacement() { IsPlaced = false; IsRemoved = true; }
        public bool CanCollect(BallColorDefinition color) => IsPlaced && !IsCompleting && !IsRemoved && Color == color && CurrentFill < Capacity;
        internal void RecordCollection() => CurrentFill++;
        internal void NotifyCollection()
        {
            fillLabel?.SetFill(CurrentFill, Capacity);
            OnBoxFillChanged?.Invoke(this);
        }

        private void OnDrawGizmos()
        {
            if (Shape == null) return;
            Gizmos.color = Color != null ? Color.displayColor : UnityEngine.Color.white;
            Gizmos.matrix = transform.localToWorldMatrix;
            float size = IsPlaced && transform.parent != null ? runtimeCellSize : 1f;
            foreach (var cell in Shape.Cells)
                Gizmos.DrawWireCube(new Vector3(cell.x * size, size * 0.2f, cell.y * size), new Vector3(size * 0.95f, size * 0.4f, size * 0.95f));
        }

        internal float runtimeCellSize = 1f;
    }
}
