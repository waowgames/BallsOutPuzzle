using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoardObstacle
    {
        public RectInt Area { get; }
        public int Count { get; internal set; }
        public bool IsCleared => Count <= 0;
        internal ObstacleVisual Visual { get; set; }

        internal BoardObstacle(RectInt area, int count)
        {
            Area = area;
            Count = count;
        }
    }

    // Stone blocks standing on lower-grid cells. Each completed box chips one off every block;
    // at zero a block crumbles and its cells open to boxes again.
    public sealed class BoardObstacleSystem
    {
        private readonly BoardGrid board;
        private readonly List<BoardObstacle> obstacles = new List<BoardObstacle>();
        public IReadOnlyList<BoardObstacle> Obstacles => obstacles;
        public event Action<BoardObstacle> OnObstacleCracked;
        public event Action<BoardObstacle> OnObstacleCleared;

        public BoardObstacleSystem(BoardGrid board, PrefabRegistry registry)
        {
            this.board = board;
            if (!board.Definition.HasObstacles) return;
            foreach (BoardObstacleData data in board.Definition.obstacles)
            {
                if (data == null) continue;
                var obstacle = new BoardObstacle(new RectInt(data.origin, data.size), Mathf.Max(1, data.count));
                board.SetObstructed(obstacle.Area, true);
                obstacle.Visual = ObstacleVisual.Create(board, registry, obstacle.Area, obstacle.Count);
                obstacles.Add(obstacle);
            }
        }

        // One box completed: every standing block loses one from its count.
        internal void Chip()
        {
            bool cleared = false;
            foreach (BoardObstacle obstacle in obstacles)
            {
                if (obstacle.IsCleared) continue;
                obstacle.Count--;
                if (obstacle.Visual != null) obstacle.Visual.SetCount(obstacle.Count);
                if (!obstacle.IsCleared)
                {
                    OnObstacleCracked?.Invoke(obstacle);
                    continue;
                }
                board.SetObstructed(obstacle.Area, false);
                cleared = true;
                OnObstacleCleared?.Invoke(obstacle);
            }
            // Wakes the ball simulation and re-checks drags through the freed cells.
            if (cleared) board.NotifyBoxStateChanged();
        }
    }
}
