using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    [CreateAssetMenu(menuName = "Balls Out/Level")]
    public sealed class LevelDefinition : LevelData
    {
        public const int MicroResolution = 4;
        public const int CapacityPerMacroCell = 48;
        [Min(1)] public int macroGridWidth = 7;
        [Min(1)] public int lowerGridHeight = 4;
        [Min(1)] public int ballAreaMacroHeight = 6;
        [Min(0.01f)] public float macroCellSize = 1f;
        public CellMask lowerGridMask = new CellMask();
        public CellMask ballAreaMask = new CellMask();
        public List<BoxSpawnData> boxes = new List<BoxSpawnData>();
        public List<BallSpawnData> balls = new List<BallSpawnData>();
        [Tooltip("Editor dense-field authoring palette; runtime uses the explicit balls list.")]
        public List<BallColorDefinition> palette = new List<BallColorDefinition>();
        public int TotalHeight => lowerGridHeight + ballAreaMacroHeight;

        public CellKind GetCell(Vector2Int cell) => cell.y < lowerGridHeight
            ? lowerGridMask.Get(cell, macroGridWidth, lowerGridHeight)
            : ballAreaMask.Get(cell - new Vector2Int(0, lowerGridHeight), macroGridWidth, ballAreaMacroHeight);
    }
}
