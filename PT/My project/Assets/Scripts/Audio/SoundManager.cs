using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public sealed class SoundManager : MonoBehaviour
{
    private const string RootName = "[SoundManager]";

    private static bool applicationQuitting;

    private SoundLibrary library;
    private bool ownsRuntimeLibrary;
    private bool initialized;
    private int ownerId;
    private AudioSourcePool pool;
    private AudioSettingsController settings;
    private AudioDuckingController ducking;
    private SfxPlayer sfxPlayer;
    private MusicPlayer musicPlayer;
    private Transform listenerTransform;
    private int[] lastMusicClipIndices;
    private float[] lastMusicStartTimes;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int[] debugInstanceCounts;
#endif

    public static SoundManager Instance { get; private set; }
    public bool IsMusicEnabled => settings != null && settings.IsMusicEnabled;
    public bool IsSfxEnabled => settings != null && settings.IsSfxEnabled;
    public bool IsVibrationEnabled => settings != null && settings.IsVibrationEnabled;

    internal static void ResetStatics()
    {
        Instance = null;
        applicationQuitting = false;
    }

    internal static SoundManager CreatePersistent(SoundLibrary soundLibrary)
    {
        if (applicationQuitting)
            return null;

        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        SoundManager manager = root.AddComponent<SoundManager>();
        manager.Initialize(soundLibrary);
        return manager;
    }

    private void Awake()
    {
        if (applicationQuitting)
        {
            Destroy(gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ownerId = GetInstanceID();
    }

    private void Start()
    {
        if (initialized)
            settings.ApplyMixerVolumes();
    }

    private void Update()
    {
        if (!initialized)
            return;

        float now = Time.unscaledTime;
        float delta = Time.unscaledDeltaTime;
        ducking.Update(now, delta);
        sfxPlayer.Update(now, delta, Time.frameCount);
        musicPlayer.Update(
            delta,
            settings.GetSourceMultiplier(SoundCategory.Music),
            ducking.MusicMultiplier);
    }

    private void LateUpdate()
    {
        if (initialized)
            sfxPlayer.FlushPending(Time.frameCount, Time.unscaledTime);
    }

    public void PlaySfx(SoundId id)
    {
        if (initialized)
            sfxPlayer.Enqueue(id);
    }

    public SoundHandle PlayTrackedSfx(SoundId id)
    {
        if (!initialized)
            return default;

        return sfxPlayer.PlayOneShot(
            id,
            ownerId,
            this,
            Time.unscaledTime,
            Time.frameCount);
    }

    public void PlaySfx(SoundId id, Vector3 worldPosition)
    {
        if (initialized)
            sfxPlayer.Enqueue(id, worldPosition);
    }

    public SoundHandle PlayLoop(SoundId id, Transform followTarget = null)
    {
        if (!initialized)
            return default;

        return sfxPlayer.PlayLoop(
            id,
            followTarget,
            ownerId,
            this,
            Time.unscaledTime,
            Time.frameCount);
    }

    public void Stop(SoundHandle handle, float fadeDuration = 0f)
    {
        if (initialized)
            sfxPlayer.Stop(handle, fadeDuration, ownerId);
    }

    public void StopSfx(SoundId id, float fadeDuration = 0f)
    {
        if (initialized)
            sfxPlayer.StopAll(id, fadeDuration);
    }

    public void PlayMusic(SoundId id, float fadeDuration = -1f)
    {
        if (!initialized || !library.TryGetDefinitionIndex(id, out int definitionIndex))
            return;

        if (musicPlayer.CurrentSoundId == id && musicPlayer.IsPlaying)
            return;

        SoundDefinition definition = library.GetDefinition(definitionIndex);
        if (definition == null || definition.Category != SoundCategory.Music)
            return;

        float now = Time.unscaledTime;
        if (now - lastMusicStartTimes[definitionIndex] < definition.Cooldown)
            return;

        if (!TrySelectMusicClip(definitionIndex, definition, out AudioClip clip, out int clipIndex))
            return;

        float safeFade = fadeDuration < 0f ? library.DefaultMusicFadeDuration : fadeDuration;
        if (musicPlayer.Play(definition, clip, safeFade))
        {
            lastMusicClipIndices[definitionIndex] = clipIndex;
            lastMusicStartTimes[definitionIndex] = now;
            if (definition.UseDucking)
                ducking.StartDuck(definition, now);
        }
    }

    public void StopMusic(float fadeDuration = -1f)
    {
        if (!initialized)
            return;

        float safeFade = fadeDuration < 0f ? library.DefaultMusicFadeDuration : fadeDuration;
        musicPlayer.Stop(safeFade);
    }

    public void SetMasterVolume(float normalizedVolume)
    {
        if (settings != null)
            settings.SetMasterVolume(normalizedVolume);
    }

    public void SetMusicVolume(float normalizedVolume)
    {
        if (settings != null)
            settings.SetMusicVolume(normalizedVolume);
    }

    public void SetSfxVolume(float normalizedVolume)
    {
        if (settings != null)
            settings.SetSfxVolume(normalizedVolume);
    }

    public void SetUiVolume(float normalizedVolume)
    {
        if (settings != null)
            settings.SetUiVolume(normalizedVolume);
    }

    public void ToggleBgMusic(bool enabled)
    {
        if (settings != null)
            settings.ToggleMusic(enabled);
    }

    public void ToggleSfx(bool enabled)
    {
        if (settings != null)
            settings.ToggleSfx(enabled);
    }

    public void ToggleVibration(bool enabled)
    {
        if (settings != null)
            settings.ToggleVibration(enabled);
    }

    public void Vibrate()
    {
        if (settings != null && settings.IsVibrationEnabled)
            Handheld.Vibrate();
    }

    [Obsolete("Use PlaySfx(SoundId) instead.")]
    public void PlaySfx(AudioClip clip)
    {
        if (initialized)
            sfxPlayer.PlayLegacyClip(clip, 1f, 1f, Time.frameCount);
    }

    [Obsolete("Use a SoundDefinition pitch range instead.")]
    public void PlaySfx(AudioClip clip, float pitch, float volumeScale = 1f)
    {
        if (initialized)
            sfxPlayer.PlayLegacyClip(clip, pitch, volumeScale, Time.frameCount);
    }

    [Obsolete("Use a configured SoundId instead.")]
    public void PlaySfxBurst(
        AudioClip clip,
        int count,
        float totalDuration,
        float pitch,
        float volumeScale = 1f)
    {
        if (initialized)
        {
            sfxPlayer.ScheduleLegacyBurst(
                clip,
                count,
                totalDuration,
                pitch,
                volumeScale,
                Time.unscaledTime);
        }
    }

    internal bool IsHandleValid(in SoundHandle handle)
    {
        return initialized && sfxPlayer.IsHandleValid(handle, ownerId);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public SoundDebugSnapshot GetDebugSnapshot()
    {
        if (!initialized)
            return default;

        sfxPlayer.FillInstanceCounts(debugInstanceCounts);
        return new SoundDebugSnapshot(
            sfxPlayer.PoolCount,
            sfxPlayer.ActiveCount,
            sfxPlayer.IdleCount,
            sfxPlayer.StartedThisFrame,
            sfxPlayer.CountActiveLoops(),
            musicPlayer.CurrentSoundId,
            ducking.MusicMultiplier,
            ducking.NonCriticalSfxMultiplier,
            sfxPlayer.CooldownSkipped,
            sfxPlayer.InstanceSkipped,
            sfxPlayer.FrameSkipped,
            sfxPlayer.PrioritySkipped,
            sfxPlayer.MissingClipSkipped,
            sfxPlayer.GlobalLimitSkipped,
            sfxPlayer.PrioritySteals,
            debugInstanceCounts);
    }

    public int DebugDefinitionCount => library != null ? library.DefinitionCount : 0;

    public SoundDefinition GetDebugDefinition(int index)
    {
        return library != null ? library.GetDefinition(index) : null;
    }
#endif

    private void Initialize(SoundLibrary soundLibrary)
    {
        if (initialized || Instance != this)
            return;

        library = soundLibrary;
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            library.hideFlags = HideFlags.DontSave;
            ownsRuntimeLibrary = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[SoundManager] Resources/Audio/SoundLibrary is missing. Audio will remain safely silent.");
#endif
        }

        library.BuildCache();
        settings = new AudioSettingsController();
        settings.Initialize(library);
        ducking = new AudioDuckingController();
        ducking.Initialize(Mathf.Max(8, library.MaxPoolSize * 2));
        pool = new AudioSourcePool();
        pool.Initialize(transform, library.InitialPoolSize, library.MaxPoolSize, library.AllowPoolGrowth);
        sfxPlayer = new SfxPlayer();
        sfxPlayer.Initialize(library, pool, settings, ducking);
        musicPlayer = new MusicPlayer();
        musicPlayer.Initialize(transform, library.MusicGroup);

        lastMusicClipIndices = new int[library.DefinitionCount];
        lastMusicStartTimes = new float[library.DefinitionCount];
        for (int i = 0; i < lastMusicClipIndices.Length; i++)
        {
            lastMusicClipIndices[i] = -1;
            lastMusicStartTimes[i] = float.NegativeInfinity;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugInstanceCounts = new int[library.DefinitionCount];
#endif

        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshListener();
        initialized = true;
    }

    private bool TrySelectMusicClip(
        int definitionIndex,
        SoundDefinition definition,
        out AudioClip clip,
        out int selectedIndex)
    {
        clip = null;
        selectedIndex = -1;
        AudioClip[] clips = definition.Clips;
        if (clips == null || clips.Length == 0)
            return false;

        int usableCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                usableCount++;
        }

        if (usableCount == 0)
            return false;

        int start = UnityEngine.Random.Range(0, clips.Length);
        int lastIndex = lastMusicClipIndices[definitionIndex];
        for (int offset = 0; offset < clips.Length; offset++)
        {
            int index = (start + offset) % clips.Length;
            if (clips[index] == null || (usableCount > 1 && index == lastIndex))
                continue;

            clip = clips[index];
            selectedIndex = index;
            return true;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            clip = clips[i];
            selectedIndex = i;
            return true;
        }

        return false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshListener();
    }

    private void RefreshListener()
    {
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        listenerTransform = listener != null ? listener.transform : null;
        if (sfxPlayer != null)
            sfxPlayer.SetListener(listenerTransform);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && settings != null)
            settings.Save();
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
        if (settings != null)
            settings.Save();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (sfxPlayer != null)
            sfxPlayer.Shutdown();
        if (musicPlayer != null)
            musicPlayer.Shutdown();
        if (ducking != null)
            ducking.Clear();
        if (settings != null)
            settings.Save();

        if (ownsRuntimeLibrary && library != null)
            Destroy(library);

        initialized = false;
        Instance = null;
    }
}
