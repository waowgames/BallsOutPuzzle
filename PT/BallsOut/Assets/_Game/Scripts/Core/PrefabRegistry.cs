using System;
using UnityEngine;

namespace BallsOut
{
    [Serializable]
    public struct BoxVisualEntry
    {
        public BoxShapeDefinition shape;
        public GameObject prefab;
        public Vector3 localOffset;
        public Vector3 localScale;
        public Vector3 fillOffset;
    }

    [CreateAssetMenu(menuName = "Balls Out/Prefab Registry")]
    public sealed class PrefabRegistry : ScriptableObject
    {
        public GameObject ballPrefab;
        public Vector3 ballScale = Vector3.one * 0.16f;
        public float ballHeight = 0.1f;
        [Tooltip("Fill center spacing, in macro-cell units: X, layer height, Z.")]
        public Vector3 fillSpacing = new Vector3(0.25f, 0.18f, 0.25f);
        public Vector3 fillOffset = new Vector3(0f, 0.22f, 0f);
        [Min(0.02f)] public float fillDuration = 0.2f;
        [Min(0f)] public float completionDuration = 0.25f;
        public GameObject floorPrefab;
        public GameObject blockedCellPrefab;
        public Vector3 tileScale = Vector3.one;
        public Vector3 tileOffset;
        public BoxVisualEntry[] boxes = Array.Empty<BoxVisualEntry>();

        public bool TryGetBox(BoxShapeDefinition shape, out BoxVisualEntry entry)
        {
            foreach (var item in boxes)
                if (item.shape == shape) { entry = item; return true; }
            entry = default;
            return false;
        }

        internal static void ApplyMaterial(GameObject visual, Material material)
        {
            if (material == null) return;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }
    }
}
