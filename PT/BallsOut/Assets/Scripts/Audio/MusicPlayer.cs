using UnityEngine;

internal sealed class MusicPlayer
{
    private struct TransitionState
    {
        internal bool Active;
        internal AudioSource Outgoing;
        internal AudioSource Incoming;
        internal float OutgoingStartGain;
        internal float IncomingStartGain;
        internal float Elapsed;
        internal float Duration;
    }

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource current;
    private float baseVolumeA;
    private float baseVolumeB;
    private float gainA;
    private float gainB;
    private TransitionState transition;
    private UnityEngine.Audio.AudioMixerGroup defaultMixerGroup;

    internal SoundId CurrentSoundId { get; private set; }
    internal bool IsPlaying => current != null && current.isPlaying;

    internal void Initialize(Transform parent, UnityEngine.Audio.AudioMixerGroup musicGroup)
    {
        defaultMixerGroup = musicGroup;
        sourceA = CreateSource(parent, "Music Source A");
        sourceB = CreateSource(parent, "Music Source B");
        gainA = 0f;
        gainB = 0f;
        CurrentSoundId = SoundId.None;
    }

    internal bool Play(SoundDefinition definition, AudioClip clip, float fadeDuration)
    {
        if (definition == null || clip == null)
            return false;

        if (current != null && current.clip == clip && current.isPlaying)
            return false;

        AudioSource outgoing = current != null && current.isPlaying ? current : null;
        AudioSource next = outgoing == sourceA ? sourceB : sourceA;
        next.Stop();
        next.clip = clip;
        next.outputAudioMixerGroup = definition.MixerGroup != null
            ? definition.MixerGroup
            : defaultMixerGroup;
        next.loop = definition.Loop;
        next.pitch = Mathf.Clamp(
            Random.Range(definition.PitchRange.x, definition.PitchRange.y),
            0.1f,
            3f);

        float baseVolume = Mathf.Clamp01(
            definition.Volume * Random.Range(definition.VolumeRange.x, definition.VolumeRange.y));
        SetBaseVolume(next, baseVolume);
        SetGain(next, 0f);
        next.Play();

        float safeDuration = Mathf.Max(0f, fadeDuration);
        if (safeDuration <= 0f)
        {
            if (outgoing != null && outgoing != next)
            {
                outgoing.Stop();
                outgoing.clip = null;
                SetGain(outgoing, 0f);
            }

            SetGain(next, 1f);
            transition.Active = false;
        }
        else
        {
            transition = new TransitionState
            {
                Active = true,
                Outgoing = outgoing,
                Incoming = next,
                OutgoingStartGain = outgoing != null ? GetGain(outgoing) : 0f,
                IncomingStartGain = 0f,
                Elapsed = 0f,
                Duration = safeDuration
            };
        }

        current = next;
        CurrentSoundId = definition.Id;
        return true;
    }

    internal void Stop(float fadeDuration)
    {
        if (current == null || !current.isPlaying)
            return;

        float safeDuration = Mathf.Max(0f, fadeDuration);
        if (safeDuration <= 0f)
        {
            sourceA.Stop();
            sourceB.Stop();
            sourceA.clip = null;
            sourceB.clip = null;
            gainA = 0f;
            gainB = 0f;
            current = null;
            transition.Active = false;
            CurrentSoundId = SoundId.None;
            return;
        }

        AudioSource other = current == sourceA ? sourceB : sourceA;
        if (other.isPlaying)
        {
            other.Stop();
            other.clip = null;
            SetGain(other, 0f);
        }

        transition = new TransitionState
        {
            Active = true,
            Outgoing = current,
            Incoming = null,
            OutgoingStartGain = GetGain(current),
            IncomingStartGain = 0f,
            Elapsed = 0f,
            Duration = safeDuration
        };
        CurrentSoundId = SoundId.None;
    }

    internal void Update(float unscaledDeltaTime, float settingsMultiplier, float duckMultiplier)
    {
        if (transition.Active)
        {
            transition.Elapsed += unscaledDeltaTime;
            float t = transition.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(transition.Elapsed / transition.Duration);

            if (transition.Outgoing != null)
                SetGain(transition.Outgoing, Mathf.Lerp(transition.OutgoingStartGain, 0f, t));
            if (transition.Incoming != null)
                SetGain(transition.Incoming, Mathf.Lerp(transition.IncomingStartGain, 1f, t));

            if (t >= 1f)
            {
                if (transition.Outgoing != null)
                {
                    transition.Outgoing.Stop();
                    transition.Outgoing.clip = null;
                    SetGain(transition.Outgoing, 0f);
                }

                if (transition.Incoming == null)
                    current = null;

                transition.Active = false;
            }
        }

        float finalMultiplier = Mathf.Clamp01(settingsMultiplier * duckMultiplier);
        sourceA.volume = Mathf.Clamp01(baseVolumeA * gainA * finalMultiplier);
        sourceB.volume = Mathf.Clamp01(baseVolumeB * gainB * finalMultiplier);
    }

    internal void Shutdown()
    {
        if (sourceA != null)
        {
            sourceA.Stop();
            sourceA.clip = null;
        }

        if (sourceB != null)
        {
            sourceB.Stop();
            sourceB.clip = null;
        }

        current = null;
        transition.Active = false;
        CurrentSoundId = SoundId.None;
    }

    private static AudioSource CreateSource(Transform parent, string objectName)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(parent, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 32;
        return source;
    }

    private void SetBaseVolume(AudioSource source, float value)
    {
        if (source == sourceA)
            baseVolumeA = value;
        else
            baseVolumeB = value;
    }

    private void SetGain(AudioSource source, float value)
    {
        if (source == sourceA)
            gainA = value;
        else if (source == sourceB)
            gainB = value;
    }

    private float GetGain(AudioSource source)
    {
        return source == sourceA ? gainA : gainB;
    }
}
