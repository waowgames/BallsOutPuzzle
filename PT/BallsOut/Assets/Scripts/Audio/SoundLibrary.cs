using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio/Sound Library", fileName = "SoundLibrary")]
public sealed class SoundLibrary : ScriptableObject
{
    [Header("Pool Limits")]
    [SerializeField, Min(1)] private int initialPoolSize = 12;
    [SerializeField, Min(1)] private int maxPoolSize = 24;
    [SerializeField, Min(1)] private int maxTotalSimultaneousSfx = 16;
    [SerializeField, Min(1)] private int maxSoundsStartedPerFrame = 6;
    [SerializeField] private bool allowPoolGrowth = true;

    [Header("Music")]
    [SerializeField, Min(0f)] private float defaultMusicFadeDuration = 0.35f;

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup masterGroup;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup uiGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string musicVolumeParameter = "MusicVolume";
    [SerializeField] private string sfxVolumeParameter = "SfxVolume";
    [SerializeField] private string uiVolumeParameter = "UiVolume";

    [Header("Diagnostics")]
    [SerializeField] private bool debugLogging;

    [Header("Definitions")]
    [SerializeField] private List<SoundDefinition> sounds = new List<SoundDefinition>();

    [System.NonSerialized] private Dictionary<SoundId, int> indexById;
    [System.NonSerialized] private int duplicateIdCount;

    public int InitialPoolSize => initialPoolSize;
    public int MaxPoolSize => maxPoolSize;
    public int MaxTotalSimultaneousSfx => maxTotalSimultaneousSfx;
    public int MaxSoundsStartedPerFrame => maxSoundsStartedPerFrame;
    public bool AllowPoolGrowth => allowPoolGrowth;
    public float DefaultMusicFadeDuration => defaultMusicFadeDuration;
    public AudioMixer Mixer => mixer;
    public AudioMixerGroup MasterGroup => masterGroup;
    public AudioMixerGroup MusicGroup => musicGroup;
    public AudioMixerGroup SfxGroup => sfxGroup;
    public AudioMixerGroup UiGroup => uiGroup;
    public AudioMixerGroup AmbienceGroup => ambienceGroup;
    public string MasterVolumeParameter => masterVolumeParameter;
    public string MusicVolumeParameter => musicVolumeParameter;
    public string SfxVolumeParameter => sfxVolumeParameter;
    public string UiVolumeParameter => uiVolumeParameter;
    public bool DebugLogging => debugLogging;
    public int DefinitionCount => sounds != null ? sounds.Count : 0;
    public int DuplicateIdCount => duplicateIdCount;

    private void OnEnable()
    {
        NormalizeValues();
        BuildCache();
    }

    public void BuildCache()
    {
        if (sounds == null)
            sounds = new List<SoundDefinition>();

        if (indexById == null)
            indexById = new Dictionary<SoundId, int>(sounds.Count);
        else
            indexById.Clear();

        duplicateIdCount = 0;
        for (int i = 0; i < sounds.Count; i++)
        {
            SoundDefinition definition = sounds[i];
            if (definition == null)
                continue;

            definition.Normalize();
            if (definition.Id == SoundId.None)
                continue;

            if (indexById.ContainsKey(definition.Id))
            {
                duplicateIdCount++;
                continue;
            }

            indexById.Add(definition.Id, i);
        }
    }

    public bool TryGetDefinitionIndex(SoundId id, out int index)
    {
        if (indexById == null)
            BuildCache();

        if (id == SoundId.None)
        {
            index = -1;
            return false;
        }

        return indexById.TryGetValue(id, out index);
    }

    public SoundDefinition GetDefinition(int index)
    {
        if (sounds == null || index < 0 || index >= sounds.Count)
            return null;

        return sounds[index];
    }

    public AudioMixerGroup GetMixerGroup(SoundCategory category)
    {
        switch (category)
        {
            case SoundCategory.Music:
                return musicGroup;
            case SoundCategory.Ui:
                return uiGroup;
            case SoundCategory.Ambience:
                return ambienceGroup;
            default:
                return sfxGroup;
        }
    }

    private void NormalizeValues()
    {
        maxPoolSize = Mathf.Max(1, maxPoolSize);
        initialPoolSize = Mathf.Clamp(initialPoolSize, 1, maxPoolSize);
        maxTotalSimultaneousSfx = Mathf.Clamp(maxTotalSimultaneousSfx, 1, maxPoolSize);
        maxSoundsStartedPerFrame = Mathf.Max(1, maxSoundsStartedPerFrame);
        defaultMusicFadeDuration = Mathf.Max(0f, defaultMusicFadeDuration);

        if (sounds == null)
            sounds = new List<SoundDefinition>();

        for (int i = 0; i < sounds.Count; i++)
        {
            if (sounds[i] != null)
                sounds[i].Normalize();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeValues();
        BuildCache();
    }

    public void ConfigureForEditor(
        AudioMixer value,
        AudioMixerGroup master,
        AudioMixerGroup music,
        AudioMixerGroup sfx,
        AudioMixerGroup ui,
        AudioMixerGroup ambience,
        List<SoundDefinition> definitions)
    {
        mixer = value;
        masterGroup = master;
        musicGroup = music;
        sfxGroup = sfx;
        uiGroup = ui;
        ambienceGroup = ambience;
        sounds = definitions ?? new List<SoundDefinition>();
        NormalizeValues();
        BuildCache();
    }
#endif
}
