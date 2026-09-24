using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CoinRewardSequence))]
public sealed class LevelUpPopup : UIPopup
{
    public static LevelUpPopup Instance { get; private set; }

    [Header("Presentation")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text rewardAmountText;
    [SerializeField] private Button getButton;

    [Header("Reward")]
    [SerializeField] private LevelRewardConfig rewardConfig;

    [Header("Celebration")]
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField, Min(0)] private int confettiCount = 3;
    [SerializeField, Min(0f)] private float confettiInterval = 0.18f;
    [SerializeField, Min(0.1f)] private float confettiDistance = 8f;
    [SerializeField, Min(0.01f)] private float confettiScale = 1.5f;
    [SerializeField, Min(0f)] private float confettiLifetime = 4f;
    [SerializeField, Min(0f)] private float popupDelay = 0.9f;
    [SerializeField] private UIPanelTransition panelTransition;
    [SerializeField] private RectTransform[] stars = System.Array.Empty<RectTransform>();
    [SerializeField, Min(0f)] private float starsDelay = 0.3f;
    [SerializeField, Min(0f)] private float starInterval = 0.28f;
    [SerializeField] private UIFireworkBurst[] fireworks = System.Array.Empty<UIFireworkBurst>();

    // Viewport spots for the confetti blasts (cycled when count > length).
    private static readonly Vector2[] ConfettiSpots =
    {
        new Vector2(0.22f, 0.62f), new Vector2(0.78f, 0.68f), new Vector2(0.5f, 0.5f)
    };

    private Vector3[] starScales;
    private Coroutine celebration;
    private CoinRewardSequence coinRewardSequence;

    private int pendingReward;
    private bool rewardClaimed;

    public override string PopupId => nameof(LevelUpPopup);

    protected override void Awake()
    {
        Instance = this;
        base.Awake();

        coinRewardSequence = GetComponent<CoinRewardSequence>();

        starScales = new Vector3[stars.Length];
        for (int i = 0; i < stars.Length; i++)
            starScales[i] = stars[i] != null ? stars[i].localScale : Vector3.one;
    }

    private void OnEnable()
    {
        GameEvents.OnLevelCompleted += HandleLevelCompleted;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelCompleted -= HandleLevelCompleted;

        if (getButton != null)
            getButton.onClick.RemoveListener(HandleCollectClicked);

        StopCelebration();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    protected override void OnShow()
    {
        transform.localScale = Vector3.one;
        if (getButton == null)
            return;

        getButton.interactable = true;
        getButton.onClick.RemoveListener(HandleCollectClicked);
        getButton.onClick.AddListener(HandleCollectClicked);
    }

    protected override void OnHide()
    {
        if (getButton != null)
            getButton.onClick.RemoveListener(HandleCollectClicked);
    }

    private void HandleLevelCompleted(int index)
    {
        pendingReward = rewardConfig != null
            ? rewardConfig.GetReward(index)
            : 1;
        rewardClaimed = false;

        if (levelText != null)
            levelText.SetText("LEVEL {0}", index + 1);

        if (rewardAmountText != null)
            rewardAmountText.SetText("+{0}", pendingReward);

        StopCelebration();
        celebration = StartCoroutine(Celebrate());
    }

    // Confetti in the scene -> popup opens -> stars pop one by one with fireworks.
    private IEnumerator Celebrate()
    {
        SetStarsHidden();

        for (int i = 0; i < confettiCount; i++)
        {
            SpawnConfetti(ConfettiSpots[i % ConfettiSpots.Length]);
            yield return new WaitForSecondsRealtime(confettiInterval);
        }

        yield return new WaitForSecondsRealtime(popupDelay);

        Show();
        panelTransition?.PlayOpen();

        yield return new WaitForSecondsRealtime(starsDelay);

        int lastStar = Mathf.Max(1, stars.Length - 1);
        for (int i = 0; i < stars.Length; i++)
        {
            RectTransform star = stars[i];
            if (star != null)
            {
                star.DOKill();
                star.DOScale(starScales[i], 0.35f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true);
            }

            // Spread the fireworks across the star sequence (first star ... last star).
            for (int f = 0; f < fireworks.Length; f++)
            {
                int starForFirework = fireworks.Length == 1 ? 0 : Mathf.RoundToInt(f * lastStar / (float)(fireworks.Length - 1));
                if (starForFirework == i && fireworks[f] != null)
                    fireworks[f].Play();
            }

            yield return new WaitForSecondsRealtime(starInterval);
        }

        celebration = null;
    }

    private void SpawnConfetti(Vector2 viewportSpot)
    {
        Camera cam = Camera.main;
        if (confettiPrefab == null || cam == null)
            return;

        Vector3 position = cam.ViewportToWorldPoint(new Vector3(viewportSpot.x, viewportSpot.y, confettiDistance));
        GameObject confetti = Instantiate(confettiPrefab, position, confettiPrefab.transform.rotation);
        confetti.transform.localScale *= confettiScale;

        foreach (ParticleSystem system in confetti.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
        }

        Destroy(confetti, confettiLifetime);
    }

    private void SetStarsHidden()
    {
        foreach (RectTransform star in stars)
        {
            if (star == null)
                continue;

            star.DOKill();
            star.localScale = Vector3.zero;
        }
    }

    private void StopCelebration()
    {
        if (celebration == null)
            return;

        StopCoroutine(celebration);
        celebration = null;
    }

    private void HandleCollectClicked()
    {
        if (rewardClaimed)
            return;

        rewardClaimed = true;
        if (getButton != null)
            getButton.interactable = false;

        coinRewardSequence.Play(pendingReward, ContinueToNextLevel);
        Hide();
    }

    private static void ContinueToNextLevel()
    {
        GameFlowController.Instance?.ContinueToNextLevel();
    }
}
