using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What a booster purchase popup offers: shown when the player has none left.</summary>
public readonly struct BoosterOffer
{
    public readonly string Title;
    public readonly string Description;
    public readonly Sprite Icon;
    public readonly int Amount;
    public readonly int Price;

    public BoosterOffer(string title, string description, Sprite icon, int amount, int price)
    {
        Title = title;
        Description = description;
        Icon = icon;
        Amount = amount;
        Price = price;
    }
}

/// <summary>
/// Shared "get booster" popup. Buying spends coins through the supplied callback.
/// </summary>
public sealed class BoosterPurchasePopup : UIPopup
{
    public static BoosterPurchasePopup Instance { get; private set; }

    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button closeButton;

    private BoosterOffer offer;
    private Func<bool> purchase;

    public override string PopupId => nameof(BoosterPurchasePopup);

    public static bool ShowIfAvailable(BoosterOffer offer, Func<bool> purchase)
    {
        BoosterPurchasePopup popup = Instance != null
            ? Instance
            : FindFirstObjectByType<BoosterPurchasePopup>(FindObjectsInactive.Include);

        if (popup == null)
            return false;

        if (!popup.gameObject.activeSelf)
            popup.gameObject.SetActive(true);

        popup.Open(offer, purchase);
        return true;
    }

    protected override void Awake()
    {
        Instance = this;
        base.Awake();

        if (buyButton != null) buyButton.onClick.AddListener(HandleBuyClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;
    }

    public void Open(BoosterOffer boosterOffer, Func<bool> purchaseCallback)
    {
        offer = boosterOffer;
        purchase = purchaseCallback;

        if (titleText != null) titleText.text = offer.Title;
        if (descriptionText != null) descriptionText.text = offer.Description;
        if (amountText != null) amountText.SetText("x{0}", offer.Amount);
        if (priceText != null) priceText.SetText("{0}", offer.Price);
        if (iconImage != null) iconImage.sprite = offer.Icon;

        RefreshAffordability();
        Show();
    }

    protected override void OnShow()
    {
        if (CurrencyWallet.Instance != null)
        {
            CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;
            CurrencyWallet.Instance.BalanceChanged += HandleBalanceChanged;
        }

        if (panel == null)
            return;

        panel.DOKill();
        panel.localScale = Vector3.one * 0.8f;
        panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    protected override void OnHide()
    {
        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged(int _)
    {
        RefreshAffordability();
    }

    private void RefreshAffordability()
    {
        if (buyButton != null)
            buyButton.interactable = CurrencyWallet.Instance != null &&
                                     CurrencyWallet.Instance.Balance >= offer.Price;
    }

    private void HandleBuyClicked()
    {
        if (purchase != null && purchase())
        {
            Hide();
            return;
        }

        RefreshAffordability();
        if (panel != null)
            panel.DOPunchPosition(Vector3.right * 20f, 0.3f, 12).SetUpdate(true);
    }
}
