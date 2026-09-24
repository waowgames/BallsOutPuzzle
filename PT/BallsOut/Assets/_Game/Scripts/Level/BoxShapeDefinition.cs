using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    [CreateAssetMenu(menuName = "Balls Out/Box Shape")]
    public sealed class BoxShapeDefinition : ScriptableObject
    {
        [SerializeField] private Vector2Int[] cells = { Vector2Int.zero };
        public IReadOnlyList<Vector2Int> Cells => cells;
        public int CellCount => cells == null ? 0 : cells.Length;

        public int FillSlotsPerLayer(bool dense)
        {
            int count = CellCount * LevelDefinition.MicroResolution * LevelDefinition.MicroResolution;
            if (!dense) return count;
            var occupied = new HashSet<Vector2Int>(cells);
            foreach (Vector2Int cell in cells)
            {
                bool right = occupied.Contains(cell + Vector2Int.right);
                bool up = occupied.Contains(cell + Vector2Int.up);
                if (right) count += LevelDefinition.MicroResolution * 2;
                if (up) count += LevelDefinition.MicroResolution * 2;
                if (right && up && occupied.Contains(cell + Vector2Int.one)) count += 4;
            }
            return count;
        }
    }
}
