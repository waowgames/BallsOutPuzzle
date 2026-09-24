using System;
using System.Collections;
using AssetKits.ParticleImage;
using UnityEngine;

public sealed class CoinRewardSequence : MonoBehaviour
{
    private const int MaximumVisualCoins = 7;
    private const float CoinInterval = 0.14f;
    private const float MinimumSequenceTime = 0.8f;
    private const float CoinArrivalTimeout = 2f;
    private const float FinalPause = 0.3f;

    [SerializeField] private RectTransform coinOrigin;

    private ParticleImage particleImage;
    private CurrencyDisplay currencyDisplay;
    private Coroutine sequence;
    private Action onComplete;
    private int startBalance;
    private int creditedReward;
    private int visualCoinCount;
    private int arrivedCoinCount;
    private int burstCount;

    private void Awake()
    {
        UIManager uiManager = GetComponentInParent<UIManager>();
        if (uiManager == null)
            return;

        particleImage = uiManager.GetComponentInChildren<ParticleImage>(true);
        currencyDisplay = uiManager.GetComponent<CurrencyDisplay>();
    }

    private void OnDisable()
    {
        if (sequence != null)
        {
            StopCoroutine(sequence);
            sequence = null;
        }

        FinishVisuals();
        onComplete = null;
    }

    public void Play(int reward, Action completion)
    {
        if (sequence != null)
        {
            StopCoroutine(sequence);
            FinishVisuals();
        }

        onComplete = completion;
        CurrencyWallet wallet = CurrencyWallet.Instance;
        startBalance = wallet != null ? wallet.Balance : 0;
        creditedReward = 0;
        visualCoinCount = 0;
        arrivedCoinCount = 0;

        if (wallet != null && reward > 0)
        {
            currencyDisplay?.BeginRewardAnimation(startBalance);
            if (wallet.Add(reward))
                creditedReward = wallet.Balance - startBalance;
        }

        if (creditedReward > 0 && particleImage != null)
            PlayCoins();
        else
            currencyDisplay?.EndRewardAnimation();

        sequence = StartCoroutine(CompleteAfterCoins());
    }

    private void PlayCoins()
    {
        visualCoinCount = Mathf.Clamp(Mathf.CeilToInt(creditedReward / 4f), 3, MaximumVisualCoins);

        particleImage.onParticleFinish.RemoveListener(HandleCoinArrived);
        particleImage.Stop(true);
        RemoveBursts();

        if (coinOrigin != null)
            particleImage.rectTransform.position = coinOrigin.position;
        if (currencyDisplay != null && currencyDisplay.Target != null)
            particleImage.attractorTarget = currencyDisplay.Target;

        for (int i = 0; i < visualCoinCount; i++)
        {
            particleImage.AddBurst(i * CoinInterval, 1);
            burstCount++;
        }

        particleImage.onParticleFinish.AddListener(HandleCoinArrived);
        particleImage.Play();
    }

    private void HandleCoinArrived()
    {
        if (arrivedCoinCount >= visualCoinCount)
            return;

        arrivedCoinCount++;
        int displayedBalance = startBalance +
            (int)((long)creditedReward * arrivedCoinCount / visualCoinCount);
        currencyDisplay?.ShowRewardProgress(displayedBalance);
    }

    private IEnumerator CompleteAfterCoins()
    {
        yield return new WaitForSecondsRealtime(MinimumSequenceTime);

        float timeout = Time.realtimeSinceStartup + CoinArrivalTimeout;
        while (arrivedCoinCount < visualCoinCount && Time.realtimeSinceStartup < timeout)
            yield return null;

        if (visualCoinCount > 0 && arrivedCoinCount == visualCoinCount)
            yield return new WaitForSecondsRealtime(FinalPause);

        FinishVisuals();
        sequence = null;
        Action completion = onComplete;
        onComplete = null;
        completion?.Invoke();
    }

    private void FinishVisuals()
    {
        currencyDisplay?.EndRewardAnimation();
        if (particleImage == null)
            return;

        particleImage.onParticleFinish.RemoveListener(HandleCoinArrived);
        particleImage.Stop(true);
        RemoveBursts();
    }

    private void RemoveBursts()
    {
        for (int i = burstCount - 1; i >= 0; i--)
            particleImage.RemoveBurst(i);
        burstCount = 0;
    }
}
