using UnityEngine;
using Voodoo.Tiny.Sauce.Internal.Analytics;
using Voodoo.Tiny.Sauce.Internal.Debugger.Widgets;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Screens
{
    public class TSDebugGameAnalyticsScreen : TSDebugScreen
    {
        private const string DefaultDesignEventName = "DebugUI:GameAnalytics:DesignEvent";
        private const float DefaultDesignEventValue = 42f;

        private const string TestCurrency = "coins";
        private const string TestItemType = "debug";
        private const string TestLevel = "debug_level_1";
        private const string TestAdSdk = "DebugSDK";
        private const string TestAdPlacement = "debug_placement";

        protected override string ScreenTitle => "GameAnalytics";

        protected override void OnScreenShow()
        {
            ClearWidgets();

            AddText("Send test events through TinySauce. Check the Unity console for confirmation logs.");

            var designSection = AddFoldableSection("Design Events");
            var designEventNameInput = designSection.AddTextInput("Event name", DefaultDesignEventName);
            var designEventValueInput = designSection.AddNumberInput("Event value", DefaultDesignEventValue);
            designSection.AddButton("Design Event", () => SendDesignEvent(designEventNameInput, false));
            designSection.AddButton("Design Event (with value)", () => SendDesignEvent(designEventNameInput, true, designEventValueInput));

            var progressionSection = AddFoldableSection("Progression Events");
            progressionSection.AddButton("Progression Start", SendProgressionStart);
            progressionSection.AddButton("Progression Complete", SendProgressionComplete);
            progressionSection.AddButton("Progression Fail", SendProgressionFail);

            var resourceSection = AddFoldableSection("Resource Events");
            resourceSection.AddButton("Resource Source (+10 coins)", SendResourceSource);
            resourceSection.AddButton("Resource Sink (-5 coins)", SendResourceSink);

            var businessSection = AddFoldableSection("Business Events");
            businessSection.AddButton("Business Event (IAP)", SendBusinessEvent);

            var adSection = AddFoldableSection("Ad Events");
            adSection.AddButton("Interstitial Show", SendInterstitialShow);
            adSection.AddButton("Interstitial Click", SendInterstitialClick);
            adSection.AddButton("Rewarded Show", SendRewardedShow);
            adSection.AddButton("Rewarded Click", SendRewardedClick);
        }

        private static void SendDesignEvent(
            TSDebugTextInputWidget eventNameInput,
            bool withValue,
            TSDebugNumberInputWidget eventValueInput = null)
        {
            var eventName = string.IsNullOrWhiteSpace(eventNameInput.GetText())
                ? DefaultDesignEventName
                : eventNameInput.GetText().Trim();

            if (withValue)
            {
                var value = eventValueInput?.GetValue() ?? DefaultDesignEventValue;
                TinySauce.TrackCustomEvent(eventName, value);
                Debug.Log($"[TSDebug] Sent TinySauce design event (value: {value})");
                return;
            }

            TinySauce.TrackCustomEvent(eventName);
            Debug.Log("[TSDebug] Sent TinySauce design event");
        }

        private static void SendProgressionStart()
        {
            TinySauce.OnGameStarted(TestLevel);
            Debug.Log("[TSDebug] Sent TinySauce progression start event");
        }

        private static void SendProgressionComplete()
        {
            TinySauce.OnGameFinished(true, 100, TestLevel);
            Debug.Log("[TSDebug] Sent TinySauce progression complete event");
        }

        private static void SendProgressionFail()
        {
            TinySauce.OnGameFinished(false, 0, TestLevel);
            Debug.Log("[TSDebug] Sent TinySauce progression fail event");
        }

        private static void SendResourceSource()
        {
            TinySauce.OnCurrencyGiven(TestCurrency, 10, TestItemType, "debug_reward");
            Debug.Log("[TSDebug] Sent TinySauce resource source event");
        }

        private static void SendResourceSink()
        {
            TinySauce.OnCurrencyTaken(TestCurrency, 5, TestItemType, "debug_purchase");
            Debug.Log("[TSDebug] Sent TinySauce resource sink event");
        }

        private static void SendBusinessEvent()
        {
            TinySauce.OnIAPPurchase("USD", 99, "Gold Pack", "1000_gold", "debug_cart");
            Debug.Log("[TSDebug] Sent TinySauce business event");
        }

        private static void SendInterstitialShow()
        {
            AnalyticsManager.TrackInterstitialShow(new AdShownEventAnalyticsInfo
            {
                AdNetworkName = TestAdSdk,
                adPlacement = TestAdPlacement
            });
            Debug.Log("[TSDebug] Sent TinySauce interstitial show event");
        }

        private static void SendInterstitialClick()
        {
            AnalyticsManager.TrackInterstitialClick(new AdClickEventAnalyticsInfo
            {
                AdNetworkName = TestAdSdk,
                adPlacement = TestAdPlacement
            });
            Debug.Log("[TSDebug] Sent TinySauce interstitial click event");
        }

        private static void SendRewardedShow()
        {
            AnalyticsManager.TrackRewardedShow(new AdShownEventAnalyticsInfo
            {
                AdNetworkName = TestAdSdk,
                adPlacement = TestAdPlacement
            });
            Debug.Log("[TSDebug] Sent TinySauce rewarded show event");
        }

        private static void SendRewardedClick()
        {
            AnalyticsManager.TrackRewardedClick(new AdClickEventAnalyticsInfo
            {
                AdNetworkName = TestAdSdk,
                adPlacement = TestAdPlacement
            });
            Debug.Log("[TSDebug] Sent TinySauce rewarded click event");
        }
    }
}
