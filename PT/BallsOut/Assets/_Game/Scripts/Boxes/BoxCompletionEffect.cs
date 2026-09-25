using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Default completion celebration for boxes without an authored BoxCompletionAnimation:
    // a lid in the box's own shape drops shut, fireworks burst, the box squashes and
    // swings side to side, then pops away in a puff of sparkles. Driven by normalised time.
    internal sealed class BoxCompletionEffect
    {
        private const float LandAt = 0.28f;
        private const float PopAt = 0.72f;
        private const float SparkleAt = 0.4f;
        private const float LidTwist = 200f;
        private const float SwingDegrees = 16f;
        private static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static Material sparkMaterial;
        private static Material ringMaterial;
        private static readonly Dictionary<Material, Material> LidMaterials = new Dictionary<Material, Material>();

        private readonly BoxController box;
        private readonly Transform art;
        private readonly Transform lid;
        private readonly Vector3 pivot;
        private readonly Vector3 lidRest;
        private readonly float cellSize;
        private readonly float extent;
        private readonly float duration;
        private readonly Color color;
        private bool landed;
        private bool sparkled;

        internal BoxCompletionEffect(BoxController box, float duration)
        {
            this.box = box;
            this.duration = Mathf.Max(0.01f, duration);
            cellSize = box.runtimeCellSize;
            color = box.Color != null ? box.Color.displayColor : Color.white;
            art = box.ClaimArtRoot();
            pivot = art.localPosition;
            float top = box.ArtTop;

            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector2Int cell in box.Shape.Cells)
            {
                min = Vector2.Min(min, cell);
                max = Vector2.Max(max, cell);
            }
            extent = (Mathf.Max(max.x - min.x, max.y - min.y) + 1f) * 0.5f;

            // The pivot sits on the footprint centre so the lid twists about its middle.
            lid = new GameObject("Completion Lid").transform;
            lid.SetParent(art, false);
            lidRest = new Vector3(0f, top - cellSize * 0.01f - pivot.y, 0f);
            var mesh = new GameObject("Lid Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.transform.SetParent(lid, false);
            mesh.transform.localPosition = new Vector3(-pivot.x, 0f, -pivot.z);
            mesh.transform.localScale = Vector3.one * cellSize;
            mesh.GetComponent<MeshFilter>().sharedMesh = BoxShapeVisual.LidMesh(box.Shape);
            Evaluate(0f);
            PrefabRegistry.ApplyMaterial(mesh, LidMaterial(box.Color != null ? box.Color.boxMaterial : null));
        }

        internal void Evaluate(float t)
        {
            t = Mathf.Clamp01(t);

            // The lid grows from nothing on the rim, twisting into place.
            float close = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / LandAt), 3f);
            lid.localPosition = lidRest;
            lid.localRotation = Quaternion.Euler(0f, (1f - close) * -LidTwist, 0f);
            lid.localScale = Vector3.one * close;
            if (!landed && t >= LandAt)
            {
                landed = true;
                box.HideFillLabel();
                SpawnFirework();
            }

            // Landing squash and a decaying left-right swing, in seconds since the lid shut.
            float since = Mathf.Max(0f, t - LandAt) * duration;
            float impact = landed ? Mathf.Exp(-since * 10f) * Mathf.Cos(since * 30f) : 0f;
            float swing = landed ? Mathf.Sin(since * 19f) * Mathf.Exp(-since * 2.6f) * Mathf.Clamp01(since * 14f) : 0f;

            // A spinning shrink out of existence, never growing first.
            float pop = Mathf.Clamp01((t - PopAt) / (1f - PopAt));
            float scale = 1f - pop * pop * (3f - 2f * pop);
            if (!sparkled && pop >= SparkleAt)
            {
                sparkled = true;
                SpawnSparkles();
            }

            art.localPosition = pivot + new Vector3(swing * 0.05f * cellSize, pop * pop * 0.2f * cellSize, 0f);
            art.localRotation = Quaternion.Euler(0f, swing * SwingDegrees + pop * pop * 170f, -swing * 6f);
            art.localScale = new Vector3(1f, 1f - Mathf.Max(0f, impact) * 0.14f, 1f) * scale;
        }

        internal void Finish()
        {
            if (!landed) { landed = true; SpawnFirework(); }
            if (!sparkled) { sparkled = true; SpawnSparkles(); }
        }

        // The recessed-tray shading would paint the lid's raised panel as a dark inner wall.
        private static Material LidMaterial(Material boxMaterial)
        {
            if (boxMaterial == null || !boxMaterial.HasProperty("_BoxGlass")) return boxMaterial;
            if (!LidMaterials.TryGetValue(boxMaterial, out Material lidMaterial))
            {
                lidMaterial = new Material(boxMaterial) { name = boxMaterial.name + " Lid" };
                lidMaterial.SetFloat("_BoxGlass", 0f);
                lidMaterial.DisableKeyword("_BOX_GLASS");
                LidMaterials.Add(boxMaterial, lidMaterial);
            }
            return lidMaterial;
        }

        private void SpawnFirework()
        {
            float world = cellSize * box.transform.lossyScale.x;
            Vector3 origin = lid.position + Vector3.up * (cellSize * 0.12f * box.transform.lossyScale.y);

            ParticleSystem sparks = CreateSystem("Completion Firework", origin, Spark, ParticleSystemRenderMode.Stretch);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f * world, 4.8f * world * Mathf.Sqrt(extent));
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * world, 0.16f * world);
            main.startColor = Palette();
            var emission = sparks.emission;
            int count = Mathf.Min(24 + box.Shape.Cells.Count * 6, 60);
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)count),
                new ParticleSystem.Burst(0.07f, (short)(count / 2))
            });
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.radius = extent * 0.35f * world;
            // A lift toward the camera makes sparks swell as they fly.
            var velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.4f * world, 2.2f * world);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var drag = sparks.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0.3f * world;
            drag.dampen = 0.12f;
            FadeAndShrink(sparks, 0.65f);
            var stretch = (ParticleSystemRenderer)sparks.GetComponent<Renderer>();
            stretch.velocityScale = 0.035f;
            stretch.lengthScale = 1.4f;
            sparks.Play();

            ParticleSystem flash = CreateSystem("Completion Flash", origin, Ring, ParticleSystemRenderMode.Billboard);
            main = flash.main;
            main.startLifetime = 0.38f;
            main.startSpeed = 0f;
            main.startSize = extent * 2.6f * world;
            main.startColor = Color.Lerp(color, Color.white, 0.6f);
            emission = flash.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var grow = flash.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f, 0f, 3f), new Keyframe(1f, 1.15f, 0f, 0f)));
            var fade = flash.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(0f);
            flash.Play();
        }

        private void SpawnSparkles()
        {
            float world = cellSize * box.transform.lossyScale.x;
            ParticleSystem sparkles = CreateSystem("Completion Sparkles", art.position, Spark, ParticleSystemRenderMode.Billboard);
            var main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * world, 1.8f * world);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * world, 0.24f * world);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Palette();
            var emission = sparkles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Min(10 + box.Shape.Cells.Count * 3, 28)) });
            var shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.radius = extent * 0.5f * world;
            FadeAndShrink(sparkles, 0.5f);
            sparkles.Play();
        }

        private ParticleSystem CreateSystem(string name, Vector3 position, Material material, ParticleSystemRenderMode mode)
        {
            // Parented to the board so the burst outlives the box it came from.
            var host = new GameObject(name);
            host.transform.SetParent(box.transform.parent, false);
            host.transform.position = position;
            ParticleSystem system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 128;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static void FadeAndShrink(ParticleSystem system, float holdUntil)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(holdUntil, 0.85f), new Keyframe(1f, 0f)));
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(holdUntil);
        }

        private static Gradient Fade(float holdUntil)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // Each spark picks one of the box colour, a pale tint, white or gold.
        private ParticleSystem.MinMaxGradient Palette()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0.4f),
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.5f), 0.65f),
                    new GradientColorKey(Gold, 0.85f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        private static Material Spark => sparkMaterial != null ? sparkMaterial : sparkMaterial = CreateMaterial("Celebration Spark", 0f);
        private static Material Ring => ringMaterial != null ? ringMaterial : ringMaterial = CreateMaterial("Celebration Ring", 1f);

        private static Material CreateMaterial(string name, float shape)
        {
            Shader shader = Shader.Find("Balls Out/Celebration Spark") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = name };
            material.SetFloat(ShapeId, shape);
            return material;
        }
    }
}
