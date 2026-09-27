using System;
using System.Collections;
using System.Collections.Generic;
using BallsOut;
using UnityEngine;

/// <summary>
/// Runs the in-game tutorials. Boots itself (Resources/TutorialConfig) and watches for a level
/// being played, then walks through whatever the player has not learned yet:
///   1. First level: drag a box under matching balls, watch it fill, clear every box to win.
///   2. The first time a mechanic appears: arrow boxes, two-layer boxes, ice, locks, chains,
///      stones, feeder tubes, the conveyor and the timer.
///   3. The level a booster unlocks: one free use, shown button first, then its target.
/// Tutorials are mandatory: while one runs, the clock is held, the shade blocks every touch
/// but the spotlit one, and only the box being taught can be dragged.
/// </summary>
public sealed class TutorialDirector : MonoBehaviour
{
    private const string FirstLevelId = "core_first_level";
    private const string BoosterPrefix = "booster_";
    private const string BoosterGiftPrefix = "booster_gift_";

    private static TutorialDirector instance;

    /// <summary>True while a tutorial step is on screen; the level timer waits meanwhile.</summary>
    public static bool HoldsClock => instance != null && instance.holding;

    private TutorialConfig config;
    private TutorialOverlay overlay;
    private BallBoxLevelRuntime runtime;
    private BoardGrid board;
    private Coroutine flow;
    private bool holding;
    private int taps;
    private int boosterUses;
    private string lastUsedBooster;
    private readonly List<Action> cleanups = new List<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        BoardDragInput.DragGate = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var config = Resources.Load<TutorialConfig>(TutorialConfig.ResourcePath);
        if (config == null || !config.tutorialsEnabled) return;
        var go = new GameObject("Tutorial Director");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<TutorialDirector>();
        instance.config = config;
    }

    private void OnEnable() => BoosterUIManager.OnBoosterUsed += HandleBoosterUsed;

    private void OnDisable()
    {
        BoosterUIManager.OnBoosterUsed -= HandleBoosterUsed;
        Abort();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void HandleBoosterUsed(string id)
    {
        lastUsedBooster = id;
        boosterUses++;
    }

    private void HandleTap() => taps++;

    // ---------------- Level watch ----------------

    private void Update()
    {
        LevelManager manager = LevelManager.Instance;
        BallBoxLevelRuntime active = BallBoxLevelRuntime.Active;
        bool playing = manager != null && manager.State == LevelState.Playing &&
                       active != null && active.Board != null && !active.HasWon;
        bool sameLevel = active == runtime && active != null && active.Board == board;

        if (flow != null && (!playing || !sameLevel))
            Abort();

        if (flow == null && playing && !sameLevel)
        {
            runtime = active;
            board = active.Board;
            flow = StartCoroutine(RunLevel());
        }
    }

    private void Abort()
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
        runtime = null;
        board = null;
        EndTutorial(immediate: true);
    }

    private IEnumerator RunLevel()
    {
        yield return WaitUntilCalm(config.startDelay);
        int level = LevelManager.Instance != null ? LevelManager.Instance.DisplayedLevel1Based : 1;

        if (level == 1 && !IsDone(FirstLevelId))
            yield return FirstLevel();

        yield return MechanicIntros();

        // Boosters wait until they would actually do something, then teach themselves.
        while (true)
        {
            bool pending = false;
            foreach (BoosterInfo booster in BoosterUIManager.GetBoosters())
            {
                if (booster.Button == null || !booster.Button.gameObject.activeInHierarchy) continue;
                if (level < booster.UnlockLevel || IsDone(BoosterPrefix + booster.Id)) continue;
                pending = true;
                if (!CanUseBooster(booster)) continue;
                yield return BoosterTutorial(booster);
                yield return WaitUntilCalm(0.3f);
            }
            if (!pending) break;
            yield return Wait(1f);
            yield return WaitUntilCalm(0f);
        }
        flow = null;
    }

    // ---------------- First level ----------------

    private IEnumerator FirstLevel()
    {
        BeginTutorial();
        BoxController placed = null;
        int attempts = 0;
        while (placed == null && attempts++ < 12)
        {
            TutorialMovePlan plan = TutorialBoard.PlanMove(runtime);
            if (plan == null) break;
            yield return DragStep(plan, box => placed = box);
        }

        if (placed != null)
        {
            yield return FillStep(placed);
        }
        else
        {
            // No legal move to demonstrate: explain in words.
            yield return InfoStep(config.dragText, null);
        }

        yield return InfoStep(config.winText, null, RemainingBoxHoles());
        MarkDone(FirstLevelId);
        EndTutorial();
    }

    private IEnumerator DragStep(TutorialMovePlan plan, Action<BoxController> onPlaced)
    {
        BoxController box = plan.Box;
        BoardDragInput.DragGate = candidate => candidate == box;
        overlay.SetDim(1f);
        overlay.PassThroughHoles = false;
        if (plan.Moves)
        {
            Vector2Int target = plan.Target;
            overlay.SetHoles(
                new TutorialHole(() => TutorialBoard.BoxRect(box)),
                new TutorialHole(() => TutorialBoard.DropZoneRect(board, box, target)));
            overlay.Hand.PlayDrag(() => TutorialBoard.ScreenPath(board, plan));
        }
        else
        {
            overlay.SetHoles(new TutorialHole(() => TutorialBoard.DropZoneRect(board, box, box.Origin)));
            overlay.Hand.PlayTap(() => TutorialBoard.BoxPoint(box));
        }
        overlay.SetText(config.dragText);
        overlay.ShowContinue(false);
        GameHaptics.Light();

        bool began = false, ended = false;
        BoxMovementSystem movement = runtime.Movement;
        Action<BoxController> onBegan = b => { if (b == box) began = true; };
        Action<BoxController> onEnded = b => { if (b == box) ended = true; };
        movement.OnDragBegan += onBegan;
        movement.OnDragEnded += onEnded;
        cleanups.Add(() =>
        {
            movement.OnDragBegan -= onBegan;
            movement.OnDragEnded -= onEnded;
        });

        bool handHidden = false;
        while (!ended)
        {
            // The player took over: the demo hand steps aside while they drag.
            if (began && !handHidden)
            {
                overlay.Hand.Stop();
                handHidden = true;
            }
            yield return null;
        }
        RunCleanups();

        if (!plan.Moves || TutorialBoard.Score(runtime, box, box.Origin) > 0)
        {
            GameHaptics.Medium();
            onPlaced(box);
        }
        else
        {
            // Dropped somewhere balls cannot reach: show the move again from where it is now.
            GameHaptics.Warning();
            yield return Wait(0.25f);
        }
    }

    private IEnumerator FillStep(BoxController box)
    {
        BoardDragInput.DragGate = _ => false;
        Vector2Int origin = box.Origin;
        overlay.Hand.Stop();
        overlay.SetHoles(new TutorialHole(() => TutorialBoard.DropZoneRect(board, box, origin, 2.4f)));
        overlay.SetText(config.fillText);
        overlay.ShowContinue(false);

        // Let the first balls pour in before offering to move on.
        int startFill = box.CurrentFill;
        float elapsed = 0f;
        while (elapsed < 3.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            bool enoughShown = box.CurrentFill - startFill >= 6 || box.IsCompleting || box.IsRemoved;
            if (enoughShown && elapsed >= config.minReadSeconds + 0.6f) break;
            yield return null;
        }
        yield return WaitForTap();
    }

    private TutorialHole[] RemainingBoxHoles()
    {
        var holes = new List<TutorialHole>();
        foreach (BoxController box in runtime.Boxes)
        {
            if (box == null || box.IsRemoved || box.IsCompleting) continue;
            BoxController captured = box;
            holes.Add(new TutorialHole(() => TutorialBoard.BoxRect(captured), padding: 10f));
            if (holes.Count == 4) break;
        }
        return holes.ToArray();
    }

    // ---------------- Mechanics ----------------

    private IEnumerator MechanicIntros()
    {
        var intros = new List<(string id, string text, TutorialHole[] holes)>();
        IReadOnlyList<BoxController> boxes = runtime.Boxes;

        BoxController axis = FindBox(b => b.MoveAxis != BoxMoveAxis.Free);
        if (axis != null) intros.Add(("mech_axis", config.axisText, BoxHoles(axis)));

        BoxController inner = FindBox(b => b.HasInnerLayer);
        if (inner != null) intros.Add(("mech_inner", config.innerLayerText, BoxHoles(inner)));

        BoxController frozen = FindBox(b => b.IsFrozen);
        if (frozen != null) intros.Add(("mech_ice", config.iceText, BoxHoles(frozen)));

        BoxController locked = FindBox(b => b.IsLocked);
        if (locked != null)
        {
            var lockBoxes = new List<BoxController> { locked };
            foreach (BoxController box in boxes)
                if (box != null && !box.IsRemoved && box.KeyId != null && box.KeyId == locked.LockId && lockBoxes.Count < 4)
                    lockBoxes.Add(box);
            intros.Add(("mech_lock", config.lockText, BoxHoles(lockBoxes.ToArray())));
        }

        BoxController chained = FindBox(b => b.ChainPartner != null);
        if (chained != null) intros.Add(("mech_chain", config.chainText, BoxHoles(chained, chained.ChainPartner)));

        if (runtime.Obstacles != null && runtime.Obstacles.Obstacles.Count > 0)
        {
            var holes = new List<TutorialHole>();
            foreach (BoardObstacle obstacle in runtime.Obstacles.Obstacles)
            {
                if (obstacle.IsCleared || holes.Count == 4) continue;
                RectInt area = obstacle.Area;
                holes.Add(new TutorialHole(() => TutorialBoard.CellsRect(board, area), padding: 12f));
            }
            if (holes.Count > 0) intros.Add(("mech_obstacle", config.obstacleText, holes.ToArray()));
        }

        LevelDefinition level = board.Definition;
        if (level.HasFeeders)
            intros.Add(("mech_feeder", config.feederText, new[] { new TutorialHole(() => TutorialBoard.TopRect(board)) }));
        if (level.HasConveyor)
            intros.Add(("mech_conveyor", config.conveyorText, new[] { new TutorialHole(() => TutorialBoard.TopRect(board)) }));

        LevelTimerDisplay timerDisplay = FindAnyObjectByType<LevelTimerDisplay>();
        if (LevelTimer.Instance != null && LevelTimer.Instance.HasTimeLimit && timerDisplay != null &&
            timerDisplay.gameObject.activeInHierarchy)
        {
            var rect = (RectTransform)timerDisplay.transform;
            intros.Add(("mech_timer", config.timerText, new[] { new TutorialHole(() => TutorialBoard.UIRect(rect), padding: 16f) }));
        }

        intros.RemoveAll(intro => IsDone(intro.id));
        if (intros.Count == 0) yield break;

        BeginTutorial();
        BoardDragInput.DragGate = _ => false;
        foreach (var intro in intros)
        {
            yield return InfoStep(intro.text, null, intro.holes);
            MarkDone(intro.id);
        }
        EndTutorial();
    }

    private BoxController FindBox(Predicate<BoxController> match)
    {
        foreach (BoxController box in runtime.Boxes)
            if (box != null && !box.IsRemoved && !box.IsCompleting && match(box)) return box;
        return null;
    }

    private static TutorialHole[] BoxHoles(params BoxController[] boxes)
    {
        var holes = new List<TutorialHole>();
        foreach (BoxController box in boxes)
        {
            if (box == null) continue;
            BoxController captured = box;
            holes.Add(new TutorialHole(() => TutorialBoard.BoxRect(captured)));
        }
        return holes.ToArray();
    }

    // ---------------- Boosters ----------------

    private IEnumerator BoosterTutorial(BoosterInfo booster)
    {
        // The free use is handed out once, and topped up if it was spent before the lesson ended.
        if (!IsDone(BoosterGiftPrefix + booster.Id) ||
            SaveService.Instance != null && SaveService.Instance.GetBoosterCount(booster.Id) <= 0)
        {
            BoosterUIManager.GrantFree(booster.Id, 1);
            MarkDone(BoosterGiftPrefix + booster.Id);
        }

        BeginTutorial();
        BoardDragInput.DragGate = _ => false;
        var button = (RectTransform)booster.Button.transform;
        ITargetedBooster targeted = booster.Button.GetComponent<ITargetedBooster>();
        int usesBefore = boosterUses;
        bool Used() => boosterUses != usesBefore && lastUsedBooster == booster.Id;
        bool Picking() => BoosterTargetingUI.Instance != null && BoosterTargetingUI.Instance.IsPicking;
        string name = string.IsNullOrEmpty(booster.DisplayName) ? booster.Id : booster.DisplayName;
        string text = string.Format(config.boosterUnlockText, name, booster.Description);

        while (!Used())
        {
            // Step 1: tap the booster button.
            overlay.SetDim(1f);
            overlay.PassThroughHoles = true;
            overlay.SetHoles(new TutorialHole(() => TutorialBoard.UIRect(button), circle: true, padding: 8f));
            overlay.SetText(text, booster.Icon);
            overlay.ShowContinue(false);
            overlay.Hand.PlayTap(() => TutorialBoard.UICenter(button));
            GameHaptics.Light();

            while (!Used() && !Picking())
            {
                if (!CanUseBooster(booster))
                {
                    // Nothing for it to act on right now: try again later in the level.
                    EndTutorial();
                    yield break;
                }
                yield return null;
            }
            if (Used() || targeted == null) break;

            // Step 2 (hammer, magnet): the picker dims the board itself; point at one box.
            BoxController pick = BestPick(booster);
            overlay.SetDim(0f);
            overlay.SetText(null);
            if (pick != null)
            {
                overlay.SetHoles(new TutorialHole(() => TutorialBoard.BoxRect(pick)));
                overlay.Hand.PlayTap(() => TutorialBoard.BoxPoint(pick));
            }
            else
            {
                overlay.SetHoles(RemainingBoxHoles());
                overlay.PassThroughHoles = true;
                overlay.Hand.Stop();
            }
            while (!Used() && Picking()) yield return null;
            // Picking was cancelled without a pick: back to the button.
        }

        MarkDone(BoosterPrefix + booster.Id);
        overlay.Hand.Stop();
        overlay.SetText(null);
        yield return Wait(0.35f);
        EndTutorial();
    }

    private bool CanUseBooster(BoosterInfo booster)
    {
        if (booster.Button == null || !booster.Button.gameObject.activeInHierarchy) return false;
        if (LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing) return false;
        if (BoosterTargetingUI.Instance != null && BoosterTargetingUI.Instance.IsPicking) return true;
        IBoosterAvailability availability = booster.Button.GetComponent<IBoosterAvailability>();
        return availability == null || availability.CanUseBooster();
    }

    // The box the hand suggests: one the booster can act on, with the most still to fill.
    private BoxController BestPick(BoosterInfo booster)
    {
        Func<BoxController, bool> canPick = null;
        if (booster.Button.GetComponent<HammerBooster>() != null) canPick = runtime.CanSmash;
        else if (booster.Button.GetComponent<MagnetBooster>() != null) canPick = runtime.CanMagnetize;
        BoxController best = null;
        int bestRemaining = -1;
        foreach (BoxController box in runtime.Boxes)
        {
            if (box == null || box.IsRemoved || canPick != null && !canPick(box)) continue;
            int remaining = box.Capacity - box.CurrentFill;
            if (remaining > bestRemaining)
            {
                best = box;
                bestRemaining = remaining;
            }
        }
        return best;
    }

    // ---------------- Steps ----------------

    private IEnumerator InfoStep(string text, Sprite icon, params TutorialHole[] holes)
    {
        overlay.SetDim(1f);
        overlay.PassThroughHoles = false;
        overlay.SetHoles(holes);
        overlay.SetText(text, icon);
        overlay.Hand.Stop();
        overlay.ShowContinue(false);
        GameHaptics.Light();
        yield return Wait(config.minReadSeconds);
        yield return WaitForTap();
    }

    private IEnumerator WaitForTap()
    {
        overlay.ShowContinue(true);
        int start = taps;
        while (taps == start) yield return null;
        overlay.ShowContinue(false);
        GameHaptics.Light();
    }

    private void BeginTutorial()
    {
        if (overlay == null)
        {
            overlay = TutorialOverlay.Create(config, transform);
            overlay.OnTapped += HandleTap;
        }
        holding = true;
        BoardDragInput.DragGate = _ => false;
        runtime?.Movement?.Cancel();
        overlay.Show();
    }

    private void EndTutorial(bool immediate = false)
    {
        RunCleanups();
        holding = false;
        BoardDragInput.DragGate = null;
        if (overlay != null) overlay.Hide(immediate);
    }

    private void RunCleanups()
    {
        foreach (Action cleanup in cleanups) cleanup();
        cleanups.Clear();
    }

    // Waits out popups, the hard-level intro and booster picking, then a short breath.
    private IEnumerator WaitUntilCalm(float delay)
    {
        do
        {
            while (UIManager.Instance != null && UIManager.Instance.IsGameplayBlocked ||
                   BoosterTargetingUI.Instance != null && BoosterTargetingUI.Instance.IsPicking ||
                   runtime != null && runtime.Movement != null && runtime.Movement.IsDragging)
                yield return null;
            if (delay > 0f) yield return Wait(delay);
        } while (UIManager.Instance != null && UIManager.Instance.IsGameplayBlocked);
    }

    private static IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ---------------- Save ----------------

    private static bool IsDone(string id) => SaveService.Instance != null && SaveService.Instance.IsTutorialDone(id);

    private static void MarkDone(string id) => SaveService.Instance?.MarkTutorialDone(id);
}
