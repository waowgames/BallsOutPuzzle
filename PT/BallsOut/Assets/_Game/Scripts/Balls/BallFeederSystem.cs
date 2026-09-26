using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Tubes on the reservoir's top edge. Each one drops its queue, first in line first, into the
    // top ball row under it whenever a site there is free. The tube shows the balls next in line
    // under a glass cover, with a counter of everything still inside.
    public sealed class BallFeederSystem
    {
        private sealed class Tube
        {
            public int column;
            public readonly List<BallColorDefinition> queue = new List<BallColorDefinition>();
            public int next;
            // visuals[i] shows queue[next + i]; it slides from starts[i] to its place in the stack.
            public readonly List<Transform> visuals = new List<Transform>();
            public readonly List<Vector3> starts = new List<Vector3>();
            public readonly List<Vector3> targets = new List<Vector3>();
            public TextMeshPro counter;
            public int Remaining => queue.Count - next;
        }

        private const int R = LevelDefinition.MicroResolution;
        private const int GlassSegments = 12;
        private static Material counterMaterial;
        private static Material glassMaterial;
        private static Mesh glassMesh;
        private static Mesh badgeMesh;
        private static Material badgeMaterial;
        private readonly BallMicroGrid grid;
        private readonly BallPool pool;
        private readonly LevelDefinition level;
        private readonly float visualHeight;
        private readonly int topRow;
        private readonly List<Tube> tubes = new List<Tube>();
        public int Remaining { get; private set; }

        public BallFeederSystem(BallMicroGrid grid, BallPool pool, Transform parent, float visualHeight)
        {
            this.grid = grid;
            this.pool = pool;
            this.visualHeight = visualHeight;
            level = grid.Board.Definition;
            topRow = grid.Height - 1;
            if (!level.HasFeeders) return;
            foreach (BallFeederData feeder in level.feeders)
            {
                var tube = new Tube { column = feeder.column };
                foreach (FeederSegment segment in feeder.queue)
                    for (int i = 0; i < segment.count; i++) tube.queue.Add(segment.color);
                Remaining += tube.queue.Count;
                // Centred on the flat top of the cap rail, which starts just outside the tube's top edge.
                tube.counter = CreateCounter(parent, level.macroCellSize, "Feeder Counter " + feeder.column,
                    new Vector3((feeder.column + 0.5f) * level.macroCellSize, level.macroCellSize * 0.36f,
                        level.FeederTop + level.macroCellSize * 0.13f));
                CreateGlass(parent, level, feeder.column);
                Restack(tube);
                for (int i = 0; i < tube.visuals.Count; i++)
                    if (tube.visuals[i] != null) tube.visuals[i].localPosition = tube.targets[i];
                UpdateCounter(tube);
                tubes.Add(tube);
            }
        }

        // Called once per simulation tick, after the pile has moved. Returns whether any ball dropped.
        internal bool Feed(Action<BallState, Vector3> launch)
        {
            bool fed = false;
            foreach (Tube tube in tubes)
            {
                bool released = false;
                for (int i = 0; i < R && tube.Remaining > 0; i++)
                {
                    var cell = new Vector2Int(tube.column * R + i, topRow);
                    if (!grid.IsEmpty(cell)) continue;
                    var ball = new BallState(tube.queue[tube.next], cell);
                    grid.Add(ball);
                    Remaining--;
                    Vector3 from = Place(tube.column, tube.next, tube.next);
                    if (tube.visuals.Count > 0)
                    {
                        ball.Visual = tube.visuals[0];
                        if (ball.Visual != null) from = ball.Visual.localPosition;
                        tube.visuals.RemoveAt(0);
                        tube.starts.RemoveAt(0);
                        tube.targets.RemoveAt(0);
                    }
                    else ball.Visual = pool.Rent(ball.Color);
                    tube.next++;
                    launch(ball, from);
                    released = true;
                }
                if (!released) continue;
                fed = true;
                Restack(tube);
                UpdateCounter(tube);
            }
            return fed;
        }

        internal void Render(float amount)
        {
            // Ease in and out so a stack that drops one row settles instead of snapping.
            float eased = amount * amount * (3f - 2f * amount);
            foreach (Tube tube in tubes)
                for (int i = 0; i < tube.visuals.Count; i++)
                    if (tube.visuals[i] != null)
                        tube.visuals[i].localPosition = Vector3.LerpUnclamped(tube.starts[i], tube.targets[i], eased);
        }

        // Each queued ball keeps its column and its row's stagger; the stack only moves straight
        // down, one row at a time, once the bottom row has fully dropped out.
        private void Restack(Tube tube)
        {
            int visible = 0;
            while (tube.next + visible < tube.queue.Count &&
                   (tube.next + visible) / R - tube.next / R < LevelDefinition.FeederRows)
                visible++;
            for (int i = 0; i < visible; i++)
            {
                Vector3 target = Place(tube.column, tube.next + i, tube.next);
                if (i < tube.visuals.Count)
                {
                    if (tube.visuals[i] != null) tube.starts[i] = tube.visuals[i].localPosition;
                    tube.targets[i] = target;
                    continue;
                }
                // Balls that come into view slide in from under the cap.
                Transform visual = pool.Rent(tube.queue[tube.next + i]);
                Vector3 start = target + Vector3.forward * level.BallRowSpacing;
                if (visual != null) visual.localPosition = start;
                tube.visuals.Add(visual);
                tube.starts.Add(start);
                tube.targets.Add(target);
            }
        }

        // The tube continues the reservoir lattice upward, squeezed between its walls like a
        // one-column reservoir chamber (BallMicroGrid.ChamberX).
        private Vector3 Place(int column, int index, int next)
        {
            int baseRow = level.ballAreaMacroHeight * R;
            int stackRow = index / R;
            float s = level.macroCellSize;
            float first = level.BallColumnX(column * R, 0);
            float last = level.BallColumnX(column * R + R - 1, 1);
            float min = (column + BallMicroGrid.WallClearance) * s;
            float max = (column + 1 - BallMicroGrid.WallClearance) * s;
            float x = level.BallColumnX(column * R + index % R, stackRow);
            x = last - first > max - min
                ? (min + max) * 0.5f + (x - (first + last) * 0.5f) * (max - min) / (last - first)
                : x + Mathf.Max(0f, min - first) + Mathf.Min(0f, max - last);
            return new Vector3(x, visualHeight, level.BallRowZ(baseRow + stackRow - next / R));
        }

        private static void UpdateCounter(Tube tube)
        {
            if (tube.counter == null) return;
            tube.counter.gameObject.SetActive(tube.Remaining > 0);
            tube.counter.text = tube.Remaining.ToString();
        }

        // A clear half-cylinder over the tube channel, from the reservoir mouth up to the cap.
        private static void CreateGlass(Transform parent, LevelDefinition level, int column)
        {
            if (glassMaterial == null)
            {
                Shader shader = Shader.Find("Balls Out/Feeder Glass");
                if (shader == null) return;
                glassMaterial = new Material(shader) { name = "Feeder Glass" };
            }
            if (glassMesh == null) glassMesh = CreateGlassMesh();
            float s = level.macroCellSize;
            float bottom = level.DepotTop - s * 0.05f;
            var glass = new GameObject("Feeder Glass " + column, typeof(MeshFilter), typeof(MeshRenderer));
            glass.transform.SetParent(parent, false);
            // The shared unit mesh spans the channel's width and length and rises over the balls.
            glass.transform.localPosition = new Vector3((column + 0.5f) * s, level.DepotFloorHeight, bottom);
            glass.transform.localScale = new Vector3(s * 0.96f, s * 0.36f - level.DepotFloorHeight, level.FeederTop - bottom);
            glass.GetComponent<MeshFilter>().sharedMesh = glassMesh;
            MeshRenderer renderer = glass.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Half-cylinder of unit width, height and length: x -0.5..0.5, y 0..1, z 0..1.
        private static Mesh CreateGlassMesh()
        {
            var vertices = new Vector3[(GlassSegments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var indices = new int[GlassSegments * 6];
            for (int i = 0; i <= GlassSegments; i++)
            {
                float t = (float)i / GlassSegments;
                float angle = Mathf.PI * (1f - t);
                var point = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle), 0f);
                vertices[i * 2] = point;
                vertices[i * 2 + 1] = point + Vector3.forward;
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                if (i == GlassSegments) break;
                int k = i * 6, v = i * 2;
                indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 3;
                indices[k + 3] = v; indices[k + 4] = v + 3; indices[k + 5] = v + 2;
            }
            var mesh = new Mesh { name = "Feeder Glass", vertices = vertices, uv = uvs, triangles = indices, hideFlags = HideFlags.DontSave };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // World-space label lying on a cap rail; no canvas or per-frame updates. The conveyor's chute uses it too.
        internal static TextMeshPro CreateCounter(Transform parent, float s, string name, Vector3 position)
        {
            if (TMP_Settings.defaultFontAsset == null) return null;
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 36f;
            text.fontStyle = FontStyles.Bold;
            text.isOrthographic = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            // White with a dark outline so it stands out on the pale rail.
            text.color = Color.white;
            if (counterMaterial == null && text.fontSharedMaterial != null)
            {
                counterMaterial = new Material(text.fontSharedMaterial) { name = "Feeder Counter" };
                if (counterMaterial.HasProperty("_OutlineWidth"))
                {
                    counterMaterial.EnableKeyword("OUTLINE_ON");
                    counterMaterial.SetFloat("_OutlineWidth", 0.3f);
                    counterMaterial.SetColor("_OutlineColor", new Color(0.18f, 0.12f, 0.42f));
                    if (counterMaterial.HasProperty("_FaceDilate")) counterMaterial.SetFloat("_FaceDilate", 0.15f);
                }
                if (counterMaterial.HasProperty("_UnderlayColor"))
                {
                    counterMaterial.EnableKeyword("UNDERLAY_ON");
                    counterMaterial.SetColor("_UnderlayColor", new Color(0.1f, 0.05f, 0.25f, 0.6f));
                    counterMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
                    counterMaterial.SetFloat("_UnderlaySoftness", 0.3f);
                }
            }
            if (counterMaterial != null) text.fontSharedMaterial = counterMaterial;
            Vector2 preferred = text.GetPreferredValues("000");
            text.rectTransform.sizeDelta = preferred;
            float scale = preferred.x > 0f && preferred.y > 0f
                ? Mathf.Min(s * 0.9f / preferred.x, s * 0.36f / preferred.y)
                : s * 0.01f;
            label.transform.localScale = Vector3.one * scale;
            CreateBadge(label.transform, s, scale);
            return text;
        }

        // Dark rounded tab behind the counter, the shape of the boxes' fill tabs. It is a child of
        // the label, so it hides with it once the tube is empty.
        private static void CreateBadge(Transform label, float s, float labelScale)
        {
            if (badgeMesh == null)
            {
                badgeMesh = new Mesh { name = "Feeder Counter Tab", hideFlags = HideFlags.DontSave };
                badgeMesh.vertices = new[]
                {
                    new Vector3(-0.44f, -0.5f, 0f), new Vector3(0.44f, -0.5f, 0f),
                    new Vector3(0.5f, -0.24f, 0f), new Vector3(0.5f, 0.24f, 0f),
                    new Vector3(0.44f, 0.5f, 0f), new Vector3(-0.44f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.24f, 0f), new Vector3(-0.5f, -0.24f, 0f)
                };
                badgeMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 6, 5, 0, 7, 6 };
                badgeMesh.RecalculateNormals();
            }
            if (badgeMaterial == null)
            {
                Shader shader = Shader.Find("Money Design/Soft Plastic")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
                if (shader == null) return;
                var color = new Color(0.24f, 0.14f, 0.52f);
                badgeMaterial = new Material(shader) { name = "Feeder Counter Tab" };
                if (badgeMaterial.HasProperty("_BaseColor")) badgeMaterial.SetColor("_BaseColor", color);
                if (badgeMaterial.HasProperty("_Color")) badgeMaterial.SetColor("_Color", color);
                if (badgeMaterial.HasProperty("_Gloss")) badgeMaterial.SetFloat("_Gloss", 0f);
                if (badgeMaterial.HasProperty("_Shade")) badgeMaterial.SetFloat("_Shade", 0f);
                if (badgeMaterial.HasProperty("_Cull")) badgeMaterial.SetFloat("_Cull", 0f);
            }
            var tab = new GameObject("Feeder Counter Tab", typeof(MeshFilter), typeof(MeshRenderer));
            tab.transform.SetParent(label, false);
            // The label lies flat, so its local +Z points down into the rail: the tab sits just under the text.
            tab.transform.localPosition = new Vector3(0f, 0f, s * 0.01f / labelScale);
            tab.transform.localRotation = Quaternion.identity;
            tab.transform.localScale = new Vector3(s * 0.78f / labelScale, s * 0.3f / labelScale, 1f);
            tab.GetComponent<MeshFilter>().sharedMesh = badgeMesh;
            MeshRenderer renderer = tab.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = badgeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
