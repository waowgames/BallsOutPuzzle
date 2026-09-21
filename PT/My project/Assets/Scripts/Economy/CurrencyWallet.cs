using System;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class CurrencyWallet : SingletonMonoBehaviour<CurrencyWallet>
{
    public int Balance { get; private set; }
    public event Action<int> BalanceChanged;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        Balance = SaveService.Instance != null
            ? SaveService.Instance.SoftCurrency
            : 0;
    }

    public bool Add(int amount)
    {
        if (amount <= 0 || SaveService.Instance == null)
            return false;

        long result = (long)Balance + amount;
        Balance = result > int.MaxValue ? int.MaxValue : (int)result;
        SaveService.Instance.SetSoftCurrency(Balance);
        BalanceChanged?.Invoke(Balance);
        return true;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || Balance < amount || SaveService.Instance == null)
            return false;

        Balance -= amount;
        SaveService.Instance.SetSoftCurrency(Balance);
        BalanceChanged?.Invoke(Balance);
        return true;
    }
}
