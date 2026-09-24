using TMPro;
using UnityEngine;

/// <summary>
/// Shows the level countdown as mm:ss and tints it when time is running low.
/// </summary>
public sealed class LevelTimerDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField, Min(0)] private int warningSeconds = 10;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.36f);
    [SerializeField] private Color frozenColor = new Color(0.55f, 0.87f, 1f);

    private int shownSeconds = -1;
    private Color shownColor;

    private void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        shownSeconds = -1;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (timerText == null)
            return;

        LevelTimer timer = LevelTimer.Instance;
        if (timer == null || !timer.HasTimeLimit)
        {
            SetTime(0, normalColor);
            return;
        }

        int seconds = Mathf.CeilToInt(timer.RemainingSeconds);
        Color color = timer.IsFrozen ? frozenColor
            : seconds <= warningSeconds ? warningColor
            : normalColor;
        SetTime(seconds, color);
    }

    private void SetTime(int seconds, Color color)
    {
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        if (color != shownColor)
        {
            shownColor = color;
            timerText.color = color;
        }
    }
}
