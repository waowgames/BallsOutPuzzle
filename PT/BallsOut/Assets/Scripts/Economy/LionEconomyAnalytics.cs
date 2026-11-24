using LionStudios.Suite.Analytics;
using UnityEngine;

internal static class LionEconomyAnalytics
{
    public const string BoosterPopup = "booster_popup";
    public const string BoosterBar = "booster_bar";
    private const string OpeningInventoryKey = "ballsout.analytics.opening_inventory";

    // The Magnet slot still uses the original save ID.
    public static string BoosterName(string id) => id == "ball_shuffle" ? "magnet" : id;

    private static VirtualCurrency Coins(int amount) => new VirtualCurrency("coin", "soft", amount);
    private static Item Booster(string id, int amount) => new Item(BoosterName(id), "power_up", amount);
    private static string OfferName(string id) => "buy_" + BoosterName(id);

    public static void ReportOpeningInventory()
    {
        var save = SaveService.Instance;
        if (save == null || PlayerPrefs.HasKey(OpeningInventoryKey)) return;

        var inventory = new Product();
        if (save.SoftCurrency > 0) inventory.AddVirtualCurrency(Coins(save.SoftCurrency));
        foreach (var entry in save.SavedBoosters)
        {
            if (entry.count > 0) inventory.AddItem(Booster(entry.id, entry.count));
        }
        if (inventory.items.Count > 0 || inventory.virtualCurrencies.Count > 0)
            LionAnalytics.Economy("opening_inventory", null, inventory, "game_start", LionMissionAnalytics.EconomyData());
        PlayerPrefs.SetInt(OpeningInventoryKey, 1);
        PlayerPrefs.Save();
    }

    public static void OfferShown(string id, string placement) =>
        LionAnalytics.UiInteraction("impression", OfferName(id), placement);

    public static void OfferClicked(string id, string placement) =>
        LionAnalytics.UiInteraction("click", OfferName(id), placement, uiType: "coin");

    public static void Purchased(string id, int price, int quantity, string placement) =>
        LionAnalytics.Economy(OfferName(id), Coins(price), Booster(id, quantity), placement, LionMissionAnalytics.EconomyData());

    public static void Gifted(string id, int quantity) =>
        LionAnalytics.Economy("tutorial_gift", null, Booster(id, quantity), "tutorial", LionMissionAnalytics.EconomyData());

    public static void LevelReward(int amount) =>
        LionAnalytics.Economy("level_reward", null, Coins(amount), "level_complete", LionMissionAnalytics.EconomyData());
}
