#if UNITY_EDITOR || DEVELOPMENT_BUILD
public readonly struct SoundDebugSnapshot
{
    public readonly int PoolTotal;
    public readonly int PoolActive;
    public readonly int PoolIdle;
    public readonly int StartedThisFrame;
    public readonly int ActiveLoops;
    public readonly SoundId CurrentMusic;
    public readonly float MusicDuckMultiplier;
    public readonly float SfxDuckMultiplier;
    public readonly int CooldownSkipped;
    public readonly int InstanceSkipped;
    public readonly int FrameSkipped;
    public readonly int PrioritySkipped;
    public readonly int MissingClipSkipped;
    public readonly int GlobalLimitSkipped;
    public readonly int PrioritySteals;
    public readonly int[] InstanceCounts;

    internal SoundDebugSnapshot(
        int poolTotal,
        int poolActive,
        int poolIdle,
        int startedThisFrame,
        int activeLoops,
        SoundId currentMusic,
        float musicDuckMultiplier,
        float sfxDuckMultiplier,
        int cooldownSkipped,
        int instanceSkipped,
        int frameSkipped,
        int prioritySkipped,
        int missingClipSkipped,
        int globalLimitSkipped,
        int prioritySteals,
        int[] instanceCounts)
    {
        PoolTotal = poolTotal;
        PoolActive = poolActive;
        PoolIdle = poolIdle;
        StartedThisFrame = startedThisFrame;
        ActiveLoops = activeLoops;
        CurrentMusic = currentMusic;
        MusicDuckMultiplier = musicDuckMultiplier;
        SfxDuckMultiplier = sfxDuckMultiplier;
        CooldownSkipped = cooldownSkipped;
        InstanceSkipped = instanceSkipped;
        FrameSkipped = frameSkipped;
        PrioritySkipped = prioritySkipped;
        MissingClipSkipped = missingClipSkipped;
        GlobalLimitSkipped = globalLimitSkipped;
        PrioritySteals = prioritySteals;
        InstanceCounts = instanceCounts;
    }
}
#endif
