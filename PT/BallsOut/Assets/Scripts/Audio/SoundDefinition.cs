using System;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public sealed class SoundDefinition
{
    public SoundId Id;
    public AudioClip[] Clips = Array.Empty<AudioClip>();
    public SoundCategory Category = SoundCategory.Sfx;
    [Range(0f, 1f)] public float Volume = 1f;
    public Vector2 VolumeRange = Vector2.one;
    public Vector2 PitchRange = Vector2.one;
    [Min(0f)] public float Cooldown;
    [Min(1)] public int MaxSimultaneousInstances = 1;
    public SoundPriority Priority = SoundPriority.Normal;
    public SoundOverlapMode OverlapMode = SoundOverlapMode.Allow;
    public AudioMixerGroup MixerGroup;
    public bool MergeSameFrameRequests = true;
    [Min(0f)] public float BatchVolumeIncrement;
    [Min(1f)] public float MaxBatchVolumeMultiplier = 1f;
    [Min(0f)] public float BatchPitchIncrement;
    [Min(0f)] public float MaxBatchPitchOffset;
    public bool Is3D;
    [Range(0f, 1f)] public float SpatialBlend;
    [Min(0f)] public float MinDistance = 1f;
    [Min(0.01f)] public float MaxDistance = 25f;
    public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;
    public bool Loop;
    public bool UseDucking;
    [Range(0f, 1f)] public float DuckVolume = 0.45f;
    [Min(0f)] public float DuckFadeInDuration = 0.08f;
    [Min(0f)] public float DuckHoldDuration = 0.35f;
    [Min(0f)] public float DuckFadeOutDuration = 0.25f;
    public DuckTarget DuckTargets = DuckTarget.Music | DuckTarget.NonCriticalSfx;

    public void Normalize()
    {
        if (Clips == null)
            Clips = Array.Empty<AudioClip>();

        Volume = Mathf.Clamp01(Volume);

        float minVolume = Mathf.Clamp(VolumeRange.x, 0f, 2f);
        float maxVolume = Mathf.Clamp(VolumeRange.y, 0f, 2f);
        VolumeRange = new Vector2(Mathf.Min(minVolume, maxVolume), Mathf.Max(minVolume, maxVolume));

        float minPitch = Mathf.Clamp(PitchRange.x, 0.1f, 3f);
        float maxPitch = Mathf.Clamp(PitchRange.y, 0.1f, 3f);
        PitchRange = new Vector2(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));

        Cooldown = Mathf.Max(0f, Cooldown);
        MaxSimultaneousInstances = Mathf.Max(1, MaxSimultaneousInstances);
        BatchVolumeIncrement = Mathf.Clamp(BatchVolumeIncrement, 0f, 1f);
        MaxBatchVolumeMultiplier = Mathf.Clamp(MaxBatchVolumeMultiplier, 1f, 4f);
        BatchPitchIncrement = Mathf.Clamp(BatchPitchIncrement, 0f, 1f);
        MaxBatchPitchOffset = Mathf.Clamp(MaxBatchPitchOffset, 0f, 2.9f);
        SpatialBlend = Is3D ? Mathf.Clamp01(SpatialBlend) : 0f;
        MinDistance = Mathf.Max(0.01f, MinDistance);
        MaxDistance = Mathf.Max(MinDistance + 0.01f, MaxDistance);
        DuckVolume = Mathf.Clamp01(DuckVolume);
        DuckFadeInDuration = Mathf.Max(0f, DuckFadeInDuration);
        DuckHoldDuration = Mathf.Max(0f, DuckHoldDuration);
        DuckFadeOutDuration = Mathf.Max(0f, DuckFadeOutDuration);
    }
}
