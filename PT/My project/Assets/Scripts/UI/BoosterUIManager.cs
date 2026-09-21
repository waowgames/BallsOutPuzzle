using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class BoosterUIManager : MonoBehaviour
{
    [SerializeField] private BoosterSlot[] boosterSlots = Array.Empty<BoosterSlot>();

    private void OnEnable()
    {
        foreach (BoosterSlot slot in boosterSlots)
            slot?.Setup(this);

        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged += HandleBalanceChanged;

        RefreshAll();
    }

    private void OnDisable()
    {
        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;

        foreach (BoosterSlot slot in boosterSlots)
            slot?.Teardown();
    }

    private void HandleBalanceChanged(int _)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (BoosterSlot slot in boosterSlots)
            slot?.Refresh();
    }

    internal bool TrySpend(int cost)
    {
        return CurrencyWallet.Instance != null &&
               CurrencyWallet.Instance.TrySpend(cost);
    }

    [Serializable]
    private sealed class BoosterSlot
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private Button boosterButton;
        [SerializeField] private UnityEvent onBoosterTriggered;
        [SerializeField, Min(0)] private int price = 10;

        [Header("Purchase UI")]
        [SerializeField] private GameObject purchaseContainer;
        [SerializeField] private GameObject watchIcon;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI priceLabel;

        [Header("Owned UI")]
        [SerializeField] private GameObject ownedContainer;
        [SerializeField] private TextMeshProUGUI ownedCountLabel;

        private BoosterUIManager owner;
        private int ownedCount;

        public void Setup(BoosterUIManager slotOwner)
        {
            owner = slotOwner;
            ownedCount = LoadOwnedCount();

            if (boosterButton != null)
            {
                boosterButton.onClick.RemoveListener(UseBooster);
                boosterButton.onClick.AddListener(UseBooster);
            }

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(PurchaseBooster);
                purchaseButton.onClick.AddListener(PurchaseBooster);
            }

            Refresh();
        }

        public void Teardown()
        {
            if (boosterButton != null)
                boosterButton.onClick.RemoveListener(UseBooster);

            if (purchaseButton != null)
                purchaseButton.onClick.RemoveListener(PurchaseBooster);

            owner = null;
        }

        public void Refresh()
        {
            ownedCount = LoadOwnedCount();
            bool hasBooster = ownedCount > 0;

            purchaseContainer?.SetActive(!hasBooster);
            ownedContainer?.SetActive(hasBooster);
            watchIcon?.SetActive(false);

            if (ownedCountLabel != null)
                ownedCountLabel.SetText("{0}", ownedCount);

            if (priceLabel != null)
                priceLabel.SetText("{0}", price);

            if (purchaseButton != null)
                purchaseButton.interactable = CanAffordBooster();

            if (boosterButton != null)
                boosterButton.interactable = hasBooster;
        }

        private void UseBooster()
        {
            if (ownedCount <= 0)
                return;

            ownedCount--;
            SaveOwnedCount();
            Refresh();
            onBoosterTriggered?.Invoke();
        }

        private void PurchaseBooster()
        {
            if (owner != null && owner.TrySpend(price))
                GrantBooster();
            else
                Refresh();
        }

        private int LoadOwnedCount()
        {
            string stableId = ResolveId();
            return SaveService.Instance != null
                ? SaveService.Instance.GetBoosterCount(stableId)
                : 0;
        }

        private void SaveOwnedCount()
        {
            SaveService.Instance?.SetBoosterCount(ResolveId(), ownedCount);
        }

        private bool CanAffordBooster()
        {
            return CurrencyWallet.Instance != null &&
                   CurrencyWallet.Instance.Balance >= price;
        }

        private void GrantBooster()
        {
            ownedCount++;
            SaveOwnedCount();
            Refresh();
        }

        private string ResolveId()
        {
            if (!string.IsNullOrWhiteSpace(id))
                return id;

            return boosterButton != null
                ? boosterButton.name
                : string.Empty;
        }
    }
}
