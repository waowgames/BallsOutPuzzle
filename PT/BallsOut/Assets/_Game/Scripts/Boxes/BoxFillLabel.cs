using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // World-space decoration: no canvas, collider or per-frame updates.
    internal sealed class BoxFillLabel
    {
        private static Mesh tabMesh;
        private static Material tabMaterial;
        private static Material outlinedTextMaterial;
        private readonly TextMeshPro text;

        private BoxFillLabel(TextMeshPro text) => this.text = text;

        internal static BoxFillLabel Create(BoxController box, GameObject visual, float cellSize)
        {
            // The board camera looks down with +Z at the top of the screen.
            int topRow = int.MinValue;
            int leftCell = int.MaxValue;
            foreach (Vector2Int cell in box.Shape.Cells)
                if (cell.y > topRow || cell.y == topRow && cell.x < leftCell)
                {
                    topRow = cell.y;
                    leftCell = cell.x;
                }

            float top = cellSize * 0.9f;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                top = Mathf.Max(top, box.transform.InverseTransformPoint(renderer.bounds.max).y);

            float left = (leftCell - 0.5f) * cellSize;
            float back = (topRow + 0.5f) * cellSize;
            Vector3 center = new Vector3(left + cellSize * 0.235f, top + cellSize * 0.045f, back - cellSize * 0.105f);

            if (tabMesh == null)
            {
                tabMesh = new Mesh { name = "Box Fill Tab" };
                tabMesh.vertices = new[]
                {
                    new Vector3(-0.44f, -0.5f, 0f), new Vector3(0.44f, -0.5f, 0f),
                    new Vector3(0.5f, -0.24f, 0f), new Vector3(0.5f, 0.24f, 0f),
                    new Vector3(0.44f, 0.5f, 0f), new Vector3(-0.44f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.24f, 0f), new Vector3(-0.5f, -0.24f, 0f)
                };
                tabMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 6, 5, 0, 7, 6 };
                tabMesh.RecalculateNormals();
            }
            var tab = new GameObject("Fill Percentage Tab", typeof(MeshFilter), typeof(MeshRenderer));
            tab.transform.SetParent(box.transform, false);
            tab.transform.localPosition = center;
            tab.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tab.transform.localScale = new Vector3(cellSize * 0.44f, cellSize * 0.21f, 1f);
            tab.GetComponent<MeshFilter>().sharedMesh = tabMesh;
            MeshRenderer tabRenderer = tab.GetComponent<MeshRenderer>();
            tabRenderer.shadowCastingMode = ShadowCastingMode.Off;
            tabRenderer.receiveShadows = false;
            if (tabMaterial == null)
            {
                Shader shader = Shader.Find("Money Design/Soft Plastic")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    tabMaterial = new Material(shader) { name = "Box Fill Tab", enableInstancing = true };
                    if (tabMaterial.HasProperty("_BaseColor")) tabMaterial.SetColor("_BaseColor", Color.white);
                    if (tabMaterial.HasProperty("_Color")) tabMaterial.SetColor("_Color", Color.white);
                    if (tabMaterial.HasProperty("_Gloss")) tabMaterial.SetFloat("_Gloss", 0f);
                    if (tabMaterial.HasProperty("_Shade")) tabMaterial.SetFloat("_Shade", 0f);
                    if (tabMaterial.HasProperty("_Cull")) tabMaterial.SetFloat("_Cull", 0f);
                }
            }
            if (tabMaterial != null) tabRenderer.sharedMaterial = tabMaterial;

            var label = new GameObject("Fill Percentage");
            label.transform.SetParent(box.transform, false);
            label.transform.localPosition = center + Vector3.up * (cellSize * 0.012f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = label.AddComponent<TextMeshPro>();
            MeshRenderer textRenderer = text.GetComponent<MeshRenderer>();
            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 36f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = "0%";
            if (outlinedTextMaterial == null && text.fontSharedMaterial != null)
            {
                outlinedTextMaterial = new Material(text.fontSharedMaterial) { name = "Box Fill Percentage" };
                if (outlinedTextMaterial.HasProperty("_OutlineWidth"))
                {
                    outlinedTextMaterial.SetFloat("_OutlineWidth", 0.1f);
                    outlinedTextMaterial.SetColor("_OutlineColor", new Color(0.20f, 0.23f, 0.36f));
                }
            }
            if (outlinedTextMaterial != null) text.fontSharedMaterial = outlinedTextMaterial;
            else text.color = new Color(0.20f, 0.23f, 0.36f);

            Vector2 preferred = text.GetPreferredValues("100%");
            text.rectTransform.sizeDelta = preferred;
            float scale = preferred.x > 0f && preferred.y > 0f
                ? Mathf.Min(cellSize * 0.31f / preferred.x, cellSize * 0.13f / preferred.y)
                : cellSize * 0.01f;
            label.transform.localScale = Vector3.one * scale;
            return new BoxFillLabel(text);
        }

        internal void SetFill(int current, int capacity)
        {
            int percent = capacity > 0 ? Mathf.Clamp((current * 100 + capacity / 2) / capacity, 0, 100) : 0;
            text.text = percent + "%";
        }
    }
}
