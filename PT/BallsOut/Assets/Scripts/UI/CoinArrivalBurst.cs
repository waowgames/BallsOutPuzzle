using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CoinArrivalBurst : MonoBehaviour
{
    private const int BurstSlots = 3;
    private const int SparksPerBurst = 6;
    private const float Duration = 0.28f;
    private const float Radius = 50f;

    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Color gold = new Color(1f, 0.82f, 0.22f, 1f);

    private Burst[] bursts;
    private int nextBurst;

    private sealed class Burst
    {
        public GameObject root;
        public Image glow;
        public Image[] sparkImages;
        public RectTransform[] sparks;
        public TweenCallback hide;
    }

    private void Awake()
    {
        bursts = new Burst[BurstSlots];
        for (int i = 0; i < bursts.Length; i++)
            bursts[i] = CreateBurst(i);
    }

    private void OnDisable()
    {
        if (bursts == null)
            return;

        foreach (Burst burst in bursts)
        {
            KillTweens(burst);
            burst.root.SetActive(false);
        }
    }

    public void Play()
    {
        if (!isActiveAndEnabled || bursts == null)
            return;

        Burst burst = bursts[nextBurst];
        nextBurst = (nextBurst + 1) % bursts.Length;
        KillTweens(burst);
        burst.root.SetActive(true);

        RectTransform glowRect = burst.glow.rectTransform;
        glowRect.localScale = Vector3.one * 0.45f;
        burst.glow.color = new Color(gold.r, gold.g, gold.b, 0.65f);
        glowRect.DOScale(1.1f, Duration).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(burst.hide);
        burst.glow.DOFade(0f, Duration * 0.8f).SetUpdate(true);

        float angleOffset = nextBurst * 12f;
        for (int i = 0; i < burst.sparks.Length; i++)
        {
            float angle = i * (360f / SparksPerBurst) + angleOffset;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            RectTransform spark = burst.sparks[i];
            Image image = burst.sparkImages[i];
            spark.anchoredPosition = direction * 8f;
            spark.localRotation = Quaternion.Euler(0f, 0f, angle);
            spark.localScale = Vector3.one;
            image.color = gold;

            spark.DOAnchorPos(direction * Radius, Duration).SetEase(Ease.OutCubic).SetUpdate(true);
            spark.DOScale(0.2f, Duration).SetEase(Ease.InQuad).SetUpdate(true);
            image.DOFade(0f, Duration * 0.7f).SetDelay(Duration * 0.25f).SetUpdate(true);
        }
    }

    private Burst CreateBurst(int index)
    {
        var root = new GameObject($"Coin Arrival {index}", typeof(RectTransform));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);

        var burst = new Burst
        {
            root = root,
            sparkImages = new Image[SparksPerBurst],
            sparks = new RectTransform[SparksPerBurst]
        };
        burst.hide = () => root.SetActive(false);
        burst.glow = CreateImage(root.transform, "Glow", glowSprite, new Vector2(96f, 96f));

        for (int i = 0; i < SparksPerBurst; i++)
        {
            Image image = CreateImage(root.transform, "Spark", sparkSprite, new Vector2(23f, 7f));
            burst.sparkImages[i] = image;
            burst.sparks[i] = image.rectTransform;
            burst.sparks[i].pivot = new Vector2(0.15f, 0.5f);
        }

        root.SetActive(false);
        return burst;
    }

    private Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = size;
        return image;
    }

    private static void KillTweens(Burst burst)
    {
        burst.glow.rectTransform.DOKill();
        burst.glow.DOKill();
        for (int i = 0; i < burst.sparks.Length; i++)
        {
            burst.sparks[i].DOKill();
            burst.sparkImages[i].DOKill();
        }
    }
}
