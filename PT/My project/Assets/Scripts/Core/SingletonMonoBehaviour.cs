using UnityEngine;

/// <summary>
/// Generic singleton base for MonoBehaviours.
/// Set <see cref="Persist"/> to true in subclass Awake (before base.Awake)
/// to survive scene loads via DontDestroyOnLoad.
/// </summary>
public class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    /// <summary>Current singleton instance. Null if not yet created.</summary>
    public static T Instance => instance;

    /// <summary>
    /// When true the singleton GameObject will persist across scene loads.
    /// Set this in the subclass Awake BEFORE calling base.Awake().
    /// </summary>
    protected bool Persist { get; set; }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;

            if (Persist)
                DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
