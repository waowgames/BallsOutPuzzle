using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    [RequireComponent(typeof(BoardDragInput))]
    public sealed class BallBoxLevelRuntime : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private PrefabRegistry prefabs;
        [SerializeField, Min(0.02f)] private float simulationTick = 0.08f;
        [SerializeField, Min(0.02f)] private float boxStepDuration = 0.08f;
        [Tooltip("Lowest ball rows above a box from which a matching ball flies straight in, past the balls under it. " +
            "Other balls never move for it. 0 = off.")]
        [SerializeField, Min(0)] private int sinkReachRows = 2;
        [SerializeField] private bool waitForLevelStart;
        [SerializeField] private bool drawDebugGizmos = true;
        private Transform content;
        private BoardDragInput dragInput;
        private BallPool pool;
        private bool running;
        private readonly List<BoxController> boxes = new List<BoxController>();
        public BoardGrid Board { get; private set; }
        public BallMicroGrid Balls { get; private set; }
        public BallSimulationSystem Simulation { get; private set; }
        public BoxMovementSystem Movement { get; private set; }
        public BallCollectionSystem Collection { get; private set; }
        public BoxFillSystem Fill { get; private set; }
        public BoxCompletionSystem Completion { get; private set; }
        public bool HasWon { get; private set; }
        public IReadOnlyList<BoxController> Boxes => boxes;
        public event Action<LevelDefinition> OnLevelStarted;
        public event Action<BoxController> OnBoxFillChanged;
        public event Action<BallState, BoxController> OnBallCollected;
        public event Action<BoxController> OnBoxCompleted;
        public event Action<BoxController> OnBoxRemoved;
        public event Action<BoxController> OnIceCracked;
        public event Action<BoxController> OnIceBroken;
        public event Action<LevelDefinition> OnLevelWon;

        private void OnEnable()
        {
            GameEvents.OnLevelStarted += HandleLevelStarted;
            if (dragInput != null) dragInput.enabled = running;
        }
        private void OnDisable()
        {
            GameEvents.OnLevelStarted -= HandleLevelStarted;
            Movement?.Cancel();
            if (dragInput != null) dragInput.enabled = false;
        }

        private void Start()
        {
            if (Board != null) return;
            // A shared runtime prefab must use the level selected by its owning loader.
            var loader = GetComponentInParent<LevelContentLoader>();
            LevelDefinition definition = loader != null ? loader.CurrentLevelData as LevelDefinition : level;
            if (definition == null && LevelManager.Instance != null)
                definition = LevelManager.Instance.CurrentLevelData as LevelDefinition;
            LoadLevel(definition);
        }

        public bool LoadLevel(LevelDefinition definition)
        {
            var errors = new List<string>();
            LevelValidator.Validate(definition, errors);
            if (errors.Count > 0)
            {
                Debug.LogError("[Balls Out] Cannot load level:\n" + string.Join("\n", errors), this);
                return false;
            }
            Clear();
            level = definition;
            GetComponentInChildren<BallHopperVisual>(true)?.Initialize(level);
            content = new GameObject("Board Content").transform;
            content.SetParent(transform, false);
            Board = new BoardGrid(level, content);
            Balls = new BallMicroGrid(Board);
            BoardPresentation.FrameBoard(Board);
            pool = new BallPool(prefabs, content);
            BoardPresentation.Build(Board, prefabs);
            foreach (var spawn in level.boxes)
            {
                var root = new GameObject("Box " + spawn.id);
                root.transform.SetParent(content, false);
                var box = root.AddComponent<BoxController>();
                box.Initialize(spawn, prefabs, Board.CellSize, level.denseBoxFill, level.FillLayerCount, level.BoxSlotsPerSide);
                box.CreateInitialFill(pool);
                box.runtimeCellSize = Board.CellSize;
                Board.TryPlace(box, spawn.startingMacroOrigin, Balls);
                root.transform.localPosition = Board.CellToLocal(box.Origin);
                boxes.Add(box);
                box.OnBoxFillChanged += ForwardFillChanged;
            }
            float height = prefabs != null ? prefabs.ballHeight : Board.CellSize * 0.1f;
            foreach (var spawn in level.balls)
            {
                var ball = new BallState(spawn);
                Balls.Add(ball);
                ball.Visual = pool.Rent(ball.Color);
                if (ball.Visual != null) ball.Visual.localPosition = Balls.CellToLocal(ball.Cell) + Vector3.up * height;
            }
            Fill = new BoxFillSystem(pool, prefabs != null ? prefabs.fillDuration : 0.26f, Balls.Count);
            Completion = new BoxCompletionSystem(Board, Fill, boxes.Count, prefabs != null ? prefabs.completionDuration : 0.25f);
            Completion.OnBoxCompleted += ForwardBoxCompleted;
            Completion.OnBoxRemoved += ForwardBoxRemoved;
            Completion.OnBoxCompleted += CrackIce;
            Collection = new BallCollectionSystem(Balls, Fill, Completion);
            Collection.OnBallCollected += ForwardBallCollected;
            Simulation = new BallSimulationSystem(Balls, Collection, simulationTick, height, AdvanceBoxSystems, HasPendingBoxWork, sinkReachRows);
            Movement = new BoxMovementSystem(Board, Balls, boxStepDuration);
            dragInput = GetComponent<BoardDragInput>();
            dragInput.Initialize(Board, Movement);
            running = !waitForLevelStart || (LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing);
            dragInput.enabled = running && isActiveAndEnabled;
            if (running) OnLevelStarted?.Invoke(level);
            if (prefabs == null || prefabs.ballPrefab == null)
                Debug.LogWarning("[Balls Out] Assign visual prefabs in the PrefabRegistry. Missing art uses editor gizmos; gameplay remains active.", this);
            return true;
        }

        private void HandleLevelStarted(int _)
        {
            if (Board == null || running || HasWon) return;
            running = true;
            dragInput.enabled = true;
            OnLevelStarted?.Invoke(level);
        }

        private void Update()
        {
            if (!running || Simulation == null) return;
            Movement.Advance(Time.deltaTime);
            Simulation.Advance(Time.deltaTime);
            Fill.Render(Simulation.InterpolationTime);
            Completion.Render(Simulation.InterpolationTime);
            if (Balls.Count == 0 && Completion.RemainingBoxes == 0 && !Fill.IsAnimating && !Simulation.IsAnimating)
            {
                HasWon = true;
                running = false;
                dragInput.enabled = false;
                LevelDefinition completedLevel = level;
                OnLevelWon?.Invoke(completedLevel);
                LevelManager manager = LevelManager.Instance;
                if (HasWon && manager != null && manager.CurrentLevelData == completedLevel && manager.State == LevelState.Playing)
                    manager.CompleteLevel();
            }
        }

        private bool HasPendingBoxWork() => Fill.IsAnimating || Completion.IsAnimating;
        private void AdvanceBoxSystems(float tickInterval)
        {
            // Occupancy release shares the deterministic ball clock. Rendering
            // interpolates separately, so frame rate cannot change collection order.
            Fill.Advance(tickInterval);
            Completion.Advance(tickInterval);
        }

        // Every completed box chips one step off each frozen box.
        private void CrackIce(BoxController completed)
        {
            bool thawed = false;
            foreach (BoxController box in boxes)
            {
                if (box == completed || !box.IsFrozen) continue;
                bool broke = box.CrackIce();
                if (broke) OnIceBroken?.Invoke(box);
                else OnIceCracked?.Invoke(box);
                thawed |= broke;
            }
            // Wakes the ball simulation so balls can drop into the freed box.
            if (thawed) Board.NotifyBoxStateChanged();
        }

        private void ForwardFillChanged(BoxController box) => OnBoxFillChanged?.Invoke(box);
        private void ForwardBallCollected(BallState ball, BoxController box) => OnBallCollected?.Invoke(ball, box);
        private void ForwardBoxCompleted(BoxController box) => OnBoxCompleted?.Invoke(box);
        private void ForwardBoxRemoved(BoxController box) => OnBoxRemoved?.Invoke(box);

        private void Clear()
        {
            running = false;
            HasWon = false;
            Movement?.Cancel();
            if (Collection != null) Collection.OnBallCollected -= ForwardBallCollected;
            if (Completion != null)
            {
                Completion.OnBoxCompleted -= ForwardBoxCompleted;
                Completion.OnBoxRemoved -= ForwardBoxRemoved;
                Completion.OnBoxCompleted -= CrackIce;
            }
            Fill?.Clear(boxes);
            foreach (var box in boxes) if (box != null) box.OnBoxFillChanged -= ForwardFillChanged;
            boxes.Clear();
            if (content != null)
            {
                content.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(content.gameObject);
                else DestroyImmediate(content.gameObject);
            }
            Board = null;
            Balls = null;
            Simulation = null;
            Movement = null;
            Collection = null;
            Fill = null;
            Completion = null;
        }

        private void OnDestroy() => Clear();
        private void OnDrawGizmos() { if (drawDebugGizmos && Board != null) BoardPresentation.DrawGizmos(Board, Balls); }
    }
}
