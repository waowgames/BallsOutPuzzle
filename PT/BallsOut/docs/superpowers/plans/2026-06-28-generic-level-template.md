# Generic Level Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. The user explicitly prohibited Git/GitHub operations, Unity test creation, and `verification-before-completion`.

**Goal:** Remove Cannon Rings and convert the project into a reusable Unity template whose level core only owns load, start, complete, fail, and retry lifecycle.

**Architecture:** `LevelManager` will be an event-driven lifecycle orchestrator with explicit state and no `Update`. Game-specific completion conditions, timers, moves, missions, lanes, bowls, revives, and input shortcuts will not live in the shared level core. Existing win/fail popups remain connected through `GameEvents`.

**Tech Stack:** Unity 6000.0.68f1, C#, ScriptableObject, PlayerPrefs, prefab YAML serialization

---

## File map

- `Assets/Scripts/Level/LevelState.cs`: lifecycle state enum.
- `Assets/Scripts/Level/LevelManager.cs`: lifecycle, active level context, retry, and saved progression.
- `Assets/Scripts/Level/LevelConfig.cs`: ordered level lookup and optional looping.
- `Assets/Scripts/Level/LevelData.cs`: extension base for game-specific level assets.
- `Assets/Scripts/Level/GameEvents.cs`: shared level, score, and UI events.
- `Assets/Scripts/Level/LevelDisplayUI.cs`: event-driven level number label.
- `Assets/Scripts/Level/LevelFailPopup.cs`: fail presentation and retry only.
- `Assets/Scripts/Level/LevelUpPopup.cs`: preserved win presentation and next-level continuation.
- `Assets/Scripts/Core`: singleton and editor-only logging helpers.
- `Assets/Scripts/UI`: shared UI manager, popup/screen bases, boosters, toggles, and UI effects.
- `Assets/Scripts/Audio`: sound settings and playback.
- `Assets/Scripts/Effects`: generic transform effect.

### Task 1: Remove isolated gameplay and obsolete optional level modules

**Files:**

- Delete: `Assets/Scripts/CannonRings`
- Delete: `Assets/Scripts/CannonRings.meta`
- Delete: `Assets/Scripts/Level/LevelCountdownUI.cs`
- Delete: `Assets/Scripts/Level/LevelCountdownUI.cs.meta`
- Delete: `Assets/Scripts/Level/LevelMissionManager.cs`
- Delete: `Assets/Scripts/Level/LevelMissionManager.cs.meta`
- Modify: `Assets/Prefabs/UI prefabs/Main UI Canvas.prefab`
- Modify: `Assets/Prefabs/UI prefabs/UI Manager.prefab`

- [ ] Remove the entire Cannon Rings folder and its folder meta because GUID scanning found no serialized scene, prefab, or ScriptableObject reference.
- [ ] Remove component `4538395641672566255` from GameObject `8952147040134566830` and delete its MonoBehaviour block from `Main UI Canvas.prefab`.
- [ ] Remove component `1573607369332994534` from GameObject `7303356749682882291` and delete its MonoBehaviour block from `UI Manager.prefab`.
- [ ] Delete the countdown and mission scripts together with their meta files.

### Task 2: Replace the level core with a lifecycle-only implementation

**Files:**

- Create: `Assets/Scripts/Level/LevelState.cs`
- Create: `Assets/Scripts/Level/LevelState.cs.meta`
- Move: `Assets/Scripts/Old/Managers/LevelManager.cs` → `Assets/Scripts/Level/LevelManager.cs`
- Move: `Assets/Scripts/Old/Managers/LevelManager.cs.meta` → `Assets/Scripts/Level/LevelManager.cs.meta`
- Modify: `Assets/Scripts/Level/GameEvents.cs`
- Modify: `Assets/Scripts/Level/LevelConfig.cs`

- [ ] Add the exact lifecycle states:

```csharp
public enum LevelState
{
    Uninitialized,
    Loaded,
    Playing,
    Completed,
    Failed
}
```

- [ ] Rewrite `LevelManager` so it exposes `CurrentLevelIndex`, `DisplayedLevel1Based`, `CurrentAttempt`, `CurrentLevelData`, and `State`; contains no `Update`; permits a null config; guards complete/fail unless state is `Playing`; advances and saves only on complete; and reloads the same index on retry.
- [ ] Keep these public calls for existing integrations:

```csharp
public void EnsureCurrentLevelLoaded();
public void LoadLevel(int index);
public void StartLevel();
public void CompleteLevel();
public void FailLevel();
public void RetryLevel();
public int CurrentLoopCount { get; }
```

- [ ] Remove timer, move, voxel, revive, score calculation, DOTween, Odin, and keyboard shortcut members from `LevelManager`.
- [ ] Reduce `GameEvents` to level, score, screen, and popup events plus matching raise methods and `ClearAll`.
- [ ] Remove `loopDifficultyMultiplier` and `loopTimeLimitReduction` from `LevelConfig`; keep safe list lookup, optional looping, unique count, and loop count.

### Task 3: Simplify level UI without breaking win/fail flow

**Files:**

- Modify: `Assets/Scripts/Level/LevelDisplayUI.cs`
- Modify: `Assets/Scripts/Level/LevelFailPopup.cs`
- Move: `Assets/Scripts/Old/Utilities/LevelUpPopup.cs` → `Assets/Scripts/Level/LevelUpPopup.cs`
- Move: `Assets/Scripts/Old/Utilities/LevelUpPopup.cs.meta` → `Assets/Scripts/Level/LevelUpPopup.cs.meta`
- Modify: `Assets/Prefabs/UI prefabs/UI Manager.prefab`

- [ ] Reduce `LevelDisplayUI` to a cached `TextMeshProUGUI`, configurable prefix, `OnLevelLoaded` subscription, and direct formatting from the event index. Remove move text creation and DOTween warning behavior.
- [ ] Reduce `LevelFailPopup` to instance caching, `OnLevelFailed` subscription, popup show/hide, and retry button setup/teardown. Remove add-time, rewarded-ad, bonus-seconds, and bonus-moves behavior.
- [ ] Preserve `LevelFailPopup.ShowIfAvailable()` as the inactive-object fallback used by `LevelManager.FailLevel`.
- [ ] Change `LevelUpPopup.HandleLevelCompleted(int index)` to display `index + 1`, ensuring it shows the completed level after progression advances.
- [ ] In `UI Manager.prefab`, leave only `fadeTime` and `retryButton` serialized on `LevelFailPopup`, and set inactive GameObject `9062712285897093681` so the unsupported secondary fail button cannot receive input or add overdraw.

### Task 4: Reorganize reusable scripts and preserve Unity GUIDs

**Files:**

- Move with meta: `Assets/Scripts/Old/DebugLogger.cs` → `Assets/Scripts/Core/DebugLogger.cs`
- Move with meta: `Assets/Scripts/Old/Managers/SingletonMonoBehaviour.cs` → `Assets/Scripts/Core/SingletonMonoBehaviour.cs`
- Move with meta: `Assets/Scripts/Old/Managers/UIManager.cs` → `Assets/Scripts/UI/UIManager.cs`
- Move with meta: `Assets/Scripts/Old/Managers/UIPopup.cs` → `Assets/Scripts/UI/UIPopup.cs`
- Move with meta: `Assets/Scripts/Old/Managers/UIScreen.cs` → `Assets/Scripts/UI/UIScreen.cs`
- Move with meta: `Assets/Scripts/Old/Managers/BoosterUIManager.cs` → `Assets/Scripts/UI/BoosterUIManager.cs`
- Move with meta: `Assets/Scripts/Old/Upgrade Systems/UIAnimator.cs` → `Assets/Scripts/UI/UIAnimator.cs`
- Move with meta: `Assets/Scripts/Old/Utilities/AnimatedToggle.cs` → `Assets/Scripts/UI/AnimatedToggle.cs`
- Move with meta: `Assets/Scripts/Old/Utilities/FlyToUIEffect.cs` → `Assets/Scripts/UI/FlyToUIEffect.cs`
- Move with meta: `Assets/Scripts/Old/Utilities/SoundManager.cs` → `Assets/Scripts/Audio/SoundManager.cs`
- Move with meta: `Assets/Scripts/Old/System/RotateAround.cs` → `Assets/Scripts/Effects/RotateAround.cs`
- Delete after empty: `Assets/Scripts/Old` and nested folder meta files

- [ ] Create semantic destination folders and Unity folder meta files with unique GUIDs.
- [ ] Move every retained script together with its existing `.meta` file; do not regenerate script GUIDs.
- [ ] Remove empty `Old` directories and their folder meta files.
- [ ] Scan the retained C# source for `CannonRing`, timer, move, mission, voxel, lane, bowl, and revive API references; expected result is no match.

### Task 5: Compile and serialized-reference checks

**Files:**

- Inspect: `Assets/**/*.unity`
- Inspect: `Assets/**/*.prefab`
- Inspect: `C:/tmp/pt-template-compile.log`

- [ ] Search all prefab, scene, and asset YAML for the deleted script GUIDs:

```powershell
rg -n "ed228d9a54af2be4b9dfe3c951cecae2|cdd39e02855741f4a9f6d956583885a2" Assets
```

Expected: no matches.

- [ ] Run Unity compilation without creating tests:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.68f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'C:\PTUnity6\PT\My project' `
  -logFile 'C:\tmp\pt-template-compile.log'
```

Expected: Unity exits with code `0`.

- [ ] Inspect the log:

```powershell
Select-String -Path 'C:\tmp\pt-template-compile.log' -Pattern 'error CS|Scripts have compiler errors|Missing script'
```

Expected: no matches.

- [ ] Do not create tests, branches, commits, staging changes, pushes, or pull requests.
