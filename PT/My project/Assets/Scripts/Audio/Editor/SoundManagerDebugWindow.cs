using UnityEditor;
using UnityEngine;

public sealed class SoundManagerDebugWindow : EditorWindow
{
    private const double RepaintInterval = 0.25d;

    private SoundId selectedId = SoundId.CoinCollect;
    private double nextRepaintTime;
    private Vector2 scroll;

    [MenuItem("Tools/Audio/Sound Debug")]
    private static void Open()
    {
        GetWindow<SoundManagerDebugWindow>("Sound Debug");
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextRepaintTime)
            return;

        nextRepaintTime = now + RepaintInterval;
        Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect and preview the sound system.", MessageType.Info);
            return;
        }

        SoundManager manager = SoundManager.Instance;
        if (manager == null)
        {
            EditorGUILayout.HelpBox("SoundManager is not active.", MessageType.Warning);
            return;
        }

        SoundDebugSnapshot snapshot = manager.GetDebugSnapshot();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPool(snapshot);
        DrawCounters(snapshot);
        DrawInstances(manager, snapshot);
        DrawPreview(manager);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawPool(SoundDebugSnapshot snapshot)
    {
        EditorGUILayout.LabelField("Pool", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Total", snapshot.PoolTotal.ToString());
        EditorGUILayout.LabelField("Active", snapshot.PoolActive.ToString());
        EditorGUILayout.LabelField("Idle", snapshot.PoolIdle.ToString());
        EditorGUILayout.LabelField("Started This Frame", snapshot.StartedThisFrame.ToString());
        EditorGUILayout.LabelField("Active Loops", snapshot.ActiveLoops.ToString());
        EditorGUILayout.LabelField("Current Music", snapshot.CurrentMusic.ToString());
        EditorGUILayout.LabelField("Music Duck", snapshot.MusicDuckMultiplier.ToString("0.000"));
        EditorGUILayout.LabelField("SFX Duck", snapshot.SfxDuckMultiplier.ToString("0.000"));
        EditorGUILayout.Space();
    }

    private static void DrawCounters(SoundDebugSnapshot snapshot)
    {
        EditorGUILayout.LabelField("Cumulative Decisions", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Cooldown Skips", snapshot.CooldownSkipped.ToString());
        EditorGUILayout.LabelField("Instance Skips", snapshot.InstanceSkipped.ToString());
        EditorGUILayout.LabelField("Frame Skips", snapshot.FrameSkipped.ToString());
        EditorGUILayout.LabelField("Priority Skips", snapshot.PrioritySkipped.ToString());
        EditorGUILayout.LabelField("Missing Clip Skips", snapshot.MissingClipSkipped.ToString());
        EditorGUILayout.LabelField("Global Limit Skips", snapshot.GlobalLimitSkipped.ToString());
        EditorGUILayout.LabelField("Priority Steals", snapshot.PrioritySteals.ToString());
        EditorGUILayout.Space();
    }

    private static void DrawInstances(SoundManager manager, SoundDebugSnapshot snapshot)
    {
        EditorGUILayout.LabelField("Active Sound IDs", EditorStyles.boldLabel);
        int[] counts = snapshot.InstanceCounts;
        if (counts == null)
            return;

        bool any = false;
        int count = Mathf.Min(manager.DebugDefinitionCount, counts.Length);
        for (int i = 0; i < count; i++)
        {
            if (counts[i] <= 0)
                continue;

            SoundDefinition definition = manager.GetDebugDefinition(i);
            if (definition == null)
                continue;

            any = true;
            EditorGUILayout.LabelField(definition.Id.ToString(), counts[i].ToString());
        }

        if (!any)
            EditorGUILayout.LabelField("None");
        EditorGUILayout.Space();
    }

    private void DrawPreview(SoundManager manager)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        selectedId = (SoundId)EditorGUILayout.EnumPopup("Sound ID", selectedId);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Play 2D"))
            manager.PlaySfx(selectedId);
        if (GUILayout.Button("Play Music"))
            manager.PlayMusic(selectedId);
        if (GUILayout.Button("Stop Music"))
            manager.StopMusic();
        EditorGUILayout.EndHorizontal();
    }
}
