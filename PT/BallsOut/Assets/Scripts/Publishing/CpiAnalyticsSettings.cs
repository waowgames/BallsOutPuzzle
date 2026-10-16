using UnityEngine;

namespace BallsOut.Publishing
{
    [CreateAssetMenu(menuName = "Publishing/Flick CPI Settings", fileName = "CpiAnalyticsSettings")]
    public sealed class CpiAnalyticsSettings : ScriptableObject
    {
        public const string ResourcePath = "Publishing/CpiAnalyticsSettings";

        [Header("AppsFlyer")]
        public bool EnableAppsFlyer = true;
        public string AppsFlyerDevKey = "NsZymPemYQycKKY8A826TU";
        [Tooltip("Numeric Apple App Store ID. Leave empty until an iOS app has been created.")]
        public string IosAppId;

        [Header("Other publisher SDKs")]
        public bool EnableMeta = true;
        [Tooltip("Remains inactive until NEW platform keys are entered in GameAnalytics Settings.")]
        public bool EnableGameAnalytics = true;

        [Tooltip("Disabled by default. Use a real device to validate actual SDK delivery.")]
        public bool EnableAnalyticsInEditor;

        [Header("iOS")]
        [TextArea] public string TrackingUsageDescription =
            "Your permission helps us measure which ads bring players to our game and improve our advertising.";
    }
}
