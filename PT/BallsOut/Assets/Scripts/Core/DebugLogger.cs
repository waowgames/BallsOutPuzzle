using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public static class DebugLogger
{
    [Conditional("UNITY_EDITOR")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void Log(object message, UnityEngine.Object context)
    {
        Debug.Log(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message)
    {
        Debug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogWarning(object message, UnityEngine.Object context)
    {
        Debug.LogWarning(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message)
    {
        Debug.LogError(message);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogError(object message, UnityEngine.Object context)
    {
        Debug.LogError(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogException(Exception exception)
    {
        Debug.LogException(exception);
    }

    [Conditional("UNITY_EDITOR")]
    public static void LogException(Exception exception, UnityEngine.Object context)
    {
        Debug.LogException(exception, context);
    }
}
