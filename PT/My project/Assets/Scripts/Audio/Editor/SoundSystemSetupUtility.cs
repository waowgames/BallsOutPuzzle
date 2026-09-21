using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class SoundSystemSetupUtility
{
    private const string MixerPath = "Assets/Audio/GameAudioMixer.mixer";
    private const string LibraryPath = "Assets/Resources/Audio/SoundLibrary.asset";
    private const string SettingsPrefabPath = "Assets/Prefabs/UI prefabs/UI Settings/Sound Manager.prefab";
    private const string SetupSessionKey = "SoundSystemSetupUtility.Completed";
    private const string ButtonClickClipPath = "Assets/SFX/sharpPop.mp3";
    private const string PassengerBoardingClipPath = "Assets/SFX/pop.mp3";
    private const string BarrierImpactClipPath = "Assets/SFX/car crash.mp3";

    private static readonly string[] ChildGroupNames = { "Music", "SFX", "UI", "Ambience" };

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticSetup()
    {
        if (Application.isBatchMode || SessionState.GetBool(SetupSessionKey, false))
            return;

        EditorApplication.delayCall -= RunAutomaticSetupIfRequired;
        EditorApplication.delayCall += RunAutomaticSetupIfRequired;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.delayCall -= RunAutomaticSetupIfRequired;
        EditorApplication.delayCall += RunAutomaticSetupIfRequired;
    }

    private static void RunAutomaticSetupIfRequired()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) == null
            || AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath) == null)
        {
            Run();
        }

        SessionState.SetBool(SetupSessionKey, true);
    }

    [MenuItem("Tools/Audio/Build Sound System Assets")]
    public static void Run()
    {
        EnsureFolders();
        AudioMixer mixer = EnsureMixer();
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceSynchronousImport);

        mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null)
            throw new InvalidOperationException("Audio mixer could not be loaded after creation.");

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(string.Empty);
        SoundLibrary library = EnsureLibrary();
        List<SoundDefinition> definitions = BuildDefaultDefinitions(library, groups);
        library.ConfigureForEditor(
            mixer,
            FindGroup(groups, "Master"),
            FindGroup(groups, "Music"),
            FindGroup(groups, "SFX"),
            FindGroup(groups, "UI"),
            FindGroup(groups, "Ambience"),
            definitions);

        CleanupSettingsPrefab();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Audio");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "Audio");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static AudioMixer EnsureMixer()
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        object controller;

        if (mixer == null)
        {
            Type controllerType = FindEditorType("UnityEditor.Audio.AudioMixerController");
            MethodInfo createMethod = FindMethod(controllerType, "CreateMixerControllerAtPath", true, 1);
            controller = createMethod.Invoke(null, new object[] { MixerPath });
            AssetDatabase.SaveAssets();
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
                mixer = controller as AudioMixer;
        }

        if (mixer == null)
            throw new InvalidOperationException("Unity did not create GameAudioMixer.mixer.");

        controller = mixer;
        PropertyInfo masterProperty = FindProperty(controller.GetType(), "masterGroup");
        object masterGroup = masterProperty.GetValue(controller, null);
        if (masterGroup == null)
            throw new InvalidOperationException("Audio mixer Master group is unavailable.");

        ExposeVolume(controller, masterGroup, "MasterVolume");
        for (int i = 0; i < ChildGroupNames.Length; i++)
        {
            string groupName = ChildGroupNames[i];
            object group = FindGroupObject(mixer, groupName);
            if (group == null)
            {
                MethodInfo createGroupMethod = FindMethod(controller.GetType(), "CreateNewGroup", false, -1);
                group = InvokeWithNameAndDefaults(createGroupMethod, controller, groupName);
                AttachChild(masterGroup, group);
            }

            if (groupName == "Music")
                ExposeVolume(controller, group, "MusicVolume");
            else if (groupName == "SFX")
                ExposeVolume(controller, group, "SfxVolume");
            else if (groupName == "UI")
                ExposeVolume(controller, group, "UiVolume");
        }

        EditorUtility.SetDirty(mixer);
        return mixer;
    }

    private static SoundLibrary EnsureLibrary()
    {
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library != null)
            return library;

        library = ScriptableObject.CreateInstance<SoundLibrary>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        return library;
    }

    private static List<SoundDefinition> BuildDefaultDefinitions(
        SoundLibrary library,
        AudioMixerGroup[] groups)
    {
        List<SoundDefinition> result = new List<SoundDefinition>(Mathf.Max(10, library.DefinitionCount));
        for (int i = 0; i < library.DefinitionCount; i++)
        {
            SoundDefinition existing = library.GetDefinition(i);
            if (existing != null)
                result.Add(existing);
        }

        EnsureDefinition(result, library, CreateCoin(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateBlockBreak(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateButtonClick(), FindGroup(groups, "UI"));
        EnsureDefinition(result, library, CreateExplosion(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateBooster(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateWin(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateFail(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateVehicleEngine(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateAmbience(), FindGroup(groups, "Ambience"));
        EnsureDefinition(result, library, CreateMainMusic(), FindGroup(groups, "Music"));
        EnsureDefinition(result, library, CreatePassengerBoarding(), FindGroup(groups, "SFX"));
        EnsureDefinition(result, library, CreateBarrierImpact(), FindGroup(groups, "SFX"));
        return result;
    }

    private static void EnsureDefinition(
        List<SoundDefinition> definitions,
        SoundLibrary library,
        SoundDefinition fallback,
        AudioMixerGroup group)
    {
        if (library.TryGetDefinitionIndex(fallback.Id, out int index))
        {
            SoundDefinition existing = library.GetDefinition(index);
            if (existing != null)
            {
                if (existing.MixerGroup == null)
                    existing.MixerGroup = group;
                if ((existing.Clips == null || existing.Clips.Length == 0)
                    && fallback.Clips != null
                    && fallback.Clips.Length > 0)
                {
                    existing.Clips = fallback.Clips;
                }
            }
            return;
        }

        fallback.MixerGroup = group;
        fallback.Normalize();
        definitions.Add(fallback);
    }

    private static SoundDefinition CreateCoin()
    {
        return new SoundDefinition
        {
            Id = SoundId.CoinCollect,
            Category = SoundCategory.Sfx,
            Cooldown = 0.04f,
            MaxSimultaneousInstances = 3,
            Priority = SoundPriority.Normal,
            OverlapMode = SoundOverlapMode.LimitInstances,
            PitchRange = new Vector2(0.96f, 1.06f),
            VolumeRange = new Vector2(0.96f, 1f),
            MergeSameFrameRequests = true,
            BatchVolumeIncrement = 0.03f,
            MaxBatchVolumeMultiplier = 1.15f,
            BatchPitchIncrement = 0.005f,
            MaxBatchPitchOffset = 0.06f
        };
    }

    private static SoundDefinition CreateBlockBreak()
    {
        return new SoundDefinition
        {
            Id = SoundId.BlockBreak,
            Cooldown = 0.03f,
            MaxSimultaneousInstances = 4,
            Priority = SoundPriority.Normal,
            OverlapMode = SoundOverlapMode.ReplaceOldest,
            PitchRange = new Vector2(0.96f, 1.04f),
            MergeSameFrameRequests = true
        };
    }

    private static SoundDefinition CreateButtonClick()
    {
        return new SoundDefinition
        {
            Id = SoundId.ButtonClick,
            Category = SoundCategory.Ui,
            Clips = LoadClip(ButtonClickClipPath),
            Cooldown = 0.08f,
            MaxSimultaneousInstances = 1,
            Priority = SoundPriority.High,
            OverlapMode = SoundOverlapMode.Restart,
            PitchRange = new Vector2(0.98f, 1.02f),
            MergeSameFrameRequests = true
        };
    }

    private static SoundDefinition CreateExplosion()
    {
        return new SoundDefinition
        {
            Id = SoundId.Explosion,
            Cooldown = 0.05f,
            MaxSimultaneousInstances = 3,
            Priority = SoundPriority.High,
            OverlapMode = SoundOverlapMode.LimitInstances,
            PitchRange = new Vector2(0.94f, 1.04f),
            Is3D = true,
            SpatialBlend = 1f,
            MinDistance = 1f,
            MaxDistance = 30f
        };
    }

    private static SoundDefinition CreateBooster()
    {
        return new SoundDefinition
        {
            Id = SoundId.Booster,
            Cooldown = 0.15f,
            MaxSimultaneousInstances = 2,
            Priority = SoundPriority.High,
            OverlapMode = SoundOverlapMode.ReplaceOldest,
            PitchRange = new Vector2(0.97f, 1.03f),
            UseDucking = true,
            DuckVolume = 0.65f,
            DuckHoldDuration = 0.25f
        };
    }

    private static SoundDefinition CreateWin()
    {
        return CreateCritical(SoundId.Win);
    }

    private static SoundDefinition CreateFail()
    {
        return CreateCritical(SoundId.Fail);
    }

    private static SoundDefinition CreateCritical(SoundId id)
    {
        return new SoundDefinition
        {
            Id = id,
            Cooldown = 1f,
            MaxSimultaneousInstances = 1,
            Priority = SoundPriority.Critical,
            OverlapMode = SoundOverlapMode.Restart,
            UseDucking = true,
            DuckVolume = 0.4f,
            DuckFadeInDuration = 0.08f,
            DuckHoldDuration = 0.6f,
            DuckFadeOutDuration = 0.3f,
            DuckTargets = DuckTarget.Music | DuckTarget.NonCriticalSfx
        };
    }

    private static SoundDefinition CreateVehicleEngine()
    {
        return new SoundDefinition
        {
            Id = SoundId.VehicleEngine,
            MaxSimultaneousInstances = 4,
            Priority = SoundPriority.Normal,
            OverlapMode = SoundOverlapMode.LimitInstances,
            PitchRange = new Vector2(0.96f, 1.04f),
            Is3D = true,
            SpatialBlend = 1f,
            MinDistance = 1f,
            MaxDistance = 25f,
            Loop = true,
            MergeSameFrameRequests = false
        };
    }

    private static SoundDefinition CreateAmbience()
    {
        return new SoundDefinition
        {
            Id = SoundId.Ambience,
            Category = SoundCategory.Ambience,
            MaxSimultaneousInstances = 2,
            Priority = SoundPriority.Low,
            OverlapMode = SoundOverlapMode.IgnoreIfPlaying,
            Loop = true
        };
    }

    private static SoundDefinition CreateMainMusic()
    {
        return new SoundDefinition
        {
            Id = SoundId.MainMusic,
            Category = SoundCategory.Music,
            MaxSimultaneousInstances = 1,
            Priority = SoundPriority.Normal,
            OverlapMode = SoundOverlapMode.IgnoreIfPlaying,
            Loop = true
        };
    }

    private static SoundDefinition CreatePassengerBoarding()
    {
        return new SoundDefinition
        {
            Id = SoundId.PassengerBoarding,
            Category = SoundCategory.Sfx,
            Clips = LoadClip(PassengerBoardingClipPath),
            Cooldown = 0f,
            MaxSimultaneousInstances = 1,
            Priority = SoundPriority.Normal,
            OverlapMode = SoundOverlapMode.IgnoreIfPlaying,
            VolumeRange = new Vector2(0.9f, 1f),
            PitchRange = new Vector2(1.12f, 1.18f),
            MergeSameFrameRequests = true
        };
    }

    private static SoundDefinition CreateBarrierImpact()
    {
        return new SoundDefinition
        {
            Id = SoundId.BarrierImpact,
            Category = SoundCategory.Sfx,
            Clips = LoadClip(BarrierImpactClipPath),
            Cooldown = 0.12f,
            MaxSimultaneousInstances = 1,
            Priority = SoundPriority.High,
            OverlapMode = SoundOverlapMode.Restart,
            PitchRange = new Vector2(0.96f, 1.04f),
            Is3D = true,
            SpatialBlend = 1f,
            MinDistance = 1f,
            MaxDistance = 25f,
            MergeSameFrameRequests = true
        };
    }

    private static AudioClip[] LoadClip(string assetPath)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        return clip != null ? new[] { clip } : Array.Empty<AudioClip>();
    }

    private static void CleanupSettingsPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
        try
        {
            if (root.GetComponentInChildren<AudioSettingsView>(true) == null)
                throw new InvalidOperationException("AudioSettingsView is missing from the settings prefab.");

            AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
                UnityEngine.Object.DestroyImmediate(sources[i]);

            PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AudioMixerGroup FindGroup(AudioMixerGroup[] groups, string name)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == name)
                return groups[i];
        }

        throw new InvalidOperationException("Audio mixer group is missing: " + name);
    }

    private static object FindGroupObject(AudioMixer mixer, string name)
    {
        AudioMixerGroup[] groups = mixer.FindMatchingGroups(string.Empty);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == name)
                return groups[i];
        }

        return null;
    }

    private static Type FindEditorType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
                return type;
        }

        throw new InvalidOperationException("Unity editor type is unavailable: " + fullName);
    }

    private static MethodInfo FindMethod(Type type, string name, bool isStatic, int parameterCount)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
        MethodInfo[] methods = type.GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != name)
                continue;
            if (parameterCount >= 0 && method.GetParameters().Length != parameterCount)
                continue;
            return method;
        }

        throw new InvalidOperationException("Unity editor method is unavailable: " + type.FullName + "." + name);
    }

    private static PropertyInfo FindProperty(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property == null)
            throw new InvalidOperationException("Unity editor property is unavailable: " + type.FullName + "." + name);
        return property;
    }

    private static object InvokeWithNameAndDefaults(MethodInfo method, object instance, string name)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object[] arguments = new object[parameters.Length];
        bool nameAssigned = false;
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (!nameAssigned && parameterType == typeof(string))
            {
                arguments[i] = name;
                nameAssigned = true;
            }
            else if (parameters[i].HasDefaultValue)
            {
                arguments[i] = parameters[i].DefaultValue;
            }
            else
            {
                arguments[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
            }
        }

        return method.Invoke(instance, arguments);
    }

    private static void AttachChild(object parentGroup, object childGroup)
    {
        PropertyInfo childrenProperty = FindProperty(parentGroup.GetType(), "children");
        Array oldChildren = childrenProperty.GetValue(parentGroup, null) as Array;
        Type elementType = childrenProperty.PropertyType.GetElementType();
        int oldLength = oldChildren != null ? oldChildren.Length : 0;
        Array newChildren = Array.CreateInstance(elementType, oldLength + 1);
        if (oldChildren != null)
            Array.Copy(oldChildren, newChildren, oldLength);
        newChildren.SetValue(childGroup, oldLength);
        childrenProperty.SetValue(parentGroup, newChildren, null);
    }

    private static void ExposeVolume(object controller, object group, string exposedName)
    {
        MethodInfo guidMethod = FindMethod(group.GetType(), "GetGUIDForVolume", false, 0);
        object guid = guidMethod.Invoke(group, null);
        if (!HasExposedParameter(controller, guid))
        {
            MethodInfo addMethod = FindMethod(controller.GetType(), "AddExposedParameter", false, 1);
            object parameterPath = CreateGroupParameterPath(group, guid);
            addMethod.Invoke(controller, new[] { parameterPath });
        }

        RenameExposedParameter(controller, guid, exposedName);
    }

    private static object CreateGroupParameterPath(object group, object guid)
    {
        Type groupPathType = FindEditorType("UnityEditor.Audio.AudioGroupParameterPath");
        ConstructorInfo[] constructors = groupPathType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < constructors.Length; i++)
        {
            ParameterInfo[] parameters = constructors[i].GetParameters();
            if (parameters.Length != 2
                || !parameters[0].ParameterType.IsInstanceOfType(group)
                || !parameters[1].ParameterType.IsInstanceOfType(guid))
            {
                continue;
            }

            return constructors[i].Invoke(new[] { group, guid });
        }

        throw new InvalidOperationException(
            "Unity AudioGroupParameterPath(AudioMixerGroupController, GUID) is unavailable.");
    }

    private static bool HasExposedParameter(object controller, object guid)
    {
        PropertyInfo property = FindProperty(controller.GetType(), "exposedParameters");
        Array values = property.GetValue(controller, null) as Array;
        if (values == null)
            return false;

        for (int i = 0; i < values.Length; i++)
        {
            object boxed = values.GetValue(i);
            FieldInfo guidField = boxed.GetType().GetField(
                "guid",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (guidField != null && Equals(guidField.GetValue(boxed), guid))
                return true;
        }

        return false;
    }

    private static void RenameExposedParameter(object controller, object guid, string exposedName)
    {
        PropertyInfo property = FindProperty(controller.GetType(), "exposedParameters");
        Array values = property.GetValue(controller, null) as Array;
        if (values == null)
            return;

        for (int i = 0; i < values.Length; i++)
        {
            object boxed = values.GetValue(i);
            Type elementType = boxed.GetType();
            FieldInfo guidField = elementType.GetField(
                "guid",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo nameField = elementType.GetField(
                "name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (guidField == null || nameField == null)
                continue;
            if (!Equals(guidField.GetValue(boxed), guid))
                continue;

            nameField.SetValue(boxed, exposedName);
            values.SetValue(boxed, i);
            property.SetValue(controller, values, null);
            return;
        }
    }
}
