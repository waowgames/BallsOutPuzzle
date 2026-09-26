using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    [CreateAssetMenu(menuName = "Balls Out/Level")]
    public sealed class LevelDefinition : LevelData
    {
        public const int MicroResolution = 4;
        public const int FillLayers = 3;
        [Min(1)] public int macroGridWidth = 7;
        [Min(1)] public int lowerGridHeight = 4;
        [Min(1)] public int ballAreaMacroHeight = 6;
        [Tooltip("0 = flat reservoir. Use 6–8 rows for a compact funnel on high-ball-count levels.")]
        [Min(0)] public int hopperMicroRows;
        [Min(0.01f)] public float macroCellSize = 1f;
        public bool denseBoxFill;
        [Min(1)] public int fillLayers = FillLayers;
        [Tooltip("Balls per box cell edge, per layer: 4 = 16 balls per cell, 3 = 9, 2 = 4. Lower values need fewer reservoir balls, so the board stays compact.")]
        [Range(1, MicroResolution)] public int boxSlotsPerSide = MicroResolution;
        [Tooltip("Macro-column boundaries that separate reservoir chambers. The outlet below stays open.")]
        public List<int> reservoirDividerColumns = new List<int>();
        public CellMask lowerGridMask = new CellMask();
        public CellMask ballAreaMask = new CellMask();
        public List<BoxSpawnData> boxes = new List<BoxSpawnData>();
        public List<BallSpawnData> balls = new List<BallSpawnData>();
        [Tooltip("Tubes on the reservoir's top edge. Each drops its queue into the top row below it whenever a site there is free.")]
        public List<BallFeederData> feeders = new List<BallFeederData>();
        [Tooltip("Conveyor over the reservoir: its queue rides a serpentine track down to a one-column gate. Empty queue = no conveyor.")]
        public BallConveyorData conveyor = new BallConveyorData();
        [Tooltip("Chained box pairs. A box drags its partner along once the chain between them is taut.")]
        public List<BoxLinkData> links = new List<BoxLinkData>();
        [Tooltip("Editor dense-field authoring palette; runtime uses the explicit balls list.")]
        public List<BallColorDefinition> palette = new List<BallColorDefinition>();
        public int FillLayerCount => fillLayers > 0 ? fillLayers : FillLayers;
        public int BoxSlotsPerSide => boxSlotsPerSide > 0 ? Mathf.Min(boxSlotsPerSide, MicroResolution) : MicroResolution;
        public int SlotsPerLayer(BoxShapeDefinition shape) => shape.FillSlotsPerLayer(denseBoxFill, BoxSlotsPerSide);
        public int BoxCapacity(BoxShapeDefinition shape) => SlotsPerLayer(shape) * FillLayerCount;
        public int TotalHeight => lowerGridHeight + ballAreaMacroHeight;
        public int HopperStartRow => ballAreaMacroHeight * MicroResolution - hopperMicroRows;
        public float BallRowSpacing => macroCellSize * 0.24f * 0.8660254f;
        public float DepotBottom => lowerGridHeight * macroCellSize;
        public float DepotTop => BallRowZ(ballAreaMacroHeight * MicroResolution - 0.5f);
        // Feeder tubes continue the reservoir lattice for this many ball rows above its top edge.
        public const int FeederRows = 8;
        public bool HasFeeders => feeders != null && feeders.Count > 0;
        public float FeederTop => BallRowZ(ballAreaMacroHeight * MicroResolution + FeederRows - 0.5f);
        public bool HasConveyor => conveyor != null && conveyor.queue != null && conveyor.queue.Count > 0;
        // Highest point of the board art: the tube caps or the conveyor's chute, with their counters.
        public float BoardTop => HasConveyor ? ConveyorTrack.ChuteTop(this) + macroCellSize * 0.3f
            : HasFeeders ? FeederTop + macroCellSize * 0.3f : DepotTop;

        public bool HasFeederAt(int column)
        {
            if (feeders == null) return false;
            foreach (BallFeederData feeder in feeders)
                if (feeder != null && feeder.column == column) return true;
            return false;
        }
        // Balls rest on this; the rails share the grid's base so the whole outline is one piece.
        public float DepotFloorHeight => macroCellSize * 0.13f;

        // Leave clearance for the rounded divider at the front of the reservoir.
        public float BallRowZ(float row) => DepotBottom + macroCellSize * 0.47f + row * BallRowSpacing;

        public float BallColumnX(int x, int row) => macroGridWidth * macroCellSize * 0.02f +
            (x + 0.5f + ((row & 1) == 0 ? -0.25f : 0.25f)) * macroCellSize * 0.24f;

        public float HopperHalfWidth(float row) => Mathf.Lerp(macroCellSize * 0.68f,
            macroGridWidth * macroCellSize * 0.43f,
            Mathf.Clamp01((row - HopperStartRow + 0.5f) / Mathf.Max(1, hopperMicroRows)));

        public CellKind GetCell(Vector2Int cell) => cell.y < lowerGridHeight
            ? lowerGridMask.Get(cell, macroGridWidth, lowerGridHeight)
            : ballAreaMask.Get(cell - new Vector2Int(0, lowerGridHeight), macroGridWidth, ballAreaMacroHeight);

        public bool IsBallMicroCell(Vector2Int cell)
        {
            int width = macroGridWidth * MicroResolution;
            int firstRow = lowerGridHeight * MicroResolution;
            int lastRow = TotalHeight * MicroResolution;
            if (cell.x < 0 || cell.x >= width || cell.y < firstRow || cell.y >= lastRow ||
                GetCell(new Vector2Int(cell.x / MicroResolution, cell.y / MicroResolution)) != CellKind.Usable)
                return false;
            int row = cell.y - firstRow;
            if (hopperMicroRows == 0 || row < HopperStartRow) return true;
            float slope = (macroGridWidth * macroCellSize * 0.43f - macroCellSize * 0.68f) /
                (hopperMicroRows * BallRowSpacing);
            // Radius clearance is perpendicular to the sloping wall, not just horizontal.
            float clearance = macroCellSize * 0.13f * Mathf.Sqrt(1f + slope * slope);
            return Mathf.Abs(BallColumnX(cell.x, row) - macroGridWidth * macroCellSize * 0.5f)
                <= HopperHalfWidth(row) - clearance;
        }
    }
}
