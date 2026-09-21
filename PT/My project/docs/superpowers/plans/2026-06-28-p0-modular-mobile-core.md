# P0 Modular Mobile Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a modular mobile-game core with centralized save data, UI-independent currency, single-scene level prefab loading, one UI-to-level flow controller, and cleaned reward/booster/audio integrations.

**Architecture:** `SaveService` owns all persisted state and is the only runtime class that accesses `PlayerPrefs`. `CurrencyWallet`, `LevelManager`, `LevelContentLoader`, and `GameFlowController` each own one runtime responsibility and communicate through direct transaction APIs or existing lifecycle events. UI components only present state and request transitions.

**Tech Stack:** Unity 6000.0.68f1, C#, MonoBehaviour, ScriptableObject, JsonUtility, PlayerPrefs JSON backend, TextMesh Pro, DOTween, prefab YAML serialization

**User constraints:** Do not create Unity tests. Do not use `verification-before-completion`. Do not create branches, stage, commit, push, or open pull requests.

---

## File map

### New runtime files

- `Assets/Scripts/Save/GameSaveData.cs`: Serializable P0 save model and booster entries.
- `Assets/Scripts/Save/SaveService.cs`: Versioned JSON load/save, legacy migration, primary/backup storage.
- `Assets/Scripts/Economy/CurrencyWallet.cs`: Soft-currency state and transactions.
- `Assets/Scripts/Economy/CurrencyDisplay.cs`: Event-driven TMP balance view.
- `Assets/Scripts/Level/LevelContentLoader.cs`: Creates the selected level prefab beneath `LevelRoot`.
- `Assets/Scripts/Level/GameFlowController.cs`: UI-facing play, retry, and continue transitions.
- `Assets/Scripts/Level/LevelRewardConfig.cs`: Completion reward calculation.
- `Assets/Settings/LevelRewardConfig.asset`: Default reward policy.

### Modified runtime files

- `Assets/Scripts/Level/LevelData.cs`: Optional shared level prefab.
- `Assets/Scripts/Level/LevelManager.cs`: Use `SaveService`, retain lifecycle-only ownership.
- `Assets/Scripts/Level/GameEvents.cs`: Remove score events.
- `Assets/Scripts/Level/LevelUpPopup.cs`: One-shot reward transaction and controller-based continue.
- `Assets/Scripts/Level/LevelFailPopup.cs`: Controller-based retry.
- `Assets/Scripts/UI/UIManager.cs`: Remove score/currency ownership.
- `Assets/Scripts/UI/MainMenuPlayStarter.cs`: Controller-based start.
- `Assets/Scripts/UI/UIAnimator.cs`: Remove gameplay-start side effect.
- `Assets/Scripts/UI/FlyToUIEffect.cs`: Visual callback only; remove unused faux implementation fields.
- `Assets/Scripts/UI/BoosterUIManager.cs`: Remove polling and ad stubs; use wallet/save events.
- `Assets/Scripts/Audio/SoundManager.cs`: Persist settings through `SaveService`.

### Modified serialized assets

- `Assets/Prefabs/UI prefabs/UI Manager.prefab`: Add services, currency display, flow, loader, and `LevelRoot`; remove old score fields and broken booster event.
- `Assets/Prefabs/UI prefabs/UI Settings/Level Up Effects.prefab`: Bind reward config, simplify fly effect fields, disable 2x button.
- `Assets/Prefabs/UI prefabs/UI Settings/Sound Manager.prefab`: Remove obsolete serialized field.

---

### Task 1: Add versioned save data and centralized persistence

**Files:**

- Create: `Assets/Scripts/Save/GameSaveData.cs`
- Create: `Assets/Scripts/Save/GameSaveData.cs.meta`
- Create: `Assets/Scripts/Save/SaveService.cs`
- Create: `Assets/Scripts/Save/SaveService.cs.meta`
- Create: `Assets/Scripts/Save.meta`

- [ ] **Step 1: Create the save folder metadata**

Use unique Unity GUIDs:

```yaml
# Assets/Scripts/Save.meta
fileFormatVersion: 2
guid: 7a4de103af9d4c7c96b2c6a4d10f1a00
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 2: Add the serializable P0 data model**

Create `GameSaveData.cs` with version, progression, settings, currency, and list-backed booster inventory:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public int currentLevelIndex;
    public int softCurrency;
    public bool musicEnabled = true;
    public bool sfxEnabled = true;
    public bool vibrationEnabled = true;
    public List<BoosterSaveEntry> boosters = new List<BoosterSaveEntry>();

    public void Sanitize()
    {
        version = Mathf.Max(1, version);
        currentLevelIndex = Mathf.Max(0, currentLevelIndex);
        softCurrency = Mathf.Max(0, softCurrency);
        boosters ??= new List<BoosterSaveEntry>();

        for (int i = boosters.Count - 1; i >= 0; i--)
        {
            BoosterSaveEntry entry = boosters[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
            {
                boosters.RemoveAt(i);
                continue;
            }

            entry.count = Mathf.Max(0, entry.count);
        }
    }

    public int GetBoosterCount(string id)
    {
        BoosterSaveEntry entry = FindBooster(id);
        return entry != null ? entry.count : 0;
    }

    public bool TryGetBoosterCount(string id, out int count)
    {
        BoosterSaveEntry entry = FindBooster(id);
        count = entry != null ? entry.count : 0;
        return entry != null;
    }

    public void SetBoosterCount(string id, int count)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        BoosterSaveEntry entry = FindBooster(id);
        if (entry == null)
        {
            entry = new BoosterSaveEntry { id = id };
            boosters.Add(entry);
        }

        entry.count = Mathf.Max(0, count);
    }

    private BoosterSaveEntry FindBooster(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || boosters == null)
            return null;

        for (int i = 0; i < boosters.Count; i++)
        {
            if (boosters[i] != null &&
                string.Equals(boosters[i].id, id, StringComparison.Ordinal))
                return boosters[i];
        }

        return null;
    }
}

[Serializable]
public sealed class BoosterSaveEntry
{
    public string id;
    public int count;
}
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a01`.

- [ ] **Step 3: Add `SaveService` as the only PlayerPrefs consumer**

Create `SaveService.cs` with this public API:

```csharp
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class SaveService : SingletonMonoBehaviour<SaveService>
{
    private const int CurrentVersion = 1;
    private const string PrimaryKey = "template_save_v1";
    private const string BackupKey = "template_save_v1_backup";

    private GameSaveData data;

    public int CurrentLevelIndex => data != null ? data.currentLevelIndex : 0;
    public int SoftCurrency => data != null ? data.softCurrency : 0;
    public bool MusicEnabled => data == null || data.musicEnabled;
    public bool SfxEnabled => data == null || data.sfxEnabled;
    public bool VibrationEnabled => data == null || data.vibrationEnabled;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        data = LoadData();
    }

    public void SetCurrentLevelIndex(int value)
    {
        int safeValue = Mathf.Max(0, value);
        if (data.currentLevelIndex == safeValue)
            return;

        data.currentLevelIndex = safeValue;
        Persist(true);
    }

    public void SetSoftCurrency(int value)
    {
        int safeValue = Mathf.Max(0, value);
        if (data.softCurrency == safeValue)
            return;

        data.softCurrency = safeValue;
        Persist(true);
    }

    public void SetMusicEnabled(bool value)
    {
        if (data.musicEnabled == value)
            return;

        data.musicEnabled = value;
        Persist(true);
    }

    public void SetSfxEnabled(bool value)
    {
        if (data.sfxEnabled == value)
            return;

        data.sfxEnabled = value;
        Persist(true);
    }

    public void SetVibrationEnabled(bool value)
    {
        if (data.vibrationEnabled == value)
            return;

        data.vibrationEnabled = value;
        Persist(true);
    }

    public int GetBoosterCount(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return 0;

        if (data.TryGetBoosterCount(id, out int savedCount))
            return savedCount;

        string legacyKey = $"booster_{id}_count";
        if (!PlayerPrefs.HasKey(legacyKey))
            return 0;

        int migratedCount = Mathf.Max(0, PlayerPrefs.GetInt(legacyKey, 0));
        data.SetBoosterCount(id, migratedCount);
        Persist(true);
        return migratedCount;
    }

    public void SetBoosterCount(string id, int value)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        int safeValue = Mathf.Max(0, value);
        if (data.GetBoosterCount(id) == safeValue)
            return;

        data.SetBoosterCount(id, safeValue);
        Persist(true);
    }

    public void Flush()
    {
        PlayerPrefs.Save();
    }

    private GameSaveData LoadData()
    {
        if (TryLoad(PrimaryKey, out GameSaveData loaded))
        {
            loaded.Sanitize();
            return loaded;
        }

        if (TryLoad(BackupKey, out loaded))
        {
            loaded.Sanitize();
            data = loaded;
            PlayerPrefs.DeleteKey(PrimaryKey);
            Persist(true);
            return loaded;
        }

        GameSaveData migrated = new GameSaveData
        {
            version = CurrentVersion,
            currentLevelIndex = Mathf.Max(0, PlayerPrefs.GetInt("lm_currentLevel", 0)),
            softCurrency = Mathf.Max(0, PlayerPrefs.GetInt("score", 0)),
            musicEnabled = PlayerPrefs.GetInt("BgMusicOn", 1) == 1,
            sfxEnabled = PlayerPrefs.GetInt("SfxOn", 1) == 1,
            vibrationEnabled = PlayerPrefs.GetInt("VibrationOn", 1) == 1
        };

        data = migrated;
        Persist(true);
        return migrated;
    }

    private static bool TryLoad(string key, out GameSaveData loaded)
    {
        loaded = null;
        if (!PlayerPrefs.HasKey(key))
            return false;

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            loaded = JsonUtility.FromJson<GameSaveData>(json);
            return loaded != null && loaded.version > 0;
        }
        catch (System.Exception exception)
        {
            DebugLogger.LogWarning($"[SaveService] Could not read '{key}': {exception.Message}");
            loaded = null;
            return false;
        }
    }

    private void Persist(bool flush)
    {
        if (data == null)
            return;

        data.version = CurrentVersion;
        data.Sanitize();

        if (PlayerPrefs.HasKey(PrimaryKey))
            PlayerPrefs.SetString(BackupKey, PlayerPrefs.GetString(PrimaryKey));

        PlayerPrefs.SetString(PrimaryKey, JsonUtility.ToJson(data));
        if (flush)
            PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Flush();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }
}
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a02`.

- [ ] **Step 4: Inspect persistence references before migration**

Run:

```powershell
rg -n "PlayerPrefs" Assets/Scripts
```

Expected at this stage: existing direct callers plus the new `SaveService`; later tasks remove all existing direct callers.

---

### Task 2: Extract currency from UIManager

**Files:**

- Create: `Assets/Scripts/Economy.meta`
- Create: `Assets/Scripts/Economy/CurrencyWallet.cs`
- Create: `Assets/Scripts/Economy/CurrencyWallet.cs.meta`
- Create: `Assets/Scripts/Economy/CurrencyDisplay.cs`
- Create: `Assets/Scripts/Economy/CurrencyDisplay.cs.meta`
- Modify: `Assets/Scripts/UI/UIManager.cs`
- Modify: `Assets/Scripts/Level/GameEvents.cs`

- [ ] **Step 1: Create economy folder metadata**

Use folder GUID `7a4de103af9d4c7c96b2c6a4d10f1a10`.

- [ ] **Step 2: Add the wallet transaction boundary**

Create `CurrencyWallet.cs`:

```csharp
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
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a03`.

- [ ] **Step 3: Add the event-driven TMP display**

Create `CurrencyDisplay.cs`:

```csharp
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
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a04`.

- [ ] **Step 4: Reduce UIManager to UI orchestration**

Remove:

```csharp
private int _score;
public int Score => _score;
[SerializeField] private TextMeshProUGUI scoreTxt;
[SerializeField] private int scoreMultiplier = 1;
public event Action<int> ScoreChanged;
private void InitializeScore() { ... }
public void ScoreAdd(int a = 1) { ... }
private void UpdateScoreText() { ... }
```

Remove `InitializeScore();` from `Awake`, and remove unused `System`, `TMPro`, and `UnityEngine.UI` imports. Preserve screen registry, popup stack, and input blocker behavior unchanged.

- [ ] **Step 5: Remove score events from the level/UI event hub**

Delete from `GameEvents.cs`:

```csharp
public static event Action<int> OnScoreChanged;
public static void RaiseScoreChanged(int score) => OnScoreChanged?.Invoke(score);
OnScoreChanged = null;
```

---

### Task 3: Add single-scene level content loading

**Files:**

- Modify: `Assets/Scripts/Level/LevelData.cs`
- Create: `Assets/Scripts/Level/LevelContentLoader.cs`
- Create: `Assets/Scripts/Level/LevelContentLoader.cs.meta`
- Modify: `Assets/Scripts/Level/LevelManager.cs`

- [ ] **Step 1: Give generic LevelData an optional runtime prefab**

Replace `LevelData.cs` with:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Template/Levels/Level Data")]
public class LevelData : ScriptableObject
{
    [SerializeField] private GameObject levelPrefab;

    public GameObject LevelPrefab => levelPrefab;
}
```

- [ ] **Step 2: Add event-driven content loading**

Create `LevelContentLoader.cs`:

```csharp
using UnityEngine;

public sealed class LevelContentLoader : MonoBehaviour
{
    [SerializeField] private Transform levelRoot;

    public LevelData CurrentLevelData { get; private set; }
    public GameObject CurrentInstance { get; private set; }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
        ClearCurrent();
    }

    public bool Load(LevelData levelData)
    {
        ClearCurrent();
        CurrentLevelData = levelData;

        if (levelData == null || levelData.LevelPrefab == null)
        {
            DebugLogger.LogWarning(
                "[LevelContentLoader] No level prefab assigned; using scene-authored gameplay.");
            return false;
        }

        Transform parent = levelRoot != null ? levelRoot : transform;
        CurrentInstance = Instantiate(levelData.LevelPrefab, parent);
        return true;
    }

    public void ClearCurrent()
    {
        if (CurrentInstance != null)
        {
            CurrentInstance.SetActive(false);
            Destroy(CurrentInstance);
            CurrentInstance = null;
        }

        CurrentLevelData = null;
    }

    private void HandleLevelLoaded(int _)
    {
        Load(LevelManager.Instance != null
            ? LevelManager.Instance.CurrentLevelData
            : null);
    }
}
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a05`.

- [ ] **Step 3: Migrate LevelManager progression to SaveService**

Remove `CurrentLevelPrefKey`, `PlayerPrefs.GetInt`, `PlayerPrefs.SetInt`, and `PlayerPrefs.Save`.

Use:

```csharp
protected override void Awake()
{
    base.Awake();
    CurrentLevelIndex = SaveService.Instance != null
        ? SaveService.Instance.CurrentLevelIndex
        : 0;
}

private void SaveProgress()
{
    SaveService.Instance?.SetCurrentLevelIndex(CurrentLevelIndex);
}
```

Guard retry so it is only valid after failure:

```csharp
public void RetryLevel()
{
    if (State != LevelState.Failed)
        return;

    int retryLevelIndex = CurrentLevelIndex;
    GameEvents.RaiseLevelRetried(retryLevelIndex);
    LoadLevel(retryLevelIndex, resetAttempt: false);
    StartLevel();
}
```

Keep `LoadLevel` saving only when the level index changed; `SaveService` already suppresses unchanged writes.

---

### Task 4: Add a single UI-to-level flow controller

**Files:**

- Create: `Assets/Scripts/Level/GameFlowController.cs`
- Create: `Assets/Scripts/Level/GameFlowController.cs.meta`
- Modify: `Assets/Scripts/UI/MainMenuPlayStarter.cs`
- Modify: `Assets/Scripts/UI/UIAnimator.cs`
- Modify: `Assets/Scripts/Level/LevelFailPopup.cs`

- [ ] **Step 1: Add GameFlowController**

Create `GameFlowController.cs`:

```csharp
public sealed class GameFlowController : SingletonMonoBehaviour<GameFlowController>
{
    public bool StartCurrentLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null)
            return false;

        manager.EnsureCurrentLevelLoaded();
        manager.StartLevel();
        return manager.State == LevelState.Playing;
    }

    public bool RetryCurrentLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Failed)
            return false;

        manager.RetryLevel();
        return manager.State == LevelState.Playing;
    }

    public bool ContinueToNextLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Completed)
            return false;

        manager.LoadLevel(manager.CurrentLevelIndex);
        manager.StartLevel();
        return manager.State == LevelState.Playing;
    }
}
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a06`.

- [ ] **Step 2: Route the main-menu play button through the controller**

Replace `MainMenuPlayStarter.HandlePlayPressed` with:

```csharp
public void HandlePlayPressed()
{
    GameFlowController.Instance?.StartCurrentLevel();
}
```

- [ ] **Step 3: Remove the gameplay side effect from UIAnimator**

Delete `StartGameplayIfNeeded()` and remove its call from `PlayAnimation()`. `UIAnimator` must only build and play its DOTween UI sequence.

- [ ] **Step 4: Route fail retry through the controller**

Replace `LevelFailPopup.HandleRetryClicked` with:

```csharp
private void HandleRetryClicked()
{
    Hide();
    GameFlowController.Instance?.RetryCurrentLevel();
}
```

---

### Task 5: Separate reward state from reward visuals

**Files:**

- Create: `Assets/Scripts/Level/LevelRewardConfig.cs`
- Create: `Assets/Scripts/Level/LevelRewardConfig.cs.meta`
- Create: `Assets/Settings/LevelRewardConfig.asset`
- Create: `Assets/Settings/LevelRewardConfig.asset.meta`
- Modify: `Assets/Scripts/Level/LevelUpPopup.cs`
- Modify: `Assets/Scripts/UI/FlyToUIEffect.cs`

- [ ] **Step 1: Add the reward policy asset type**

Create `LevelRewardConfig.cs`:

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "LevelRewardConfig", menuName = "Template/Economy/Level Reward Config")]
public sealed class LevelRewardConfig : ScriptableObject
{
    [SerializeField, Min(0)] private int baseReward = 3;
    [SerializeField, Min(0)] private int rewardPerLevel = 3;
    [SerializeField, Min(0)] private int maximumReward;

    public int GetReward(int completedLevelIndex)
    {
        long reward = baseReward +
            (long)Mathf.Max(0, completedLevelIndex) * rewardPerLevel;

        if (maximumReward > 0)
            reward = System.Math.Min(reward, maximumReward);

        return (int)System.Math.Min(reward, int.MaxValue);
    }
}
```

Use script meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a07`.

- [ ] **Step 2: Create the default reward asset**

Create:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a07, type: 3}
  m_Name: LevelRewardConfig
  m_EditorClassIdentifier:
  baseReward: 3
  rewardPerLevel: 3
  maximumReward: 0
```

Use asset meta GUID `7a4de103af9d4c7c96b2c6a4d10f1a08`.

- [ ] **Step 3: Reduce FlyToUIEffect to a visual completion callback**

Replace `FlyToUIEffect.cs` with:

```csharp
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class FlyToUIEffect : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField, Min(0f)] private float completionDelay = 0.25f;

    private Coroutine routine;
    private Action pendingCompletion;

    public void Play(Action onComplete)
    {
        CompletePending();

        if (target == null || completionDelay <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        pendingCompletion = onComplete;
        target.DOKill();
        target.DOPunchScale(Vector3.one * 0.15f, completionDelay, 3)
            .SetUpdate(true);
        routine = StartCoroutine(CompleteAfterDelay());
    }

    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSecondsRealtime(completionDelay);
        routine = null;
        CompletePending();
    }

    private void OnDisable()
    {
        target?.DOKill();
        CompletePending();
    }

    private void CompletePending()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Action callback = pendingCompletion;
        pendingCompletion = null;
        callback?.Invoke();
    }
}
```

- [ ] **Step 4: Rewrite LevelUpPopup around one reward transaction**

Keep the existing `UIPopup` base and `GameEvents.OnLevelCompleted` subscription, but replace ad and delayed manager logic with these fields and handlers:

```csharp
[SerializeField] private TMP_Text levelText;
[SerializeField] private TMP_Text rewardAmountText;
[SerializeField] private Button getButton;
[SerializeField] private ParticleImage particleImage;
[SerializeField] private LevelRewardConfig rewardConfig;
[SerializeField] private FlyToUIEffect flyEffect;

private int pendingReward;
private bool rewardClaimed;

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

    Show();
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

private void HandleCollectClicked()
{
    if (rewardClaimed)
        return;

    rewardClaimed = true;
    if (getButton != null)
        getButton.interactable = false;

    CurrencyWallet.Instance?.Add(pendingReward);
    particleImage?.Play();
    Hide();

    if (flyEffect != null)
        flyEffect.Play(ContinueToNextLevel);
    else
        ContinueToNextLevel();
}

private static void ContinueToNextLevel()
{
    GameFlowController.Instance?.ContinueToNextLevel();
}
```

Remove:

- `reward2xButton`
- rewarded-ad handlers and availability methods
- `_onMissionConfirmed`
- manager load coroutine and `nextLevelDelay`
- direct `UIManager.ScoreAdd`
- direct `LevelManager.LoadLevel` / `StartLevel`

---

### Task 6: Remove booster polling and migrate audio/settings

**Files:**

- Modify: `Assets/Scripts/UI/BoosterUIManager.cs`
- Modify: `Assets/Scripts/Audio/SoundManager.cs`

- [ ] **Step 1: Make BoosterUIManager event-driven**

Remove:

```csharp
private Coroutine waitRoutine;
private void Start() => InvokeRepeating(nameof(RefreshAll), 1, 2);
private IEnumerator WaitForScoreManager() { ... }
private bool IsRewardedAdReady() => false;
private bool TryPurchaseWithAd() => false;
```

Subscribe directly on enable:

```csharp
private void OnEnable()
{
    foreach (BoosterSlot slot in boosterSlots)
        slot?.Setup(this);

    if (CurrencyWallet.Instance != null)
        CurrencyWallet.Instance.BalanceChanged += HandleBalanceChanged;

    RefreshAll();
}

private void OnDisable()
{
    if (CurrencyWallet.Instance != null)
        CurrencyWallet.Instance.BalanceChanged -= HandleBalanceChanged;

    foreach (BoosterSlot slot in boosterSlots)
        slot?.Teardown();
}

private void HandleBalanceChanged(int _)
{
    RefreshAll();
}

internal bool TrySpend(int cost)
{
    return CurrencyWallet.Instance != null &&
           CurrencyWallet.Instance.TrySpend(cost);
}
```

Use `SaveService` in each slot:

```csharp
private int LoadOwnedCount()
{
    return SaveService.Instance != null
        ? SaveService.Instance.GetBoosterCount(id)
        : 0;
}

private void SaveOwnedCount()
{
    SaveService.Instance?.SetBoosterCount(id, ownedCount);
}

private bool CanAffordBooster()
{
    return CurrencyWallet.Instance != null &&
           CurrencyWallet.Instance.Balance >= price;
}
```

`PurchaseBooster` must only spend currency, and `Refresh` must set `watchIcon` inactive and show the numeric price:

```csharp
private void PurchaseBooster()
{
    if (owner != null && owner.TrySpend(price))
        GrantBooster();
    else
        Refresh();
}

public void Refresh()
{
    ownedCount = LoadOwnedCount();
    bool hasBooster = ownedCount > 0;

    purchaseContainer?.SetActive(!hasBooster);
    ownedContainer?.SetActive(hasBooster);
    watchIcon?.SetActive(false);

    if (ownedCountLabel != null)
        ownedCountLabel.SetText("{0}", ownedCount);
    if (priceLabel != null)
        priceLabel.SetText("{0}", price);
    if (purchaseButton != null)
        purchaseButton.interactable = CanAffordBooster();
    if (boosterButton != null)
        boosterButton.interactable = hasBooster;
}
```

- [ ] **Step 2: Migrate SoundManager settings**

Replace `LoadPreferences`:

```csharp
private void LoadPreferences()
{
    SaveService save = SaveService.Instance;
    isBgMusicOn = save == null || save.MusicEnabled;
    isSfxOn = save == null || save.SfxEnabled;
    isVibrationOn = save == null || save.VibrationEnabled;
}
```

Initialize toggles without invoking callbacks:

```csharp
bgMusicToggle?.SetIsOnWithoutNotify(isBgMusicOn);
sfxToggle?.SetIsOnWithoutNotify(isSfxOn);
vibrationToggle?.SetIsOnWithoutNotify(isVibrationOn);
```

Then add listeners only when the toggle is non-null. Replace direct `PlayerPrefs.SetInt` calls:

```csharp
SaveService.Instance?.SetMusicEnabled(on);
SaveService.Instance?.SetSfxEnabled(on);
SaveService.Instance?.SetVibrationEnabled(on);
```

Remove the unused serialized `retryButton`. Add `OnDestroy` listener cleanup for all non-null toggles.

---

### Task 7: Update prefab serialization

**Files:**

- Modify: `Assets/Prefabs/UI prefabs/UI Manager.prefab`
- Modify: `Assets/Prefabs/UI prefabs/UI Settings/Level Up Effects.prefab`
- Modify: `Assets/Prefabs/UI prefabs/UI Settings/Sound Manager.prefab`

- [ ] **Step 1: Add modular services to UI Manager prefab**

On root GameObject fileID `7303356749682882291`, append these MonoBehaviour components:

```yaml
# SaveService
m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a02, type: 3}

# CurrencyWallet
m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a03, type: 3}

# CurrencyDisplay
m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a04, type: 3}
currencyText: {fileID: 6298439037724017117}

# GameFlowController
m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a06, type: 3}

# LevelContentLoader
m_Script: {fileID: 11500000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a05, type: 3}
levelRoot: {fileID: 880000000000000007}
```

Use unique component fileIDs `880000000000000001` through `880000000000000005` and list them in the root `m_Component` array.

Add an active child `LevelRoot`:

```yaml
--- !u!1 &880000000000000006
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 880000000000000007}
  m_Layer: 0
  m_Name: LevelRoot
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &880000000000000007
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 880000000000000006}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 9164304886976461349}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
```

Append transform `880000000000000007` to root transform `9164304886976461349.m_Children`.

- [ ] **Step 2: Remove score ownership and broken booster callbacks**

Change the `UIManager` block to contain only:

```yaml
inputBlocker: {fileID: 9087662377492969269}
```

Delete `scoreTxt` and `scoreMultiplier`.

In `BoosterUIManager` serialized slot `addtime`, replace the persistent `LevelManager.AddTime` call with:

```yaml
onBoosterTriggered:
  m_PersistentCalls:
    m_Calls: []
```

Set watch-ad GameObjects `2636968196279737557` and `7112194730322326439` inactive.

- [ ] **Step 3: Simplify the Level Up Effects prefab**

Set GameObject `700568474195572514` (`2x rewarded next`) to:

```yaml
m_IsActive: 0
```

Replace FlyToUIEffect serialized fields with:

```yaml
target: {fileID: 0}
completionDelay: 0.25
```

Replace LevelUpPopup serialized fields with:

```yaml
fadeTime: 0.2
levelText: {fileID: 4490381019757144804}
rewardAmountText: {fileID: 8513682931579686091}
getButton: {fileID: 3458742469162843953}
particleImage: {fileID: 0}
rewardConfig: {fileID: 11400000, guid: 7a4de103af9d4c7c96b2c6a4d10f1a08, type: 2}
flyEffect: {fileID: 8524498362424636395}
```

Delete stale `rootCg`, `lvlTxtOnScreen`, and `reward2xButton` serialized fields.

- [ ] **Step 4: Remove obsolete SoundManager prefab data**

Delete:

```yaml
retryButton: {fileID: 0}
```

Preserve the AudioSource and toggle references.

---

### Task 8: Static reference and Unity compilation checks

**Files:**

- Inspect: `Assets/Scripts/**/*.cs`
- Inspect: `Assets/**/*.prefab`
- Inspect: `Assets/**/*.unity`
- Inspect: `C:/tmp/pt-p0-compile.log`

- [ ] **Step 1: Confirm runtime PlayerPrefs ownership**

Run:

```powershell
rg -n "PlayerPrefs" Assets/Scripts -g "*.cs" -g "!PlayerPrefsEditor/**"
```

Expected: runtime matches only in `Assets/Scripts/Save/SaveService.cs`.

- [ ] **Step 2: Confirm removed APIs and stubs have no references**

Run:

```powershell
rg -n "UIManager\\.Instance\\.(Score|ScoreAdd)|ScoreChanged|OnScoreChanged|RaiseScoreChanged|IsRewardedAdReady|TryPurchaseWithAd|HandleRewardedAdAvailabilityChanged|RestoreButtonsAfterRewardedUnavailable|m_MethodName: AddTime" Assets
```

Expected: no matches.

- [ ] **Step 3: Confirm new script GUID usage**

Run:

```powershell
rg -n "7a4de103af9d4c7c96b2c6a4d10f1a0[2-8]" Assets
```

Expected: every new MonoBehaviour or ScriptableObject GUID appears in its `.meta`; service/view/loader/controller GUIDs also appear in the expected prefab, and reward GUIDs appear in the reward asset/prefab.

- [ ] **Step 4: Compile without creating tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.68f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\PTUnity6\PT\My project' `
  -logFile 'C:\tmp\pt-p0-compile.log'
```

Expected: Unity exits with code `0`.

- [ ] **Step 5: Inspect compiler and missing-script output**

Run:

```powershell
Select-String -Path 'C:\tmp\pt-p0-compile.log' `
  -Pattern 'error CS|Scripts have compiler errors|Missing script|The referenced script'
```

Expected: no matches.

- [ ] **Step 6: Respect repository constraints**

Do not create tests, branches, commits, staged changes, pushes, or pull requests.
