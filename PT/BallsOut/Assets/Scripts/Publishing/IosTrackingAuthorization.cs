using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BallsOut.Publishing
{
    internal static class IosTrackingAuthorization
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int FlickGetTrackingAuthorizationStatus();
        [DllImport("__Internal")] private static extern void FlickRequestTrackingAuthorization();
#endif
        public static bool Authorized
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return FlickGetTrackingAuthorizationStatus() == 3;
#else
                return true;
#endif
            }
        }

        public static IEnumerator Request()
        {
#if UNITY_IOS && !UNITY_EDITOR
            var interval = new WaitForSecondsRealtime(0.2f);
            while (!Application.isFocused)
                yield return interval;

            if (FlickGetTrackingAuthorizationStatus() != 0)
                yield break;

            FlickRequestTrackingAuthorization();
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            while (FlickGetTrackingAuthorizationStatus() == 0 && Time.realtimeSinceStartupAsDouble < deadline)
                yield return interval;
#else
            yield break;
#endif
        }
    }
}
