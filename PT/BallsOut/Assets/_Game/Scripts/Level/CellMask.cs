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
    public sealed class BoxSpawnData
    {
        public string id;
        public BoxShapeDefinition shape;
        public BallColorDefinition color;
        public Vector2Int startingMacroOrigin;
        public bool startsLocked;
        public string lockId;
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
}
