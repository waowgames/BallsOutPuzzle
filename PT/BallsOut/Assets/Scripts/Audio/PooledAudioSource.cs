using UnityEngine;

internal sealed class PooledAudioSource
{
    internal readonly GameObject GameObject;
    internal readonly Transform Transform;
    internal readonly AudioSource Source;
    internal bool Active;
    internal bool Loop;
    internal int DefinitionIndex;
    internal SoundId SoundId;
    internal SoundCategory Category;
    internal SoundPriority Priority;
    internal Transform FollowTarget;
    internal bool FollowTargetRequired;
    internal ulong StartSequence;
    internal uint Generation;
    internal float BaseVolume;
    internal bool Fading;
    internal float FadeStartVolume;
    internal float FadeDuration;
    internal float FadeElapsed;

    internal PooledAudioSource(Transform parent, int index)
    {
        GameObject = new GameObject($"SFX Source {index:00}");
        Transform = GameObject.transform;
        Transform.SetParent(parent, false);
        Source = GameObject.AddComponent<AudioSource>();
        Source.playOnAwake = false;
        Source.loop = false;
        Source.spatialBlend = 0f;
        Generation = 1;
        ResetForIdle();
    }

    internal void ResetForIdle()
    {
        Source.Stop();
        Source.clip = null;
        Source.outputAudioMixerGroup = null;
        Source.volume = 1f;
        Source.pitch = 1f;
        Source.loop = false;
        Source.mute = false;
        Source.priority = 128;
        Source.spatialBlend = 0f;
        Source.rolloffMode = AudioRolloffMode.Logarithmic;
        Source.minDistance = 1f;
        Source.maxDistance = 25f;
        Source.dopplerLevel = 0f;
        Transform.localPosition = Vector3.zero;

        Active = false;
        Loop = false;
        DefinitionIndex = -1;
        SoundId = SoundId.None;
        Category = SoundCategory.Sfx;
        Priority = SoundPriority.Low;
        FollowTarget = null;
        FollowTargetRequired = false;
        StartSequence = 0;
        BaseVolume = 1f;
        Fading = false;
        FadeStartVolume = 1f;
        FadeDuration = 0f;
        FadeElapsed = 0f;
        GameObject.SetActive(false);
    }
}
