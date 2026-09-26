using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Procedural padlock and key, built once and shared. Model units are cells; both lie
    // flat facing +Y with screen-up along +Z, and callers tilt them toward the camera.
    internal static class LockKeyModels
    {
        // Padlock body: rounded slab.
        internal const float BodyHalfWidth = 0.23f;
        internal const float BodyHalfDepth = 0.18f;
        internal const float BodyRadius = 0.085f;
        internal const float BodyThickness = 0.13f;
        // Shackle: a tube arch over the back edge of the body.
        internal const float ShackleSpan = 0.135f;
        internal const float ShackleTube = 0.042f;
        internal const float ShackleHeight = BodyThickness * 0.55f;
        internal const float ShackleBase = BodyHalfDepth - 0.07f;
        internal const float ShackleRise = BodyHalfDepth + 0.035f;
        // Keyhole and counter badge on the lid.
        internal static readonly Vector3 Keyhole = new Vector3(0f, BodyThickness, 0.02f);
        internal static readonly Vector3 Badge = new Vector3(0.2f, BodyThickness + 0.012f, -0.15f);
        internal const float BadgeRadius = 0.085f;
        // Key along +X: bow ring on the left, bit on the right; the tip is at +KeyTip.
        internal const float KeyTip = 0.215f;
        private const float KeyAxis = 0.034f;

        private static readonly Color Gold = new Color(1f, 0.74f, 0.16f);
        private static readonly Color PaleGold = new Color(1f, 0.84f, 0.38f);
        private static readonly Color Ink = new Color(0.16f, 0.1f, 0.2f);
        private static readonly Color White = new Color(0.97f, 0.97f, 1f);

        private static Mesh body, shackle, key, badge;
        private static Material goldMaterial, paleGoldMaterial, inkMaterial, whiteMaterial;

        internal static Material GoldMaterial => GetMaterial(ref goldMaterial, Gold, "Lock Gold", 0.9f, 0.55f);
        internal static Material PaleGoldMaterial => GetMaterial(ref paleGoldMaterial, PaleGold, "Lock Shackle", 0.9f, 0.5f);
        internal static Material InkMaterial => GetMaterial(ref inkMaterial, Ink, "Lock Ink", 0f, 0f);
        internal static Material WhiteMaterial => GetMaterial(ref whiteMaterial, White, "Lock White", 0f, 0f);

        // Submesh 0 is gold, submesh 1 the dark keyhole.
        internal static Mesh Body => body != null ? body : body = BuildBody();
        // Pivot at the foot of the left leg, so the open shackle swings about it.
        internal static Mesh Shackle => shackle != null ? shackle : shackle = BuildShackle();
        internal static Mesh Key => key != null ? key : key = BuildKey();
        // Submesh 0 is the white rim, submesh 1 the dark disc.
        internal static Mesh CounterBadge => badge != null ? badge : badge = BuildBadge();

        internal static GameObject CreatePart(string name, Transform parent, Mesh mesh, params Material[] materials)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterials = materials;
            return part;
        }

        // ---- Padlock ----

        private static Mesh BuildBody()
        {
            var b = new Builder();
            // Rings bottom to top: (inset, height, how far the normal leans up).
            var rings = new[]
            {
                new Vector3(0.014f, 0f, -0.6f), new Vector3(0f, 0.018f, -0.1f),
                new Vector3(0f, BodyThickness - 0.034f, 0.05f), new Vector3(0.011f, BodyThickness - 0.01f, 0.5f),
                new Vector3(0.032f, BodyThickness, 0.9f)
            };
            int count = RoundedRect(BodyHalfWidth, BodyHalfDepth, BodyRadius, null, null);
            for (int r = 0; r < rings.Length; r++)
            {
                var points = new List<Vector2>();
                var normals = new List<Vector2>();
                float inset = rings[r].x;
                RoundedRect(BodyHalfWidth - inset, BodyHalfDepth - inset, BodyRadius - inset, points, normals);
                for (int i = 0; i < count; i++)
                {
                    Vector3 normal = new Vector3(normals[i].x, 0f, normals[i].y) * (1f - Mathf.Abs(rings[r].z)) +
                        Vector3.up * rings[r].z;
                    b.Vertex(new Vector3(points[i].x, rings[r].y, points[i].y), normal);
                }
                if (r == 0) continue;
                int previous = b.Count - count * 2;
                int current = b.Count - count;
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    b.Quad(0, previous + i, current + i, current + next, previous + next);
                }
            }
            // Flat lid: duplicated rim so the bevel keeps its own normals.
            {
                var points = new List<Vector2>();
                RoundedRect(BodyHalfWidth - rings[rings.Length - 1].x, BodyHalfDepth - rings[rings.Length - 1].x,
                    BodyRadius - rings[rings.Length - 1].x, points, null);
                b.Fan(0, Vector3.up * BodyThickness, points.ConvertAll(p => new Vector3(p.x, BodyThickness, p.y)), Vector3.up);
            }
            // Keyhole: a round head over a tapering slot, just above the lid.
            float y = BodyThickness + 0.003f;
            var head = new List<Vector3>();
            for (int i = 0; i < 20; i++)
            {
                float angle = i / 20f * Mathf.PI * 2f;
                head.Add(Keyhole + new Vector3(Mathf.Cos(angle) * 0.05f, 0.003f, Mathf.Sin(angle) * 0.05f));
            }
            b.Fan(1, Keyhole + Vector3.up * 0.003f, head, Vector3.up);
            b.Quad(1,
                b.Vertex(new Vector3(-0.034f, y, Keyhole.z - 0.115f), Vector3.up),
                b.Vertex(new Vector3(-0.019f, y, Keyhole.z), Vector3.up),
                b.Vertex(new Vector3(0.019f, y, Keyhole.z), Vector3.up),
                b.Vertex(new Vector3(0.034f, y, Keyhole.z - 0.115f), Vector3.up));
            return b.Build("Padlock Body", 2);
        }

        private static Mesh BuildShackle()
        {
            var path = new List<Vector3> { new Vector3(0f, 0f, 0f) };
            float top = ShackleRise - ShackleBase;
            for (int i = 0; i <= 16; i++)
            {
                float angle = Mathf.PI - i / 16f * Mathf.PI;
                path.Add(new Vector3(ShackleSpan + Mathf.Cos(angle) * ShackleSpan, 0f, top + Mathf.Sin(angle) * ShackleSpan));
            }
            path.Add(new Vector3(ShackleSpan * 2f, 0f, 0f));
            var b = new Builder();
            b.Tube(0, path, false, ShackleTube, 14, true);
            return b.Build("Padlock Shackle", 1);
        }

        private static Mesh BuildBadge()
        {
            var b = new Builder();
            var rim = new List<Vector3>();
            var disc = new List<Vector3>();
            for (int i = 0; i < 28; i++)
            {
                float angle = i / 28f * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                rim.Add(direction * BadgeRadius);
                disc.Add(direction * (BadgeRadius - 0.017f) + Vector3.up * 0.002f);
            }
            b.Fan(0, Vector3.zero, rim, Vector3.up);
            b.Fan(1, Vector3.up * 0.002f, disc, Vector3.up);
            return b.Build("Padlock Counter", 2);
        }

        // ---- Key ----

        private static Mesh BuildKey()
        {
            var b = new Builder();
            // Bow: a ring of tube.
            var ring = new List<Vector3>();
            const float bowCenter = -0.135f;
            const float bowRadius = 0.078f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i / 24f * Mathf.PI * 2f;
                ring.Add(new Vector3(bowCenter + Mathf.Cos(angle) * bowRadius, KeyAxis, Mathf.Sin(angle) * bowRadius));
            }
            b.Tube(0, ring, true, 0.03f, 12, false);
            // Shaft from the bow to the tip, with a collar where it meets the bow.
            b.Tube(0, new List<Vector3> { new Vector3(bowCenter + bowRadius, KeyAxis, 0f), new Vector3(KeyTip, KeyAxis, 0f) },
                false, 0.026f, 12, true);
            b.Box(0, new Vector3(-0.035f, KeyAxis, 0f), new Vector3(0.028f, 0.072f, 0.078f));
            // Bit: two teeth hanging toward screen-down.
            b.Box(0, new Vector3(0.12f, KeyAxis, -0.048f), new Vector3(0.036f, 0.05f, 0.07f));
            b.Box(0, new Vector3(0.184f, KeyAxis, -0.04f), new Vector3(0.036f, 0.05f, 0.056f));
            return b.Build("Key", 1);
        }

        // ---- Helpers ----

        // Rounded rectangle, counter-clockwise from above, with outward normals. Returns the point count.
        private static int RoundedRect(float halfX, float halfZ, float radius, List<Vector2> points, List<Vector2> normals)
        {
            const int segments = 6;
            var centers = new[]
            {
                new Vector2(halfX - radius, halfZ - radius), new Vector2(-halfX + radius, halfZ - radius),
                new Vector2(-halfX + radius, -halfZ + radius), new Vector2(halfX - radius, -halfZ + radius)
            };
            int count = 0;
            for (int corner = 0; corner < 4; corner++)
                for (int i = 0; i <= segments; i++, count++)
                {
                    float angle = (corner + i / (float)segments) * Mathf.PI * 0.5f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    points?.Add(centers[corner] + direction * radius);
                    normals?.Add(direction);
                }
            return count;
        }

        private static Material GetMaterial(ref Material material, Color color, string name, float gloss, float shade)
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Money Design/Soft Plastic")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            material = new Material(shader) { name = name, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Gloss")) material.SetFloat("_Gloss", gloss);
            if (material.HasProperty("_Shade")) material.SetFloat("_Shade", shade);
            return material;
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>() };
            internal int Count => vertices.Count;

            internal int Vertex(Vector3 position, Vector3 normal)
            {
                vertices.Add(position);
                normals.Add(normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up);
                return vertices.Count - 1;
            }

            // Corners clockwise seen from outside (Unity's front-face winding).
            internal void Quad(int submesh, int a, int b, int c, int d)
            {
                List<int> list = triangles[submesh];
                list.Add(a); list.Add(b); list.Add(c);
                list.Add(a); list.Add(c); list.Add(d);
            }

            // Convex polygon around a centre; the outline winds counter-clockwise seen from above.
            internal void Fan(int submesh, Vector3 center, List<Vector3> outline, Vector3 normal)
            {
                int hub = Vertex(center, normal);
                int first = Count;
                foreach (Vector3 point in outline) Vertex(point, normal);
                for (int i = 0; i < outline.Count; i++)
                {
                    triangles[submesh].Add(hub);
                    triangles[submesh].Add(first + (i + 1) % outline.Count);
                    triangles[submesh].Add(first + i);
                }
            }

            internal void Tube(int submesh, List<Vector3> path, bool closed, float radius, int sides, bool caps)
            {
                int first = Count;
                for (int p = 0; p < path.Count; p++)
                {
                    Vector3 before = path[closed ? (p + path.Count - 1) % path.Count : Mathf.Max(0, p - 1)];
                    Vector3 after = path[closed ? (p + 1) % path.Count : Mathf.Min(path.Count - 1, p + 1)];
                    Vector3 tangent = (after - before).normalized;
                    Vector3 reference = Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.forward;
                    Vector3 side = Vector3.Cross(reference, tangent).normalized;
                    Vector3 lift = Vector3.Cross(tangent, side);
                    for (int s = 0; s < sides; s++)
                    {
                        float angle = s / (float)sides * Mathf.PI * 2f;
                        Vector3 direction = side * Mathf.Cos(angle) + lift * Mathf.Sin(angle);
                        Vertex(path[p] + direction * radius, direction);
                    }
                }
                int segments = closed ? path.Count : path.Count - 1;
                for (int p = 0; p < segments; p++)
                {
                    int a = first + p * sides;
                    int c = first + (p + 1) % path.Count * sides;
                    for (int s = 0; s < sides; s++)
                    {
                        int next = (s + 1) % sides;
                        Quad(submesh, a + s, a + next, c + next, c + s);
                    }
                }
                if (!caps || closed) return;
                for (int end = 0; end < 2; end++)
                {
                    int p = end == 0 ? 0 : path.Count - 1;
                    Vector3 tangent = (path[end == 0 ? 1 : p] - path[end == 0 ? 0 : p - 1]).normalized * (end == 0 ? -1f : 1f);
                    int hub = Vertex(path[p], tangent);
                    int ringStart = Count;
                    for (int s = 0; s < sides; s++) Vertex(vertices[first + p * sides + s], tangent);
                    for (int s = 0; s < sides; s++)
                    {
                        int next = (s + 1) % sides;
                        triangles[submesh].Add(hub);
                        triangles[submesh].Add(end == 0 ? ringStart + next : ringStart + s);
                        triangles[submesh].Add(end == 0 ? ringStart + s : ringStart + next);
                    }
                }
            }

            internal void Box(int submesh, Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                Face(Vector3.up, Vector3.right, Vector3.forward);
                Face(Vector3.down, Vector3.forward, Vector3.right);
                Face(Vector3.right, Vector3.forward, Vector3.up);
                Face(Vector3.left, Vector3.up, Vector3.forward);
                Face(Vector3.forward, Vector3.up, Vector3.right);
                Face(Vector3.back, Vector3.right, Vector3.up);

                void Face(Vector3 normal, Vector3 u, Vector3 v)
                {
                    Vector3 c = center + Vector3.Scale(normal, h);
                    Vector3 du = Vector3.Scale(u, h);
                    Vector3 dv = Vector3.Scale(v, h);
                    int a = Vertex(c - du - dv, normal);
                    int b = Vertex(c - du + dv, normal);
                    int d = Vertex(c + du + dv, normal);
                    int e = Vertex(c + du - dv, normal);
                    Quad(submesh, a, b, d, e);
                }
            }

            internal Mesh Build(string name, int submeshes)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = submeshes;
                for (int i = 0; i < submeshes; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    internal static class LockKeyEffects
    {
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
        private static Material sparkMaterial;

        // A short gold spark burst, parented to the board so it outlives the box.
        internal static void Burst(Transform parent, Vector3 position, float world, int count)
        {
            if (sparkMaterial == null)
            {
                Shader shader = Shader.Find("Balls Out/Celebration Spark") ?? Shader.Find("Sprites/Default");
                sparkMaterial = new Material(shader) { name = "Padlock Spark" };
                sparkMaterial.SetFloat(ShapeId, 0f);
            }
            var host = new GameObject("Padlock Burst");
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            ParticleSystem system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * world, 3.2f * world);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * world, 0.18f * world);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[] { new GradientColorKey(Gold, 0.6f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
            main.maxParticles = 64;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f * world;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.85f), new Keyframe(1f, 0f)));
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = sparkMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
        }
    }
}
