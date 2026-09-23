using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BallsOut.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var level = (LevelDefinition)target;
            if (GUILayout.Button("Validate Level (Including Color Capacity)"))
            {
                var errors = new List<string>();
                LevelValidator.Validate(level, errors);
                if (errors.Count == 0) Debug.Log("[Balls Out] Level data is valid; ball counts exactly match capacity for every color. This does not prove puzzle solvability.", level);
                else Debug.LogError(string.Join("\n", errors), level);
            }
            EditorGUILayout.HelpBox("Generate fills every usable ball-area micro cell with deterministic macro-column color stripes. Replaces the balls list; supports Undo.", MessageType.Info);
            if (GUILayout.Button("Generate Dense Ball Field From Palette"))
            {
                if (level.palette == null || level.palette.Count == 0 || level.palette.Contains(null) ||
                    level.macroGridWidth < 1 || level.lowerGridHeight < 1 || level.ballAreaMacroHeight < 1 || level.ballAreaMask == null ||
                    level.ballAreaMask.overrides == null || (long)level.macroGridWidth * ((long)level.lowerGridHeight + level.ballAreaMacroHeight) * LevelDefinition.MicroResolution * LevelDefinition.MicroResolution > int.MaxValue)
                {
                    Debug.LogError("Set positive dimensions, a ball mask, and a palette containing assigned colors.", level);
                    return;
                }
                Undo.RecordObject(level, "Generate dense ball field");
                GenerateDenseField(level);
                EditorUtility.SetDirty(level);
            }
        }

        internal static void GenerateDenseField(LevelDefinition level)
        {
            level.balls.Clear();
            for (int y = level.lowerGridHeight * LevelDefinition.MicroResolution; y < level.TotalHeight * LevelDefinition.MicroResolution; y++)
                for (int x = 0; x < level.macroGridWidth * LevelDefinition.MicroResolution; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (level.GetCell(BallMicroGrid.ToMacro(cell)) != CellKind.Usable) continue;
                    level.balls.Add(new BallSpawnData { cell = cell, color = level.palette[(x / LevelDefinition.MicroResolution) % level.palette.Count] });
                }
        }

        [MenuItem("Tools/Balls Out/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");
            var errors = new List<string>();
            int invalid = 0;
            foreach (string guid in guids)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                errors.Clear();
                LevelValidator.Validate(level, errors);
                if (errors.Count == 0) continue;
                invalid++;
                Debug.LogError($"[Balls Out] {level.name}:\n" + string.Join("\n", errors), level);
            }
            if (invalid != 0 && Application.isBatchMode)
                throw new System.InvalidOperationException($"{invalid} of {guids.Length} levels are invalid.");
            Debug.Log($"[Balls Out] Validated {guids.Length} levels; invalid: {invalid}. Solvability is not checked.");
        }
    }
}
