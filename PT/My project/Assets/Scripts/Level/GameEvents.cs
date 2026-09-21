using System;
using UnityEngine;

/// <summary>
/// Shared event hub for template-wide level and UI lifecycle notifications.
/// Game-specific features should define their own focused event channels.
/// </summary>
public static class GameEvents
{
    public static event Action<int> OnLevelLoaded;
    public static event Action<int> OnLevelStarted;
    public static event Action<int> OnLevelCompleted;
    public static event Action<int> OnLevelFailed;
    public static event Action<int> OnLevelRetried;

    public static event Action<string> OnScreenOpened;
    public static event Action<string> OnScreenClosed;
    public static event Action<string> OnPopupOpened;
    public static event Action<string> OnPopupClosed;

    public static void RaiseLevelLoaded(int index) => OnLevelLoaded?.Invoke(index);
    public static void RaiseLevelStarted(int index) => OnLevelStarted?.Invoke(index);
    public static void RaiseLevelCompleted(int index) => OnLevelCompleted?.Invoke(index);
    public static void RaiseLevelFailed(int index) => OnLevelFailed?.Invoke(index);
    public static void RaiseLevelRetried(int index) => OnLevelRetried?.Invoke(index);

    public static void RaiseScreenOpened(string id) => OnScreenOpened?.Invoke(id);
    public static void RaiseScreenClosed(string id) => OnScreenClosed?.Invoke(id);
    public static void RaisePopupOpened(string id) => OnPopupOpened?.Invoke(id);
    public static void RaisePopupClosed(string id) => OnPopupClosed?.Invoke(id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ClearAll();
    }

    public static void ClearAll()
    {
        OnLevelLoaded = null;
        OnLevelStarted = null;
        OnLevelCompleted = null;
        OnLevelFailed = null;
        OnLevelRetried = null;
        OnScreenOpened = null;
        OnScreenClosed = null;
        OnPopupOpened = null;
        OnPopupClosed = null;
    }
}
