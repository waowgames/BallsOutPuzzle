using System;
using Facebook.Unity;
using UnityEngine;

namespace BallsOut.Publishing
{
    internal sealed class MetaAnalytics
    {
        private bool initialized;
        private bool trackingAuthorized;

        public void Initialize(bool authorized)
        {
            trackingAuthorized = authorized;
            try
            {
                if (FB.IsInitialized) OnInitialized();
                else FB.Init(OnInitialized);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Flick CPI] Meta initialization failed: " + exception.Message);
            }
        }

        private void OnInitialized()
        {
            try
            {
                initialized = FB.IsInitialized;
                if (!initialized)
                {
                    Debug.LogError("[Flick CPI] Meta SDK could not initialize.");
                    return;
                }
#if UNITY_IOS && !UNITY_EDITOR
                FB.Mobile.SetAdvertiserTrackingEnabled(trackingAuthorized);
#endif
                FB.ActivateApp();
                CpiAnalyticsLog.Log("[Flick CPI] Meta initialized and app activation recorded.");
            }
            catch (Exception exception)
            {
                initialized = false;
                Debug.LogError("[Flick CPI] Meta activation failed: " + exception.Message);
            }
        }

        public void Resume()
        {
            if (!initialized) return;
            try { FB.ActivateApp(); }
            catch (Exception exception)
            {
                Debug.LogError("[Flick CPI] Meta resume failed: " + exception.Message);
            }
        }
    }
}
