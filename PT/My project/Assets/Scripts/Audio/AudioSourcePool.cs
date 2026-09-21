using System.Collections.Generic;
using UnityEngine;

internal sealed class AudioSourcePool
{
    private List<PooledAudioSource> slots;
    private Transform parent;
    private int maxSize;
    private bool allowGrowth;
    private int activeCount;

    internal int Count => slots != null ? slots.Count : 0;
    internal int ActiveCount => activeCount;
    internal int IdleCount => Count - activeCount;

    internal void Initialize(Transform sourceParent, int initialSize, int maximumSize, bool canGrow)
    {
        parent = sourceParent;
        maxSize = Mathf.Max(1, maximumSize);
        allowGrowth = canGrow;
        slots = new List<PooledAudioSource>(maxSize);
        activeCount = 0;

        int createCount = Mathf.Clamp(initialSize, 1, maxSize);
        for (int i = 0; i < createCount; i++)
            slots.Add(new PooledAudioSource(parent, i));
    }

    internal bool TryAcquireIdle(out int slotIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].Active)
            {
                slotIndex = i;
                return true;
            }
        }

        slotIndex = -1;
        return false;
    }

    internal bool TryGrow(out int slotIndex)
    {
        if (!allowGrowth || slots.Count >= maxSize)
        {
            slotIndex = -1;
            return false;
        }

        slotIndex = slots.Count;
        slots.Add(new PooledAudioSource(parent, slotIndex));
        return true;
    }

    internal PooledAudioSource GetSlot(int index)
    {
        return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    internal void Activate(int index)
    {
        PooledAudioSource slot = GetSlot(index);
        if (slot == null || slot.Active)
            return;

        slot.Active = true;
        activeCount++;
        slot.GameObject.SetActive(true);
    }

    internal void Release(int index)
    {
        PooledAudioSource slot = GetSlot(index);
        if (slot == null || !slot.Active)
            return;

        activeCount = Mathf.Max(0, activeCount - 1);
        slot.Generation++;
        if (slot.Generation == 0)
            slot.Generation = 1;
        slot.ResetForIdle();
    }

    internal void ReleaseAll()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Active)
                Release(i);
        }
    }

    internal int CountInstances(int definitionIndex)
    {
        int count = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            PooledAudioSource slot = slots[i];
            if (slot.Active && slot.DefinitionIndex == definitionIndex)
                count++;
        }

        return count;
    }

    internal int FindOldestInstance(int definitionIndex)
    {
        int candidate = -1;
        ulong oldestSequence = ulong.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            PooledAudioSource slot = slots[i];
            if (!slot.Active || slot.DefinitionIndex != definitionIndex)
                continue;

            if (slot.StartSequence < oldestSequence)
            {
                oldestSequence = slot.StartSequence;
                candidate = i;
            }
        }

        return candidate;
    }

    internal int FindExistingLoop(int definitionIndex, Transform target)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            PooledAudioSource slot = slots[i];
            if (slot.Active && slot.Loop && slot.DefinitionIndex == definitionIndex && slot.FollowTarget == target)
                return i;
        }

        return -1;
    }

    internal int FindStealCandidate(SoundPriority incomingPriority)
    {
        int candidate = -1;
        SoundPriority candidatePriority = SoundPriority.Critical;
        ulong oldestSequence = ulong.MaxValue;

        for (int i = 0; i < slots.Count; i++)
        {
            PooledAudioSource slot = slots[i];
            if (!slot.Active || slot.Priority >= incomingPriority)
                continue;

            if (candidate < 0
                || slot.Priority < candidatePriority
                || (slot.Priority == candidatePriority && slot.StartSequence < oldestSequence))
            {
                candidate = i;
                candidatePriority = slot.Priority;
                oldestSequence = slot.StartSequence;
            }
        }

        return candidate;
    }

    internal bool IsHandleValid(int ownerId, in SoundHandle handle)
    {
        if (handle.OwnerId != ownerId)
            return false;

        PooledAudioSource slot = GetSlot(handle.SlotIndex);
        return slot != null && slot.Active && slot.Generation == handle.Generation;
    }
}
