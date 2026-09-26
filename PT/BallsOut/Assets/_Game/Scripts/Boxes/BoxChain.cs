using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // A steel chain between two boxes. Their footprints may drift at most Length free cells
    // apart on either axis (diagonals included); BoxMovementSystem tows the partner along once
    // the chain is taut. The chain runs between swivel posts on the two lids, well clear of the
    // board: nearly straight, with a slight droop only while the boxes stand close together.
    // It twangs when pulled and snaps apart when either box completes.
    internal sealed class BoxChain : MonoBehaviour
    {
        private const int Samples = 24;
        // Droop at the middle of the chain, in cells, while the boxes touch; none at full stretch.
        private const float MaxSag = 0.07f;
        private const float SagStiffness = 260f;
        private const float SagDamping = 32f;
        private const float StrainCooldown = 0.35f;
        private const float TwangFrequency = 42f;
        private const float TwangDecay = 9f;
        private const float BreakDuration = 0.8f;
        // Posts on a key or padlock box stand aside toward the partner, clear of the lid centre.
        private const float KeyClearance = 0.36f;

        internal BoxController A { get; private set; }
        internal BoxController B { get; private set; }
        internal int Length { get; private set; }
        internal bool IsBroken { get; private set; }
        // Largest distance, in cells, between facing cell centres of the two footprints.
        private float Limit => Length + 1f;

        private float cellSize;
        private Transform postA, postB;
        private readonly List<Transform> links = new List<Transform>();
        private readonly Vector3[] points = new Vector3[Samples + 1];
        private readonly float[] arc = new float[Samples + 1];
        private bool settled;
        private float sag;
        private float sagVelocity;
        private float twang = -1f;
        private float twangStrength;
        private float lastStrain = -10f;
        private float broken = -1f;
        private readonly List<Transform> debris = new List<Transform>();
        private readonly List<Vector3> debrisVelocity = new List<Vector3>();
        private readonly List<Vector3> debrisSpin = new List<Vector3>();

        internal static BoxChain Create(BoxController a, BoxController b, int length, BoardGrid board)
        {
            var root = new GameObject($"Chain {a.Id} - {b.Id}");
            root.transform.SetParent(board.Root, false);
            var chain = root.AddComponent<BoxChain>();
            chain.A = a;
            chain.B = b;
            chain.Length = Mathf.Max(1, length);
            chain.cellSize = board.CellSize;
            a.Chain = chain;
            b.Chain = chain;
            chain.postA = chain.CreatePost(a, b);
            chain.postB = chain.CreatePost(b, a);
            return chain;
        }

        internal BoxController PartnerOf(BoxController box) => IsBroken ? null : box == A ? B : box == B ? A : null;

        // The shape's lowest and highest cell offset along one axis.
        internal static void Span(BoxShapeDefinition shape, bool horizontal, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;
            IReadOnlyList<Vector2Int> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                int value = horizontal ? cells[i].x : cells[i].y;
                if (value < min) min = value;
                if (value > max) max = value;
            }
        }

        // Distance, in cells, between the facing cell centres of the two footprints along one axis.
        private float Gap(BoxController mover, Vector3 moverPosition, BoxController partner, Vector3 partnerPosition, bool horizontal)
        {
            float m = (horizontal ? moverPosition.x : moverPosition.z) / cellSize - 0.5f;
            float p = (horizontal ? partnerPosition.x : partnerPosition.z) / cellSize - 0.5f;
            Span(mover.Shape, horizontal, out int moverMin, out int moverMax);
            Span(partner.Shape, horizontal, out int partnerMin, out int partnerMax);
            return Mathf.Max(p + partnerMin - (m + moverMax), m + moverMin - (p + partnerMax));
        }

        // How far, along one axis, the partner must follow for `mover` to stand at `moverPosition`.
        // Zero while the chain still has slack on that axis.
        internal float Excess(BoxController mover, Vector3 moverPosition, Vector3 partnerPosition, bool horizontal)
        {
            BoxController partner = PartnerOf(mover);
            if (partner == null) return 0f;
            float gap = Gap(mover, moverPosition, partner, partnerPosition, horizontal);
            if (gap <= Limit + 1e-4f) return 0f;
            float m = (horizontal ? moverPosition.x : moverPosition.z) / cellSize;
            float p = (horizontal ? partnerPosition.x : partnerPosition.z) / cellSize;
            Span(mover.Shape, horizontal, out int moverMin, out int moverMax);
            Span(partner.Shape, horizontal, out int partnerMin, out int partnerMax);
            float direction = m + (moverMin + moverMax) * 0.5f > p + (partnerMin + partnerMax) * 0.5f ? 1f : -1f;
            return direction * (gap - Limit) * cellSize;
        }

        // The chain went taut against something that will not move. Returns false while cooling down.
        internal bool Strain()
        {
            if (IsBroken || Time.time - lastStrain < StrainCooldown) return false;
            lastStrain = Time.time;
            Twang(1f);
            return true;
        }

        // The partner starts following.
        internal void Tug() => Twang(0.5f);

        private void Twang(float strength)
        {
            // A fresh pull never quiets a ring that is still louder.
            float ringing = twang >= 0f ? twangStrength * Mathf.Exp(-TwangDecay * twang) : 0f;
            twangStrength = Mathf.Max(strength, ringing);
            twang = 0f;
        }

        // Either box completed: the chain snaps and its links scatter.
        internal void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            broken = 0f;
            Vector3 middle = points[Samples / 2];
            foreach (Transform link in links)
                if (link.gameObject.activeSelf) AddDebris(link, middle);
            postA.SetParent(transform, true);
            postB.SetParent(transform, true);
            AddDebris(postA, middle);
            AddDebris(postB, middle);
            float world = cellSize * transform.lossyScale.x;
            LockKeyEffects.Burst(transform, transform.TransformPoint(middle), world, 8);
        }

        private void AddDebris(Transform piece, Vector3 middle)
        {
            Vector3 outward = piece.localPosition - middle;
            outward.y = 0f;
            if (outward.sqrMagnitude < 1e-6f) outward = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            debris.Add(piece);
            debrisVelocity.Add((outward.normalized * Random.Range(0.5f, 1.3f) + Vector3.up * Random.Range(1.4f, 2.4f)) *
                cellSize * 1.7f);
            debrisSpin.Add(Random.insideUnitSphere * 720f);
        }

        // A swivel post on the lid: the box's centre, or beside a key or padlock toward the partner.
        private Transform CreatePost(BoxController box, BoxController partner)
        {
            Vector2 center = BoxLockVisual.FootprintCenter(box.Shape);
            if (box.HasKey || box.LockId != null)
            {
                Vector2 toward = (Vector2)(partner.Origin - box.Origin) + BoxLockVisual.FootprintCenter(partner.Shape) - center;
                Vector2 step = Mathf.Abs(toward.x) >= Mathf.Abs(toward.y)
                    ? new Vector2(Mathf.Sign(toward.x), 0f)
                    : new Vector2(0f, Mathf.Sign(toward.y));
                center += step * KeyClearance;
            }
            var post = LockKeyModels.CreatePart("Chain Post", box.transform, ChainModels.Post,
                ChainModels.BoltMaterial, ChainModels.SteelMaterial).transform;
            // Frozen boxes wear an ice block 0.03 cells over the lid; the plate sits on top of it.
            float lid = box.ArtTop + cellSize * (box.IsFrozen ? 0.035f : 0.004f);
            post.localPosition = new Vector3(center.x * cellSize, lid, center.y * cellSize);
            post.localScale = Vector3.one * cellSize;
            return post;
        }

        private Vector3 Eye(Transform post) =>
            transform.InverseTransformPoint(post.TransformPoint(new Vector3(0f, ChainModels.EyeHeight, 0f)));

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (broken >= 0f)
            {
                AnimateBreak(dt);
                return;
            }
            if (A == null || B == null || postA == null || postB == null) return;
            // The eyes swivel to face each other.
            Vector3 flat = transform.InverseTransformPoint(postB.position) - transform.InverseTransformPoint(postA.position);
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-8f)
            {
                postA.rotation = transform.rotation * Quaternion.LookRotation(flat);
                postB.rotation = transform.rotation * Quaternion.LookRotation(-flat);
            }
            // Slack only while the boxes stand closer than the chain's reach.
            float stretch = Mathf.Max(Gap(A, A.transform.localPosition, B, B.transform.localPosition, true),
                Gap(A, A.transform.localPosition, B, B.transform.localPosition, false));
            float target = MaxSag * cellSize * (1f - Mathf.Clamp01((stretch - 1f) / Length));
            if (!settled)
            {
                settled = true;
                sag = target;
            }
            sagVelocity += (SagStiffness * (target - sag) - SagDamping * sagVelocity) * dt;
            sag = Mathf.Max(0f, sag + sagVelocity * dt);
            if (twang >= 0f)
            {
                twang += dt;
                if (twang > 0.8f) twang = -1f;
            }
            BuildCurve(Eye(postA), Eye(postB));
            PlaceLinks();
        }

        private void BuildCurve(Vector3 start, Vector3 end)
        {
            Vector3 flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
            Vector3 lateral = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            // A plucked chain: a quick, small side-to-side ring that dies away.
            float vibration = twang >= 0f
                ? Mathf.Sin(twang * TwangFrequency) * Mathf.Exp(-twang * TwangDecay) * twangStrength * cellSize * 0.035f
                : 0f;
            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                float bell = 4f * t * (1f - t);
                points[i] = Vector3.Lerp(start, end, t) + Vector3.down * (sag * bell) + lateral * (vibration * bell);
            }
            arc[0] = 0f;
            for (int i = 1; i <= Samples; i++) arc[i] = arc[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        private void PlaceLinks()
        {
            float total = arc[Samples];
            float pitch = ChainModels.LinkPitch * cellSize;
            // Spacing stretches a hair so the last link hooks into the far eye.
            int count = Mathf.Max(1, Mathf.RoundToInt(total / pitch));
            float spacing = total / count;
            while (links.Count < count)
            {
                bool deep = (links.Count & 1) == 1;
                var link = LockKeyModels.CreatePart("Link", transform, ChainModels.Link,
                    deep ? ChainModels.DeepSteelMaterial : ChainModels.SteelMaterial).transform;
                link.localScale = Vector3.one * cellSize;
                links.Add(link);
            }
            int segment = 1;
            for (int k = 0; k < links.Count; k++)
            {
                Transform link = links[k];
                if (k >= count)
                {
                    if (link.gameObject.activeSelf) link.gameObject.SetActive(false);
                    continue;
                }
                if (!link.gameObject.activeSelf) link.gameObject.SetActive(true);
                float s = (k + 0.5f) * spacing;
                while (segment < Samples && arc[segment] < s) segment++;
                float length = arc[segment] - arc[segment - 1];
                float u = length > 1e-6f ? (s - arc[segment - 1]) / length : 0f;
                Vector3 tangent = points[segment] - points[segment - 1];
                if (tangent.sqrMagnitude < 1e-8f) tangent = points[Samples] - points[0];
                if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.forward;
                // Links alternate flat and upright; the first hooks the upright eye lying flat.
                link.localPosition = Vector3.Lerp(points[segment - 1], points[segment], u);
                link.localRotation = Quaternion.LookRotation(tangent, Vector3.up) *
                    Quaternion.Euler(0f, 0f, (k & 1) == 1 ? 90f : 0f);
            }
        }

        private void AnimateBreak(float dt)
        {
            broken += dt;
            float shrink = 1f - Smooth((broken - 0.35f) / (BreakDuration - 0.35f));
            for (int i = 0; i < debris.Count; i++)
            {
                Transform piece = debris[i];
                if (piece == null) continue;
                Vector3 velocity = debrisVelocity[i] + Vector3.down * (cellSize * 11f * dt);
                debrisVelocity[i] = velocity;
                piece.localPosition += velocity * dt;
                piece.localRotation = Quaternion.Euler(debrisSpin[i] * dt) * piece.localRotation;
                piece.localScale = Vector3.one * (cellSize * shrink);
            }
            if (broken >= BreakDuration) Destroy(gameObject);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
