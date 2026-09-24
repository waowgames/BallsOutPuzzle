using UnityEngine;

namespace BallsOut
{
    public static class BoardPresentation
    {
        internal static void Build(BoardGrid board, PrefabRegistry registry)
        {
            if (registry == null) return;
            for (int y = 0; y < board.Definition.lowerGridHeight; y++)
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
                    if (kind == CellKind.Usable && ((x + y) & 1) != 0 && registry.alternateTileMaterial != null)
                        visual.GetComponentInChildren<Renderer>().sharedMaterial = registry.alternateTileMaterial;
                }
        }

        internal static void FrameBoard(BoardGrid board)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic) return;
            float width = board.Width * board.CellSize;
            float top = board.Definition.DepotTop;
            Vector3 center = board.Root.TransformPoint(new Vector3(width * 0.5f, 0f, top * 0.5f));
            Vector3 delta = center - camera.transform.position;
            camera.transform.position += camera.transform.right * Vector3.Dot(delta, camera.transform.right) +
                camera.transform.up * Vector3.Dot(delta, camera.transform.up);
            float extentX = 0f, extentY = 0f;
            for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = board.Root.TransformPoint(new Vector3(x * width, 0f, z * top)) - center;
                    extentX = Mathf.Max(extentX, Mathf.Abs(Vector3.Dot(corner, camera.transform.right)));
                    extentY = Mathf.Max(extentY, Mathf.Abs(Vector3.Dot(corner, camera.transform.up)));
                }
            // Reserve room for the top HUD and bottom controls on both wide and tall levels.
            camera.orthographicSize = Mathf.Max((extentY + board.CellSize * 0.5f) / 0.80f,
                (extentX + board.CellSize * 0.5f) / Mathf.Max(0.1f, camera.aspect));
        }

        internal static void DrawGizmos(BoardGrid board, BallMicroGrid balls)
        {
            Gizmos.matrix = board.Root.localToWorldMatrix;
            for (int y = 0; y < board.Definition.lowerGridHeight; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    CellKind kind = board.GetCell(cell);
                    if (kind == CellKind.Outside) continue;
                    Gizmos.color = kind == CellKind.Blocked ? Color.red : Color.gray;
                    Gizmos.DrawWireCube(board.CellToLocal(cell), new Vector3(board.CellSize, 0.02f, board.CellSize));
                }
            if (balls == null) return;
            for (int y = board.Definition.lowerGridHeight * LevelDefinition.MicroResolution; y < balls.Height; y++)
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
