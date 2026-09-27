using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Hammer booster finish: the box squashes flat under the blow, then bursts into chunks of its
    // own colour that fly out and tumble away. Driven by normalised time.
    internal sealed class BoxSmashEffect
    {
        private const float BreakAt = 0.2f;
        private const int ChunksPerCell = 5;
        private static Mesh chunkMesh;

        private struct Chunk
        {
            public Transform transform;
            public Vector3 start;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spin;
            public float size;
        }

        private readonly Transform art;
        private readonly float duration;
        private readonly float cellSize;
        private readonly List<Chunk> chunks = new List<Chunk>();
        private bool broken;

        internal BoxSmashEffect(BoxController box, float duration)
        {
            this.duration = Mathf.Max(0.01f, duration);
            cellSize = box.runtimeCellSize;
            art = box.ClaimArtRoot();
            box.HideTopDecals();
            Material material = box.Color != null ? box.Color.boxMaterial : null;
            float top = box.ArtTop * 0.6f;
            foreach (Vector2Int cell in box.Shape.Cells)
                for (int i = 0; i < ChunksPerCell; i++)
                    chunks.Add(CreateChunk(box, cell, top, material));
            Evaluate(0f);
        }

        private Chunk CreateChunk(BoxController box, Vector2Int cell, float top, Material material)
        {
            if (chunkMesh == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunkMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(cube);
            }
            var chunk = new GameObject("Smash Chunk", typeof(MeshFilter), typeof(MeshRenderer));
            chunk.transform.SetParent(box.transform, false);
            chunk.GetComponent<MeshFilter>().sharedMesh = chunkMesh;
            MeshRenderer renderer = chunk.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            Vector2 jitter = Random.insideUnitCircle * 0.35f;
            var start = new Vector3((cell.x + jitter.x) * cellSize, top, (cell.y + jitter.y) * cellSize);
            Vector3 outward = new Vector3(jitter.x, 0f, jitter.y).normalized;
            if (outward == Vector3.zero) outward = Vector3.right;
            chunk.SetActive(false);
            return new Chunk
            {
                transform = chunk.transform,
                start = start,
                velocity = (outward * Random.Range(1.2f, 2.4f) + Vector3.up * Random.Range(2.2f, 3.6f)) * cellSize,
                spinAxis = Random.onUnitSphere,
                spin = Random.Range(360f, 900f),
                size = Random.Range(0.14f, 0.26f) * cellSize,
            };
        }

        internal void Evaluate(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < BreakAt)
            {
                // The blow flattens the box and spreads it wide.
                float k = Mathf.Sin(t / BreakAt * Mathf.PI * 0.5f);
                art.localScale = new Vector3(1f + 0.4f * k, 1f - 0.65f * k, 1f + 0.4f * k);
                return;
            }
            if (!broken)
            {
                broken = true;
                foreach (Chunk chunk in chunks) chunk.transform.gameObject.SetActive(true);
            }
            float after = (t - BreakAt) / (1f - BreakAt);
            float shrink = Mathf.Clamp01(after / 0.35f);
            art.localScale = new Vector3(1.4f, 0.35f, 1.4f) * (1f - shrink * shrink);
            float time = after * duration * (1f - BreakAt);
            Vector3 gravity = Vector3.down * 14f * cellSize;
            foreach (Chunk chunk in chunks)
            {
                chunk.transform.localPosition = chunk.start + chunk.velocity * time + gravity * (0.5f * time * time);
                chunk.transform.localRotation = Quaternion.AngleAxis(chunk.spin * time, chunk.spinAxis);
                chunk.transform.localScale = Vector3.one * chunk.size * (1f - after * after);
            }
        }

        internal void Finish()
        {
            foreach (Chunk chunk in chunks)
                if (chunk.transform != null) Object.Destroy(chunk.transform.gameObject);
            chunks.Clear();
        }
    }
}
