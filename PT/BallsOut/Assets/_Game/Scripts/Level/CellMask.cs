using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public enum CellKind { Outside, Usable, Blocked }
    public enum BallSpecialType { Normal, Key }

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
        [Tooltip("Global micro coordinate: lower-grid height * 3 is the first ball-area row.")]
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
    }
}
