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
        [SerializeField, Range(0, 3)] private int sinkReachRows = 2;
        [SerializeField] private bool waitForLevelStart;
        [SerializeField] private bool drawDebugGizmos = true;
        private Transform content;
        private BoardDragInput dragInput;
        private BallPool pool;
        private bool running;
        private readonly List<BoxController> boxes = new List<BoxController>();
        // Boosters wait for the ball clock's next safe point, when no ball is mid-step.
        private int pendingFillBoxes;
        private bool pendingShuffle;
        private readonly List<BallState> shuffleBalls = new List<BallState>();
        private readonly List<Vector3> shuffleFrom = new List<Vector3>();
        private float shuffleElapsed = -1f;
        private const float ShuffleDuration = 0.5f;
        private const float FillStagger = 0.05f;
        // Longest a booster fill keeps launching balls into one box.
        private const float FillSpread = 0.8f;
        // The runtime of the level being played, if any.
        public static BallBoxLevelRuntime Active { get; private set; }
        // Raised once a runtime has built its level and its systems are ready to subscribe to.
        public static event Action<BallBoxLevelRuntime> OnRuntimeLoaded;
        public BoardGrid Board { get; private set; }
        public BallMicroGrid Balls { get; private set; }
        public BallFeederSystem Feeder { get; private set; }
        public BallConveyorSystem Conveyor { get; private set; }
        public BoardObstacleSystem Obstacles { get; private set; }
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
        public event Action<BoxController> OnInnerLayerCompleted;
        public event Action<BoxController> OnBoxRemoved;
        public event Action<BoxController> OnIceCracked;
        public event Action<BoxController> OnIceBroken;
        // A key reached this padlocked box; OnBoxUnlocked follows when it was the last one.
        public event Action<BoxController> OnKeyDelivered;
        public event Action<BoxController> OnBoxUnlocked;
        // The completed box's chain snapped, freeing its partner.
        public event Action<BoxController> OnChainBroken;
        // A completed box chipped this stone block; OnObstacleCleared follows when it crumbled.
        public event Action<BoardObstacle> OnObstacleCracked;
        public event Action<BoardObstacle> OnObstacleCleared;
        public event Action<LevelDefinition> OnLevelWon;
        public event Action OnBoxesFilledByBooster;
        public event Action OnBallsShuffled;

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
            foreach (BoxController box in boxes)
            {
                if (box.LockId == null) continue;
                int keys = 0;
                foreach (BoxController other in boxes)
                    if (other.KeyId == box.LockId) keys++;
                box.SetupLock(keys, Board.CellSize);
            }
            Obstacles = new BoardObstacleSystem(Board, prefabs);
            Obstacles.OnObstacleCracked += ForwardObstacleCracked;
            Obstacles.OnObstacleCleared += ForwardObstacleCleared;
            if (level.links != null)
                foreach (BoxLinkData link in level.links)
                {
                    BoxController a = FindBox(link.boxA), b = FindBox(link.boxB);
                    if (a != null && b != null) BoxChain.Create(a, b, link.length, Board);
                }
            float height = prefabs != null ? prefabs.ballHeight : Board.CellSize * 0.1f;
            foreach (var spawn in level.balls)
            {
                var ball = new BallState(spawn);
                Balls.Add(ball);
                ball.Visual = pool.Rent(ball.Color);
                if (ball.Visual != null) ball.Visual.localPosition = Balls.CellToLocal(ball.Cell) + Vector3.up * height;
            }
            Feeder = new BallFeederSystem(Balls, pool, content, height);
            Conveyor = new BallConveyorSystem(Balls, pool, content, height);
            Fill = new BoxFillSystem(pool, prefabs != null ? prefabs.fillDuration : 0.26f,
                Balls.Count + Feeder.Remaining + Conveyor.Remaining);
            Completion = new BoxCompletionSystem(Board, Fill, boxes.Count, prefabs != null ? prefabs.completionDuration : 1.1f,
                prefabs != null ? prefabs.completionDelay : 0.3f);
            Completion.OnBoxCompleted += ForwardBoxCompleted;
            Completion.OnBoxRemoved += ForwardBoxRemoved;
            Completion.OnInnerLayerCompleted += ForwardInnerLayerCompleted;
            Completion.OnBoxCompleted += CrackIce;
            Completion.OnBoxCompleted += SendKey;
            Completion.OnBoxCompleted += BreakChain;
            Completion.OnBoxCompleted += ChipObstacles;
            Collection = new BallCollectionSystem(Balls, Fill, Completion, sinkReachRows);
            Collection.OnBallCollected += ForwardBallCollected;
            Simulation = new BallSimulationSystem(Balls, Collection, simulationTick, height, AdvanceBoxSystems, HasPendingBoxWork, sinkReachRows, Feeder,
                Conveyor);
            Movement = new BoxMovementSystem(Board, Balls, boxStepDuration);
            dragInput = GetComponent<BoardDragInput>();
            dragInput.Initialize(Board, Movement);
            running = !waitForLevelStart || (LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing);
            dragInput.enabled = running && isActiveAndEnabled;
            Active = this;
            OnRuntimeLoaded?.Invoke(this);
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
            RenderShuffle(Time.deltaTime);
            Fill.Render(Simulation.InterpolationTime);
            Completion.Render(Simulation.InterpolationTime);
            if (Balls.Count == 0 && Feeder.Remaining == 0 && Conveyor.Remaining == 0 && Completion.RemainingBoxes == 0 && !Fill.IsAnimating && !Simulation.IsAnimating)
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
            RunPendingBoosters();
            // Occupancy release shares the deterministic ball clock. Rendering
            // interpolates separately, so frame rate cannot change collection order.
            Fill.Advance(tickInterval);
            Completion.Advance(tickInterval);
        }

        // ---- Boosters ----

        public bool CanFillBoxes()
        {
            if (!running || Balls == null || pendingFillBoxes > 0) return false;
            foreach (BoxController box in boxes)
                if (CanBoosterFill(box) && HasBallOfColor(box.ActiveColor)) return true;
            return false;
        }

        // Flies matching balls from anywhere in the pile into up to maxBoxes boxes, fullest first.
        public bool FillBoxes(int maxBoxes)
        {
            if (!CanFillBoxes()) return false;
            pendingFillBoxes = Mathf.Max(1, maxBoxes);
            Board.NotifyBoxStateChanged();
            return true;
        }

        public bool CanShuffle() => running && Balls != null && Balls.Count > 1 && !pendingShuffle && shuffleElapsed < 0f;

        // Reshuffles the pile so each open box finds its own colour right above it.
        public bool ShuffleBalls()
        {
            if (!CanShuffle()) return false;
            pendingShuffle = true;
            Board.NotifyBoxStateChanged();
            return true;
        }

        private static bool CanBoosterFill(BoxController box) =>
            box.IsPlaced && !box.IsFrozen && !box.IsLocked && !box.IsCompleting && !box.IsSwappingLayer &&
            !box.IsRemoved && !box.IsInTransit && box.CurrentFill < box.Capacity;

        private bool HasBallOfColor(BallColorDefinition color)
        {
            foreach (BallState ball in PileBottomUp())
                if (ball.Color == color) return true;
            return false;
        }

        private List<BallState> PileBottomUp()
        {
            var pile = new List<BallState>(Balls.Count);
            for (int y = 0; y < Balls.Height; y++)
                for (int x = 0; x < Balls.Width; x++)
                {
                    BallState ball = Balls.Get(new Vector2Int(x, y));
                    if (ball != null) pile.Add(ball);
                }
            return pile;
        }

        // Runs on the ball clock between ticks, so no ball is mid-step.
        private void RunPendingBoosters()
        {
            if (pendingFillBoxes > 0)
            {
                int maxBoxes = pendingFillBoxes;
                pendingFillBoxes = 0;
                RunFillBoxes(maxBoxes);
            }
            if (pendingShuffle)
            {
                pendingShuffle = false;
                RunShuffle();
            }
        }

        private void RunFillBoxes(int maxBoxes)
        {
            // Lowest balls go first, so the pile barely shifts.
            List<BallState> pile = PileBottomUp();
            var targets = new List<BoxController>();
            foreach (BoxController box in boxes)
                if (CanBoosterFill(box)) targets.Add(box);
            targets.Sort((a, b) => (a.Capacity - a.CurrentFill).CompareTo(b.Capacity - b.CurrentFill));
            int filled = 0;
            foreach (BoxController box in targets)
            {
                if (filled >= maxBoxes) break;
                // Boxes fill side by side; a big box streams its balls faster to finish in time.
                float stagger = Mathf.Min(FillStagger, FillSpread / Mathf.Max(1, box.Capacity - box.CurrentFill));
                float delay = 0f;
                bool any = false;
                for (int i = 0; i < pile.Count && box.CurrentFill < box.Capacity; i++)
                {
                    BallState ball = pile[i];
                    if (ball == null || ball.Color != box.ActiveColor) continue;
                    pile[i] = null;
                    Collection.Collect(ball, box, delay);
                    delay += stagger;
                    any = true;
                }
                if (any) filled++;
            }
            if (filled > 0) OnBoxesFilledByBooster?.Invoke();
        }

        private void RunShuffle()
        {
            List<BallState> pile = PileBottomUp();
            if (pile.Count < 2) return;
            var cells = new List<Vector2Int>(pile.Count);
            foreach (BallState ball in pile) cells.Add(ball.Cell);
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
            // Cells run bottom row first: each one right above an open box takes a ball of that
            // box's colour while any are left; every other cell gets a random ball.
            int boxRow = Board.Definition.lowerGridHeight - 1;
            shuffleBalls.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                BoxController below = Board.GetBox(new Vector2Int(BallMicroGrid.ToMacro(cells[i]).x, boxRow));
                BallState pick = null;
                if (below != null && CanBoosterFill(below))
                    for (int j = 0; j < pile.Count; j++)
                        if (pile[j] != null && pile[j].Color == below.ActiveColor) { pick = pile[j]; pile[j] = null; break; }
                shuffleBalls.Add(pick);
            }
            int next = 0;
            for (int i = 0; i < shuffleBalls.Count; i++)
            {
                if (shuffleBalls[i] != null) continue;
                while (pile[next] == null) next++;
                shuffleBalls[i] = pile[next++];
            }
            shuffleFrom.Clear();
            foreach (BallState ball in shuffleBalls)
                shuffleFrom.Add(ball.Visual != null ? ball.Visual.localPosition : Vector3.zero);
            Balls.Rearrange(shuffleBalls, cells);
            shuffleElapsed = 0f;
            Simulation.Hold(ShuffleDuration);
            OnBallsShuffled?.Invoke();
        }

        private void RenderShuffle(float deltaTime)
        {
            if (shuffleElapsed < 0f) return;
            shuffleElapsed += deltaTime;
            float t = Mathf.Clamp01(shuffleElapsed / ShuffleDuration);
            float eased = t * t * (3f - 2f * t);
            float height = prefabs != null ? prefabs.ballHeight : Board.CellSize * 0.1f;
            float hop = Mathf.Sin(t * Mathf.PI) * Board.CellSize * 0.35f;
            for (int i = 0; i < shuffleBalls.Count; i++)
            {
                BallState ball = shuffleBalls[i];
                if (ball.Visual == null) continue;
                Vector3 end = Balls.CellToLocal(ball.Cell) + Vector3.up * height;
                ball.Visual.localPosition = Vector3.Lerp(shuffleFrom[i], end, eased) + Vector3.up * hop;
            }
            if (t < 1f) return;
            shuffleElapsed = -1f;
            shuffleBalls.Clear();
            shuffleFrom.Clear();
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

        // A completed key box sends its key flying to the matching padlock.
        private void SendKey(BoxController completed)
        {
            if (!completed.HasKey) return;
            BoxController target = null;
            foreach (BoxController box in boxes)
                if (box.IsLocked && box.LockId == completed.KeyId) { target = box; break; }
            completed.SendKey(target, content, DeliverKey);
        }

        // A completed box drops its chain; the partner is free from then on.
        private void BreakChain(BoxController completed)
        {
            if (completed.Chain == null || completed.Chain.IsBroken) return;
            completed.Chain.Break();
            OnChainBroken?.Invoke(completed);
        }

        // Every completed box chips one off each standing stone block.
        private void ChipObstacles(BoxController completed) => Obstacles?.Chip();

        private BoxController FindBox(string id)
        {
            foreach (BoxController box in boxes)
                if (box.Id == id) return box;
            return null;
        }

        private void DeliverKey(BoxController target)
        {
            // The level may have been reloaded while the key was in the air.
            if (target == null || Board == null || !boxes.Contains(target)) return;
            bool opened = target.ReceiveKey();
            OnKeyDelivered?.Invoke(target);
            if (!opened) return;
            // Wakes the ball simulation so balls can drop into the freed box.
            Board.NotifyBoxStateChanged();
            OnBoxUnlocked?.Invoke(target);
        }

        private void ForwardFillChanged(BoxController box) => OnBoxFillChanged?.Invoke(box);
        private void ForwardBallCollected(BallState ball, BoxController box) => OnBallCollected?.Invoke(ball, box);
        private void ForwardBoxCompleted(BoxController box) => OnBoxCompleted?.Invoke(box);
        private void ForwardBoxRemoved(BoxController box) => OnBoxRemoved?.Invoke(box);
        private void ForwardInnerLayerCompleted(BoxController box) => OnInnerLayerCompleted?.Invoke(box);
        private void ForwardObstacleCracked(BoardObstacle obstacle) => OnObstacleCracked?.Invoke(obstacle);
        private void ForwardObstacleCleared(BoardObstacle obstacle) => OnObstacleCleared?.Invoke(obstacle);

        private void Clear()
        {
            running = false;
            HasWon = false;
            pendingFillBoxes = 0;
            pendingShuffle = false;
            shuffleElapsed = -1f;
            shuffleBalls.Clear();
            shuffleFrom.Clear();
            Movement?.Cancel();
            if (Collection != null) Collection.OnBallCollected -= ForwardBallCollected;
            if (Completion != null)
            {
                Completion.OnBoxCompleted -= ForwardBoxCompleted;
                Completion.OnBoxRemoved -= ForwardBoxRemoved;
                Completion.OnInnerLayerCompleted -= ForwardInnerLayerCompleted;
                Completion.OnBoxCompleted -= CrackIce;
                Completion.OnBoxCompleted -= SendKey;
                Completion.OnBoxCompleted -= BreakChain;
                Completion.OnBoxCompleted -= ChipObstacles;
            }
            if (Obstacles != null)
            {
                Obstacles.OnObstacleCracked -= ForwardObstacleCracked;
                Obstacles.OnObstacleCleared -= ForwardObstacleCleared;
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
            Feeder = null;
            Conveyor = null;
            Obstacles = null;
            Simulation = null;
            Movement = null;
            Collection = null;
            Fill = null;
            Completion = null;
        }

        private void OnDestroy()
        {
            Clear();
            if (Active == this) Active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            OnRuntimeLoaded = null;
        }
        private void OnDrawGizmos() { if (drawDebugGizmos && Board != null) BoardPresentation.DrawGizmos(Board, Balls); }
    }
}
