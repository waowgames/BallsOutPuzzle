using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial card popup. Extends <see cref="UIPopup"/> for standardized
/// show/hide and popup-stack integration.
/// Preserves existing icon sequence animation logic.
/// </summary>
public class TutorialCardView : UIPopup
{
    [Header("Tutorial Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image dimBackground;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button continueButton;

    public override string PopupId => "TutorialCardView";

    /// <summary>Whether the tutorial card is visible (legacy compat).</summary>
    public bool IsVisible => root != null && root.activeSelf;

    private Action onContinue;
    private Coroutine iconSequenceRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinue);

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinue);
    }

    // ══════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Shows the tutorial card with the given content.
    /// </summary>
    public void ShowCard(string title, string description, Sprite icon = null,
                         List<Sprite> iconSequence = null, float frameDuration = 0.15f,
                         bool loopSequence = true, Action onContinueCallback = null)
    {
        onContinue = onContinueCallback;

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        if (iconImage != null && icon != null) iconImage.sprite = icon;

        if (root != null) root.SetActive(true);
        if (dimBackground != null) dimBackground.gameObject.SetActive(true);

        // Start icon sequence if provided
        if (iconSequence != null && TryGetValidSequence(iconSequence, out var validSeq))
        {
            StopIconSequence();
            iconSequenceRoutine = StartCoroutine(PlayIconSequence(validSeq, frameDuration, loopSequence));
        }

        // Call UIPopup base Show
        Show();
    }

    /// <summary>Hides the tutorial card immediately without animation.</summary>
    public void HideImmediate()
    {
        StopIconSequence();
        if (root != null) root.SetActive(false);
        if (dimBackground != null) dimBackground.gameObject.SetActive(false);
        onContinue = null;
        SetHiddenImmediate();
    }

    // ── UIPopup hooks ──────────────────────────────────────

    protected override void OnHide()
    {
        StopIconSequence();
        if (root != null) root.SetActive(false);
        if (dimBackground != null) dimBackground.gameObject.SetActive(false);
        onContinue = null;
    }

    // ── Icon Sequence (preserved from original) ────────────

    private IEnumerator PlayIconSequence(IReadOnlyList<Sprite> sequence, float frameDuration, bool loop)
    {
        if (sequence.Count == 0 || iconImage == null)
            yield break;

        var wait = new WaitForSeconds(Mathf.Max(0.01f, frameDuration));

        do
        {
            for (var i = 0; i < sequence.Count; i++)
            {
                iconImage.sprite = sequence[i];
                yield return wait;
            }
        } while (loop);

        iconSequenceRoutine = null;
    }

    private void StopIconSequence()
    {
        if (iconSequenceRoutine == null) return;
        StopCoroutine(iconSequenceRoutine);
        iconSequenceRoutine = null;
    }

    private static bool TryGetValidSequence(List<Sprite> sequence, out List<Sprite> validSequence)
    {
        validSequence = null;
        if (sequence == null || sequence.Count == 0) return false;

        validSequence = new List<Sprite>(sequence.Count);
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] != null)
                validSequence.Add(sequence[i]);
        }
        return validSequence.Count > 0;
    }

    private void HandleContinue()
    {
        var callback = onContinue;
        Hide();
        callback?.Invoke();
    }
}
