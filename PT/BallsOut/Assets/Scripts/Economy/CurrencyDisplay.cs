using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class CurrencyDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI currencyText;

    private Vector3 textScale;
    private int latestBalance;
    private bool rewardAnimationActive;
    private Sequence pulse;

    public RectTransform Target => currencyText != null ? currencyText.rectTransform : null;

    private void Awake()
    {
        if (currencyText != null)
            textScale = currencyText.rectTransform.localScale;
    }

    private void OnEnable()
    {
        if (CurrencyWallet.Instance == null)
        {
            SetBalance(0);
            return;
        }

        CurrencyWallet.Instance.BalanceChanged += SetBalance;
        SetBalance(CurrencyWallet.Instance.Balance);
    }

    private void OnDisable()
    {
        if (CurrencyWallet.Instance != null)
            CurrencyWallet.Instance.BalanceChanged -= SetBalance;

        rewardAnimationActive = false;
        if (currencyText != null)
        {
            pulse?.Kill();
            pulse = null;
            currencyText.rectTransform.localScale = textScale;
        }
    }

    private void SetBalance(int balance)
    {
        latestBalance = balance;
        if (!rewardAnimationActive)
            ShowBalance(balance);
    }

    public void BeginRewardAnimation(int startingBalance)
    {
        rewardAnimationActive = true;
        ShowBalance(startingBalance);
    }

    public void ShowRewardProgress(int balance)
    {
        if (!rewardAnimationActive || currencyText == null)
            return;

        ShowBalance(balance);

        RectTransform target = currencyText.rectTransform;
        pulse?.Kill();
        target.localScale = textScale;
        pulse = DOTween.Sequence().SetUpdate(true)
            .Append(target.DOScale(textScale * 1.16f, 0.03f).SetEase(Ease.OutQuad))
            .Append(target.DOScale(textScale, 0.05f).SetEase(Ease.OutQuad));
    }

    public void EndRewardAnimation()
    {
        if (!rewardAnimationActive)
            return;

        rewardAnimationActive = false;
        ShowBalance(CurrencyWallet.Instance != null ? CurrencyWallet.Instance.Balance : latestBalance);
    }

    private void ShowBalance(int balance)
    {
        if (currencyText != null)
            currencyText.SetText("{0}", Mathf.Max(0, balance));
    }
}
