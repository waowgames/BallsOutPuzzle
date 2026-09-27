using System.Collections.Generic;
using BallsOut;
using UnityEngine;

/// <summary>A move the first-level tutorial demonstrates: which box, and the cells it passes through.</summary>
public sealed class TutorialMovePlan
{
    public BoxController Box;
    // Box origins from the current one to the target, turning points only.
    public List<Vector2Int> Path;
    // The shape cell the hand grabs, near the middle of the box.
    public Vector2Int Grab;
    public Vector2Int Target => Path[Path.Count - 1];
    public bool Moves => Path.Count > 1;
}

/// <summary>
/// Board queries for the tutorial: screen rectangles of boxes, cells and UI, and a planner
/// that finds a good first move (a box slid under balls of its own colour).
/// </summary>
public static class TutorialBoard
{
    private const int NearRows = 3;
    private const int FarRows = 8;

    // ---------------- Screen geometry ----------------

    public static Rect? WorldRect(Bounds bounds)
    {
        Camera camera = Camera.main;
        if (camera == null) return null;
        Vector3 min = bounds.min, max = bounds.max;
        var rect = new Rect();
        bool any = false;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            Vector3 screen = camera.WorldToScreenPoint(corner);
            if (screen.z <= 0f) return null;
            if (!any) rect = new Rect(screen.x, screen.y, 0f, 0f);
            else rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, screen.x), Mathf.Min(rect.yMin, screen.y),
                Mathf.Max(rect.xMax, screen.x), Mathf.Max(rect.yMax, screen.y));
            any = true;
        }
        return rect;
    }

    public static Vector2? ScreenPoint(Vector3 world)
    {
        Camera camera = Camera.main;
        if (camera == null) return null;
        Vector3 screen = camera.WorldToScreenPoint(world);
        return screen.z > 0f ? new Vector2(screen.x, screen.y) : (Vector2?)null;
    }

    public static Rect? BoxRect(BoxController box)
    {
        if (box == null) return null;
        BoxCollider[] colliders = box.GetComponents<BoxCollider>();
        if (colliders.Length == 0) return null;
        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
        return WorldRect(bounds);
    }

    /// <summary>Screen point on the middle of a box, where the hand taps it.</summary>
    public static Vector2? BoxPoint(BoxController box)
    {
        Rect? rect = BoxRect(box);
        return rect.HasValue ? rect.Value.center : (Vector2?)null;
    }

    /// <summary>Board-local rectangle (x/z, in world units before the root transform) to screen.</summary>
    public static Rect? BoardRect(BoardGrid board, float xMin, float zMin, float xMax, float zMax)
    {
        if (board == null || board.Root == null) return null;
        var bounds = new Bounds(board.Root.TransformPoint(new Vector3(xMin, 0f, zMin)), Vector3.zero);
        bounds.Encapsulate(board.Root.TransformPoint(new Vector3(xMax, 0f, zMin)));
        bounds.Encapsulate(board.Root.TransformPoint(new Vector3(xMin, 0f, zMax)));
        bounds.Encapsulate(board.Root.TransformPoint(new Vector3(xMax, 0f, zMax)));
        bounds.Encapsulate(board.Root.TransformPoint(new Vector3(xMin, board.CellSize * 0.3f, zMin)));
        return WorldRect(bounds);
    }

    public static Rect? CellsRect(BoardGrid board, RectInt cells)
    {
        if (board == null) return null;
        float size = board.CellSize;
        return BoardRect(board, cells.xMin * size, cells.yMin * size, cells.xMax * size, cells.yMax * size);
    }

    /// <summary>The cells a box covers at the origin, plus the lowest ball rows above them.</summary>
    public static Rect? DropZoneRect(BoardGrid board, BoxController box, Vector2Int origin, float ballRows = 1.4f)
    {
        if (board == null || box == null) return null;
        RectInt area = Footprint(box, origin);
        float size = board.CellSize;
        float top = area.yMax >= board.Definition.lowerGridHeight
            ? (board.Definition.lowerGridHeight + ballRows) * size
            : area.yMax * size;
        return BoardRect(board, area.xMin * size, area.yMin * size, area.xMax * size, top);
    }

    /// <summary>The strip over the reservoir where feeder tubes and the conveyor sit.</summary>
    public static Rect? TopRect(BoardGrid board)
    {
        if (board == null) return null;
        LevelDefinition level = board.Definition;
        float top = level.BoardTop;
        float bottom = Mathf.Min(level.DepotTop, top - level.macroCellSize);
        return BoardRect(board, 0f, bottom, level.macroGridWidth * level.macroCellSize, top);
    }

    public static Rect? UIRect(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return null;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public static Vector2? UICenter(RectTransform rect)
    {
        Rect? screen = UIRect(rect);
        return screen.HasValue ? screen.Value.center : (Vector2?)null;
    }

    public static RectInt Footprint(BoxController box, Vector2Int origin)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        foreach (Vector2Int cell in box.Shape.Cells)
        {
            xMin = Mathf.Min(xMin, origin.x + cell.x);
            yMin = Mathf.Min(yMin, origin.y + cell.y);
            xMax = Mathf.Max(xMax, origin.x + cell.x + 1);
            yMax = Mathf.Max(yMax, origin.y + cell.y + 1);
        }
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // ---------------- First move planner ----------------

    /// <summary>
    /// How well the box would collect at this origin: matching balls straight above its cells
    /// on the collection row, the lowest rows counting most. Zero means nothing would drop in.
    /// </summary>
    public static int Score(BallBoxLevelRuntime runtime, BoxController box, Vector2Int origin)
    {
        if (runtime == null || runtime.Balls == null || box == null) return 0;
        BoardGrid board = runtime.Board;
        BallMicroGrid balls = runtime.Balls;
        int mouthRow = board.Definition.lowerGridHeight - 1;
        int firstBallRow = board.Definition.lowerGridHeight * LevelDefinition.MicroResolution;
        int score = 0;
        foreach (Vector2Int offset in box.Shape.Cells)
        {
            Vector2Int cell = origin + offset;
            if (cell.y != mouthRow) continue;
            for (int x = cell.x * LevelDefinition.MicroResolution; x < (cell.x + 1) * LevelDefinition.MicroResolution; x++)
                for (int row = 0; row < FarRows; row++)
                {
                    BallState ball = balls.Get(new Vector2Int(x, firstBallRow + row));
                    if (ball != null && ball.Color == box.ActiveColor) score += row < NearRows ? 3 : 1;
                }
        }
        return score;
    }

    /// <summary>Finds the clearest first move on the board, or null when no box can move.</summary>
    public static TutorialMovePlan PlanMove(BallBoxLevelRuntime runtime)
    {
        if (runtime == null || runtime.Board == null) return null;
        TutorialMovePlan best = null, bestMoving = null;
        int bestScore = -1, bestMovingScore = 0;
        int bestLength = int.MaxValue, bestMovingLength = int.MaxValue;
        foreach (BoxController box in runtime.Boxes)
        {
            if (box == null || !box.CanMove || box.IsInTransit) continue;
            Dictionary<Vector2Int, Vector2Int> parents = Reachable(runtime, box);
            foreach (Vector2Int origin in parents.Keys)
            {
                int score = Score(runtime, box, origin);
                int length = PathLength(parents, box.Origin, origin);
                if (score > bestScore || score == bestScore && length < bestLength)
                {
                    best = Plan(box, parents, origin);
                    bestScore = score;
                    bestLength = length;
                }
                if (length > 0 && score > 0 && (score > bestMovingScore || score == bestMovingScore && length < bestMovingLength))
                {
                    bestMoving = Plan(box, parents, origin);
                    bestMovingScore = score;
                    bestMovingLength = length;
                }
            }
        }
        // Teaching the drag matters more than the perfect spot: show a real slide when a decent one exists.
        if (bestMoving != null && bestMovingScore * 2 >= bestScore) return bestMoving;
        return best;
    }

    /// <summary>Screen path the hand drags along: the grabbed cell's centre at each turning point.</summary>
    public static List<Vector2> ScreenPath(BoardGrid board, TutorialMovePlan plan)
    {
        if (board == null || plan == null || plan.Box == null) return null;
        var path = new List<Vector2>(plan.Path.Count);
        float lift = board.CellSize * 0.2f;
        // The press lands on the box where it stands now.
        foreach (Vector2Int origin in plan.Path)
        {
            Vector2? point = ScreenPoint(board.CellToWorld(origin + plan.Grab) + board.Root.up * lift);
            if (!point.HasValue) return null;
            path.Add(point.Value);
        }
        return path;
    }

    private static Dictionary<Vector2Int, Vector2Int> Reachable(BallBoxLevelRuntime runtime, BoxController box)
    {
        var parents = new Dictionary<Vector2Int, Vector2Int> { [box.Origin] = box.Origin };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(box.Origin);
        var steps = new List<Vector2Int>(4);
        if (box.MoveAxis != BoxMoveAxis.Vertical) { steps.Add(Vector2Int.left); steps.Add(Vector2Int.right); }
        if (box.MoveAxis != BoxMoveAxis.Horizontal) { steps.Add(Vector2Int.down); steps.Add(Vector2Int.up); }
        while (queue.Count > 0 && parents.Count < 256)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int step in steps)
            {
                Vector2Int next = current + step;
                if (parents.ContainsKey(next) || !runtime.Board.CanPlace(box, next, runtime.Balls)) continue;
                parents[next] = current;
                queue.Enqueue(next);
            }
        }
        return parents;
    }

    private static int PathLength(Dictionary<Vector2Int, Vector2Int> parents, Vector2Int start, Vector2Int end)
    {
        int length = 0;
        for (Vector2Int cell = end; cell != start; cell = parents[cell]) length++;
        return length;
    }

    private static TutorialMovePlan Plan(BoxController box, Dictionary<Vector2Int, Vector2Int> parents, Vector2Int target)
    {
        var full = new List<Vector2Int>();
        for (Vector2Int cell = target; ; cell = parents[cell])
        {
            full.Add(cell);
            if (cell == box.Origin) break;
        }
        full.Reverse();
        // Keep only the start, the corners and the end, so the hand drags in clean straight runs.
        var path = new List<Vector2Int> { full[0] };
        for (int i = 1; i < full.Count - 1; i++)
            if (full[i] - full[i - 1] != full[i + 1] - full[i]) path.Add(full[i]);
        if (full.Count > 1) path.Add(full[full.Count - 1]);
        return new TutorialMovePlan { Box = box, Path = path, Grab = GrabCell(box) };
    }

    private static Vector2Int GrabCell(BoxController box)
    {
        Vector2 centre = Vector2.zero;
        foreach (Vector2Int cell in box.Shape.Cells) centre += cell;
        centre /= Mathf.Max(1, box.Shape.Cells.Count);
        Vector2Int best = box.Shape.Cells[0];
        float bestDistance = float.MaxValue;
        foreach (Vector2Int cell in box.Shape.Cells)
        {
            float distance = (cell - centre).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = cell; }
        }
        return best;
    }
}
