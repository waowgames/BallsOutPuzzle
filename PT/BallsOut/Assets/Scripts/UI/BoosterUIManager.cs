using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>What the tutorial needs to know about one booster slot.</summary>
public readonly struct BoosterInfo
{
    public readonly string Id;
    public readonly int UnlockLevel;
    public readonly Button Button;
    public readonly string DisplayName;
    public readonly string Description;
    public readonly Sprite Icon;

    public BoosterInfo(string id, int unlockLevel, Button button, string displayName, string description, Sprite icon)
    {
        Id = id;
        UnlockLevel = unlockLevel;
        Button = button;
        DisplayName = displayName;
        Description = description;
        Icon = icon;
    }
}

public sealed class BoosterUIManager : MonoBehaviour
{
    [SerializeField] private BoosterSlot[] boosterSlots = Array.Empty<BoosterSlot>();

    [Header("Locked Visual")]
    [SerializeField] private Sprite lockedFrameSprite;
    [SerializeField] private Sprite lockIconSprite;
    [SerializeField] private string lockedLabelFormat = "Lv.{0}";

    internal Sprite LockedFrameSprite => lockedFrameSprite;
    internal Sprite LockIconSprite => lockIconSprite;
    internal string LockedLabelFormat => lockedLabelFormat;

    private static readonly List<BoosterUIManager> Enabled = new List<BoosterUIManager>();

    /// <summary>Raised with the booster id whenever a booster is actually spent.</summary>
    public static event Action<string> OnBoosterUsed;

    /// <summary>Every booster slot on the enabled booster bars.</summary>
    public static List<BoosterInfo> GetBoosters()
    {
        var result = new List<BoosterInfo>();
        foreach (BoosterUIManager manager in Enabled)
            foreach (BoosterSlot slot in manager.boosterSlots)
                if (slot != null) result.Add(slot.Info);
        return result;
    }

    /// <summary>Adds free uses of a booster (tutorial gift) and refreshes its slot.</summary>
    public static bool GrantFree(string id, int amount)
    {
        foreach (BoosterUIManager manager in Enabled)
            foreach (BoosterSlot slot in manager.boosterSlots)
                if (slot != null && slot.Info.Id == id)
                {
                    slot.Grant(amount);
                    return true;
                }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled.Clear();
        OnBoosterUsed = null;
    }

    private void OnEnable()
    {
        if (!Enabled.Contains(this))
            Enabled.Add(this);

        foreach (BoosterSlot slot in boosterSlots)
            slot?.Setup(this);

        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged += HandleBalanceChanged;

        GameEvents.OnLevelLoaded += HandleLevelLoaded;

        RefreshAll();
    }

    private void OnDisable()
    {
        Enabled.Remove(this);

        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;

        GameEvents.OnLevelLoaded -= HandleLevelLoaded;

        foreach (BoosterSlot slot in boosterSlots)
            slot?.Teardown();
    }

    private void HandleBalanceChanged(int _)
    {
        RefreshAll();
    }

    private void HandleLevelLoaded(int _)
    {
        RefreshAll();
    }

    internal static int CurrentLevelNumber()
    {
        if (LevelManager.Instance != null)
            return LevelManager.Instance.DisplayedLevel1Based;

        return SaveService.Instance != null
            ? SaveService.Instance.CurrentLevelIndex + 1
            : 1;
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
        [SerializeField, Min(1)] private int unlockLevel = 1;
        [SerializeField] private Button boosterButton;
        [SerializeField] private UnityEvent onBoosterTriggered;
        [SerializeField, Min(0)] private int price = 10;

        [Header("Purchase Popup")]
        [SerializeField] private string displayName = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int purchaseAmount = 1;

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
        private GameObject lockedOverlay;
        private TextMeshProUGUI lockedLabel;

        private bool IsLocked => BoosterUIManager.CurrentLevelNumber() < unlockLevel;

        public BoosterInfo Info => new BoosterInfo(ResolveId(), unlockLevel, boosterButton, displayName, description, icon);

        public void Grant(int amount)
        {
            if (amount > 0)
                GrantBooster(amount);
        }

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
            bool locked = IsLocked;

            if (purchaseContainer != null) purchaseContainer.SetActive(!locked && !hasBooster);
            if (ownedContainer != null) ownedContainer.SetActive(!locked && hasBooster);
            RefreshLockedOverlay(locked);
            if (watchIcon != null) watchIcon.SetActive(false);

            if (ownedCountLabel != null)
                ownedCountLabel.SetText("{0}", ownedCount);

            if (priceLabel != null)
                priceLabel.SetText("{0}", price);

            if (purchaseButton != null)
                purchaseButton.interactable = CanAffordBooster();

            // An empty booster stays clickable: it opens the purchase popup.
            if (boosterButton != null)
                boosterButton.interactable = true;
        }

        private void UseBooster()
        {
            if (IsLocked)
            {
                GameHaptics.Warning();
                return;
            }

            if (ownedCount <= 0)
            {
                OpenPurchasePopup();
                return;
            }

            LevelManager manager = LevelManager.Instance;
            if (manager != null && manager.State != LevelState.Playing)
                return;

            // Never spend a booster that would do nothing right now.
            IBoosterAvailability availability = boosterButton != null
                ? boosterButton.GetComponent<IBoosterAvailability>()
                : null;
            if (availability != null && !availability.CanUseBooster())
            {
                GameHaptics.Warning();
                return;
            }

            // Targeted boosters spend only once the player has picked a target.
            ITargetedBooster targeted = boosterButton != null
                ? boosterButton.GetComponent<ITargetedBooster>()
                : null;
            if (targeted != null)
            {
                GameHaptics.Light();
                if (!targeted.BeginTargeting(Spend))
                    GameHaptics.Warning();
                return;
            }

            Spend();
        }

        private void Spend()
        {
            if (ownedCount <= 0)
                return;

            ownedCount--;
            SaveOwnedCount();
            Refresh();
            GameHaptics.Medium();
            onBoosterTriggered?.Invoke();
            OnBoosterUsed?.Invoke(ResolveId());
        }

        private void PurchaseBooster()
        {
            if (IsLocked)
                return;

            TryPurchase();
        }

        private void RefreshLockedOverlay(bool locked)
        {
            if (!locked)
            {
                if (lockedOverlay != null)
                    lockedOverlay.SetActive(false);
                return;
            }

            EnsureLockedOverlay();
            if (lockedOverlay == null)
                return;

            lockedOverlay.SetActive(true);
            lockedOverlay.transform.SetAsLastSibling();

            if (lockedLabel != null)
                lockedLabel.SetText(string.Format(owner.LockedLabelFormat, unlockLevel));
        }

        // Built at runtime over the booster button so every slot shares the same locked look.
        private void EnsureLockedOverlay()
        {
            if (lockedOverlay != null || boosterButton == null || owner == null)
                return;

            RectTransform root = CreateUIChild("Locked", boosterButton.transform, Vector2.zero, Vector2.one);
            Image frame = root.gameObject.AddComponent<Image>();
            frame.sprite = owner.LockedFrameSprite;
            frame.raycastTarget = false;
            frame.enabled = frame.sprite != null;

            RectTransform iconRect = CreateUIChild("Lock Icon", root, new Vector2(0.22f, 0.3f), new Vector2(0.78f, 0.88f));
            Image lockIcon = iconRect.gameObject.AddComponent<Image>();
            lockIcon.sprite = owner.LockIconSprite;
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockIcon.enabled = lockIcon.sprite != null;

            RectTransform labelRect = CreateUIChild("Level Txt", root, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.32f));
            lockedLabel = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (ownedCountLabel != null)
            {
                lockedLabel.font = ownedCountLabel.font;
                lockedLabel.fontSharedMaterial = ownedCountLabel.fontSharedMaterial;
            }
            lockedLabel.alignment = TextAlignmentOptions.Center;
            lockedLabel.enableAutoSizing = true;
            lockedLabel.fontSizeMin = 10f;
            lockedLabel.fontSizeMax = 60f;
            lockedLabel.color = Color.white;
            lockedLabel.raycastTarget = false;

            lockedOverlay = root.gameObject;
        }

        private static RectTransform CreateUIChild(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.layer = parent.gameObject.layer;
            return rect;
        }

        private void OpenPurchasePopup()
        {
            var offer = new BoosterOffer(displayName, description, icon, purchaseAmount, price);
            BoosterPurchasePopup.ShowIfAvailable(offer, TryPurchase);
        }

        private bool TryPurchase()
        {
            if (owner != null && owner.TrySpend(price))
            {
                GrantBooster(purchaseAmount);
                return true;
            }

            Refresh();
            return false;
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

        private void GrantBooster(int amount)
        {
            ownedCount += amount;
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
