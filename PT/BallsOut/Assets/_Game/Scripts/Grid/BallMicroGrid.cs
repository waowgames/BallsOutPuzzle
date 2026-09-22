using UnityEngine;

namespace BallsOut
{
    public sealed class BallMicroGrid
    {
        private readonly BallState[] occupants;
        private readonly bool[] mask;
        private readonly int[] visualReservations;
        public BoardGrid Board { get; }
        public int Width => Board.Width * LevelDefinition.MicroResolution;
        public int Height => Board.Height * LevelDefinition.MicroResolution;
        public int Count { get; private set; }

        public BallMicroGrid(BoardGrid board)
        {
            Board = board;
            occupants = new BallState[Width * Height];
            mask = new bool[occupants.Length];
            visualReservations = new int[board.Width * board.Height];
            for (int y = board.Definition.lowerGridHeight * 3; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    mask[y * Width + x] = board.GetCell(ToMacro(new Vector2Int(x, y))) == CellKind.Usable;
        }

        public static Vector2Int ToMacro(Vector2Int cell) => new Vector2Int(Mathf.FloorToInt(cell.x / 3f), Mathf.FloorToInt(cell.y / 3f));
        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        public bool IsBallCell(Vector2Int cell) => Contains(cell) && mask[cell.y * Width + cell.x];
        public BallState Get(Vector2Int cell) => Contains(cell) ? occupants[cell.y * Width + cell.x] : null;
        public bool IsEmpty(Vector2Int cell) => IsBallCell(cell) && Get(cell) == null && Board.GetBox(ToMacro(cell)) == null;
        public bool HasBalls(Vector2Int macro)
        {
            if (Board.Contains(macro) && visualReservations[macro.y * Board.Width + macro.x] > 0) return true;
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    if (Get(macro * 3 + new Vector2Int(x, y)) != null) return true;
            return false;
        }

        internal void ReserveVisual(Vector2Int macro, int change) => visualReservations[macro.y * Board.Width + macro.x] += change;

        public Vector3 CellToLocal(Vector2Int cell)
        {
            float pitch = Board.CellSize / 3f;
            // Odd-r: odd global rows shift right by half a pitch. Center the stagger
            // around each column so every site's center stays in its owning macro cell.
            float offset = (cell.y & 1) == 0 ? -0.25f : 0.25f;
            return new Vector3((cell.x + 0.5f + offset) * pitch, 0f, (cell.y + 0.5f) * pitch);
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
