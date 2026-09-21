using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundLibrary))]
public sealed class SoundLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        SoundLibrary library = (SoundLibrary)target;
        DrawValidation(library);
    }

    private static void DrawValidation(SoundLibrary library)
    {
        if (library.Mixer == null)
            EditorGUILayout.HelpBox("GameAudioMixer is not assigned.", MessageType.Warning);
        if (library.MasterGroup == null
            || library.MusicGroup == null
            || library.SfxGroup == null
            || library.UiGroup == null
            || library.AmbienceGroup == null)
        {
            EditorGUILayout.HelpBox("One or more mixer groups are not assigned.", MessageType.Warning);
        }

        HashSet<SoundId> ids = new HashSet<SoundId>();
        int emptyClipDefinitions = 0;
        for (int i = 0; i < library.DefinitionCount; i++)
        {
            SoundDefinition definition = library.GetDefinition(i);
            if (definition == null)
            {
                EditorGUILayout.HelpBox("Definition " + i + " is null.", MessageType.Warning);
                continue;
            }

            if (definition.Id == SoundId.None)
                EditorGUILayout.HelpBox("Definition " + i + " uses SoundId.None.", MessageType.Warning);
            else if (!ids.Add(definition.Id))
                EditorGUILayout.HelpBox("Duplicate SoundId: " + definition.Id, MessageType.Error);

            if (!HasUsableClip(definition.Clips))
                emptyClipDefinitions++;

            if (definition.Category == SoundCategory.Music && !definition.Loop)
            {
                EditorGUILayout.HelpBox(
                    definition.Id + " is Music but Loop is disabled.",
                    MessageType.Info);
            }

            if (definition.Category == SoundCategory.Ui && definition.Loop)
            {
                EditorGUILayout.HelpBox(
                    definition.Id + " is a looping UI sound.",
                    MessageType.Warning);
            }
        }

        if (emptyClipDefinitions > 0)
        {
            EditorGUILayout.HelpBox(
                emptyClipDefinitions + " sound definitions have no assigned clip. They will skip safely at runtime.",
                MessageType.Info);
        }
    }

    private static bool HasUsableClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }
}
