using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoardGrid
    {
        private readonly CellKind[] cells;
        private readonly BoxController[] boxes;
        private readonly BoxController[] transit;
        public LevelDefinition Definition { get; }
        public Transform Root { get; }
        public int Width => Definition.macroGridWidth;
        public int Height => Definition.TotalHeight;
        public int Revision { get; private set; }
        public float CellSize => Definition.macroCellSize;
        public event Action OnOccupancyChanged;

        public BoardGrid(LevelDefinition definition, Transform root)
        {
            Definition = definition;
            Root = root;
            cells = new CellKind[Width * Height];
            boxes = new BoxController[cells.Length];
            transit = new BoxController[cells.Length];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    cells[y * Width + x] = definition.GetCell(new Vector2Int(x, y));
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        public CellKind GetCell(Vector2Int cell) => Contains(cell) ? cells[cell.y * Width + cell.x] : CellKind.Outside;
        public BoxController GetBox(Vector2Int cell) => Contains(cell) ? boxes[cell.y * Width + cell.x] ?? transit[cell.y * Width + cell.x] : null;
        public Vector3 CellToLocal(Vector2Int cell) => new Vector3((cell.x + 0.5f) * CellSize, 0f, (cell.y + 0.5f) * CellSize);
        public Vector3 CellToWorld(Vector2Int cell) => Root.TransformPoint(CellToLocal(cell));
        public Vector2Int WorldToCell(Vector3 point)
        {
            Vector3 local = Root.InverseTransformPoint(point);
            return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize));
        }

        public bool CanPlace(BoxController box, Vector2Int origin, BallMicroGrid balls)
        {
            foreach (Vector2Int offset in box.Shape.Cells)
            {
                Vector2Int cell = origin + offset;
                if (GetCell(cell) != CellKind.Usable) return false;
                BoxController occupant = GetBox(cell);
                if (occupant != null && occupant != box) return false;
                if (balls != null && balls.HasBalls(cell)) return false;
            }
            return true;
        }

        internal bool TryPlace(BoxController box, Vector2Int origin, BallMicroGrid balls, bool reservePrevious = false)
        {
            if (!CanPlace(box, origin, balls)) return false;
            if (box.IsPlaced)
                foreach (var offset in box.Shape.Cells)
                {
                    if (reservePrevious) transit[(box.Origin.y + offset.y) * Width + box.Origin.x + offset.x] = box;
                    boxes[(box.Origin.y + offset.y) * Width + box.Origin.x + offset.x] = null;
                }
            foreach (var offset in box.Shape.Cells)
                boxes[(origin.y + offset.y) * Width + origin.x + offset.x] = box;
            box.SetOrigin(origin);
            Revision++;
            OnOccupancyChanged?.Invoke();
            return true;
        }

        internal void FinishTransit(BoxController box, Vector2Int previousOrigin)
        {
            foreach (var offset in box.Shape.Cells)
                transit[(previousOrigin.y + offset.y) * Width + previousOrigin.x + offset.x] = null;
            box.IsInTransit = false;
            Revision++;
            OnOccupancyChanged?.Invoke();
        }

        internal void Remove(BoxController box)
        {
            if (!box.IsPlaced) return;
            foreach (var offset in box.Shape.Cells)
            {
                Vector2Int cell = box.Origin + offset;
                int index = cell.y * Width + cell.x;
                if (boxes[index] == box) boxes[index] = null;
            }
            box.ClearPlacement();
            Revision++;
            OnOccupancyChanged?.Invoke();
        }
    }
}
