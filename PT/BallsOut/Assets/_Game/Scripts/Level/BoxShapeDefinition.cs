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

        public int FillSlotsPerLayer(bool dense) => FillSlotsPerLayer(dense, LevelDefinition.MicroResolution);

        // slotsPerSide x slotsPerSide balls per cell; dense fill also packs the seams between cells.
        public int FillSlotsPerLayer(bool dense, int slotsPerSide)
        {
            int side = Mathf.Clamp(slotsPerSide, 1, LevelDefinition.MicroResolution);
            int seam = FillSeam(dense, side);
            int count = CellCount * side * side;
            if (seam == 0) return count;
            var occupied = new HashSet<Vector2Int>(cells);
            foreach (Vector2Int cell in cells)
            {
                bool right = occupied.Contains(cell + Vector2Int.right);
                bool up = occupied.Contains(cell + Vector2Int.up);
                if (right) count += side * seam;
                if (up) count += side * seam;
                if (right && up && occupied.Contains(cell + Vector2Int.one)) count += seam * seam;
            }
            return count;
        }

        public static int FillSeam(bool dense, int slotsPerSide) => dense ? slotsPerSide / 2 : 0;
    }
}
