using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BallsOut.Editor
{
    // Explicit development-only art. Gameplay never depends on these primitive meshes.
    public static class Phase7PrototypeBuilder
    {
        private const string Root = "Assets/_Game/Prototype";

        [MenuItem("Tools/Balls Out/Create Phase 7 Prototype")]
        public static void Create()
        {
            if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                Debug.LogError("[Balls Out] Save the current untitled scene before creating the prototype.");
                return;
            }
            var existingLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Root + "/Level.asset");
            if (existingLevel != null)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Phase7.unity") == null)
                    BuildScene(existingLevel, AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/LevelRuntime.prefab"));
                else Debug.Log("[Balls Out] Prototype already exists: " + Root + "/Phase7.unity");
                return;
            }
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("A Lit shader is required for development visuals.");
            var red = MakeColor("Coral", new Color(0.95f, 0.25f, 0.18f), shader);
            var blue = MakeColor("Blue", new Color(0.12f, 0.45f, 0.95f), shader);
            var floorMaterial = new Material(shader) { name = "Development Floor", color = new Color(0.15f, 0.18f, 0.22f), enableInstancing = true };
            AssetDatabase.CreateAsset(floorMaterial, Root + "/Floor.mat");
            Vector2Int[][] cells = {
                new[] { V(0, 0) }, new[] { V(0, 0), V(1, 0) },
                new[] { V(0, 0), V(0, 1), V(1, 0) },
                new[] { V(0, 0), V(1, 0), V(2, 0), V(1, 1) },
                new[] { V(0, 0), V(1, 0), V(0, 1), V(1, 1) }
            };
            string[] names = { "Single", "Domino", "L", "T", "Square" };
            var registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            registry.ballPrefab = PrimitivePrefab("DevelopmentBall", PrimitiveType.Sphere, Vector3.one, null);
            registry.ballScale = Vector3.one * 0.225f;
            registry.floorPrefab = PrimitivePrefab("DevelopmentFloor", PrimitiveType.Cube, new Vector3(0.98f, 0.06f, 0.98f), floorMaterial);
            registry.tileOffset = Vector3.down * 0.08f;
            registry.blockedCellPrefab = PrimitivePrefab("DevelopmentBlock", PrimitiveType.Cube, new Vector3(0.98f, 0.5f, 0.98f), floorMaterial);
            registry.boxes = new BoxVisualEntry[cells.Length];
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.lowerGridHeight = 5;
            level.palette.Add(red);
            level.palette.Add(blue);
            Vector2Int[] origins = { V(1, 4), V(3, 3), V(5, 3), V(0, 1), V(4, 0) };
            for (int i = 0; i < cells.Length; i++)
            {
                var shape = ScriptableObject.CreateInstance<BoxShapeDefinition>();
                var data = new SerializedObject(shape);
                SerializedProperty offsets = data.FindProperty("cells");
                offsets.arraySize = cells[i].Length;
                for (int j = 0; j < cells[i].Length; j++) offsets.GetArrayElementAtIndex(j).vector2IntValue = cells[i][j];
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(shape, Root + "/" + names[i] + ".asset");
                registry.boxes[i] = new BoxVisualEntry { shape = shape, prefab = BoxPrefab(names[i], cells[i]), localScale = Vector3.one };
                level.boxes.Add(new BoxSpawnData { id = names[i], shape = shape, color = i < 3 ? blue : red, startingMacroOrigin = origins[i] });
            }
            LevelDefinitionEditor.GenerateDenseField(level);
            AssetDatabase.CreateAsset(registry, Root + "/Prefabs.asset");
            AssetDatabase.CreateAsset(level, Root + "/Level.asset");
            var runtimeObject = new GameObject("Ball Box Level");
            var runtime = runtimeObject.AddComponent<BallBoxLevelRuntime>();
            var runtimeData = new SerializedObject(runtime);
            runtimeData.FindProperty("level").objectReferenceValue = level;
            runtimeData.FindProperty("prefabs").objectReferenceValue = registry;
            runtimeData.ApplyModifiedPropertiesWithoutUndo();
            GameObject runtimePrefab = PrefabUtility.SaveAsPrefabAsset(runtimeObject, Root + "/LevelRuntime.prefab");
            UnityEngine.Object.DestroyImmediate(runtimeObject);
            var levelData = new SerializedObject(level);
            levelData.FindProperty("levelPrefab").objectReferenceValue = runtimePrefab;
            levelData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            BuildScene(level, runtimePrefab);
        }

        internal static void BuildScene(LevelDefinition level, GameObject runtimePrefab, string scenePath = Root + "/Phase7.unity")
        {
            if (runtimePrefab == null) throw new InvalidOperationException("Assign or restore the prototype LevelRuntime.prefab before creating its scene.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            PrefabUtility.InstantiatePrefab(runtimePrefab, scene);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(level.TotalHeight * 0.55f, level.macroGridWidth / (9f / 16f) * 0.55f);
            camera.transform.position = new Vector3(level.macroGridWidth * 0.5f, 15f, level.TotalHeight * 0.5f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
            EditorSceneManager.SaveScene(scene, scenePath);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Balls Out] Prototype created: " + scenePath + ". Art is temporary; runtime includes Phases 1–10.");
        }

        private static BallColorDefinition MakeColor(string name, Color color, Shader shader)
        {
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
            var definition = ScriptableObject.CreateInstance<BallColorDefinition>();
            definition.id = name.ToLowerInvariant();
            definition.displayColor = color;
            definition.ballMaterial = material;
            definition.boxMaterial = material;
            AssetDatabase.CreateAsset(definition, Root + "/" + name + ".asset");
            return definition;
        }

        private static GameObject PrimitivePrefab(string name, PrimitiveType type, Vector3 scale, Material material)
        {
            var root = new GameObject(name);
            GameObject mesh = GameObject.CreatePrimitive(type);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            if (material != null) mesh.GetComponent<Renderer>().sharedMaterial = material;
            GameObject result = PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return result;
        }

        private static GameObject BoxPrefab(string name, Vector2Int[] cells)
        {
            var root = new GameObject("Development " + name);
            foreach (var cell in cells)
            {
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(root.transform, false);
                mesh.transform.localPosition = new Vector3(cell.x, 0.03f, cell.y);
                mesh.transform.localScale = new Vector3(0.91f, 0.18f, 0.91f);
                UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            }
            GameObject result = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Development" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return result;
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    }
}
