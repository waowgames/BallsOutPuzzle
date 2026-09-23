using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BallsOut.Editor
{
    public static class Phase10SampleBuilder
    {
        private const string Root = "Assets/_Game/Samples";
        private const string Prototype = "Assets/_Game/Prototype/";

        [MenuItem("Tools/Balls Out/Create Phase 10 Sample Levels")]
        public static void Create()
        {
            if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                Debug.LogError("Save the current untitled scene before creating sample scenes.");
                return;
            }
            Phase7PrototypeBuilder.Create();
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            BallColorDefinition red = Load<BallColorDefinition>("Coral.asset");
            BallColorDefinition blue = Load<BallColorDefinition>("Blue.asset");
            BoxShapeDefinition single = Load<BoxShapeDefinition>("Single.asset");
            BoxShapeDefinition domino = Load<BoxShapeDefinition>("Domino.asset");
            BoxShapeDefinition elbow = Load<BoxShapeDefinition>("L.asset");
            var green = AssetDatabase.LoadAssetAtPath<BallColorDefinition>(Root + "/Green.asset");
            if (green == null)
            {
                green = ScriptableObject.CreateInstance<BallColorDefinition>();
                green.id = "green";
                green.displayColor = new Color(0.12f, 0.8f, 0.35f);
                var material = new Material(red.ballMaterial) { color = green.displayColor, enableInstancing = true };
                AssetDatabase.CreateAsset(material, Root + "/Green.mat");
                green.ballMaterial = green.boxMaterial = material;
                AssetDatabase.CreateAsset(green, Root + "/Green.asset");
            }

            var a = NewLevel(7, 5, red, blue);
            for (int x = 0; x < 7; x++)
                a.boxes.Add(Spawn("Single-" + x, single, x % 2 == 0 ? red : blue, x, x == 0 ? 4 : 1));
            LevelDefinitionEditor.GenerateDenseField(a);
            Save("A_LooseSingles", a);

            var b = NewLevel(6, 5, red, blue, green);
            b.boxes.Add(Spawn("Coral-L", elbow, red, 0, 3));
            b.boxes.Add(Spawn("Blue-Domino", domino, blue, 3, 3));
            b.boxes.Add(Spawn("Green-Single", single, green, 5, 4));
            // Contiguous mixed-color clusters with exact 144/96/48 quotas.
            for (int i = 0; i < 288; i++)
                b.balls.Add(new BallSpawnData { cell = new Vector2Int(i % 24, b.lowerGridHeight * LevelDefinition.MicroResolution + i / 24), color = i < 144 ? red : i < 240 ? blue : green });
            Save("B_ThreeColorsShapes", b);

            var c = NewLevel(7, 3, red, blue);
            c.lowerGridMask.overrides.Add(new CellOverride { cell = new Vector2Int(0, 0), kind = CellKind.Outside });
            c.lowerGridMask.overrides.Add(new CellOverride { cell = new Vector2Int(6, 0), kind = CellKind.Outside });
            c.lowerGridMask.overrides.Add(new CellOverride { cell = new Vector2Int(2, 0), kind = CellKind.Blocked });
            c.lowerGridMask.overrides.Add(new CellOverride { cell = new Vector2Int(4, 0), kind = CellKind.Blocked });
            for (int x = 0; x < 7; x++)
                c.boxes.Add(Spawn("Single-" + x, single, x % 2 == 0 ? red : blue, x, 1));
            LevelDefinitionEditor.GenerateDenseField(c);
            Save("C_BlockedLowerGrid", c);

            var d = NewLevel(8, 4, red, blue);
            for (int i = 0; i < 4; i++)
                d.boxes.Add(Spawn("Domino-" + i, domino, i % 2 == 0 ? red : blue, i * 2, i % 2));
            LevelDefinitionEditor.GenerateDenseField(d);
            Save("D_SharedColorBoxes", d);
            AssetDatabase.SaveAssets();
            LevelDefinitionEditor.ValidateAllLevels();
            // Scene changes can unload asset references held by local variables.
            // Save all definitions first, then load each scene's inputs afresh.
            string[] names = { "A_LooseSingles", "B_ThreeColorsShapes", "C_BlockedLowerGrid", "D_SharedColorBoxes" };
            foreach (string name in names)
            {
                string folder = Root + "/" + name;
                string scenePath = folder + "/" + name + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) continue;
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/Level.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/LevelRuntime.prefab");
                Phase7PrototypeBuilder.BuildScene(level, prefab, scenePath);
            }
            Debug.Log("[Balls Out] Phase 10 sample levels/scenes A–D are ready in " + Root);
        }

        private static LevelDefinition NewLevel(int width, int lowerHeight, params BallColorDefinition[] colors)
        {
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.macroGridWidth = width;
            level.lowerGridHeight = lowerHeight;
            level.ballAreaMacroHeight = 3;
            level.palette.AddRange(colors);
            return level;
        }

        private static BoxSpawnData Spawn(string id, BoxShapeDefinition shape, BallColorDefinition color, int x, int y)
            => new BoxSpawnData { id = id, shape = shape, color = color, startingMacroOrigin = new Vector2Int(x, y) };

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(Prototype + name);
            if (asset == null) throw new InvalidOperationException("Missing prototype asset: " + name);
            return asset;
        }

        private static void Save(string name, LevelDefinition generated)
        {
            string folder = Root + "/" + name;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/Level.asset");
            if (level == null)
            {
                level = generated;
                AssetDatabase.CreateAsset(level, folder + "/Level.asset");
            }
            else UnityEngine.Object.DestroyImmediate(generated);
            var errors = new List<string>();
            LevelValidator.Validate(level, errors);
            if (errors.Count != 0) throw new InvalidOperationException(name + ": " + string.Join("\n", errors));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/LevelRuntime.prefab");
            if (prefab == null)
            {
                GameObject instance = PrefabUtility.LoadPrefabContents(Prototype + "LevelRuntime.prefab");
                try
                {
                    var data = new SerializedObject(instance.GetComponent<BallBoxLevelRuntime>());
                    data.FindProperty("level").objectReferenceValue = level;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/LevelRuntime.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(instance); }
                var levelData = new SerializedObject(level);
                levelData.FindProperty("levelPrefab").objectReferenceValue = prefab;
                levelData.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
