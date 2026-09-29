#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Desktop shortcuts for changing the game's playback speed.</summary>
public sealed class GameSpeedHotkeys : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("Game Speed Hotkeys");
        DontDestroyOnLoad(go);
        go.AddComponent<GameSpeedHotkeys>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f1Key.wasPressedThisFrame) Time.timeScale = 1f;
        else if (keyboard.f2Key.wasPressedThisFrame) Time.timeScale = 4f;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F1)) Time.timeScale = 1f;
        else if (Input.GetKeyDown(KeyCode.F2)) Time.timeScale = 4f;
#endif
    }
}
#endif
