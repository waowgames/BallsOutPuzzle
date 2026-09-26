using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns screen registration, popup stacking, and global UI input blocking.
/// </summary>
public sealed class UIManager : SingletonMonoBehaviour<UIManager>
{
    [Header("Input Blocking")]
    [SerializeField] private CanvasGroup inputBlocker;

    private readonly Dictionary<string, UIScreen> screens =
        new Dictionary<string, UIScreen>();
    private readonly List<UIPopup> popupStack = new List<UIPopup>();
    private readonly HashSet<Object> gameplayBlockers = new HashSet<Object>();

    public bool HasActivePopup => popupStack.Count > 0;

    /// <summary>
    /// True while a popup or a full-screen presentation (e.g. the hard-level intro)
    /// should pause the level clock and board input.
    /// </summary>
    public bool IsGameplayBlocked => HasActivePopup || gameplayBlockers.Count > 0;

    public void SetGameplayBlocked(Object owner, bool blocked)
    {
        if (owner == null)
            return;

        if (blocked) gameplayBlockers.Add(owner);
        else gameplayBlockers.Remove(owner);
    }

    protected override void Awake()
    {
        base.Awake();
        SetInputBlocked(false);
    }

    public void RegisterScreen(UIScreen screen)
    {
        if (screen != null)
            screens[screen.ScreenId] = screen;
    }

    public void UnregisterScreen(UIScreen screen)
    {
        if (screen != null)
            screens.Remove(screen.ScreenId);
    }

    public bool ShowScreen(string screenId)
    {
        if (!screens.TryGetValue(screenId, out UIScreen screen))
        {
            DebugLogger.LogWarning($"[UIManager] Screen '{screenId}' not found.");
            return false;
        }

        screen.Show();
        return true;
    }

    public bool HideScreen(string screenId)
    {
        if (!screens.TryGetValue(screenId, out UIScreen screen))
            return false;

        screen.Hide();
        return true;
    }

    public void HideAllScreens()
    {
        foreach (UIScreen screen in screens.Values)
            screen.Hide();
    }

    public UIScreen GetScreen(string screenId)
    {
        screens.TryGetValue(screenId, out UIScreen screen);
        return screen;
    }

    public T GetScreen<T>(string screenId) where T : UIScreen
    {
        return GetScreen(screenId) as T;
    }

    internal void PushPopup(UIPopup popup)
    {
        if (popup == null || popupStack.Contains(popup))
            return;

        popupStack.Add(popup);
        RefreshInputBlocker();
    }

    internal void RemovePopup(UIPopup popup)
    {
        if (popup == null)
            return;

        popupStack.Remove(popup);
        RefreshInputBlocker();
    }

    public void CloseTopPopup()
    {
        if (popupStack.Count == 0)
            return;

        popupStack[popupStack.Count - 1].Hide();
    }

    public void CloseAllPopups()
    {
        while (popupStack.Count > 0)
            popupStack[popupStack.Count - 1].Hide();
    }

    public void SetInputBlocked(bool blocked)
    {
        if (inputBlocker == null)
            return;

        inputBlocker.gameObject.SetActive(blocked);
        inputBlocker.alpha = 0f;
        inputBlocker.interactable = false;
        inputBlocker.blocksRaycasts = blocked;
    }

    private void RefreshInputBlocker()
    {
        SetInputBlocked(HasActivePopup);
    }
}
