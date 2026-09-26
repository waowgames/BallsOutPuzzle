using UnityEngine;

namespace BallsOut
{
    // Plain state: no per-ball MonoBehaviour, Update or physics authority.
    public sealed class BallState
    {
        public BallColorDefinition Color { get; }
        public Vector2Int Cell { get; internal set; }
        public Transform Visual { get; internal set; }
        internal Vector3 AnimationStart;
        internal Vector3 AnimationEnd;
        internal Vector2Int PreviousMacro;

        public BallState(BallSpawnData spawn) : this(spawn.color, spawn.cell) { }

        public BallState(BallColorDefinition color, Vector2Int cell)
        {
            Color = color;
            Cell = cell;
        }
    }
}
