using System;
using System.Globalization;
using UnityEngine;

namespace BallsOut.Publishing
{
    /// <summary>Measures disjoint foreground segments. No per-frame work or network calls.</summary>
    public sealed class ForegroundPlaytimeTracker : MonoBehaviour
    {
        private const string TotalTimeKey = "Flick.TotalForegroundSeconds";
        private bool ready;
        private bool paused;
        private bool focused;
        private bool segmentOpen;
        private bool quitting;
        private int fullscreenAdDepth;
        private double segmentStart;
        private double pendingForegroundSeconds;
        private long totalSeconds;

        public event Action<int, long, string> SegmentEnded;

        private void Awake()
        {
            focused = Application.isFocused;
            long.TryParse(PlayerPrefs.GetString(TotalTimeKey, "0"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out totalSeconds);
            totalSeconds = Math.Max(0L, totalSeconds);
        }

        public void BeginTracking()
        {
            ready = true;
            ResumeSegment();
        }

        public void BeginFullscreenAd()
        {
            if (fullscreenAdDepth++ != 0)
                return;

            // Preserve play before the ad, exclude ad duration, and send nothing for the ad.
            AccumulateSegment();
        }

        public void EndFullscreenAd()
        {
            fullscreenAdDepth = Math.Max(0, fullscreenAdDepth - 1);
            ResumeSegment();
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (value) EndSegment("pause");
            else ResumeSegment();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) EndSegment("background");
            else ResumeSegment();
        }

        private void OnApplicationQuit()
        {
            EndSegment("quit");
            quitting = true;
        }

        private void ResumeSegment()
        {
            if (!ready || paused || !focused || quitting || fullscreenAdDepth > 0 || segmentOpen)
                return;

            segmentStart = Time.realtimeSinceStartupAsDouble;
            segmentOpen = true;
        }

        private void AccumulateSegment()
        {
            if (!segmentOpen)
                return;

            pendingForegroundSeconds += Math.Max(0d, Time.realtimeSinceStartupAsDouble - segmentStart);
            segmentOpen = false;
        }

        private void EndSegment(string reason)
        {
            if (!ready || fullscreenAdDepth > 0 || !segmentOpen)
                return;

            // Closing the segment BEFORE notifying deduplicates Pause + Focus + Quit callbacks.
            AccumulateSegment();
            int seconds = (int)Math.Min(int.MaxValue, Math.Floor(pendingForegroundSeconds));
            pendingForegroundSeconds = 0d;
            if (seconds <= 0)
                return;

            totalSeconds += seconds;
            PlayerPrefs.SetString(TotalTimeKey, totalSeconds.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            SegmentEnded?.Invoke(seconds, totalSeconds, reason);
        }
    }
}
