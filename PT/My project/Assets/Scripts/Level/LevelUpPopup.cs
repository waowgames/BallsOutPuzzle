using AssetKits.ParticleImage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelUpPopup : UIPopup
{
    public static LevelUpPopup Instance { get; private set; }

    [Header("Presentation")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text rewardAmountText;
    [SerializeField] private Button getButton;
    [SerializeField] private ParticleImage particleImage;

    [Header("Reward")]
    [SerializeField] private LevelRewardConfig rewardConfig;
    [SerializeField] private FlyToUIEffect flyEffect;

    private int pendingReward;
    private bool rewardClaimed;

    public override string PopupId => nameof(LevelUpPopup);

    protected override void Awake()
    {
        Instance = this;
        base.Awake();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelCompleted += HandleLevelCompleted;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelCompleted -= HandleLevelCompleted;

        if (getButton != null)
            getButton.onClick.RemoveListener(HandleCollectClicked);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    protected override void OnShow()
    {
        transform.localScale = Vector3.one;
        if (getButton == null)
            return;

        getButton.interactable = true;
        getButton.onClick.RemoveListener(HandleCollectClicked);
        getButton.onClick.AddListener(HandleCollectClicked);
    }

    protected override void OnHide()
    {
        if (getButton != null)
            getButton.onClick.RemoveListener(HandleCollectClicked);
    }

    private void HandleLevelCompleted(int index)
    {
        pendingReward = rewardConfig != null
            ? rewardConfig.GetReward(index)
            : 1;
        rewardClaimed = false;

        if (levelText != null)
            levelText.SetText("LEVEL {0}", index + 1);

        if (rewardAmountText != null)
            rewardAmountText.SetText("+{0}", pendingReward);

        Show();
    }

    private void HandleCollectClicked()
    {
        if (rewardClaimed)
            return;

        rewardClaimed = true;
        if (getButton != null)
            getButton.interactable = false;

        CurrencyWallet.Instance?.Add(pendingReward);
        particleImage?.Play();
        Hide();

        if (flyEffect != null)
            flyEffect.Play(ContinueToNextLevel);
        else
            ContinueToNextLevel();
    }

    private static void ContinueToNextLevel()
    {
        GameFlowController.Instance?.ContinueToNextLevel();
    }
}
