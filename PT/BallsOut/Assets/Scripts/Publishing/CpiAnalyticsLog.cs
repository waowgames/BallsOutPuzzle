using System.Diagnostics;

namespace BallsOut.Publishing
{
    internal static class CpiAnalyticsLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message) => UnityEngine.Debug.Log(message);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Warning(string message) => UnityEngine.Debug.LogWarning(message);
    }
}
