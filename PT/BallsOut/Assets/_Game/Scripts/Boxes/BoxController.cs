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
        public Vector2Int Origin { get; private set; }
        public bool IsPlaced { get; private set; }
        public bool IsInTransit { get; internal set; }
        public int CurrentFill { get; private set; }
        public int Capacity { get; private set; }
        public bool IsCompleting { get; internal set; }
        public bool IsRemoved { get; internal set; }
        public int IceCount { get; private set; }
        public bool IsFrozen => IceCount > 0;
        public bool CanMove => IsPlaced && !IsFrozen && !IsCompleting && !IsRemoved && CurrentFill < Capacity;
        public Transform FillRoot { get; private set; }
        public Vector3 FillSpacing { get; private set; }
        public BoxCompletionAnimation CompletionAnimation { get; private set; }
        private BoxFillLabel fillLabel;
        private IceBoxVisual ice;
        private MeshRenderer shadow;
        // Collection pulse: a small damped spring, pivoting on the footprint centre.
        private const float PulseStiffness = 420f;
        private const float PulseDamping = 13f;
        private const float PulseImpulse = 1.1f;
        private const float PulseLimit = 0.08f;
        private Transform pulseRoot;
        private float pulse;
        private float pulseVelocity;
        internal List<BallState> CollectedBalls { get; private set; }
        internal Vector3[] FillSlots { get; private set; }
        internal int PendingFillAnimations;
        public event Action<BoxController> OnBoxFillChanged;

        internal void Initialize(BoxSpawnData spawn, PrefabRegistry registry, float cellSize, bool denseFill, int fillLayers)
        {
            Id = spawn.id;
            Shape = spawn.shape;
            Color = spawn.color;
            Capacity = Shape.FillSlotsPerLayer(denseFill) * fillLayers;
            CurrentFill = spawn.initialFillCount;
            CollectedBalls = new List<BallState>(Capacity);
            FillRoot = new GameObject("Fill").transform;
            FillRoot.SetParent(transform, false);
            FillRoot.localPosition = (registry != null ? registry.fillOffset : new Vector3(0f, 0.085f, 0f)) * cellSize;
            int resolution = LevelDefinition.MicroResolution;
            int seam = denseFill ? 2 : 0;
            int stride = resolution + seam;
            float slotSpacing = cellSize * (denseFill ? 0.168f : 0.68f / resolution);
            FillSpacing = new Vector3(slotSpacing, cellSize / LevelDefinition.MicroResolution * 0.9f, slotSpacing);
            FillSlots = new Vector3[Shape.FillSlotsPerLayer(denseFill)];
            var cells = new HashSet<Vector2Int>(Shape.Cells);
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
                        FillSlots[slot++] = new Vector3(
                            startX + (startColumn + column) * slotSpacing,
                            0f,
                            startZ + (startRow + row) * slotSpacing);
                    }
            }
            // The game camera looks down with screen up along local +Z.
            Array.Sort(FillSlots, (a, b) =>
            {
                int rowOrder = a.z.CompareTo(b.z);
                return rowOrder != 0 ? rowOrder : a.x.CompareTo(b.x);
            });
            if (registry != null && registry.TryGetBox(Shape, out var entry) && entry.prefab != null)
            {
                GameObject visual = Instantiate(entry.prefab, transform);
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
                GameObject visual = BoxShapeVisual.Create(Shape, Color.boxMaterial, transform, cellSize);
                fillLabel = BoxFillLabel.Create(this, visual, cellSize);
                fillLabel.SetFill(CurrentFill, Capacity);
            }
            if (registry != null && registry.shadowMaterial != null) CreateShadow(registry, cellSize);
            IceCount = Mathf.Max(0, spawn.iceCount);
            // Hit proxies follow the data footprint, never mesh bounds.
            foreach (Vector2Int cell in Shape.Cells)
            {
                var hit = gameObject.AddComponent<BoxCollider>();
                hit.center = new Vector3(cell.x * cellSize, cellSize * 0.2f, cell.y * cellSize);
                hit.size = new Vector3(cellSize * 0.95f, cellSize * 0.4f, cellSize * 0.95f);
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

        internal void SetOrigin(Vector2Int origin) { Origin = origin; IsPlaced = true; }
        internal void CreateInitialFill(BallPool pool)
        {
            for (int index = 0; index < CurrentFill; index++)
            {
                var ball = new BallState(new BallSpawnData { color = Color });
                ball.Visual = pool.Rent(Color);
                if (ball.Visual != null)
                {
                    ball.Visual.SetParent(FillRoot, false);
                    ball.Visual.localPosition = BoxFillSystem.GetSlotPosition(this, index);
                    ball.Visual.localScale = Vector3.one * FillSpacing.x;
                }
                CollectedBalls.Add(ball);
            }
        }
        internal void ClearPlacement() { IsPlaced = false; IsRemoved = true; }
        public bool CanCollect(BallColorDefinition color) => IsPlaced && !IsFrozen && !IsCompleting && !IsRemoved && Color == color && CurrentFill < Capacity;
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
