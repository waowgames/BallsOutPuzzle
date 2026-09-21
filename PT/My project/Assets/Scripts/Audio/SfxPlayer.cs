using UnityEngine;

internal sealed class SfxPlayer
{
    private const int LegacyBurstCapacity = 4;

    private struct DefinitionState
    {
        internal float LastStartedTime;
        internal int LastClipIndex;
    }

    private struct PendingRequest
    {
        internal bool Pending;
        internal int Frame;
        internal int Count;
        internal bool HasPosition;
        internal Vector3 Position;
        internal float ListenerDistanceSquared;
    }

    private struct LegacyBurst
    {
        internal bool Active;
        internal AudioClip Clip;
        internal int Remaining;
        internal float NextTime;
        internal float Interval;
        internal float Pitch;
        internal float Volume;
    }

    private SoundLibrary library;
    private AudioSourcePool pool;
    private AudioSettingsController settings;
    private AudioDuckingController ducking;
    private DefinitionState[] definitionStates;
    private PendingRequest[] pendingRequests;
    private LegacyBurst[] legacyBursts;
    private Transform listenerTransform;
    private ulong startSequence;
    private int budgetFrame = -1;
    private int startedThisFrame;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal int CooldownSkipped { get; private set; }
    internal int InstanceSkipped { get; private set; }
    internal int FrameSkipped { get; private set; }
    internal int PrioritySkipped { get; private set; }
    internal int MissingClipSkipped { get; private set; }
    internal int GlobalLimitSkipped { get; private set; }
    internal int PrioritySteals { get; private set; }
#endif

    internal int StartedThisFrame => startedThisFrame;
    internal int PoolCount => pool != null ? pool.Count : 0;
    internal int ActiveCount => pool != null ? pool.ActiveCount : 0;
    internal int IdleCount => pool != null ? pool.IdleCount : 0;

    internal void Initialize(
        SoundLibrary soundLibrary,
        AudioSourcePool sourcePool,
        AudioSettingsController settingsController,
        AudioDuckingController duckingController)
    {
        library = soundLibrary;
        pool = sourcePool;
        settings = settingsController;
        ducking = duckingController;

        int definitionCount = library != null ? library.DefinitionCount : 0;
        definitionStates = new DefinitionState[definitionCount];
        pendingRequests = new PendingRequest[definitionCount];
        for (int i = 0; i < definitionStates.Length; i++)
        {
            definitionStates[i].LastStartedTime = float.NegativeInfinity;
            definitionStates[i].LastClipIndex = -1;
        }

        legacyBursts = new LegacyBurst[LegacyBurstCapacity];
    }

    internal void SetListener(Transform value)
    {
        listenerTransform = value;
    }

    internal void Enqueue(SoundId id)
    {
        RecordRequest(id, false, Vector3.zero);
    }

    internal void Enqueue(SoundId id, Vector3 worldPosition)
    {
        RecordRequest(id, true, worldPosition);
    }

    internal void FlushPending(int frame, float now)
    {
        EnsureFrameBudget(frame);
        FlushPriority(SoundPriority.Critical, frame, now);
        FlushPriority(SoundPriority.High, frame, now);
        FlushPriority(SoundPriority.Normal, frame, now);
        FlushPriority(SoundPriority.Low, frame, now);
    }

    internal SoundHandle PlayOneShot(
        SoundId id,
        int ownerId,
        SoundManager owner,
        float now,
        int frame)
    {
        if (library == null
            || !library.TryGetDefinitionIndex(id, out int definitionIndex))
        {
            return default;
        }

        SoundDefinition definition = library.GetDefinition(definitionIndex);
        if (definition == null
            || definition.Category == SoundCategory.Music
            || definition.Loop)
        {
            return default;
        }

        EnsureFrameBudget(frame);
        if (!TryStartDefinition(
                definitionIndex,
                definition,
                1,
                false,
                Vector3.zero,
                false,
                null,
                now,
                out int slotIndex))
        {
            return default;
        }

        PooledAudioSource slot = pool.GetSlot(slotIndex);
        return new SoundHandle(owner, ownerId, slotIndex, slot.Generation);
    }

    internal SoundHandle PlayLoop(
        SoundId id,
        Transform followTarget,
        int ownerId,
        SoundManager owner,
        float now,
        int frame)
    {
        if (library == null || !library.TryGetDefinitionIndex(id, out int definitionIndex))
            return default;

        SoundDefinition definition = library.GetDefinition(definitionIndex);
        if (definition == null || !definition.Loop || definition.Category == SoundCategory.Music)
            return default;

        int existing = pool.FindExistingLoop(definitionIndex, followTarget);
        if (existing >= 0)
        {
            PooledAudioSource existingSlot = pool.GetSlot(existing);
            return new SoundHandle(owner, ownerId, existing, existingSlot.Generation);
        }

        EnsureFrameBudget(frame);
        if (!TryStartDefinition(
                definitionIndex,
                definition,
                1,
                followTarget != null,
                followTarget != null ? followTarget.position : Vector3.zero,
                true,
                followTarget,
                now,
                out int slotIndex))
        {
            return default;
        }

        PooledAudioSource slot = pool.GetSlot(slotIndex);
        return new SoundHandle(owner, ownerId, slotIndex, slot.Generation);
    }

    internal bool Stop(in SoundHandle handle, float fadeDuration, int ownerId)
    {
        if (!pool.IsHandleValid(ownerId, handle))
            return false;

        StopSlot(handle.SlotIndex, fadeDuration);
        return true;
    }

    internal void StopAll(SoundId id, float fadeDuration)
    {
        if (library == null
            || pool == null
            || !library.TryGetDefinitionIndex(id, out int definitionIndex))
        {
            return;
        }

        pendingRequests[definitionIndex] = default;

        for (int i = 0; i < pool.Count; i++)
        {
            PooledAudioSource slot = pool.GetSlot(i);
            if (slot == null
                || !slot.Active
                || slot.DefinitionIndex != definitionIndex)
            {
                continue;
            }

            StopSlot(i, fadeDuration);
        }
    }

    internal bool IsHandleValid(in SoundHandle handle, int ownerId)
    {
        return pool != null && pool.IsHandleValid(ownerId, handle);
    }

    private void StopSlot(int slotIndex, float fadeDuration)
    {
        PooledAudioSource slot = pool.GetSlot(slotIndex);
        if (slot == null || !slot.Active)
            return;

        float safeDuration = Mathf.Max(0f, fadeDuration);
        if (safeDuration <= 0f)
        {
            pool.Release(slotIndex);
            return;
        }

        slot.Fading = true;
        slot.FadeStartVolume = 1f;
        slot.FadeDuration = safeDuration;
        slot.FadeElapsed = 0f;
    }

    internal void PlayLegacyClip(
        AudioClip clip,
        float pitch,
        float volumeScale,
        int frame)
    {
        if (clip == null || pool == null || settings == null || !settings.IsSfxEnabled)
        {
            IncrementMissingClip();
            return;
        }

        EnsureFrameBudget(frame);
        if (startedThisFrame >= library.MaxSoundsStartedPerFrame)
        {
            IncrementFrameSkip();
            return;
        }

        if (!TryAcquireSlot(SoundPriority.Normal, out int slotIndex))
            return;

        PooledAudioSource slot = pool.GetSlot(slotIndex);
        ConfigureCommonSlot(slot, -1, SoundId.None, SoundCategory.Sfx, SoundPriority.Normal, false, null);
        slot.BaseVolume = Mathf.Clamp01(volumeScale);
        slot.Source.clip = clip;
        slot.Source.outputAudioMixerGroup = library.SfxGroup;
        slot.Source.volume = slot.BaseVolume * settings.GetSourceMultiplier(SoundCategory.Sfx);
        slot.Source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        slot.Source.spatialBlend = 0f;
        slot.Source.Play();
        startedThisFrame++;
    }

    internal void ScheduleLegacyBurst(
        AudioClip clip,
        int count,
        float totalDuration,
        float pitch,
        float volumeScale,
        float now)
    {
        if (clip == null || count <= 0)
            return;

        int index = FindLegacyBurst(clip);
        if (index < 0)
            index = FindFreeLegacyBurst();
        if (index < 0)
            index = FindEarliestLegacyBurst();

        int safeCount = Mathf.Max(1, count);
        float safeDuration = Mathf.Max(0.03f, totalDuration);
        legacyBursts[index] = new LegacyBurst
        {
            Active = true,
            Clip = clip,
            Remaining = safeCount,
            NextTime = now,
            Interval = safeDuration / safeCount,
            Pitch = Mathf.Clamp(pitch, 0.1f, 3f),
            Volume = Mathf.Clamp01(volumeScale)
        };
    }

    internal void Update(float now, float unscaledDeltaTime, int frame)
    {
        EnsureFrameBudget(frame);
        UpdateLegacyBursts(now, frame);

        for (int i = 0; i < pool.Count; i++)
        {
            PooledAudioSource slot = pool.GetSlot(i);
            if (slot == null || !slot.Active)
                continue;

            if (slot.Loop && slot.FollowTargetRequired)
            {
                if (slot.FollowTarget == null || !slot.FollowTarget.gameObject.activeInHierarchy)
                {
                    pool.Release(i);
                    continue;
                }

                slot.Transform.position = slot.FollowTarget.position;
            }

            if (!slot.Loop && !slot.Source.isPlaying)
            {
                pool.Release(i);
                continue;
            }

            float fadeMultiplier = 1f;
            if (slot.Fading)
            {
                slot.FadeElapsed += unscaledDeltaTime;
                if (slot.FadeElapsed >= slot.FadeDuration)
                {
                    pool.Release(i);
                    continue;
                }

                fadeMultiplier = 1f - Mathf.Clamp01(slot.FadeElapsed / slot.FadeDuration);
            }

            float duckMultiplier = slot.Priority < SoundPriority.Critical
                ? ducking.NonCriticalSfxMultiplier
                : 1f;
            slot.Source.volume = Mathf.Clamp01(
                slot.BaseVolume
                * fadeMultiplier
                * settings.GetSourceMultiplier(slot.Category)
                * duckMultiplier);
        }
    }

    internal int CountActiveLoops()
    {
        int count = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            PooledAudioSource slot = pool.GetSlot(i);
            if (slot != null && slot.Active && slot.Loop)
                count++;
        }

        return count;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal int FillInstanceCounts(int[] countsByDefinition)
    {
        if (countsByDefinition == null)
            return 0;

        int clearCount = Mathf.Min(countsByDefinition.Length, definitionStates.Length);
        for (int i = 0; i < clearCount; i++)
            countsByDefinition[i] = 0;

        int activeIds = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            PooledAudioSource slot = pool.GetSlot(i);
            if (slot == null || !slot.Active || slot.DefinitionIndex < 0 || slot.DefinitionIndex >= clearCount)
                continue;

            if (countsByDefinition[slot.DefinitionIndex] == 0)
                activeIds++;
            countsByDefinition[slot.DefinitionIndex]++;
        }

        return activeIds;
    }
#endif

    internal void Shutdown()
    {
        if (pool != null)
            pool.ReleaseAll();

        if (legacyBursts != null)
        {
            for (int i = 0; i < legacyBursts.Length; i++)
                legacyBursts[i].Active = false;
        }
    }

    private void RecordRequest(SoundId id, bool hasPosition, Vector3 position)
    {
        if (library == null || !library.TryGetDefinitionIndex(id, out int definitionIndex))
            return;

        int frame = Time.frameCount;
        PendingRequest request = pendingRequests[definitionIndex];
        if (!request.Pending || request.Frame != frame)
        {
            request.Pending = true;
            request.Frame = frame;
            request.Count = 1;
            request.HasPosition = hasPosition;
            request.Position = position;
            request.ListenerDistanceSquared = GetListenerDistanceSquared(hasPosition, position);
            pendingRequests[definitionIndex] = request;
            return;
        }

        if (request.Count < int.MaxValue)
            request.Count++;

        if (hasPosition)
        {
            float distanceSquared = GetListenerDistanceSquared(true, position);
            if (!request.HasPosition || distanceSquared < request.ListenerDistanceSquared)
            {
                request.HasPosition = true;
                request.Position = position;
                request.ListenerDistanceSquared = distanceSquared;
            }
        }

        pendingRequests[definitionIndex] = request;
    }

    private float GetListenerDistanceSquared(bool hasPosition, Vector3 position)
    {
        if (!hasPosition || listenerTransform == null)
            return hasPosition ? 0f : float.MaxValue;

        return (listenerTransform.position - position).sqrMagnitude;
    }

    private void FlushPriority(SoundPriority priority, int frame, float now)
    {
        for (int i = 0; i < pendingRequests.Length; i++)
        {
            PendingRequest request = pendingRequests[i];
            if (!request.Pending || request.Frame > frame)
                continue;

            SoundDefinition definition = library.GetDefinition(i);
            if (definition == null || definition.Priority != priority)
                continue;

            int starts = definition.MergeSameFrameRequests
                ? 1
                : Mathf.Min(request.Count, library.MaxSoundsStartedPerFrame);

            for (int start = 0; start < starts; start++)
            {
                int batchCount = definition.MergeSameFrameRequests ? request.Count : 1;
                if (!TryStartDefinition(
                        i,
                        definition,
                        batchCount,
                        request.HasPosition,
                        request.Position,
                        false,
                        null,
                        now,
                        out _))
                {
                    if (startedThisFrame >= library.MaxSoundsStartedPerFrame)
                        break;
                }
            }

            request.Pending = false;
            pendingRequests[i] = request;
        }
    }

    private bool TryStartDefinition(
        int definitionIndex,
        SoundDefinition definition,
        int requestCount,
        bool hasPosition,
        Vector3 position,
        bool loop,
        Transform followTarget,
        float now,
        out int slotIndex)
    {
        slotIndex = -1;
        if (!IsCategoryEnabled(definition.Category))
            return false;

        if (!TrySelectClip(definitionIndex, definition, out AudioClip clip, out int clipIndex))
        {
            IncrementMissingClip();
            return false;
        }

        DefinitionState state = definitionStates[definitionIndex];
        if (now - state.LastStartedTime < definition.Cooldown)
        {
            IncrementCooldownSkip();
            return false;
        }

        if (startedThisFrame >= library.MaxSoundsStartedPerFrame)
        {
            IncrementFrameSkip();
            return false;
        }

        int instanceCount = pool.CountInstances(definitionIndex);
        switch (definition.OverlapMode)
        {
            case SoundOverlapMode.IgnoreIfPlaying:
                if (instanceCount > 0)
                {
                    IncrementInstanceSkip();
                    return false;
                }
                break;
            case SoundOverlapMode.Restart:
                ReleaseAllInstances(definitionIndex);
                break;
            case SoundOverlapMode.LimitInstances:
                if (instanceCount >= definition.MaxSimultaneousInstances)
                {
                    IncrementInstanceSkip();
                    return false;
                }
                break;
            case SoundOverlapMode.ReplaceOldest:
                if (instanceCount >= definition.MaxSimultaneousInstances)
                {
                    int oldest = pool.FindOldestInstance(definitionIndex);
                    if (oldest >= 0)
                        pool.Release(oldest);
                }
                break;
        }

        if (!TryAcquireSlot(definition.Priority, out slotIndex))
            return false;

        PooledAudioSource slot = pool.GetSlot(slotIndex);
        ConfigureCommonSlot(
            slot,
            definitionIndex,
            definition.Id,
            definition.Category,
            definition.Priority,
            loop,
            followTarget);

        float randomVolume = Random.Range(definition.VolumeRange.x, definition.VolumeRange.y);
        float batchVolume = 1f + definition.BatchVolumeIncrement * Mathf.Max(0, requestCount - 1);
        batchVolume = Mathf.Min(batchVolume, definition.MaxBatchVolumeMultiplier);
        float batchPitch = definition.BatchPitchIncrement * Mathf.Max(0, requestCount - 1);
        batchPitch = Mathf.Min(batchPitch, definition.MaxBatchPitchOffset);

        slot.BaseVolume = Mathf.Clamp01(definition.Volume * randomVolume * batchVolume);
        slot.Source.clip = clip;
        slot.Source.outputAudioMixerGroup = definition.MixerGroup != null
            ? definition.MixerGroup
            : library.GetMixerGroup(definition.Category);
        slot.Source.volume = slot.BaseVolume * settings.GetSourceMultiplier(definition.Category);
        slot.Source.pitch = Mathf.Clamp(
            Random.Range(definition.PitchRange.x, definition.PitchRange.y) + batchPitch,
            0.1f,
            3f);
        slot.Source.loop = loop;
        slot.Source.spatialBlend = definition.Is3D ? definition.SpatialBlend : 0f;
        slot.Source.rolloffMode = definition.RolloffMode;
        slot.Source.minDistance = definition.MinDistance;
        slot.Source.maxDistance = definition.MaxDistance;
        slot.Source.dopplerLevel = 0f;
        slot.Transform.position = hasPosition ? position : Vector3.zero;
        slot.Source.Play();

        state.LastStartedTime = now;
        state.LastClipIndex = clipIndex;
        definitionStates[definitionIndex] = state;
        startedThisFrame++;

        if (definition.UseDucking)
            ducking.StartDuck(definition, now);

        return true;
    }

    private bool IsCategoryEnabled(SoundCategory category)
    {
        if (settings == null)
            return false;

        if (category == SoundCategory.Ui)
        {
            return settings.IsSfxEnabled
                && settings.UiVolume > 0.0001f
                && settings.MasterVolume > 0.0001f;
        }

        return category != SoundCategory.Music
            && settings.IsSfxEnabled
            && settings.MasterVolume > 0.0001f;
    }

    private bool TrySelectClip(
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

        int lastIndex = definitionStates[definitionIndex].LastClipIndex;
        int startIndex = Random.Range(0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            int index = (startIndex + offset) % clips.Length;
            if (clips[index] == null)
                continue;
            if (usableCount > 1 && index == lastIndex)
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

    private bool TryAcquireSlot(SoundPriority incomingPriority, out int slotIndex)
    {
        if (pool.ActiveCount < library.MaxTotalSimultaneousSfx)
        {
            if (pool.TryAcquireIdle(out slotIndex) || pool.TryGrow(out slotIndex))
            {
                pool.Activate(slotIndex);
                return true;
            }
        }

        int victim = pool.FindStealCandidate(incomingPriority);
        if (victim < 0)
        {
            if (pool.ActiveCount >= library.MaxTotalSimultaneousSfx)
                IncrementGlobalSkip();
            else
                IncrementPrioritySkip();
            slotIndex = -1;
            return false;
        }

        pool.Release(victim);
        pool.Activate(victim);
        slotIndex = victim;
        IncrementPrioritySteal();
        return true;
    }

    private void ConfigureCommonSlot(
        PooledAudioSource slot,
        int definitionIndex,
        SoundId id,
        SoundCategory category,
        SoundPriority priority,
        bool loop,
        Transform followTarget)
    {
        slot.DefinitionIndex = definitionIndex;
        slot.SoundId = id;
        slot.Category = category;
        slot.Priority = priority;
        slot.Loop = loop;
        slot.FollowTarget = followTarget;
        slot.FollowTargetRequired = followTarget != null;
        slot.StartSequence = ++startSequence;
        slot.Fading = false;
        slot.Source.priority = PriorityToAudioSourcePriority(priority);
    }

    private static int PriorityToAudioSourcePriority(SoundPriority priority)
    {
        switch (priority)
        {
            case SoundPriority.Critical:
                return 0;
            case SoundPriority.High:
                return 64;
            case SoundPriority.Normal:
                return 128;
            default:
                return 192;
        }
    }

    private void ReleaseAllInstances(int definitionIndex)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            PooledAudioSource slot = pool.GetSlot(i);
            if (slot != null && slot.Active && slot.DefinitionIndex == definitionIndex)
                pool.Release(i);
        }
    }

    private void EnsureFrameBudget(int frame)
    {
        if (budgetFrame == frame)
            return;

        budgetFrame = frame;
        startedThisFrame = 0;
    }

    private int FindLegacyBurst(AudioClip clip)
    {
        for (int i = 0; i < legacyBursts.Length; i++)
        {
            if (legacyBursts[i].Active && legacyBursts[i].Clip == clip)
                return i;
        }

        return -1;
    }

    private int FindFreeLegacyBurst()
    {
        for (int i = 0; i < legacyBursts.Length; i++)
        {
            if (!legacyBursts[i].Active)
                return i;
        }

        return -1;
    }

    private int FindEarliestLegacyBurst()
    {
        int candidate = 0;
        float earliest = float.MaxValue;
        for (int i = 0; i < legacyBursts.Length; i++)
        {
            if (legacyBursts[i].NextTime < earliest)
            {
                earliest = legacyBursts[i].NextTime;
                candidate = i;
            }
        }

        return candidate;
    }

    private void UpdateLegacyBursts(float now, int frame)
    {
        for (int i = 0; i < legacyBursts.Length; i++)
        {
            LegacyBurst burst = legacyBursts[i];
            if (!burst.Active || now < burst.NextTime)
                continue;

            PlayLegacyClip(burst.Clip, burst.Pitch, burst.Volume, frame);
            burst.Remaining--;
            if (burst.Remaining <= 0)
            {
                burst.Active = false;
                burst.Clip = null;
            }
            else
            {
                burst.NextTime += burst.Interval;
                if (burst.NextTime < now)
                    burst.NextTime = now + burst.Interval;
            }

            legacyBursts[i] = burst;
        }
    }

    private void IncrementCooldownSkip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CooldownSkipped++;
#endif
    }

    private void IncrementInstanceSkip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        InstanceSkipped++;
#endif
    }

    private void IncrementFrameSkip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        FrameSkipped++;
#endif
    }

    private void IncrementPrioritySkip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PrioritySkipped++;
#endif
    }

    private void IncrementMissingClip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MissingClipSkipped++;
#endif
    }

    private void IncrementGlobalSkip()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GlobalLimitSkipped++;
#endif
    }

    private void IncrementPrioritySteal()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PrioritySteals++;
#endif
    }
}
