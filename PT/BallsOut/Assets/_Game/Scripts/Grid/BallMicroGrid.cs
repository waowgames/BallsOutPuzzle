using UnityEngine;

namespace BallsOut
{
    public sealed class BallMicroGrid
    {
        // Ball radius plus a wall's half-thickness, in macro cells.
        private const float WallClearance = 0.15f;
        private readonly BallState[] occupants;
        private readonly bool[] mask;
        private readonly int[] visualReservations;
        private readonly Vector3[] positions;
        public BoardGrid Board { get; }
        public int Width => Board.Width * LevelDefinition.MicroResolution;
        public int Height => Board.Height * LevelDefinition.MicroResolution;
        public int Count { get; private set; }

        public BallMicroGrid(BoardGrid board)
        {
            Board = board;
            occupants = new BallState[Width * Height];
            mask = new bool[occupants.Length];
            positions = new Vector3[occupants.Length];
            visualReservations = new int[board.Width * board.Height];
            for (int y = board.Definition.lowerGridHeight * LevelDefinition.MicroResolution; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    mask[y * Width + x] = board.Definition.IsBallMicroCell(cell);
                    positions[y * Width + x] = CalculatePosition(cell);
                }
        }

        public static Vector2Int ToMacro(Vector2Int cell) => new Vector2Int(Mathf.FloorToInt((float)cell.x / LevelDefinition.MicroResolution), Mathf.FloorToInt((float)cell.y / LevelDefinition.MicroResolution));
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        public bool IsBallCell(Vector2Int cell) => Contains(cell) && mask[cell.y * Width + cell.x];
        public BallState Get(Vector2Int cell) => Contains(cell) ? occupants[cell.y * Width + cell.x] : null;
        public bool IsEmpty(Vector2Int cell) => IsBallCell(cell) && Get(cell) == null && Board.GetBox(ToMacro(cell)) == null;
        public bool CanTravel(Vector2Int from, Vector2Int to)
        {
            var dividers = Board.Definition.reservoirDividerColumns;
            if (dividers == null || dividers.Count == 0 ||
                from.y < Board.Definition.lowerGridHeight * LevelDefinition.MicroResolution ||
                to.y < Board.Definition.lowerGridHeight * LevelDefinition.MicroResolution)
                return true;
            for (int i = 0; i < dividers.Count; i++)
            {
                int boundary = dividers[i] * LevelDefinition.MicroResolution;
                if (from.x < boundary && to.x >= boundary || from.x >= boundary && to.x < boundary)
                    return false;
            }
            return true;
        }
        public bool HasBalls(Vector2Int macro)
        {
            if (Board.Contains(macro) && visualReservations[macro.y * Board.Width + macro.x] > 0) return true;
            for (int y = 0; y < LevelDefinition.MicroResolution; y++)
                for (int x = 0; x < LevelDefinition.MicroResolution; x++)
                    if (Get(macro * LevelDefinition.MicroResolution + new Vector2Int(x, y)) != null) return true;
            return false;
        }

        internal void ReserveVisual(Vector2Int macro, int change) => visualReservations[macro.y * Board.Width + macro.x] += change;

        public Vector3 CellToLocal(Vector2Int cell) => positions[cell.y * Width + cell.x];

        private Vector3 CalculatePosition(Vector2Int cell)
        {
            // Logical sites only drive flow. The depot has no visible macro tiles.
            // Touching staggered rows, with small stable offsets, form a loose pile.
            float pitch = Board.CellSize * 0.24f;
            int row = cell.y - Board.Definition.lowerGridHeight * LevelDefinition.MicroResolution;
            float noise = Mathf.Sin(cell.x * 12.9898f + row * 78.233f);
            float x = ChamberX(cell, row) + noise * 0.025f * pitch;
            return new Vector3(
                x,
                Mathf.Abs(noise) * Board.CellSize * 0.025f,
                Board.Definition.BallRowZ(row) + noise * 0.025f * pitch);
        }

        // The lattice pitch ignores macro cells, so a chamber between walls (cutout edges or
        // dividers) can hold columns that straddle them. Each chamber's lattice is shifted
        // inside its walls, and squeezed when the chamber is narrower than the lattice.
        private float ChamberX(Vector2Int cell, int row)
        {
            LevelDefinition level = Board.Definition;
            const int r = LevelDefinition.MicroResolution;
            Vector2Int macro = ToMacro(cell);
            int left = macro.x, right = macro.x + 1;
            while (!IsWall(left, macro.y)) left--;
            while (!IsWall(right, macro.y)) right++;
            float first = level.BallColumnX(left * r, 0);
            float last = level.BallColumnX(right * r - 1, 1);
            float min = (left + WallClearance) * Board.CellSize;
            float max = (right - WallClearance) * Board.CellSize;
            float x = level.BallColumnX(cell.x, row);
            if (last - first > max - min)
                return (min + max) * 0.5f + (x - (first + last) * 0.5f) * (max - min) / (last - first);
            return x + Mathf.Max(0f, min - first) + Mathf.Min(0f, max - last);
        }

        private bool IsWall(int boundary, int macroRow)
        {
            var dividers = Board.Definition.reservoirDividerColumns;
            return Board.GetCell(new Vector2Int(boundary - 1, macroRow)) != CellKind.Usable ||
                Board.GetCell(new Vector2Int(boundary, macroRow)) != CellKind.Usable ||
                dividers != null && dividers.Contains(boundary);
        }

        public static void DownNeighbors(Vector2Int cell, out Vector2Int down, out Vector2Int left, out Vector2Int right)
        {
            int odd = cell.y & 1;
            down = cell + new Vector2Int(0, -2);
            left = cell + new Vector2Int(odd - 1, -1);
            right = cell + new Vector2Int(odd, -1);
        }

        internal bool Add(BallState ball)
        {
            if (!IsEmpty(ball.Cell)) return false;
            occupants[ball.Cell.y * Width + ball.Cell.x] = ball;
            Count++;
            return true;
        }

        internal void Move(BallState ball, Vector2Int destination)
        {
            occupants[ball.Cell.y * Width + ball.Cell.x] = null;
            ball.Cell = destination;
            occupants[destination.y * Width + destination.x] = ball;
        }

        internal void Remove(BallState ball)
        {
            occupants[ball.Cell.y * Width + ball.Cell.x] = null;
            Count--;
        }
    }
}
