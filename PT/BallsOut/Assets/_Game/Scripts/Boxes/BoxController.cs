using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxController : MonoBehaviour
    {
        public string Id { get; private set; }
        public BoxShapeDefinition Shape { get; private set; }
        public BallColorDefinition Color { get; private set; }
        // Nested boxes collect their inner color first; null once the inner layer is done.
        public BallColorDefinition InnerColor { get; private set; }
        public bool HasInnerLayer => InnerColor != null;
        public BallColorDefinition ActiveColor => InnerColor != null ? InnerColor : Color;
        public BoxMoveAxis MoveAxis { get; private set; }
        public bool IsSwappingLayer { get; internal set; }
        public Vector2Int Origin { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsInTransit { get; internal set; }
        public int CurrentFill { get; private set; }
        public int Capacity { get; private set; }
        public bool IsCompleting { get; internal set; }
        public bool IsRemoved { get; internal set; }
        public bool HasPlayerInteracted { get; private set; }
        public int IceCount { get; private set; }
        public bool IsFrozen => IceCount > 0;
        public bool CanMove => IsPlaced && !IsFrozen && !IsCompleting && !IsSwappingLayer && !IsRemoved && CurrentFill < Capacity;
        public Transform FillRoot { get; private set; }
        public Vector3 FillSpacing { get; private set; }
        public float FillBallDiameter { get; private set; }
        // Usable interior width of one box cell, in cells.
        private const float FillInterior = 0.76f;
        public BoxCompletionAnimation CompletionAnimation { get; private set; }
        private BoxFillLabel fillLabel;
        private GameObject axisArrow;
        internal Transform InnerArt { get; private set; }
        private IceBoxVisual ice;
        private MeshRenderer shadow;
        // Collection pulse: a small damped spring, pivoting on the footprint centre.
        private const float PulseStiffness = 420f;
        private const float PulseDamping = 13f;
        private const float PulseImpulse = 1.1f;
        private const float PulseLimit = 0.08f;
        private Transform pulseRoot;
        private GameObject art;
        private float pulse;
        private float pulseVelocity;
        internal List<BallState> CollectedBalls { get; private set; }
        internal Vector3[] FillSlots { get; private set; }
        internal int PendingFillAnimations;
        public event Action<BoxController> OnBoxFillChanged;

        internal void Initialize(BoxSpawnData spawn, PrefabRegistry registry, float cellSize, bool denseFill, int fillLayers,
            int slotsPerSide = LevelDefinition.MicroResolution)
        {
            Id = spawn.id;
            Shape = spawn.shape;
            Color = spawn.color;
            InnerColor = spawn.innerColor;
            MoveAxis = spawn.moveAxis;
            IsSwappingLayer = false;
            HasPlayerInteracted = false;
            int resolution = Mathf.Clamp(slotsPerSide, 1, LevelDefinition.MicroResolution);
            int slotsPerLayer = Shape.FillSlotsPerLayer(denseFill, resolution);
            Capacity = slotsPerLayer * fillLayers;
            CurrentFill = spawn.initialFillCount;
            CollectedBalls = new List<BallState>(Capacity);
            FillRoot = new GameObject("Fill").transform;
            FillRoot.SetParent(transform, false);
            FillRoot.localPosition = (registry != null ? registry.fillOffset : new Vector3(0f, 0.085f, 0f)) * cellSize;
            int seam = BoxShapeDefinition.FillSeam(denseFill, resolution);
            int stride = resolution + seam;
            // Slots span the whole interior (walls stand ~0.12 cells in), so a full box reads as
            // packed wall to wall; fewer slots per cell simply means larger balls.
            bool seamed = seam != 0 && slotsPerLayer != Shape.CellCount * resolution * resolution;
            float slotSpacing = cellSize * (seamed ? Mathf.Min(FillInterior / resolution, 1.008f / stride) : FillInterior / resolution);
            FillSpacing = new Vector3(slotSpacing, cellSize / LevelDefinition.MicroResolution * 0.9f, slotSpacing);
            // Slight overlap hides the gaps of a square grid and reads as a heap.
            FillBallDiameter = slotSpacing * 1.08f;
            FillSlots = new Vector3[slotsPerLayer];
            var cells = new HashSet<Vector2Int>(Shape.Cells);
            float cellOffset = -(resolution - 1) * slotSpacing * 0.5f;
            int slot = 0;
            foreach (Vector2Int cell in Shape.Cells)
            {
                int minX = cell.x, maxX = cell.x, minZ = cell.y, maxZ = cell.y;
                while (cells.Contains(new Vector2Int(minX - 1, cell.y))) minX--;
                while (cells.Contains(new Vector2Int(maxX + 1, cell.y))) maxX++;
                while (cells.Contains(new Vector2Int(cell.x, minZ - 1))) minZ--;
                while (cells.Contains(new Vector2Int(cell.x, maxZ + 1))) maxZ++;
                int columns = resolution * (maxX - minX + 1) + seam * (maxX - minX);
                int rows = resolution * (maxZ - minZ + 1) + seam * (maxZ - minZ);
                float startX = (minX + maxX) * cellSize * 0.5f - (columns - 1) * slotSpacing * 0.5f;
                float startZ = (minZ + maxZ) * cellSize * 0.5f - (rows - 1) * slotSpacing * 0.5f + 0.01f * cellSize;
                int startColumn = (cell.x - minX) * stride;
                int startRow = (cell.y - minZ) * stride;
                bool right = seam != 0 && cells.Contains(cell + Vector2Int.right);
                bool up = seam != 0 && cells.Contains(cell + Vector2Int.up);
                bool diagonal = right && up && cells.Contains(cell + Vector2Int.one);
                for (int row = 0; row < resolution + (up ? seam : 0); row++)
                    for (int column = 0; column < resolution + (right ? seam : 0); column++)
                    {
                        if (row >= resolution && column >= resolution && !diagonal) continue;
                        // Seamed fill anchors every cell on its own centre: stride slots span one cell, so
                        // rows and columns line up across the whole shape (L arms included) as one clean
                        // grid. Unseamed runs pack contiguously instead.
                        FillSlots[slot++] = seamed
                            ? new Vector3(cell.x * cellSize + cellOffset + column * slotSpacing, 0f,
                                cell.y * cellSize + cellOffset + row * slotSpacing + 0.01f * cellSize)
                            : new Vector3(startX + (startColumn + column) * slotSpacing, 0f,
                                startZ + (startRow + row) * slotSpacing);
                    }
            }
            // The game camera looks down with screen up along local +Z: fill row by row, left to right.
            // Half-slot tolerance keeps rows from differently centred runs together.
            float rowTolerance = slotSpacing * 0.5f;
            Array.Sort(FillSlots, (a, b) =>
            {
                if (Mathf.Abs(a.z - b.z) > rowTolerance) return a.z.CompareTo(b.z);
                return a.x.CompareTo(b.x);
            });
            GameObject visual;
            if (registry != null && registry.TryGetBox(Shape, out var entry) && entry.prefab != null)
            {
                visual = Instantiate(entry.prefab, transform);
                visual.transform.localPosition = entry.localOffset;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = entry.localScale == Vector3.zero ? Vector3.one : entry.localScale;
                PrefabRegistry.ApplyMaterial(visual, Color.boxMaterial);
                FillRoot.localPosition += entry.fillOffset;
                CompletionAnimation = visual.GetComponentInChildren<BoxCompletionAnimation>(true);
                fillLabel = BoxFillLabel.Create(this, visual, cellSize);
                fillLabel.SetFill(CurrentFill, Capacity);
            }
            else
            {
                visual = BoxShapeVisual.Create(Shape, Color.boxMaterial, transform, cellSize, registry, out Vector3 fillOffset);
                FillRoot.localPosition += fillOffset;
                fillLabel = BoxFillLabel.Create(this, visual, cellSize);
                fillLabel.SetFill(CurrentFill, Capacity);
            }
            art = visual;
            // Outer color frames an inset inner tray until the inner layer is filled.
            if (HasInnerLayer)
                InnerArt = BoxShapeVisual.CreateInner(Shape, InnerColor.boxMaterial, transform, cellSize, registry);
            if (MoveAxis != BoxMoveAxis.Free) axisArrow = BoxAxisArrow.Create(this, visual, cellSize);
            if (registry != null && registry.shadowMaterial != null) CreateShadow(registry, cellSize);
            IceCount = Mathf.Max(0, spawn.iceCount);
            // Keep the pick volume level with the visible box, including its raised rim.
            MeshFilter boxMesh = visual.GetComponentInChildren<MeshFilter>();
            float hitBottom = 0f;
            float hitTop = cellSize * 0.4f;
            if (boxMesh != null && boxMesh.sharedMesh != null)
            {
                Bounds meshBounds = boxMesh.sharedMesh.bounds;
                hitBottom = transform.InverseTransformPoint(boxMesh.transform.TransformPoint(
                    new Vector3(0f, meshBounds.min.y, 0f))).y;
                hitTop = transform.InverseTransformPoint(boxMesh.transform.TransformPoint(
                    new Vector3(0f, meshBounds.max.y, 0f))).y;
                if (hitTop < hitBottom) (hitBottom, hitTop) = (hitTop, hitBottom);
            }
            float hitHeight = Mathf.Max(0.01f, hitTop - hitBottom);
            foreach (Vector2Int cell in Shape.Cells)
            {
                var hit = gameObject.AddComponent<BoxCollider>();
                hit.center = new Vector3(cell.x * cellSize, (hitBottom + hitTop) * 0.5f, cell.y * cellSize);
                hit.size = new Vector3(cellSize * 0.95f, hitHeight, cellSize * 0.95f);
            }
            // Colliders stay on the root; only the art and its fill pulse.
            Vector3 center = Vector3.zero;
            foreach (Vector2Int cell in Shape.Cells) center += new Vector3(cell.x, 0f, cell.y);
            pulseRoot = new GameObject("Pulse").transform;
            pulseRoot.SetParent(transform, false);
            pulseRoot.localPosition = center * (cellSize / Shape.Cells.Count);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != pulseRoot) child.SetParent(pulseRoot, true);
            }
            if (IsFrozen)
            {
                // Added after the pulse rig: the ice stays put while the box beneath is locked.
                ice = IceBoxVisual.Create(this, cellSize, IceCount);
                SetArtVisible(false);
            }
        }

        // A blurred footprint on the tiles; it rides the pulse and completion shrink with the art.
        private void CreateShadow(PrefabRegistry registry, float cellSize)
        {
            // Box meshes stop 0.025 cells short of each outer footprint edge.
            const float inset = 0.025f;
            var cells = new HashSet<Vector2Int>(Shape.Cells);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector2Int cell in Shape.Cells)
            {
                min = Vector2.Min(min, cell - Vector2.one * 0.5f);
                max = Vector2.Max(max, cell + Vector2.one * 0.5f);
            }
            var mask = new SoftShadowMask(Rect.MinMaxRect(min.x, min.y, max.x, max.y), 16f, registry.boxShadowSoftness);
            foreach (Vector2Int cell in Shape.Cells)
                mask.AddRect(Rect.MinMaxRect(
                    cell.x - 0.5f + (cells.Contains(cell + Vector2Int.left) ? 0f : inset),
                    cell.y - 0.5f + (cells.Contains(cell + Vector2Int.down) ? 0f : inset),
                    cell.x + 0.5f - (cells.Contains(cell + Vector2Int.right) ? 0f : inset),
                    cell.y + 0.5f - (cells.Contains(cell + Vector2Int.up) ? 0f : inset)));
            shadow = SoftShadowMask.CreateRenderer(transform, registry.shadowMaterial, "Soft Shadow");
            mask.ApplyTo(shadow, registry.boxShadowHeight, registry.boxShadowOffset, cellSize);
        }

        private void OnDestroy() => SoftShadowMask.Release(shadow);

        internal void PlayCollectPulse()
        {
            if (pulseRoot == null || IsRemoved) return;
            pulseVelocity = Mathf.Min(pulseVelocity + PulseImpulse, PulseImpulse * 1.6f);
        }

        private void Update()
        {
            if (pulseRoot == null || pulse == 0f && pulseVelocity == 0f) return;
            // Semi-implicit Euler stays stable with the step capped well below 2/ω.
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            pulseVelocity += (-PulseStiffness * pulse - PulseDamping * pulseVelocity) * dt;
            pulse = Mathf.Clamp(pulse + pulseVelocity * dt, -PulseLimit, PulseLimit);
            if (Mathf.Abs(pulse) < 0.0005f && Mathf.Abs(pulseVelocity) < 0.01f) pulse = pulseVelocity = 0f;
            // Widen and squash together so the box reads as catching the ball.
            pulseRoot.localScale = new Vector3(1f + pulse, 1f - pulse * 0.6f, 1f + pulse);
        }

        // The completion celebration takes over the art pivot from the collect pulse.
        internal Transform ClaimArtRoot()
        {
            pulse = pulseVelocity = 0f;
            if (pulseRoot != null) pulseRoot.localScale = Vector3.one;
            return pulseRoot;
        }

        // Rim height of the box art, in box-local space.
        internal float ArtTop
        {
            get
            {
                float top = 0f;
                if (art == null) return top;
                foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>())
                    if (filter.TryGetComponent(out Renderer renderer))
                        top = Mathf.Max(top, transform.InverseTransformPoint(renderer.bounds.max).y);
                return top;
            }
        }

        internal void HideTopDecals()
        {
            fillLabel?.SetVisible(false);
            if (axisArrow != null) axisArrow.SetActive(false);
        }

        // The inner layer is full and its balls are gone: start over in the outer color.
        internal void FinishInnerLayer()
        {
            InnerColor = null;
            if (InnerArt != null) Destroy(InnerArt.gameObject);
            InnerArt = null;
            CurrentFill = 0;
            IsSwappingLayer = false;
            NotifyCollection();
        }

        internal void SetOrigin(Vector2Int origin) { Origin = origin; IsPlaced = true; }
        internal void MarkPlayerInteraction() => HasPlayerInteracted = true;
        internal void CreateInitialFill(BallPool pool)
        {
            for (int index = 0; index < CurrentFill; index++)
            {
                var ball = new BallState(new BallSpawnData { color = ActiveColor });
                ball.Visual = pool.Rent(ActiveColor);
                if (ball.Visual != null)
                {
                    ball.Visual.SetParent(FillRoot, false);
                    ball.Visual.localPosition = BoxFillSystem.GetSlotPosition(this, index);
                    ball.Visual.localScale = Vector3.one * FillBallDiameter;
                }
                CollectedBalls.Add(ball);
            }
        }
        internal void ClearPlacement() { IsPlaced = false; IsRemoved = true; }
        public bool CanCollect(BallColorDefinition color) => HasPlayerInteracted && IsPlaced && !IsFrozen && !IsCompleting && !IsSwappingLayer && !IsRemoved && ActiveColor == color && CurrentFill < Capacity;
        // Returns true when this step breaks the ice and frees the box.
        internal bool CrackIce()
        {
            if (!IsFrozen || IsRemoved) return false;
            IceCount--;
            if (ice != null) ice.SetCount(IceCount);
            if (IsFrozen) return false;
            SetArtVisible(true);
            return true;
        }

        // The ice block stands in for the box until it shatters; the shadow stays.
        private void SetArtVisible(bool visible)
        {
            foreach (Renderer renderer in pulseRoot.GetComponentsInChildren<Renderer>(true))
                if (renderer != shadow) renderer.enabled = visible;
        }

        internal void RecordCollection() => CurrentFill++;
        internal void NotifyCollection()
        {
            fillLabel?.SetFill(CurrentFill, Capacity);
            OnBoxFillChanged?.Invoke(this);
        }

        private void OnDrawGizmos()
        {
            if (Shape == null) return;
            Gizmos.color = Color != null ? Color.displayColor : UnityEngine.Color.white;
            Gizmos.matrix = transform.localToWorldMatrix;
            float size = IsPlaced && transform.parent != null ? runtimeCellSize : 1f;
            foreach (var cell in Shape.Cells)
                Gizmos.DrawWireCube(new Vector3(cell.x * size, size * 0.2f, cell.y * size), new Vector3(size * 0.95f, size * 0.4f, size * 0.95f));
        }

        internal float runtimeCellSize = 1f;
    }
}
