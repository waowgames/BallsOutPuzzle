using UnityEngine;
using UnityEngine.Audio;

internal sealed class AudioSettingsController
{
    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string UiVolumeKey = "Audio.UiVolume";
    private const string LegacyMusicEnabledKey = "BgMusicOn";
    private const string LegacySfxEnabledKey = "SfxOn";
    private const string VibrationEnabledKey = "VibrationOn";

    private SoundLibrary library;
    private AudioMixer mixer;
    private string masterParameter;
    private string musicParameter;
    private string sfxParameter;
    private string uiParameter;
    private float masterVolume;
    private float musicVolume;
    private float sfxVolume;
    private float uiVolume;
    private float lastMusicVolume = 1f;
    private float lastSfxVolume = 1f;
    private bool vibrationEnabled;
    private bool mixerApplied;
    private bool masterParameterAvailable;
    private bool musicParameterAvailable;
    private bool sfxParameterAvailable;
    private bool uiParameterAvailable;

    internal float MasterVolume => masterVolume;
    internal float MusicVolume => musicVolume;
    internal float SfxVolume => sfxVolume;
    internal float UiVolume => uiVolume;
    internal bool IsMusicEnabled => musicVolume > 0.0001f;
    internal bool IsSfxEnabled => sfxVolume > 0.0001f;
    internal bool IsVibrationEnabled => vibrationEnabled;

    internal void Initialize(SoundLibrary soundLibrary)
    {
        library = soundLibrary;
        mixer = library != null ? library.Mixer : null;
        masterParameter = library != null ? library.MasterVolumeParameter : "MasterVolume";
        musicParameter = library != null ? library.MusicVolumeParameter : "MusicVolume";
        sfxParameter = library != null ? library.SfxVolumeParameter : "SfxVolume";
        uiParameter = library != null ? library.UiVolumeParameter : "UiVolume";

        masterVolume = ReadNormalized(MasterVolumeKey, 1f);
        musicVolume = ReadNormalized(
            MusicVolumeKey,
            PlayerPrefs.GetInt(LegacyMusicEnabledKey, 1) == 1 ? 1f : 0f);
        sfxVolume = ReadNormalized(
            SfxVolumeKey,
            PlayerPrefs.GetInt(LegacySfxEnabledKey, 1) == 1 ? 1f : 0f);
        uiVolume = ReadNormalized(UiVolumeKey, 1f);
        vibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, 1) == 1;

        if (musicVolume > 0.0001f)
            lastMusicVolume = musicVolume;
        if (sfxVolume > 0.0001f)
            lastSfxVolume = sfxVolume;

        masterParameterAvailable = mixer != null;
        musicParameterAvailable = mixer != null;
        sfxParameterAvailable = mixer != null;
        uiParameterAvailable = mixer != null;
    }

    internal static float NormalizedToDecibels(float value)
    {
        return value <= 0f ? -80f : Mathf.Clamp(20f * Mathf.Log10(value), -80f, 0f);
    }

    internal void ApplyMixerVolumes()
    {
        mixerApplied = true;
        if (mixer == null)
            return;

        ApplyParameter(ref masterParameterAvailable, masterParameter, masterVolume);
        ApplyParameter(ref musicParameterAvailable, musicParameter, musicVolume);
        ApplyParameter(ref sfxParameterAvailable, sfxParameter, sfxVolume);
        ApplyParameter(ref uiParameterAvailable, uiParameter, uiVolume);
    }

    internal void SetMasterVolume(float normalizedVolume)
    {
        masterVolume = Mathf.Clamp01(normalizedVolume);
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        ApplyIfReady(ref masterParameterAvailable, masterParameter, masterVolume);
    }

    internal void SetMusicVolume(float normalizedVolume)
    {
        musicVolume = Mathf.Clamp01(normalizedVolume);
        if (musicVolume > 0.0001f)
            lastMusicVolume = musicVolume;
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.SetInt(LegacyMusicEnabledKey, IsMusicEnabled ? 1 : 0);
        ApplyIfReady(ref musicParameterAvailable, musicParameter, musicVolume);
    }

    internal void SetSfxVolume(float normalizedVolume)
    {
        sfxVolume = Mathf.Clamp01(normalizedVolume);
        if (sfxVolume > 0.0001f)
            lastSfxVolume = sfxVolume;
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.SetInt(LegacySfxEnabledKey, IsSfxEnabled ? 1 : 0);
        ApplyIfReady(ref sfxParameterAvailable, sfxParameter, sfxVolume);
    }

    internal void SetUiVolume(float normalizedVolume)
    {
        uiVolume = Mathf.Clamp01(normalizedVolume);
        PlayerPrefs.SetFloat(UiVolumeKey, uiVolume);
        ApplyIfReady(ref uiParameterAvailable, uiParameter, uiVolume);
    }

    internal void ToggleMusic(bool enabled)
    {
        SetMusicVolume(enabled ? Mathf.Max(0.0001f, lastMusicVolume) : 0f);
    }

    internal void ToggleSfx(bool enabled)
    {
        SetSfxVolume(enabled ? Mathf.Max(0.0001f, lastSfxVolume) : 0f);
    }

    internal void ToggleVibration(bool enabled)
    {
        vibrationEnabled = enabled;
        PlayerPrefs.SetInt(VibrationEnabledKey, enabled ? 1 : 0);
    }

    internal float GetSourceMultiplier(SoundCategory category)
    {
        bool routed = library != null && mixer != null && library.GetMixerGroup(category) != null;
        float masterMultiplier = routed && masterParameterAvailable ? 1f : masterVolume;
        float categoryMultiplier;

        switch (category)
        {
            case SoundCategory.Music:
                categoryMultiplier = routed && musicParameterAvailable ? 1f : musicVolume;
                break;
            case SoundCategory.Ui:
                categoryMultiplier = routed && uiParameterAvailable ? 1f : uiVolume;
                break;
            case SoundCategory.Ambience:
                categoryMultiplier = sfxVolume;
                break;
            default:
                categoryMultiplier = routed && sfxParameterAvailable ? 1f : sfxVolume;
                break;
        }

        return masterMultiplier * categoryMultiplier;
    }

    internal void Save()
    {
        PlayerPrefs.Save();
    }

    private static float ReadNormalized(string key, float fallback)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));
    }

    private void ApplyIfReady(ref bool available, string parameter, float value)
    {
        if (mixerApplied)
            ApplyParameter(ref available, parameter, value);
    }

    private void ApplyParameter(ref bool available, string parameter, float value)
    {
        if (!available || mixer == null || string.IsNullOrEmpty(parameter))
        {
            available = false;
            return;
        }

        if (!mixer.SetFloat(parameter, NormalizedToDecibels(value)))
            available = false;
    }
}
