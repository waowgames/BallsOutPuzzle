using System;

public readonly struct SoundHandle : IEquatable<SoundHandle>
{
    private readonly SoundManager owner;
    internal readonly int OwnerId;
    internal readonly int SlotIndex;
    internal readonly uint Generation;

    internal SoundHandle(SoundManager owner, int ownerId, int slotIndex, uint generation)
    {
        this.owner = owner;
        OwnerId = ownerId;
        SlotIndex = slotIndex;
        Generation = generation;
    }

    public bool IsValid => owner != null && owner.IsHandleValid(this);

    public void Stop(float fadeDuration = 0f)
    {
        if (owner != null)
            owner.Stop(this, fadeDuration);
    }

    public bool Equals(SoundHandle other)
    {
        return ReferenceEquals(owner, other.owner)
            && OwnerId == other.OwnerId
            && SlotIndex == other.SlotIndex
            && Generation == other.Generation;
    }

    public override bool Equals(object obj)
    {
        return obj is SoundHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = owner != null ? owner.GetInstanceID() : 0;
            hash = (hash * 397) ^ OwnerId;
            hash = (hash * 397) ^ SlotIndex;
            hash = (hash * 397) ^ (int)Generation;
            return hash;
        }
    }

    public static bool operator ==(SoundHandle left, SoundHandle right) => left.Equals(right);
    public static bool operator !=(SoundHandle left, SoundHandle right) => !left.Equals(right);
}
