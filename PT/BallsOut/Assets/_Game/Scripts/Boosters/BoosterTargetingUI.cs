using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BallsOut
{
    /// <summary>
    /// Box picker shared by the targeted boosters (hammer, magnet). The board darkens, the boxes the
    /// booster can act on stay lit with a pulsing outline, and a tap on one of them hands it over.
    /// Lives on the HUD canvas; its transparent Image swallows every tap while picking, so the rest
    /// of the HUD and the board stay inert. The close button cancels without spending the booster.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class BoosterTargetingUI : MonoBehaviour, IPointerClickHandler
    {
        public const string HighlightLayerName = "BoosterHighlight";
        // Used when the project has no layer by that name; any unused layer works.
        private const int FallbackLayer = 31;

        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private Button closeButton;
        [Tooltip("HUD groups faded out while picking, so only the lit boxes draw the eye.")]
        [SerializeField] private CanvasGroup[] fadedGroups = Array.Empty<CanvasGroup>();
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.72f;
        [SerializeField, Range(0f, 1f)] private float fadedHudAlpha = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
        [Tooltip("Outline thickness in board cells.")]
        [SerializeField, Min(0f)] private float outlineWidth = 0.05f;

        private struct Highlight
        {
            public BoxController box;
            public List<KeyValuePair<GameObject, int>> layers;
            public List<GameObject> outlines;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int WidthId = Shader.PropertyToID("_Width");
        private static readonly Dictionary<Mesh, Mesh> SmoothMeshes = new Dictionary<Mesh, Mesh>();

        public static BoosterTargetingUI Instance { get; private set; }
        public bool IsPicking => onPicked != null;

        private readonly List<Highlight> highlights = new List<Highlight>();
        private readonly List<float> groupAlphas = new List<float>();
        private Image catcher;
        private BallBoxLevelRuntime runtime;
        private Func<BoxController, bool> canPick;
        private Action<BoxController> onPicked;
        private Action onCancelled;
        private Camera gameCamera;
        private Camera highlightCamera;
        private int gameCullingMask;
        private Transform dimQuad;
        private Material dimMaterial;
        private Material outlineMaterial;
        private Color outlineColor;
        private float fade;
        private float fadeTarget;
        private float worldWidth;

        private void Awake()
        {
            Instance = this;
            catcher = GetComponent<Image>();
            catcher.enabled = false;
            if (content != null) content.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Cancel);
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Cancel);
            ReleaseCamera();
            if (dimQuad != null) Destroy(dimQuad.gameObject);
            if (dimMaterial != null) Destroy(dimMaterial);
            if (outlineMaterial != null) Destroy(outlineMaterial);
            if (Instance == this) Instance = null;
        }

        /// <summary>Starts picking. False when nothing on the board qualifies or picking is already on.</summary>
        public bool Begin(string prompt, Color outline, Func<BoxController, bool> canPickBox,
            Action<BoxController> picked, Action cancelled)
        {
            if (IsPicking || picked == null || canPickBox == null) return false;
            BallBoxLevelRuntime active = BallBoxLevelRuntime.Active;
            Camera camera = Camera.main;
            if (active == null || camera == null) return false;
            var targets = new List<BoxController>();
            foreach (BoxController box in active.Boxes)
                if (box != null && canPickBox(box)) targets.Add(box);
            if (targets.Count == 0) return false;

            runtime = active;
            canPick = canPickBox;
            onPicked = picked;
            onCancelled = cancelled;
            outlineColor = outline;
            transform.SetAsLastSibling();
            catcher.enabled = true;
            if (content != null) content.SetActive(true);
            if (promptLabel != null) promptLabel.text = prompt;
            UIManager.Instance?.SetGameplayBlocked(this, true);
            if (fade <= 0f)
            {
                groupAlphas.Clear();
                foreach (CanvasGroup group in fadedGroups) groupAlphas.Add(group != null ? group.alpha : 1f);
            }
            SetupCamera(camera);
            worldWidth = outlineWidth * active.Board.CellSize * active.Board.Root.lossyScale.x;
            foreach (BoxController box in targets) highlights.Add(CreateHighlight(box));
            fadeTarget = 1f;
            return true;
        }

        public void Cancel()
        {
            if (!IsPicking) return;
            Action cancelled = onCancelled;
            End();
            cancelled?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsPicking || gameCamera == null) return;
            BoxController box = BoxUnder(eventData.position);
            if (box == null) return;
            bool lit = false;
            foreach (Highlight highlight in highlights) lit |= highlight.box == box;
            if (!lit || !canPick(box))
            {
                box.NudgeLock();
                GameHaptics.Warning();
                return;
            }
            Action<BoxController> picked = onPicked;
            End();
            picked(box);
        }

        private BoxController BoxUnder(Vector2 screenPoint)
        {
            Ray ray = gameCamera.ScreenPointToRay(screenPoint);
            BoxController nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(ray))
            {
                BoxController box = hit.collider.GetComponentInParent<BoxController>();
                if (box == null || hit.distance >= distance) continue;
                nearest = box;
                distance = hit.distance;
            }
            return nearest;
        }

        private void End()
        {
            onPicked = null;
            onCancelled = null;
            canPick = null;
            runtime = null;
            catcher.enabled = false;
            if (content != null) content.SetActive(false);
            UIManager.Instance?.SetGameplayBlocked(this, false);
            foreach (Highlight highlight in highlights) RemoveHighlight(highlight);
            highlights.Clear();
            fadeTarget = 0f;
        }

        private void Update()
        {
            // The level can reload or end under the picker; nothing left to pick then.
            if (IsPicking && (runtime == null || BallBoxLevelRuntime.Active != runtime || runtime.HasWon)) Cancel();
            if (fade == fadeTarget && fade == 0f) return;
            fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime / fadeDuration);
            float eased = fade * fade * (3f - 2f * fade);
            if (dimMaterial != null) dimMaterial.SetColor(ColorId, new Color(0f, 0f, 0f, dimAlpha * eased));
            for (int i = 0; i < fadedGroups.Length && i < groupAlphas.Count; i++)
                if (fadedGroups[i] != null) fadedGroups[i].alpha = Mathf.Lerp(groupAlphas[i], fadedHudAlpha, eased);
            if (outlineMaterial != null)
            {
                float pulse = 1f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
                outlineMaterial.SetFloat(WidthId, worldWidth * pulse);
                outlineMaterial.SetColor(ColorId, outlineColor);
            }
            if (fade <= 0f) ReleaseCamera();
        }

        // ---- Rendering: a shade in front of the game camera, lit boxes redrawn over it ----

        private static int HighlightLayer
        {
            get
            {
                int layer = LayerMask.NameToLayer(HighlightLayerName);
                return layer >= 0 ? layer : FallbackLayer;
            }
        }

        private void SetupCamera(Camera camera)
        {
            if (gameCamera != camera) ReleaseCamera();
            if (gameCamera == null) gameCullingMask = camera.cullingMask;
            gameCamera = camera;
            int layer = HighlightLayer;
            gameCamera.cullingMask = gameCullingMask & ~(1 << layer);

            if (dimMaterial == null)
            {
                Shader shader = Shader.Find("Balls Out/Booster Dim");
                dimMaterial = new Material(shader) { name = "Booster Dim" };
                dimMaterial.SetColor(ColorId, Color.clear);
            }
            if (dimQuad == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Booster Dim";
                Destroy(quad.GetComponent<Collider>());
                MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = dimMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                dimQuad = quad.transform;
            }
            dimQuad.gameObject.layer = 0;
            dimQuad.SetParent(gameCamera.transform, false);
            float distance = gameCamera.nearClipPlane + 0.05f;
            float height = gameCamera.orthographic
                ? gameCamera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(gameCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            dimQuad.localPosition = new Vector3(0f, 0f, distance);
            dimQuad.localRotation = Quaternion.identity;
            dimQuad.localScale = new Vector3(height * gameCamera.aspect, height, 1f) * 1.2f;
            dimQuad.gameObject.SetActive(true);

            if (highlightCamera == null)
            {
                highlightCamera = new GameObject("Booster Highlight Camera").AddComponent<Camera>();
                highlightCamera.gameObject.SetActive(false);
            }
            highlightCamera.CopyFrom(gameCamera);
            highlightCamera.transform.SetParent(gameCamera.transform, false);
            highlightCamera.transform.localPosition = Vector3.zero;
            highlightCamera.transform.localRotation = Quaternion.identity;
            highlightCamera.transform.localScale = Vector3.one;
            highlightCamera.cullingMask = 1 << layer;
            highlightCamera.targetTexture = null;
            UniversalAdditionalCameraData gameData = gameCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData highlightData = highlightCamera.GetUniversalAdditionalCameraData();
            highlightData.renderType = CameraRenderType.Overlay;
            highlightData.renderShadows = gameData.renderShadows;
            highlightData.renderPostProcessing = gameData.renderPostProcessing;
            highlightCamera.gameObject.SetActive(true);
            if (!gameData.cameraStack.Contains(highlightCamera)) gameData.cameraStack.Add(highlightCamera);
        }

        private void ReleaseCamera()
        {
            if (dimQuad != null) dimQuad.gameObject.SetActive(false);
            if (gameCamera != null)
            {
                gameCamera.cullingMask = gameCullingMask;
                if (highlightCamera != null) gameCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(highlightCamera);
            }
            if (highlightCamera != null) highlightCamera.gameObject.SetActive(false);
            gameCamera = null;
            fade = 0f;
        }

        // ---- Lit boxes: moved to the highlight layer and wrapped in an inverted-hull outline ----

        private Highlight CreateHighlight(BoxController box)
        {
            var highlight = new Highlight
            {
                box = box,
                layers = new List<KeyValuePair<GameObject, int>>(),
                outlines = new List<GameObject>(),
            };
            int layer = HighlightLayer;
            foreach (Transform child in box.GetComponentsInChildren<Transform>(true))
            {
                highlight.layers.Add(new KeyValuePair<GameObject, int>(child.gameObject, child.gameObject.layer));
                child.gameObject.layer = layer;
            }
            if (outlineMaterial == null)
            {
                Shader shader = Shader.Find("Balls Out/Booster Outline");
                outlineMaterial = new Material(shader) { name = "Booster Outline" };
            }
            outlineMaterial.SetColor(ColorId, outlineColor);
            outlineMaterial.SetFloat(WidthId, worldWidth);
            AddOutlines(box.Art, highlight.outlines, layer);
            if (box.InnerArt != null) AddOutlines(box.InnerArt.gameObject, highlight.outlines, layer);
            return highlight;
        }

        private void AddOutlines(GameObject art, List<GameObject> outlines, int layer)
        {
            if (art == null) return;
            foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<TMP_Text>() != null) continue;
                if (!filter.TryGetComponent(out MeshRenderer source) || !source.enabled) continue;
                var outline = new GameObject("Booster Outline", typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
                outline.transform.SetParent(filter.transform, false);
                outline.GetComponent<MeshFilter>().sharedMesh = SmoothNormals(filter.sharedMesh);
                MeshRenderer renderer = outline.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = outlineMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                outlines.Add(outline);
            }
        }

        private static void RemoveHighlight(Highlight highlight)
        {
            foreach (GameObject outline in highlight.outlines)
                if (outline != null) Destroy(outline);
            foreach (KeyValuePair<GameObject, int> entry in highlight.layers)
                if (entry.Key != null) entry.Key.layer = entry.Value;
        }

        // Hard-edged meshes split their corners, which tears an extruded hull apart; the outline copy
        // shares one averaged normal per position instead. Unreadable meshes outline as they are.
        private static Mesh SmoothNormals(Mesh source)
        {
            if (SmoothMeshes.TryGetValue(source, out Mesh smooth) && smooth != null) return smooth;
            if (!source.isReadable) return source;
            smooth = Instantiate(source);
            smooth.name = source.name + " (Outline)";
            smooth.hideFlags = HideFlags.DontSave;
            Vector3[] vertices = smooth.vertices;
            Vector3[] normals = smooth.normals;
            if (normals == null || normals.Length != vertices.Length) return source;
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                keys[i] = Vector3Int.RoundToInt(vertices[i] * 10000f);
                sums.TryGetValue(keys[i], out Vector3 sum);
                sums[keys[i]] = sum + normals[i];
            }
            for (int i = 0; i < vertices.Length; i++) normals[i] = sums[keys[i]].normalized;
            smooth.normals = normals;
            SmoothMeshes[source] = smooth;
            return smooth;
        }
    }
}
