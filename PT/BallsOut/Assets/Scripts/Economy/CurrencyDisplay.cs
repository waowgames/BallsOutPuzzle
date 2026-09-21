using TMPro;
using UnityEngine;

public sealed class CurrencyDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI currencyText;

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
    }

    private void SetBalance(int balance)
    {
        if (currencyText != null)
            currencyText.SetText("{0}", Mathf.Max(0, balance));
    }
}
