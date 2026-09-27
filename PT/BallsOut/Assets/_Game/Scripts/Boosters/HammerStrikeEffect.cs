using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    /// <summary>
    /// The hammer booster's swing: the hammer grows in beside the box raised high, slams its head
    /// onto the box, shakes the camera, then recoils and shrinks away. Built for _ART/Hammer.fbx:
    /// handle along the model's -Y, striking faces on ±X.
    /// </summary>
    public sealed class HammerStrikeEffect : MonoBehaviour
    {
        private const float AppearTime = 0.22f;
        private const float WindupHold = 0.12f;
        private const float StrikeTime = 0.12f;
        private const float ImpactHold = 0.16f;
        private const float LeaveTime = 0.24f;
        private const float WindupAngle = -80f;
        private const float RecoilAngle = -28f;
        private const float ShakeTime = 0.22f;
        // Model measurements, in units of the unscaled mesh.
        private const float HandleEnd = -0.37f;
        private const float HeadCenter = 0.83f;
        private const float HeadHalfWidth = 0.61f;
        private const float HeadWidth = 1.22f;
        // How far the hammer floats off the lid toward the camera, in cells, so the board never clips it.
        private const float Lift = 2.5f;
        private static Material paint;

        private Action onImpact;
        private Quaternion facing;
        private float elapsed;
        private bool struck;
        private Transform shakenCamera;
        private Vector3 cameraRest;
        private float shake;
        private float cellSize;

        /// <summary>Swings a hammer onto the box; onImpact fires the moment the head lands.</summary>
        public static void Play(GameObject model, BoxController box, float sizeCells, Action onImpact)
        {
            Camera camera = Camera.main;
            if (box == null || camera == null)
            {
                onImpact?.Invoke();
                return;
            }
            float cell = box.runtimeCellSize * box.transform.lossyScale.x;
            float scale = sizeCells * cell / HeadWidth;
            // Aim at the footprint centre, on the lid.
            Vector3 center = Vector3.zero;
            foreach (Vector2Int c in box.Shape.Cells) center += new Vector3(c.x, 0f, c.y);
            center *= box.runtimeCellSize / box.Shape.CellCount;
            center.y = box.ArtTop;
            Vector3 lid = box.transform.TransformPoint(center);

            // The camera looks down on the board, so the swing plays in the screen plane: the rig faces
            // the camera, floats toward it off the lid, and pivots on the handle end to the right of the box.
            Transform view = camera.transform;
            Vector3 anchor = lid - view.forward * (Lift * cell);
            var rig = new GameObject("Hammer Strike");
            rig.transform.SetPositionAndRotation(
                anchor + view.right * ((HeadCenter - HandleEnd) * scale) + view.up * (HeadHalfWidth * scale * 0.5f),
                view.rotation);

            if (model != null)
            {
                GameObject hammer = Instantiate(model, rig.transform, false);
                hammer.name = "Hammer";
                // Handle along rig -X from the pivot, striking faces toward screen up and down.
                hammer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                hammer.transform.localPosition = new Vector3(HandleEnd * scale, 0f, 0f);
                hammer.transform.localScale = Vector3.one * scale;
                Material paint = Paint;
                foreach (Renderer renderer in hammer.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (paint == null) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = paint;
                    renderer.sharedMaterials = materials;
                }
            }

            var effect = rig.AddComponent<HammerStrikeEffect>();
            effect.onImpact = onImpact;
            effect.facing = view.rotation;
            effect.cellSize = cell;
            effect.Evaluate();
        }

        // The imported model's palette texture is not in the project; this paints it like the HUD icon.
        private static Material Paint
        {
            get
            {
                if (paint != null) return paint;
                Shader shader = Shader.Find("Balls Out/Booster Hammer");
                if (shader != null) paint = new Material(shader) { name = "Booster Hammer" };
                return paint;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            Evaluate();
            UpdateShake();
        }

        private void Evaluate()
        {
            float t = elapsed;
            float angle, size = 1f;
            if (t < AppearTime)
            {
                float k = t / AppearTime;
                size = BackOut(k);
                angle = Mathf.Lerp(WindupAngle * 0.6f, WindupAngle, k);
            }
            else if ((t -= AppearTime) < WindupHold)
                angle = WindupAngle - 6f * Mathf.Sin(t / WindupHold * Mathf.PI * 0.5f);
            else if ((t -= WindupHold) < StrikeTime)
            {
                // Accelerates into the blow.
                float k = t / StrikeTime;
                angle = Mathf.Lerp(WindupAngle - 6f, 0f, k * k * k);
            }
            else
            {
                t -= StrikeTime;
                if (!struck) Impact();
                if (t < ImpactHold)
                    angle = -4f * Mathf.Sin(t / ImpactHold * Mathf.PI);
                else if ((t -= ImpactHold) < LeaveTime)
                {
                    float k = t / LeaveTime;
                    angle = Mathf.Lerp(0f, RecoilAngle, 1f - (1f - k) * (1f - k));
                    size = 1f - k * k;
                }
                else
                {
                    if (shake <= 0f) Destroy(gameObject);
                    angle = RecoilAngle;
                    size = 0f;
                }
            }
            transform.rotation = facing * Quaternion.AngleAxis(angle, Vector3.forward);
            transform.localScale = Vector3.one * Mathf.Max(0f, size);
        }

        private void Impact()
        {
            struck = true;
            Camera camera = Camera.main;
            if (camera != null)
            {
                shakenCamera = camera.transform;
                cameraRest = shakenCamera.localPosition;
                shake = ShakeTime;
            }
            GameHaptics.Heavy();
            Action callback = onImpact;
            onImpact = null;
            callback?.Invoke();
        }

        private void UpdateShake()
        {
            if (shake <= 0f || shakenCamera == null) return;
            shake -= Time.deltaTime;
            if (shake <= 0f)
            {
                shakenCamera.localPosition = cameraRest;
                return;
            }
            float strength = shake / ShakeTime * cellSize * 0.06f;
            shakenCamera.localPosition = cameraRest + (Vector3)(UnityEngine.Random.insideUnitCircle * strength);
        }

        private void OnDestroy()
        {
            if (shake > 0f && shakenCamera != null) shakenCamera.localPosition = cameraRest;
        }

        private static float BackOut(float k)
        {
            const float s = 1.70158f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }
    }
}
