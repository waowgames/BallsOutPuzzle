using System.Collections;
using UnityEngine;

namespace BallsOut.Publishing
{
    public sealed class CpiAnalyticsBootstrap : MonoBehaviour
    {
        private const string NoAdsKey = "Flick.NoAds";
        private static CpiAnalyticsBootstrap instance;
        private CpiAnalyticsSettings settings;
        private ForegroundPlaytimeTracker playtime;
        private AppsFlyerAnalytics appsFlyer;
        private MetaAnalytics meta;
        private LevelAnalytics levels;
        private bool noAds;
        private bool wasPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null)
                return;

            new GameObject("[Flick CPI]").AddComponent<CpiAnalyticsBootstrap>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<CpiAnalyticsSettings>(CpiAnalyticsSettings.ResourcePath);
            noAds = PlayerPrefs.GetInt(NoAdsKey, 0) == 1;
        }

        private IEnumerator Start()
        {
            if (settings == null || (!settings.EnableAppsFlyer && !settings.EnableMeta && !settings.EnableGameAnalytics))
                yield break;
#if UNITY_EDITOR
            if (!settings.EnableAnalyticsInEditor)
                yield break;
#elif !UNITY_ANDROID && !UNITY_IOS
            yield break;
#endif
            playtime = gameObject.AddComponent<ForegroundPlaytimeTracker>();
            playtime.SegmentEnded += OnSegmentEnded;
            yield return IosTrackingAuthorization.Request();

            if (settings.EnableMeta)
            {
                meta = new MetaAnalytics();
                meta.Initialize(IosTrackingAuthorization.Authorized);
            }
            if (settings.EnableGameAnalytics)
            {
                levels = new LevelAnalytics();
                levels.Initialize();
            }

            InitializeAppsFlyer();
        }

        private async void InitializeAppsFlyer()
        {
            appsFlyer = new AppsFlyerAnalytics();
            appsFlyer.SessionStarted += OnAppsFlyerSessionStarted;
            await appsFlyer.Initialize(settings, gameObject);
        }

        private void OnAppsFlyerSessionStarted()
        {
            if (this != null && playtime != null)
                playtime.BeginTracking();
        }

        private void OnSegmentEnded(int seconds, long total, string reason)
        {
            int stage = LevelManager.Instance != null ? LevelManager.Instance.DisplayedLevel1Based : 1;
            appsFlyer?.SendPlaytime(seconds, total, reason, noAds, stage);
        }

        private void OnApplicationPause(bool paused)
        {
            if (wasPaused && !paused)
                meta?.Resume();
            wasPaused = paused;
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            if (playtime != null)
                playtime.SegmentEnded -= OnSegmentEnded;
            appsFlyer?.Dispose();
            levels?.Dispose();
            instance = null;
        }

        // Call these from the ad SDK's will-show/closed/failed callbacks before it changes focus.
        public static void BeginFullscreenAd() => instance?.playtime?.BeginFullscreenAd();
        public static void EndFullscreenAd() => instance?.playtime?.EndFullscreenAd();

        // Call only after a verified remove-ads purchase or entitlement restore.
        public static void SetNoAds(bool owned)
        {
            if (instance != null)
                instance.noAds = owned;
            PlayerPrefs.SetInt(NoAdsKey, owned ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
