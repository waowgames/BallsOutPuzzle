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
    }
}
