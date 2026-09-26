using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public enum CellKind { Outside, Usable, Blocked }
    public enum BallSpecialType { Normal, Key }
    public enum BoxMoveAxis { Free, Horizontal, Vertical }

    [Serializable]
    public struct CellOverride
    {
        public Vector2Int cell;
        public CellKind kind;
    }

    [Serializable]
    public sealed class CellMask
    {
        public CellKind defaultKind = CellKind.Usable;
        public List<CellOverride> overrides = new List<CellOverride>();

        public CellKind Get(Vector2Int cell, int width, int height)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                return CellKind.Outside;
            for (int i = overrides.Count - 1; i >= 0; i--)
                if (overrides[i].cell == cell) return overrides[i].kind;
            return defaultKind;
        }
    }

    [Serializable]
    public sealed class BallSpawnData
    {
        public BallColorDefinition color;
        [Tooltip("Global micro coordinate: lower-grid height * micro resolution is the first ball-area row.")]
        public Vector2Int cell;
        public BallSpecialType specialType;
        public string keyId;
    }

    [Serializable]
    public sealed class FeederSegment
    {
        public BallColorDefinition color;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class BallFeederData
    {
        [Tooltip("Macro column of the reservoir's top edge the tube pours into.")]
        public int column;
        [Tooltip("Queued balls, first out first: each segment drops its count of one color.")]
        public List<FeederSegment> queue = new List<FeederSegment>();
    }

    [Serializable]
    public sealed class BallConveyorData
    {
        [Tooltip("Macro column of the reservoir's top edge the conveyor's gate pours into.")]
        public int column;
        [Tooltip("One-cell-wide runs of track stacked over the reservoir, joined by U-turns.")]
        [Range(1, 4)] public int runs = 3;
        [Tooltip("Balls on the belt, first out first: each segment carries its count of one color.")]
        public List<FeederSegment> queue = new List<FeederSegment>();
    }

    [Serializable]
    public sealed class BoxSpawnData
    {
        public string id;
        public BoxShapeDefinition shape;
        public BallColorDefinition color;
        public Vector2Int startingMacroOrigin;
        [Tooltip("Padlocked: the box cannot move or collect until every box carrying its key has completed.")]
        public bool startsLocked;
        [Tooltip("Name of this box's padlock. Key boxes with a matching keyId open it.")]
        public string lockId;
        [Tooltip("Optional key: completing this box sends a key to the padlocked box whose lockId matches.")]
        public string keyId;
        [Tooltip("0 = no ice. Frozen boxes cannot move or collect; each other completed box lowers the count by one.")]
        [Min(0)] public int iceCount;
        [Tooltip("Balls already held by this box when the level starts (counted against the inner layer when there is one).")]
        [Min(0)] public int initialFillCount;
        [Tooltip("Free, or locked to one axis. Locked boxes show a double-headed arrow.")]
        public BoxMoveAxis moveAxis;
        [Tooltip("Optional nested box: fills with this color first, then empties and fills again with the outer color. " +
            "Each layer holds the full shape capacity.")]
        public BallColorDefinition innerColor;
    }

    [Serializable]
    public sealed class BoxLinkData
    {
        [Tooltip("Id of one chained box.")]
        public string boxA;
        [Tooltip("Id of the box at the other end of the chain.")]
        public string boxB;
        [Tooltip("Free cells the chain spans: the two footprints can drift at most this many cells apart on either axis. " +
            "Dragging one further tows the other along.")]
        [Min(1)] public int length = 1;
    }
}
