using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small UI firework: a glow flash plus sparks flying out radially. Builds its own images.
/// </summary>
public sealed class UIFireworkBurst : MonoBehaviour
{
    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Color color = new Color(1f, 0.86f, 0.3f, 1f);
    [SerializeField, Min(1)] private int sparkCount = 14;
    [SerializeField, Min(0f)] private float radius = 180f;
    [SerializeField, Min(0.05f)] private float duration = 0.75f;
    [SerializeField] private Vector2 sparkSize = new Vector2(64f, 19f);
    [SerializeField, Min(0f)] private float glowSize = 260f;

    private RectTransform[] sparks;
    private Image[] sparkImages;
    private Image glow;

    private void Awake()
    {
        Build();
    }

    private void OnDisable()
    {
        KillTweens();
        HideAll();
    }

    public void Play()
    {
        if (sparks == null)
            Build();

        KillTweens();

        glow.rectTransform.localScale = Vector3.one * 0.2f;
        glow.color = color;
        glow.rectTransform.DOScale(1.3f, duration * 0.45f).SetEase(Ease.OutQuad).SetUpdate(true);
        glow.DOFade(0f, duration * 0.55f).SetDelay(duration * 0.1f).SetUpdate(true);

        float step = 360f / sparks.Length;
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = i * step + Random.Range(-step * 0.35f, step * 0.35f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float distance = radius * Random.Range(0.7f, 1.1f);
            float time = duration * Random.Range(0.85f, 1.1f);

            RectTransform spark = sparks[i];
            Image image = sparkImages[i];
            spark.anchoredPosition = dir * 12f;
            spark.localRotation = Quaternion.Euler(0f, 0f, angle);
            spark.localScale = Vector3.one * Random.Range(0.8f, 1.15f);
            image.color = color;

            spark.DOAnchorPos(dir * distance, time).SetEase(Ease.OutCubic).SetUpdate(true);
            spark.DOScale(0.15f, time).SetEase(Ease.InQuad).SetUpdate(true);
            image.DOFade(0f, time * 0.45f).SetDelay(time * 0.55f).SetUpdate(true);
        }
    }

    private void Build()
    {
        if (sparks != null)
            return;

        glow = CreateImage("Glow", glowSprite, new Vector2(glowSize, glowSize));
        sparks = new RectTransform[sparkCount];
        sparkImages = new Image[sparkCount];
        for (int i = 0; i < sparkCount; i++)
        {
            sparkImages[i] = CreateImage("Spark", sparkSprite, sparkSize);
            sparks[i] = sparkImages[i].rectTransform;
            // Pivot at the tail so the streak points outward from the centre.
            sparks[i].pivot = new Vector2(0.15f, 0.5f);
        }

        HideAll();
    }

    private Image CreateImage(string objectName, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    private void HideAll()
    {
        if (glow != null)
            glow.color = Color.clear;

        if (sparkImages == null)
            return;

        foreach (Image image in sparkImages)
            image.color = Color.clear;
    }

    private void KillTweens()
    {
        if (glow != null)
        {
            glow.rectTransform.DOKill();
            glow.DOKill();
        }

        if (sparks == null)
            return;

        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i].DOKill();
            sparkImages[i].DOKill();
        }
    }
}
