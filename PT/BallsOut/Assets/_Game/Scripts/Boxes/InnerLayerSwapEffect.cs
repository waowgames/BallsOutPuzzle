using UnityEngine;

namespace BallsOut
{
    // A finished nested tray lifts out of its outer frame with its balls, a lid twists shut on
    // it and the tray spins away. A quicker, lighter take on the full completion. Driven by normalised time.
    internal sealed class InnerLayerSwapEffect
    {
        private const float LiftEnd = 0.35f;
        private const float LidStart = 0.18f;
        private const float LidEnd = 0.45f;
        private const float PopAt = 0.58f;
        private const float LidTwist = 120f;

        private readonly Transform tray;
        private readonly Transform lid;
        private readonly Vector3 rest;
        private readonly float lift;
        private readonly float duration;

        internal InnerLayerSwapEffect(BoxController box, float duration)
        {
            this.duration = Mathf.Max(0.01f, duration);
            float cellSize = box.runtimeCellSize;
            tray = box.InnerArt;
            rest = tray.localPosition;
            lift = cellSize * 0.3f;
            Transform shape = tray.GetChild(0);

            // The load rides the tray; the lid shuts over whichever is taller.
            float top = BoxShapeVisual.InnerTrayTop * shape.localScale.y;
            foreach (BallState ball in box.CollectedBalls)
            {
                if (ball.Visual == null) continue;
                ball.Visual.SetParent(tray, true);
                top = Mathf.Max(top, ball.Visual.localPosition.y + ball.Visual.localScale.y * 0.5f);
            }

            // The pivot sits on the footprint centre so the lid twists about its middle.
            lid = new GameObject("Inner Lid").transform;
            lid.SetParent(tray, false);
            lid.localPosition = Vector3.up * (top - cellSize * 0.005f);
            var mesh = new GameObject("Inner Lid Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.transform.SetParent(lid, false);
            mesh.transform.localPosition = new Vector3(shape.localPosition.x, 0f, shape.localPosition.z);
            mesh.transform.localScale = Vector3.one * cellSize;
            mesh.GetComponent<MeshFilter>().sharedMesh = BoxShapeVisual.InnerLidMesh(box.Shape);
            Evaluate(0f);
            PrefabRegistry.ApplyMaterial(mesh, BoxCompletionEffect.LidMaterial(box.InnerColor != null ? box.InnerColor.boxMaterial : null));
        }

        internal void Evaluate(float t)
        {
            t = Mathf.Clamp01(t);

            // Rises with a small overshoot.
            float r = Mathf.Clamp01(t / LiftEnd) - 1f;
            float rise = 1f + 2.2f * r * r * r + 1.2f * r * r;

            float close = 1f - Mathf.Pow(1f - Mathf.Clamp01((t - LidStart) / (LidEnd - LidStart)), 3f);
            lid.localRotation = Quaternion.Euler(0f, (1f - close) * -LidTwist, 0f);
            lid.localScale = Vector3.one * close;

            // A light squash as the lid lands, in seconds since it shut.
            float since = Mathf.Max(0f, t - LidEnd) * duration;
            float impact = t >= LidEnd ? Mathf.Max(0f, Mathf.Exp(-since * 14f) * Mathf.Cos(since * 36f)) : 0f;

            float pop = Mathf.Clamp01((t - PopAt) / (1f - PopAt));
            float scale = 1f - pop * pop * (3f - 2f * pop);
            tray.localPosition = rest + Vector3.up * (lift * rise + pop * pop * lift * 0.6f);
            tray.localRotation = Quaternion.Euler(0f, pop * pop * 150f, 0f);
            tray.localScale = new Vector3(1f, 1f - impact * 0.1f, 1f) * scale;
        }
    }
}
