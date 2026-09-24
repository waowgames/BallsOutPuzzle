using System.IO;
using UnityEditor;
using UnityEngine;

namespace BallsOut.EditorTools
{
    // Renders every configured level through the game camera into Temp/LevelPreviews.
    [InitializeOnLoad]
    internal static class LevelPreviewCapture
    {
        private const string RuntimePrefabPath = "Assets/_Game/Content/GridAndBox/ExampleLevelRuntime.prefab";
        private static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/LevelPreviews"));
        private static string RequestFile => Path.Combine(OutputFolder, "capture.request");

        private static double nextPoll;

        static LevelPreviewCapture()
        {
            // Polled so a capture can be requested from outside the editor without a recompile.
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 1.0;
                if (!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Import external edits first; a script change reloads the domain and the next poll captures.
                AssetDatabase.Refresh();
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                File.Delete(RequestFile);
                Capture();
            };
        }

        [MenuItem("Tools/Balls Out/Capture Level Previews")]
        private static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
            Camera camera = Camera.main;
            if (prefab == null || camera == null)
            {
                Debug.LogWarning("[Balls Out] Level preview capture needs the runtime prefab and a main camera.");
                return;
            }
            Directory.CreateDirectory(OutputFolder);
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            RenderTexture target = camera.targetTexture;
            var texture = new RenderTexture(1080, 1920, 24);
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition"))
                {
                    var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    var instance = Object.Instantiate(prefab);
                    instance.hideFlags = HideFlags.DontSave;
                    try
                    {
                        if (!instance.GetComponent<BallBoxLevelRuntime>().LoadLevel(level)) continue;
                        camera.targetTexture = texture;
                        camera.Render();
                        RenderTexture.active = texture;
                        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(OutputFolder, level.name + ".png"), pixels.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = target;
                        Object.DestroyImmediate(instance);
                    }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(pixels);
            }
            File.WriteAllText(Path.Combine(OutputFolder, "capture.done"), System.DateTime.Now.ToString("O"));
            Debug.Log("[Balls Out] Level previews written to " + OutputFolder);
        }
    }
}
