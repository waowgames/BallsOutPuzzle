using System;

public enum SoundPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum SoundOverlapMode
{
    Allow,
    IgnoreIfPlaying,
    Restart,
    LimitInstances,
    ReplaceOldest
}

public enum SoundCategory
{
    Sfx,
    Ui,
    Music,
    Ambience
}

[Flags]
public enum DuckTarget
{
    None = 0,
    Music = 1,
    NonCriticalSfx = 2
}
