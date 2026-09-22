using UnityEngine;

namespace BallsOut
{
    public static class BoardPresentation
    {
        internal static void Build(BoardGrid board, PrefabRegistry registry)
        {
            if (registry == null) return;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    CellKind kind = board.GetCell(cell);
                    GameObject prefab = kind == CellKind.Usable ? registry.floorPrefab : kind == CellKind.Blocked ? registry.blockedCellPrefab : null;
                    if (prefab == null) continue;
                    Transform visual = Object.Instantiate(prefab, board.Root).transform;
                    visual.localPosition = board.CellToLocal(cell) + registry.tileOffset;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = registry.tileScale * board.CellSize;
                }
        }

        internal static void DrawGizmos(BoardGrid board, BallMicroGrid balls)
        {
            Gizmos.matrix = board.Root.localToWorldMatrix;
            for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    CellKind kind = board.GetCell(cell);
                    if (kind == CellKind.Outside) continue;
                    Gizmos.color = kind == CellKind.Blocked ? Color.red : y < board.Definition.lowerGridHeight ? Color.gray : Color.cyan;
                    Gizmos.DrawWireCube(board.CellToLocal(cell), new Vector3(board.CellSize, 0.02f, board.CellSize));
                }
            if (balls == null) return;
            for (int y = board.Definition.lowerGridHeight * 3; y < balls.Height; y++)
                for (int x = 0; x < balls.Width; x++)
                {
                    BallState ball = balls.Get(new Vector2Int(x, y));
                    if (ball == null || ball.Visual != null) continue;
                    Gizmos.color = ball.Color.displayColor;
                    Gizmos.DrawSphere(balls.CellToLocal(ball.Cell) + Vector3.up * 0.1f, board.CellSize * 0.08f);
                }
        }
    }
}
