using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BallsOut.Editor
{
    [InitializeOnLoad]
    public static class GridAndBoxGameBuilder
    {
        private const string Art = "Assets/_ART/GridAndBox";
        private const string Content = "Assets/_Game/Content/GridAndBox";
        private const string LevelPath = Content + "/ExampleLevel.asset";
        private const string RuntimePath = Content + "/ExampleLevelRuntime.prefab";
        private const string ScenePath = "Assets/Scenes/Game Scene.unity";

        private static readonly string[] ArtPrefabs =
        {
            Art + "/Prefabs/PF_GridCell.prefab",
            Art + "/Prefabs/PF_Grid_6x6.prefab",
            Art + "/Prefabs/PF_Desk.prefab",
            Art + "/Prefabs/PF_Box_1x1.prefab",
            Art + "/Prefabs/PF_Box_1x2.prefab",
            Art + "/Prefabs/PF_Box_1x3.prefab",
            Art + "/Prefabs/PF_Box_2x1.prefab",
            Art + "/Prefabs/PF_Box_L.prefab",
            Art + "/Prefabs/PF_Box_Plus.prefab"
        };

        static GridAndBoxGameBuilder()
        {
            EditorApplication.delayCall += BuildOnce;
        }

        [MenuItem("Tools/Balls Out/Build Grid And Box Game")]
        public static void Build()
        {
            Directory.CreateDirectory(Content);
            AssetDatabase.Refresh();

            foreach (string path in ArtPrefabs) NormalizeVisualPrefab(path);

            Material coral = Load<Material>(Art + "/Materials/Coral.mat");
            Material aqua = Load<Material>(Art + "/Materials/Aqua.mat");
            Material saffron = Load<Material>(Art + "/Materials/Saffron.mat");
            Material lilac = Load<Material>(Art + "/Materials/Lilac.mat");
            Material emerald = Load<Material>(Art + "/Materials/Emerald.mat");
            Material[] materials = { coral, aqua, saffron, lilac, emerald };
            foreach (Material material in materials)
            {
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
            }

            GameObject ballPrefab = CreateBallPrefab(emerald);
            BoxShapeDefinition one = CreateShape("Shape_1x1", V(0, 0));
            BoxShapeDefinition verticalTwo = CreateShape("Shape_1x2", V(0, 0), V(0, 1));
            BoxShapeDefinition verticalThree = CreateShape("Shape_1x3", V(0, 0), V(0, 1), V(0, 2));
            BoxShapeDefinition horizontalTwo = CreateShape("Shape_2x1", V(0, 0), V(1, 0));
            BoxShapeDefinition elbow = CreateShape("Shape_L", V(0, 0), V(0, 1), V(0, 2), V(1, 0));
            BoxShapeDefinition plus = CreateShape("Shape_Plus", V(1, 0), V(0, 1), V(1, 1), V(2, 1), V(1, 2));

            BallColorDefinition[] colors =
            {
                CreateColor("Color_Coral", "coral", coral),
                CreateColor("Color_Aqua", "aqua", aqua),
                CreateColor("Color_Saffron", "saffron", saffron),
                CreateColor("Color_Lilac", "lilac", lilac),
                CreateColor("Color_Emerald", "emerald", emerald)
            };

            var entries = new[]
            {
                Entry(one, "PF_Box_1x1"),
                Entry(verticalTwo, "PF_Box_1x2"),
                Entry(verticalThree, "PF_Box_1x3", new Vector3(0f, 0f, 1f)),
                Entry(horizontalTwo, "PF_Box_2x1"),
                Entry(elbow, "PF_Box_L", new Vector3(0f, 0f, 1f)),
                Entry(plus, "PF_Box_Plus", new Vector3(1f, 0f, 1f))
            };
            PrefabRegistry registry = CreateRegistry(ballPrefab, entries);
            LevelDefinition level = CreateLevel(one, verticalTwo, horizontalTwo, verticalThree, elbow, plus, colors);
            GameObject runtimePrefab = CreateRuntimePrefab(level, registry);
            SetObjectReference(level, "levelPrefab", runtimePrefab);
            ConfigureLevelList(level);
            ConfigureGameScene(level);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Balls Out] Grid/box art, example level and single-scene Game Scene setup are ready.", level);
        }

        private static void BuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePath) != null)
                return;
            try { Build(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void NormalizeVisualPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    foreach (Collider collider in child.GetComponents<Collider>())
                        UnityEngine.Object.DestroyImmediate(collider);
                    foreach (Rigidbody body in child.GetComponents<Rigidbody>())
                        UnityEngine.Object.DestroyImmediate(body);
                }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null) material.enableInstancing = true;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/Balls Out/Repair Game Scene")]
        public static void RepairGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PrefabRegistry registry = Load<PrefabRegistry>(Content + "/PrefabRegistry.asset");
            CreateRegistry(CreateBallPrefab(Load<Material>(Art + "/Materials/Emerald.mat")), registry.boxes);
            ConfigureGameScene(Load<LevelDefinition>(LevelPath));
            AssetDatabase.SaveAssets();
            Debug.Log("[Balls Out] Game scene repaired: Game owns gameplay, automatic start and sphere balls.");
        }

        [MenuItem("Tools/Balls Out/Update Board Layout")]
        public static void UpdateBoardLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PrefabRegistry registry = Load<PrefabRegistry>(Content + "/PrefabRegistry.asset");
            CreateRegistry(registry.ballPrefab, registry.boxes);
            for (int i = 0; i < registry.boxes.Length; i++)
            {
                registry.boxes[i].localOffset.y = 0.22f;
                registry.boxes[i].fillOffset.y = 0.22f;
            }
            LevelDefinition level = Load<LevelDefinition>(LevelPath);
            ShapePileCrown(level);
            GameObject root = PrefabUtility.LoadPrefabContents(RuntimePath);
            try
            {
                Transform art = root.transform.Find("Board Art");
                Transform oldGrid = art.Find("Ball Grid");
                if (oldGrid != null) UnityEngine.Object.DestroyImmediate(oldGrid.gameObject);
                Transform oldDepot = art.Find("Ball Depot");
                if (oldDepot != null) UnityEngine.Object.DestroyImmediate(oldDepot.gameObject);
                CreateBallDepot(art, level);
                PrefabUtility.SaveAsPrefabAsset(root, RuntimePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Balls Out] Board layout updated: lower grid cells, raised boxes and a separate ball depot.");
        }

        private static GameObject CreateBallPrefab(Material material)
        {
            string path = Art + "/Prefabs/PF_Ball.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/PF_Money.prefab") != null)
            {
                string error = AssetDatabase.MoveAsset(Art + "/Prefabs/PF_Money.prefab", path);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "PF_Ball";
            UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static BoxShapeDefinition CreateShape(string name, params Vector2Int[] cells)
        {
            string path = Content + "/" + name + ".asset";
            BoxShapeDefinition shape = AssetDatabase.LoadAssetAtPath<BoxShapeDefinition>(path);
            if (shape == null)
            {
                shape = ScriptableObject.CreateInstance<BoxShapeDefinition>();
                AssetDatabase.CreateAsset(shape, path);
            }
            var data = new SerializedObject(shape);
            SerializedProperty property = data.FindProperty("cells");
            property.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) property.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return shape;
        }

        private static BallColorDefinition CreateColor(string name, string id, Material material)
        {
            string path = Content + "/" + name + ".asset";
            BallColorDefinition color = AssetDatabase.LoadAssetAtPath<BallColorDefinition>(path);
            if (color == null)
            {
                color = ScriptableObject.CreateInstance<BallColorDefinition>();
                AssetDatabase.CreateAsset(color, path);
            }
            color.id = id;
            color.displayColor = material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            color.ballMaterial = material;
            color.boxMaterial = material;
            color.ballPrefabOverride = null;
            EditorUtility.SetDirty(color);
            return color;
        }

        private static PrefabRegistry CreateRegistry(GameObject ballPrefab, BoxVisualEntry[] entries)
        {
            string path = Content + "/PrefabRegistry.asset";
            PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<PrefabRegistry>();
                AssetDatabase.CreateAsset(registry, path);
            }
            registry.ballPrefab = ballPrefab;
            registry.ballScale = Vector3.one * 0.32f;
            registry.ballHeight = 0.29f;
            registry.fillSpacing = Vector3.one * 0.25f;
            registry.fillOffset = new Vector3(0f, 0.2f, 0f);
            registry.fillDuration = 0.16f;
            registry.completionDuration = 0.22f;
            registry.floorPrefab = Load<GameObject>(Art + "/Prefabs/PF_GridCell.prefab");
            registry.blockedCellPrefab = null;
            registry.tileScale = Vector3.one;
            registry.tileOffset = new Vector3(0f, 0.1f, 0f);
            registry.boxes = entries;
            EditorUtility.SetDirty(registry);
            return registry;
        }

        private static LevelDefinition CreateLevel(
            BoxShapeDefinition one,
            BoxShapeDefinition verticalTwo,
            BoxShapeDefinition horizontalTwo,
            BoxShapeDefinition verticalThree,
            BoxShapeDefinition elbow,
            BoxShapeDefinition plus,
            BallColorDefinition[] colors)
        {
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }
            level.macroGridWidth = 6;
            level.lowerGridHeight = 6;
            level.ballAreaMacroHeight = 9;
            level.macroCellSize = 1f;
            level.lowerGridMask = new CellMask();
            level.ballAreaMask = new CellMask();
            level.ballAreaMask.overrides.Add(new CellOverride { cell = V(3, 8), kind = CellKind.Outside });
            level.ballAreaMask.overrides.Add(new CellOverride { cell = V(4, 8), kind = CellKind.Outside });
            level.ballAreaMask.overrides.Add(new CellOverride { cell = V(5, 8), kind = CellKind.Outside });
            level.boxes.Clear();
            level.boxes.Add(Spawn("Coral 1x1", one, colors[0], 5, 4));
            level.boxes.Add(Spawn("Aqua 1x2", verticalTwo, colors[1], 2, 3));
            level.boxes.Add(Spawn("Saffron 2x1", horizontalTwo, colors[2], 3, 5));
            level.boxes.Add(Spawn("Lilac 1x3", verticalThree, colors[3], 0, 3));
            level.boxes.Add(Spawn("Emerald L", elbow, colors[4], 4, 0));
            level.boxes.Add(Spawn("Emerald Plus", plus, colors[4], 0, 0));
            level.palette.Clear();
            level.palette.AddRange(colors);
            level.balls.Clear();

            int[] macroCounts = { 3, 6, 6, 9, 27 };
            int colorIndex = 0;
            int remaining = macroCounts[0];
            for (int macroY = 0; macroY < level.ballAreaMacroHeight; macroY++)
                for (int macroX = 0; macroX < 6; macroX++)
                {
                    if (level.ballAreaMask.Get(V(macroX, macroY), 6, level.ballAreaMacroHeight) != CellKind.Usable)
                        continue;
                    while (remaining == 0) remaining = macroCounts[++colorIndex];
                    for (int microY = 0; microY < 3; microY++)
                        for (int microX = 0; microX < 3; microX++)
                            level.balls.Add(new BallSpawnData
                            {
                                color = colors[colorIndex],
                                cell = new Vector2Int(macroX * 3 + microX, (level.lowerGridHeight + macroY) * 3 + microY)
                            });
                    remaining--;
                }

            ShapePileCrown(level);
            var errors = new List<string>();
            LevelValidator.Validate(level, errors);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            EditorUtility.SetDirty(level);
            return level;
        }

        private static void ShapePileCrown(LevelDefinition level)
        {
            // Redistribute only the sample's top 63 balls; preserve colors and capacity.
            int firstRow = level.TotalHeight * 3 - 5;
            var crown = level.balls.FindAll(ball => ball.cell.y >= firstRow);
            if (level.macroGridWidth != 6 || crown.Count != 63) return;
            level.ballAreaMask.overrides.Clear();
            int[] widths = { 17, 16, 14, 10, 6 };
            int index = 0;
            for (int row = 0; row < widths.Length; row++)
                for (int x = 0; x < widths[row]; x++)
                    crown[index++].cell = V((19 - widths[row]) / 2 + x, firstRow + row);
            EditorUtility.SetDirty(level);
        }

        private static GameObject CreateRuntimePrefab(LevelDefinition level, PrefabRegistry registry)
        {
            var root = new GameObject("Example Level Runtime");
            var runtime = root.AddComponent<BallBoxLevelRuntime>();
            var runtimeData = new SerializedObject(runtime);
            runtimeData.FindProperty("level").objectReferenceValue = level;
            runtimeData.FindProperty("prefabs").objectReferenceValue = registry;
            runtimeData.FindProperty("waitForLevelStart").boolValue = true;
            runtimeData.FindProperty("drawDebugGizmos").boolValue = false;
            runtimeData.ApplyModifiedPropertiesWithoutUndo();

            var artRoot = new GameObject("Board Art").transform;
            artRoot.SetParent(root.transform, false);
            AddArt(Load<GameObject>(Art + "/Prefabs/PF_Desk.prefab"), artRoot, new Vector3(3f, -0.65f, level.TotalHeight * 0.5f));
            AddArt(Load<GameObject>(Art + "/Prefabs/PF_Grid_6x6.prefab"), artRoot, new Vector3(3f, 0f, 3f));
            CreateBallDepot(artRoot, level);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RuntimePath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateBallDepot(Transform parent, LevelDefinition level)
        {
            var depot = new GameObject("Ball Depot").transform;
            depot.SetParent(parent, false);
            float width = level.macroGridWidth * level.macroCellSize;
            float bottom = (level.lowerGridHeight + 0.28f) * level.macroCellSize;
            float depth = (level.ballAreaMacroHeight * 3 * 0.32f * 0.8660254f + 0.2f) * level.macroCellSize;
            Material floor = Load<Material>(Art + "/Materials/Recess.mat");
            Material rim = Load<Material>(Art + "/Materials/ReferenceIvory.mat");
            AddDepotPart("Floor", depot, new Vector3(width * 0.5f, 0.06f, bottom + depth * 0.5f), new Vector3(width, 0.12f, depth), floor);
            AddDepotPart("Left Rim", depot, new Vector3(-0.08f, 0.14f, bottom + depth * 0.5f), new Vector3(0.16f, 0.4f, depth), rim);
            AddDepotPart("Right Rim", depot, new Vector3(width + 0.08f, 0.14f, bottom + depth * 0.5f), new Vector3(0.16f, 0.4f, depth), rim);
            AddDepotPart("Back Rim", depot, new Vector3(width * 0.5f, 0.14f, bottom + depth + 0.08f), new Vector3(width + 0.32f, 0.4f, 0.16f), rim);
        }

        private static void AddDepotPart(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ConfigureLevelList(LevelDefinition level)
        {
            LevelConfig config = Load<LevelConfig>("Assets/Scripts/LevelConfig.asset");
            var data = new SerializedObject(config);
            SerializedProperty levels = data.FindProperty("levels");
            levels.arraySize = 1;
            levels.GetArrayElementAtIndex(0).objectReferenceValue = level;
            data.FindProperty("enableLooping").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGameScene(LevelDefinition level)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            GameObject game = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Game") game = root;
            if (game == null) game = new GameObject("Game");

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "UI Manager")
                {
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    // Unpack only the outer container so existing UI references and nested prefabs survive reparenting.
                    if (root.GetComponent<LevelContentLoader>() != null && PrefabUtility.IsPartOfPrefabInstance(root))
                        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    Transform levelRoot = root.transform.Find("LevelRoot");
                    if (levelRoot != null) levelRoot.SetParent(game.transform, false);
                    foreach (Type type in new[] { typeof(SaveService), typeof(CurrencyWallet), typeof(LevelManager), typeof(GameFlowController), typeof(LevelContentLoader) })
                    {
                        Component source = root.GetComponent(type);
                        if (source == null) continue;
                        Component destination = game.GetComponent(type);
                        if (destination == null) destination = game.AddComponent(type);
                        EditorUtility.CopySerialized(source, destination);
                        UnityEngine.Object.DestroyImmediate(source);
                    }
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                }
                Camera camera = root.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.orthographic = true;
                    camera.orthographicSize = 10.2f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.93f, 0.89f, 0.8f);
                    camera.transform.SetPositionAndRotation(
                        new Vector3(level.macroGridWidth * 0.5f, 18f, level.TotalHeight * 0.5f),
                        Quaternion.Euler(90f, 0f, 0f));
                }
                Light light = root.GetComponent<Light>();
                if (light != null)
                {
                    light.type = LightType.Directional;
                    light.intensity = 1.25f;
                    light.shadows = LightShadows.None;
                    light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                }
            }

            GameFlowController flow = game.GetComponent<GameFlowController>();
            if (flow != null)
            {
                var data = new SerializedObject(flow);
                data.FindProperty("startOnSceneLoad").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private static BoxVisualEntry Entry(BoxShapeDefinition shape, string prefab, Vector3 offset = default)
            => new BoxVisualEntry
            {
                shape = shape,
                prefab = Load<GameObject>(Art + "/Prefabs/" + prefab + ".prefab"),
                localOffset = offset + Vector3.up * 0.22f,
                fillOffset = Vector3.up * 0.22f,
                localScale = Vector3.one
            };

        private static BoxSpawnData Spawn(string id, BoxShapeDefinition shape, BallColorDefinition color, int x, int y)
            => new BoxSpawnData { id = id, shape = shape, color = color, startingMacroOrigin = V(x, y) };

        private static void AddArt(GameObject prefab, Transform parent, Vector3 position, string name = null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            if (!string.IsNullOrEmpty(name)) instance.name = name;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(propertyName).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    }
}
