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
        public int Capacity => Shape.CellCount * LevelDefinition.CapacityPerMacroCell;
        public bool IsCompleting { get; internal set; }
        public bool IsRemoved { get; internal set; }
        public bool CanMove => IsPlaced && !IsCompleting && !IsRemoved && CurrentFill < Capacity;
        public Transform FillRoot { get; private set; }
        public Vector3 FillSpacing { get; private set; }
        public BoxCompletionAnimation CompletionAnimation { get; private set; }
        internal List<BallState> CollectedBalls { get; private set; }
        internal int PendingFillAnimations;
        public event Action<BoxController> OnBoxFillChanged;

        internal void Initialize(BoxSpawnData spawn, PrefabRegistry registry, float cellSize)
        {
            Id = spawn.id;
            Shape = spawn.shape;
            Color = spawn.color;
            CollectedBalls = new List<BallState>(Capacity);
            FillRoot = new GameObject("Fill").transform;
            FillRoot.SetParent(transform, false);
            FillRoot.localPosition = (registry != null ? registry.fillOffset : new Vector3(0f, 0.22f, 0f)) * cellSize;
            FillSpacing = (registry != null ? registry.fillSpacing : new Vector3(0.25f, 0.18f, 0.25f)) * cellSize;
            if (registry != null && registry.TryGetBox(Shape, out var entry) && entry.prefab != null)
            {
                GameObject visual = Instantiate(entry.prefab, transform);
                visual.transform.localPosition = entry.localOffset;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = entry.localScale == Vector3.zero ? Vector3.one : entry.localScale;
                PrefabRegistry.ApplyMaterial(visual, Color.boxMaterial);
                FillRoot.localPosition += entry.fillOffset;
                CompletionAnimation = visual.GetComponentInChildren<BoxCompletionAnimation>(true);
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
        public bool CanCollect(BallColorDefinition color) => IsPlaced && !IsCompleting && !IsRemoved && !IsInTransit && Color == color && CurrentFill < Capacity;
        internal void RecordCollection() => CurrentFill++;
        internal void NotifyCollection() => OnBoxFillChanged?.Invoke(this);

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
