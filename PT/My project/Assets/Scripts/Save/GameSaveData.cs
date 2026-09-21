using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public int currentLevelIndex;
    public int softCurrency;
    public bool musicEnabled = true;
    public bool sfxEnabled = true;
    public bool vibrationEnabled = true;
    public List<BoosterSaveEntry> boosters = new List<BoosterSaveEntry>();

    public void Sanitize()
    {
        version = Mathf.Max(1, version);
        currentLevelIndex = Mathf.Max(0, currentLevelIndex);
        softCurrency = Mathf.Max(0, softCurrency);
        boosters ??= new List<BoosterSaveEntry>();

        for (int i = boosters.Count - 1; i >= 0; i--)
        {
            BoosterSaveEntry entry = boosters[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
            {
                boosters.RemoveAt(i);
                continue;
            }

            entry.count = Mathf.Max(0, entry.count);
        }
    }

    public int GetBoosterCount(string id)
    {
        BoosterSaveEntry entry = FindBooster(id);
        return entry != null ? entry.count : 0;
    }

    public bool TryGetBoosterCount(string id, out int count)
    {
        BoosterSaveEntry entry = FindBooster(id);
        count = entry != null ? entry.count : 0;
        return entry != null;
    }

    public void SetBoosterCount(string id, int count)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        BoosterSaveEntry entry = FindBooster(id);
        if (entry == null)
        {
            entry = new BoosterSaveEntry { id = id };
            boosters.Add(entry);
        }

        entry.count = Mathf.Max(0, count);
    }

    private BoosterSaveEntry FindBooster(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || boosters == null)
            return null;

        for (int i = 0; i < boosters.Count; i++)
        {
            BoosterSaveEntry entry = boosters[i];
            if (entry != null &&
                string.Equals(entry.id, id, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }
}

[Serializable]
public sealed class BoosterSaveEntry
{
    public string id;
    public int count;
}
