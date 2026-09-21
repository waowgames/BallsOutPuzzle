using UnityEngine;
using UnityEngine.UI;

public sealed class AudioSettingsView : MonoBehaviour
{
    [SerializeField] private Toggle bgMusicToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle vibrationToggle;

    private SoundManager manager;
    private bool bound;

    private void OnEnable() => Bind();
    private void Start() => Bind();
    private void OnDisable() => Unbind();

    private void Bind()
    {
        if (bound)
            return;

        manager = SoundManager.Instance;
        if (manager == null)
            return;

        if (bgMusicToggle != null)
        {
            bgMusicToggle.SetIsOnWithoutNotify(manager.IsMusicEnabled);
            bgMusicToggle.onValueChanged.AddListener(OnMusicChanged);
        }

        if (sfxToggle != null)
        {
            sfxToggle.SetIsOnWithoutNotify(manager.IsSfxEnabled);
            sfxToggle.onValueChanged.AddListener(OnSfxChanged);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.SetIsOnWithoutNotify(manager.IsVibrationEnabled);
            vibrationToggle.onValueChanged.AddListener(OnVibrationChanged);
        }

        bound = true;
    }

    private void Unbind()
    {
        if (!bound)
            return;

        if (bgMusicToggle != null)
            bgMusicToggle.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxToggle != null)
            sfxToggle.onValueChanged.RemoveListener(OnSfxChanged);
        if (vibrationToggle != null)
            vibrationToggle.onValueChanged.RemoveListener(OnVibrationChanged);

        bound = false;
        manager = null;
    }

    private void OnMusicChanged(bool enabled)
    {
        if (manager != null)
            manager.ToggleBgMusic(enabled);
    }

    private void OnSfxChanged(bool enabled)
    {
        if (manager != null)
            manager.ToggleSfx(enabled);
    }

    private void OnVibrationChanged(bool enabled)
    {
        if (manager != null)
            manager.ToggleVibration(enabled);
    }
}
