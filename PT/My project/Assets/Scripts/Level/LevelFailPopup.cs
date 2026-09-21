using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic fail popup. Game-specific revive flows belong in separate features.
/// </summary>
public sealed class LevelFailPopup : UIPopup
{
    public static LevelFailPopup Instance { get; private set; }

    [SerializeField] private Button retryButton;

    public override string PopupId => nameof(LevelFailPopup);

    public static bool ShowIfAvailable()
    {
        LevelFailPopup popup = Instance != null
            ? Instance
            : FindFirstObjectByType<LevelFailPopup>(FindObjectsInactive.Include);

        if (popup == null)
            return false;

        if (!popup.gameObject.activeSelf)
            popup.gameObject.SetActive(true);

        popup.Show();
        return true;
    }

    protected override void Awake()
    {
        Instance = this;
        base.Awake();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelFailed += HandleLevelFailed;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelFailed -= HandleLevelFailed;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    protected override void OnShow()
    {
        transform.localScale = Vector3.one;

        if (retryButton == null)
            return;

        retryButton.onClick.RemoveListener(HandleRetryClicked);
        retryButton.onClick.AddListener(HandleRetryClicked);
    }

    protected override void OnHide()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(HandleRetryClicked);
    }

    private void HandleRetryClicked()
    {
        Hide();
        GameFlowController.Instance?.RetryCurrentLevel();
    }

    private void HandleLevelFailed(int levelIndex)
    {
        Show();
    }
}
