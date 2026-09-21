using UnityEngine;

internal static class SoundManagerBootstrap
{
    private const string LibraryResourcePath = "Audio/SoundLibrary";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SoundManager.ResetStatics();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateManager()
    {
        SoundLibrary library = Resources.Load<SoundLibrary>(LibraryResourcePath);
        SoundManager.CreatePersistent(library);
    }
}
