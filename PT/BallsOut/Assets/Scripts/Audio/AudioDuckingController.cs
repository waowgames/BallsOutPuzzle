using UnityEngine;

internal sealed class AudioDuckingController
{
    private struct DuckRequest
    {
        internal bool Active;
        internal DuckTarget Targets;
        internal float DuckVolume;
        internal float StartTime;
        internal float FadeInEndTime;
        internal float HoldEndTime;
        internal float EndTime;
    }

    private DuckRequest[] requests;

    internal float MusicMultiplier { get; private set; } = 1f;
    internal float NonCriticalSfxMultiplier { get; private set; } = 1f;
    internal bool IsActive => MusicMultiplier < 0.9999f || NonCriticalSfxMultiplier < 0.9999f;

    internal void Initialize(int capacity)
    {
        requests = new DuckRequest[Mathf.Max(8, capacity)];
        MusicMultiplier = 1f;
        NonCriticalSfxMultiplier = 1f;
    }

    internal void StartDuck(SoundDefinition definition, float now)
    {
        if (definition == null || !definition.UseDucking || definition.DuckTargets == DuckTarget.None)
            return;

        int slotIndex = FindFreeRequest();
        if (slotIndex < 0)
            slotIndex = FindMergeCandidate(definition.DuckTargets);

        float fadeInEnd = now + definition.DuckFadeInDuration;
        float holdEnd = fadeInEnd + definition.DuckHoldDuration;
        float end = holdEnd + definition.DuckFadeOutDuration;

        if (requests[slotIndex].Active)
        {
            DuckRequest merged = requests[slotIndex];
            merged.Targets |= definition.DuckTargets;
            merged.DuckVolume = Mathf.Min(merged.DuckVolume, definition.DuckVolume);
            merged.FadeInEndTime = Mathf.Min(merged.FadeInEndTime, fadeInEnd);
            merged.HoldEndTime = Mathf.Max(merged.HoldEndTime, holdEnd);
            merged.EndTime = Mathf.Max(merged.EndTime, end);
            requests[slotIndex] = merged;
            return;
        }

        requests[slotIndex] = new DuckRequest
        {
            Active = true,
            Targets = definition.DuckTargets,
            DuckVolume = definition.DuckVolume,
            StartTime = now,
            FadeInEndTime = fadeInEnd,
            HoldEndTime = holdEnd,
            EndTime = end
        };
    }

    internal bool Update(float now, float unscaledDeltaTime)
    {
        float musicTarget = 1f;
        float sfxTarget = 1f;

        if (requests != null)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                DuckRequest request = requests[i];
                if (!request.Active)
                    continue;

                if (now >= request.EndTime)
                {
                    requests[i].Active = false;
                    continue;
                }

                float multiplier = EvaluateRequest(request, now);
                if ((request.Targets & DuckTarget.Music) != 0)
                    musicTarget = Mathf.Min(musicTarget, multiplier);
                if ((request.Targets & DuckTarget.NonCriticalSfx) != 0)
                    sfxTarget = Mathf.Min(sfxTarget, multiplier);
            }
        }

        bool changed = !Mathf.Approximately(MusicMultiplier, musicTarget)
            || !Mathf.Approximately(NonCriticalSfxMultiplier, sfxTarget);
        MusicMultiplier = musicTarget;
        NonCriticalSfxMultiplier = sfxTarget;
        return changed;
    }

    internal void Clear()
    {
        if (requests != null)
        {
            for (int i = 0; i < requests.Length; i++)
                requests[i].Active = false;
        }

        MusicMultiplier = 1f;
        NonCriticalSfxMultiplier = 1f;
    }

    private int FindFreeRequest()
    {
        for (int i = 0; i < requests.Length; i++)
        {
            if (!requests[i].Active)
                return i;
        }

        return -1;
    }

    private int FindMergeCandidate(DuckTarget targets)
    {
        int candidate = 0;
        float earliestEnd = float.MaxValue;
        for (int i = 0; i < requests.Length; i++)
        {
            DuckRequest request = requests[i];
            if (request.Targets == targets)
                return i;

            if (request.EndTime < earliestEnd)
            {
                earliestEnd = request.EndTime;
                candidate = i;
            }
        }

        return candidate;
    }

    private static float EvaluateRequest(DuckRequest request, float now)
    {
        if (now < request.FadeInEndTime)
        {
            float duration = request.FadeInEndTime - request.StartTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01((now - request.StartTime) / duration);
            return Mathf.Lerp(1f, request.DuckVolume, t);
        }

        if (now < request.HoldEndTime)
            return request.DuckVolume;

        float fadeOutDuration = request.EndTime - request.HoldEndTime;
        float fadeOutT = fadeOutDuration <= 0f
            ? 1f
            : Mathf.Clamp01((now - request.HoldEndTime) / fadeOutDuration);
        return Mathf.Lerp(request.DuckVolume, 1f, fadeOutT);
    }
}
