using System;
using System.Globalization;
using GameAnalyticsSDK;
using UnityEngine;

namespace BallsOut.Publishing
{
    /// <summary>Optional GA progression reporting through the existing event hub.</summary>
    internal sealed class LevelAnalytics
    {
        private bool subscribed;

        public void Initialize()
        {
            try
            {
                var settings = GameAnalytics.SettingsGA;
                int index = settings != null ? settings.Platforms.IndexOf(Application.platform) : -1;
                if (index < 0 || !ValidKey(settings.GetGameKey(index), 32) ||
                    !ValidKey(settings.GetSecretKey(index), 40))
                {
                    CpiAnalyticsLog.Warning("[Flick CPI] GameAnalytics is inactive: new publisher keys are pending.");
                    return;
                }

                if (!GameAnalytics.Initialized)
                {
                    settings.InfoLogBuild = Debug.isDebugBuild;
                    settings.VerboseLogBuild = false;
                    if (UnityEngine.Object.FindFirstObjectByType<GameAnalytics>() == null)
                        new GameObject("[Flick GameAnalytics]").AddComponent<GameAnalytics>();
                    GameAnalytics.Initialize();
                }
                if (!GameAnalytics.Initialized || subscribed) return;

                subscribed = true;
                GameEvents.OnLevelStarted += OnStarted;
                GameEvents.OnLevelCompleted += OnCompleted;
                GameEvents.OnLevelFailed += OnFailed;
                GameEvents.OnLevelRetried += OnRetried;
                if (LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing)
                    OnStarted(LevelManager.Instance.CurrentLevelIndex);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Flick CPI] GameAnalytics initialization failed: " + exception.Message);
            }
        }

        public void Dispose()
        {
            GameEvents.OnLevelStarted -= OnStarted;
            GameEvents.OnLevelCompleted -= OnCompleted;
            GameEvents.OnLevelFailed -= OnFailed;
            GameEvents.OnLevelRetried -= OnRetried;
            subscribed = false;
        }

        private static bool ValidKey(string key, int length)
        {
            if (string.IsNullOrEmpty(key) || key.Length != length) return false;
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f') && !(c >= 'A' && c <= 'F'))
                    return false;
            }
            return true;
        }

        private static string Stage(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);
        private void OnStarted(int index) => Send(GAProgressionStatus.Start, index);
        private void OnCompleted(int index) => Send(GAProgressionStatus.Complete, index);
        private void OnFailed(int index) => Send(GAProgressionStatus.Fail, index);

        private void OnRetried(int index)
        {
            if (LevelManager.Instance != null && LevelManager.Instance.State == LevelState.Playing)
                OnFailed(index);
        }

        private void Send(GAProgressionStatus status, int index)
        {
            try { GameAnalytics.NewProgressionEvent(status, "level", Stage(index)); }
            catch (Exception exception)
            {
                // An optional analytics failure must not interrupt the game's event subscribers.
                Dispose();
                Debug.LogError("[Flick CPI] GameAnalytics reporting disabled: " + exception.Message);
            }
        }
    }
}
