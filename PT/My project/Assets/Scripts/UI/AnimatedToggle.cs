using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(Toggle))]
public class AnimatedToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Toggle toggle;
    [SerializeField] private RectTransform knob;
    [Tooltip("Toggle'ın arka plan Image bileşeni")]
    [SerializeField] private Image backgroundImage;

    [Header("Sprites")]
    [Tooltip("On durumunda gösterilecek yeşil sprite")]
    [SerializeField] private Sprite onSprite;
    [Tooltip("Off durumunda gösterilecek kırmızı sprite")]
    [SerializeField] private Sprite offSprite;

    [Header("Animation Settings")]
    [SerializeField] private float offPositionX = -50f;
    [SerializeField] private float onPositionX = 50f;
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;

    private void Reset()
    {
        toggle = GetComponent<Toggle>();
        if (toggle.targetGraphic != null)
        {
            var rt = toggle.targetGraphic.GetComponent<RectTransform>();
            if (rt != null) knob = rt;
        }
    }

    private void Awake()
    {
        if (toggle == null) toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void Start()
    {
        // Başlangıçta hem knob pozisyonu hem de sprite ayarı
        bool startOn = toggle.isOn;
        knob.localPosition = new Vector3(startOn ? onPositionX : offPositionX,
                                         knob.localPosition.y,
                                         knob.localPosition.z);
        UpdateBackgroundSprite(startOn);
    }

    private void OnToggleChanged(bool isOn)
    {
        // Animate knob
        float targetX = isOn ? onPositionX : offPositionX;
        knob.DOKill();
        knob.DOLocalMoveX(targetX, duration).SetEase(ease);

        // Update sprite
        UpdateBackgroundSprite(isOn);
    }

    private void UpdateBackgroundSprite(bool isOn)
    {
        if (backgroundImage == null) return;
        backgroundImage.sprite = isOn ? onSprite : offSprite;
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
