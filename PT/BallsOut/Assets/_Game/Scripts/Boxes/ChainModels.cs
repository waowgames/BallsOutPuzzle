using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Procedural chain parts, built once and shared. Model units are cells.
    internal static class ChainModels
    {
        // Link: a stadium loop of round wire lying flat in XZ, long axis along +Z.
        private const float LinkStraight = 0.036f;
        private const float LinkBend = 0.04f;
        internal const float LinkWire = 0.0175f;
        // Distance between neighbouring link centres: each link hooks into the next one's inner bend.
        internal const float LinkPitch = 2f * (LinkStraight + LinkBend - LinkWire);
        // Post: a riveted plate on the lid, a short stem with a collar, and an upright swivel eye
        // on top whose ring lies in YZ, so the chain leaves it along +Z.
        private const float PlateRadius = 0.1f;
        private const float PlateThickness = 0.022f;
        private const float StemRadius = 0.024f;
        private const float StemHeight = 0.1f;
        private const float EyeRadius = 0.038f;
        private const float EyeWire = 0.015f;
        // Where the first link hooks in, above the lid.
        internal const float EyeHeight = StemHeight + EyeRadius + EyeWire * 0.4f;

        private static readonly Color Steel = new Color(0.74f, 0.78f, 0.86f);
        private static readonly Color DeepSteel = new Color(0.55f, 0.6f, 0.7f);
        private static readonly Color Bolt = new Color(0.36f, 0.39f, 0.47f);

        private static Mesh link, post;
        private static Material steelMaterial, deepSteelMaterial, boltMaterial;

        internal static Material SteelMaterial => LockKeyModels.GetMaterial(ref steelMaterial, Steel, "Chain Steel", 1f, 0.6f);
        // Every other link is a shade darker, so the chain reads link by link from above.
        internal static Material DeepSteelMaterial => LockKeyModels.GetMaterial(ref deepSteelMaterial, DeepSteel, "Chain Steel Deep", 1f, 0.65f);
        internal static Material BoltMaterial => LockKeyModels.GetMaterial(ref boltMaterial, Bolt, "Chain Bolt", 0.7f, 0.55f);

        internal static Mesh Link => link != null ? link : link = BuildLink();
        // Submesh 0 is the dark plate, submesh 1 the steel stem, rivets and eye.
        internal static Mesh Post => post != null ? post : post = BuildPost();

        private static Mesh BuildLink()
        {
            var path = new List<Vector3>();
            const int bend = 10;
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? LinkStraight : -LinkStraight;
                for (int i = 0; i <= bend; i++)
                {
                    float angle = (end + i / (float)bend) * Mathf.PI;
                    path.Add(new Vector3(Mathf.Cos(angle) * LinkBend, 0f, z + Mathf.Sin(angle) * LinkBend));
                }
            }
            var b = new LockKeyModels.Builder();
            b.Tube(0, path, true, LinkWire, 10, false);
            return b.Build("Chain Link", 1);
        }

        private static Mesh BuildPost()
        {
            var b = new LockKeyModels.Builder();
            // Plate: a low disc with a bevelled top step.
            b.Tube(0, new List<Vector3> { Vector3.zero, new Vector3(0f, PlateThickness * 0.65f, 0f) }, false, PlateRadius, 24, true);
            b.Tube(0, new List<Vector3> { new Vector3(0f, PlateThickness * 0.65f, 0f), new Vector3(0f, PlateThickness, 0f) },
                false, PlateRadius * 0.86f, 24, true);
            // Four rivet heads round the rim.
            for (int i = 0; i < 4; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 0.5f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (PlateRadius * 0.62f);
                b.Tube(1, new List<Vector3> { at + Vector3.up * PlateThickness * 0.8f, at + Vector3.up * (PlateThickness + 0.009f) },
                    false, 0.011f, 8, true);
            }
            // Stem with a collar where it meets the eye.
            b.Tube(1, new List<Vector3> { new Vector3(0f, PlateThickness * 0.5f, 0f), new Vector3(0f, StemHeight, 0f) },
                false, StemRadius, 12, true);
            b.Tube(1, new List<Vector3> { new Vector3(0f, StemHeight - 0.022f, 0f), new Vector3(0f, StemHeight - 0.004f, 0f) },
                false, StemRadius * 1.45f, 12, true);
            // Eye: an upright ring standing on the stem.
            var ring = new List<Vector3>();
            for (int i = 0; i < 20; i++)
            {
                float angle = i / 20f * Mathf.PI * 2f;
                ring.Add(new Vector3(0f, EyeHeight + Mathf.Sin(angle) * EyeRadius, Mathf.Cos(angle) * EyeRadius));
            }
            b.Tube(1, ring, true, EyeWire, 10, false);
            return b.Build("Chain Post", 2);
        }
    }
}
