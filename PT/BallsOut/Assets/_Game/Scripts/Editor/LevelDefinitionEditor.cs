using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BallsOut.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : UnityEditor.Editor
    {
        private enum ColorLayout { HorizontalBands, VerticalBands, DiagonalBands, Chevron }
        private ColorLayout colorLayout;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var level = (LevelDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Funnel: set Hopper Micro Rows to 0 to disable, or 6–8 for dense levels. " +
                "Paint the lower mask to change the outer frame. Click cells to cycle usable / blocked / outside. " +
                "After changing dimensions or boxes, rebuild the capacity-matched ball field.", MessageType.Info);
            DrawMask(level, level.lowerGridMask, level.lowerGridHeight, "Lower grid / outer frame", true);
            DrawMask(level, level.ballAreaMask, level.ballAreaMacroHeight, "Reservoir silhouette", false);
            colorLayout = (ColorLayout)EditorGUILayout.EnumPopup("Ball color layout", colorLayout);
            if (GUILayout.Button("Rebuild Balls To Match Box Capacity"))
                GenerateCapacityField(level, (int)colorLayout);
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

        private static void DrawMask(LevelDefinition level, CellMask mask, int height, string label, bool blocked)
        {
            if (mask == null || mask.overrides == null || level.macroGridWidth < 1 || height < 1 ||
                level.macroGridWidth > 16 || height > 20) return;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            Color oldColor = GUI.backgroundColor;
            for (int y = height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < level.macroGridWidth; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    CellKind kind = mask.Get(cell, level.macroGridWidth, height);
                    GUI.backgroundColor = kind == CellKind.Usable ? new Color(0.45f, 0.75f, 1f) :
                        kind == CellKind.Blocked ? new Color(1f, 0.5f, 0.4f) : Color.gray;
                    if (!GUILayout.Button(kind == CellKind.Usable ? "+" : kind == CellKind.Blocked ? "X" : "·", GUILayout.Width(22), GUILayout.Height(22))) continue;
                    Undo.RecordObject(level, "Paint level silhouette");
                    CellKind next = kind == CellKind.Outside ? CellKind.Usable :
                        kind == CellKind.Usable && blocked ? CellKind.Blocked : CellKind.Outside;
                    mask.overrides.RemoveAll(item => item.cell == cell);
                    if (next != mask.defaultKind) mask.overrides.Add(new CellOverride { cell = cell, kind = next });
                    EditorUtility.SetDirty(level);
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = oldColor;
        }

        internal static void GenerateCapacityField(LevelDefinition level, int pattern = 0)
        {
            if (level.macroGridWidth < 1 || level.lowerGridHeight < 1 || level.ballAreaMacroHeight < 1 ||
                level.ballAreaMask?.overrides == null || level.boxes == null || level.balls == null) return;
            var colors = new List<BallColorDefinition>();
            var counts = new List<int>();
            int total = 0;
            foreach (var box in level.boxes)
            {
                if (box?.color == null || box.shape == null) { Debug.LogError("Assign every box shape and color first.", level); return; }
                int capacity = level.BoxCapacity(box.shape);
                if (box.innerColor != null)
                {
                    Add(box.innerColor, capacity - box.initialFillCount);
                    Add(box.color, capacity);
                }
                else Add(box.color, capacity - box.initialFillCount);
            }
            void Add(BallColorDefinition color, int count)
            {
                int index = colors.IndexOf(color);
                if (index < 0) { index = colors.Count; colors.Add(color); counts.Add(0); }
                counts[index] += count;
                total += count;
            }
            var cells = new List<Vector2Int>();
            for (int y = level.lowerGridHeight * LevelDefinition.MicroResolution; y < level.TotalHeight * LevelDefinition.MicroResolution; y++)
                for (int x = 0; x < level.macroGridWidth * LevelDefinition.MicroResolution; x++)
                    if (level.IsBallMicroCell(new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
            if (total == 0 || total > cells.Count)
            { Debug.LogError($"Boxes need {total} balls; the reservoir has {cells.Count} usable positions. Resize it or change the boxes.", level); return; }
            cells.RemoveRange(total, cells.Count - total);
            cells.Sort((a, b) =>
            {
                float Key(Vector2Int cell) => pattern == 1 ? cell.x : pattern == 2 ? cell.y + cell.x * 0.65f :
                    pattern == 3 ? cell.y + Mathf.Abs(cell.x - level.macroGridWidth * 2f) * 0.8f : cell.y;
                int order = Key(a).CompareTo(Key(b));
                if (order == 0) order = a.y.CompareTo(b.y);
                return order != 0 ? order : a.x.CompareTo(b.x);
            });
            Undo.RecordObject(level, "Rebuild capacity-matched balls");
            level.balls.Clear();
            int cursor = 0;
            for (int i = 0; i < colors.Count; i++)
                for (int n = 0; n < counts[i]; n++)
                    level.balls.Add(new BallSpawnData { cell = cells[cursor++], color = colors[i] });
            EditorUtility.SetDirty(level);
        }

        internal static void GenerateDenseField(LevelDefinition level)
        {
            level.balls.Clear();
            for (int y = level.lowerGridHeight * LevelDefinition.MicroResolution; y < level.TotalHeight * LevelDefinition.MicroResolution; y++)
                for (int x = 0; x < level.macroGridWidth * LevelDefinition.MicroResolution; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!level.IsBallMicroCell(cell)) continue;
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
