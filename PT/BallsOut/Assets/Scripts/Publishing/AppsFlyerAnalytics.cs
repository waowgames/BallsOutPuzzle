using System;
using System.Collections.Generic;
using System.Globalization;
using AppsFlyerSDK;
using UnityEngine;

namespace BallsOut.Publishing
{
    internal sealed class AppsFlyerAnalytics
    {
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private bool disposed;
        private bool startingSession;
        public bool IsReady { get; private set; }
        public event Action SessionStarted;

        public async Awaitable<bool> Initialize(CpiAnalyticsSettings settings, GameObject owner)
        {
            if (!settings.EnableAppsFlyer || string.IsNullOrWhiteSpace(settings.AppsFlyerDevKey))
                return false;

#if UNITY_IOS && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(settings.IosAppId))
            {
                CpiAnalyticsLog.Warning("[Flick CPI] Set the Apple App Store ID before an iOS CPI build.");
                return false;
            }
#endif
            try
            {
                var bridge = owner.AddComponent<AppsFlyer>();
                AppsFlyer.OnSessionReady += OnSessionReady;
                await AppsFlyer.enableDebug(Debug.isDebugBuild);
                await AppsFlyer.init(settings.AppsFlyerDevKey, settings.IosAppId, bridge);
                if (!AppsFlyer.isRPCBridgeAvailable())
                {
                    Debug.LogError("[Flick CPI] AppsFlyer native bridge is missing. Resolve Android SDK dependencies.");
                    Dispose();
                    return false;
                }

                if (disposed) return false;
                // SDK 7 signals readiness for every foreground session. Keep the listener
                // until disposal and start each session from its callback, after ATT.
                await AppsFlyer.registerSessionReadyListener();
                return !disposed;
            }
            catch (Exception exception)
            {
                Dispose();
                Debug.LogError("[Flick CPI] AppsFlyer initialization failed: " + exception.Message);
                return false;
            }
        }

        private async void OnSessionReady(object sender, EventArgs args)
        {
            if (disposed || startingSession) return;
            startingSession = true;
            try
            {
                await AppsFlyer.start();
                if (disposed) return;
                IsReady = true;
                CpiAnalyticsLog.Log("[Flick CPI] AppsFlyer session started; native delivery is asynchronous.");
                SessionStarted?.Invoke();
            }
            catch (Exception exception)
            {
                IsReady = false;
                Debug.LogError("[Flick CPI] AppsFlyer session could not start: " + exception.Message);
            }
            finally
            {
                startingSession = false;
            }
        }

        public void Dispose()
        {
            disposed = true;
            IsReady = false;
            AppsFlyer.OnSessionReady -= OnSessionReady;
            SessionStarted = null;
        }

        public async void SendPlaytime(int seconds, long totalSeconds, string reason, bool noAds, int stage)
        {
            if (!IsReady)
                return;

            var payload = new Dictionary<string, string>(6)
            {
                { "playtime", seconds.ToString(CultureInfo.InvariantCulture) },
                { "total_play_time", totalSeconds.ToString(CultureInfo.InvariantCulture) },
                { "session_id", sessionId },
                { "reason", reason },
                { "no_ads", noAds ? "1" : "0" },
                { "current_stage", stage.ToString(CultureInfo.InvariantCulture) }
            };

            try
            {
                // Native SDK queues offline events. Do not wait for HTTP during a pause/quit callback.
                await AppsFlyer.logEvent("f_custom_playtime", payload);
                CpiAnalyticsLog.Log("[Flick CPI] f_custom_playtime: " + seconds + "s, " + reason);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Flick CPI] Playtime could not be queued: " + exception.Message);
            }
        }
    }
}
