using TMPro;
using UnityEngine;

/// <summary>
/// Look and copy of the in-game tutorial. Lives at Resources/TutorialConfig so the
/// tutorial director can boot itself in any scene that plays levels.
/// </summary>
[CreateAssetMenu(menuName = "Balls Out/Tutorial Config", fileName = "TutorialConfig")]
public sealed class TutorialConfig : ScriptableObject
{
    public const string ResourcePath = "TutorialConfig";

    [Header("Enable")]
    public bool tutorialsEnabled = true;

    [Header("Art")]
    public Shader focusMaskShader;
    public Sprite handSprite;
    [Tooltip("Fingertip position inside the hand sprite, 0-1 from the bottom-left corner.")]
    public Vector2 handFingertip = new Vector2(0.235f, 0.885f);
    [Min(40f)] public float handSize = 250f;
    public TMP_FontAsset font;

    [Header("Colors")]
    public Color dimColor = new Color(0.02f, 0.03f, 0.08f, 0.8f);
    public Color ringColor = new Color(1f, 0.92f, 0.45f, 1f);
    public Color bubbleColor = new Color(1f, 1f, 1f, 0.98f);
    public Color bubbleTextColor = new Color(0.16f, 0.18f, 0.29f, 1f);
    public Color highlightTextColor = new Color(1f, 0.52f, 0.1f, 1f);
    public Color trailColor = new Color(1f, 1f, 1f, 0.75f);

    [Header("Layout")]
    [Tooltip("Reference resolution of the tutorial canvas; match the HUD canvas.")]
    public Vector2 referenceResolution = new Vector2(1170f, 2532f);
    [Range(0f, 1f)] public float matchWidthOrHeight;
    [Min(200f)] public float bubbleWidth = 940f;
    [Min(20f)] public float bubbleFontSize = 58f;
    [Min(0f)] public float holePadding = 22f;

    [Header("Timing")]
    [Tooltip("An info step accepts the continue tap only after this long, so nobody skips it by accident.")]
    [Min(0f)] public float minReadSeconds = 0.9f;
    [Tooltip("Delay after the level is ready before the first tutorial step.")]
    [Min(0f)] public float startDelay = 0.45f;

    [Header("Texts - First Level")]
    [TextArea] public string dragText = "Drag the box under the balls of the <hl>same color</hl>!";
    [TextArea] public string fillText = "Matching balls drop into the box.\nFill it up to <hl>clear</hl> it!";
    [TextArea] public string winText = "Clear <hl>every box</hl> to win the level!";
    [TextArea] public string tapToContinue = "Tap to continue";

    [Header("Texts - Mechanics")]
    [TextArea] public string axisText = "Arrow boxes slide <hl>only</hl> the way their arrow points.";
    [TextArea] public string innerLayerText = "Two-layer box! Fill the <hl>inner color</hl> first, then the outer one.";
    [TextArea] public string iceText = "Frozen box! Complete <hl>other boxes</hl> to crack the ice.";
    [TextArea] public string lockText = "Locked box! Complete the box with the <hl>key</hl> to open it.";
    [TextArea] public string chainText = "Chained boxes pull each other. Complete one to <hl>break the chain</hl>.";
    [TextArea] public string obstacleText = "Stones block the way. Each completed box <hl>chips</hl> them until they break.";
    [TextArea] public string feederText = "The tubes keep <hl>dropping new balls</hl> as space opens up.";
    [TextArea] public string conveyorText = "The conveyor keeps <hl>bringing new balls</hl> into the pile.";
    [TextArea] public string timerText = "Beat the clock! Clear all boxes <hl>before time runs out</hl>.";

    [Header("Texts - Boosters")]
    [Tooltip("{0} = booster name, {1} = booster description.")]
    [TextArea] public string boosterUnlockText = "<size=125%><hl>{0}</hl> unlocked!</size>\n{1}\nHere's one for <hl>free</hl>. Tap to use it!";
    [TextArea] public string boosterPickText = "Tap this box!";

    public string Format(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string hex = ColorUtility.ToHtmlStringRGB(highlightTextColor);
        return text.Replace("<hl>", $"<color=#{hex}><b>").Replace("</hl>", "</b></color>");
    }
}
