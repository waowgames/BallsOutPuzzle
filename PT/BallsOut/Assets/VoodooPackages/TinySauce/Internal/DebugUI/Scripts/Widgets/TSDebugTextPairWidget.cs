using System;
using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Common.Extension;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    public class TSDebugTextPairWidget : TSDebugWidget
    {
        private const string PrefabResourcePath = "Prefabs/TSDebugTextPairWidget";
        private const int RowHeight = 68;
        private const int CopyIconSize = 24;

        private static TSDebugTextPairWidget _prefab;

        [SerializeField] private Text _label;
        [SerializeField] private Text _value;
        [SerializeField] private Button _copyButton;

        private string _copyValue;
        private Button _labelCopyButton;
        private Button _valueCopyButton;

        private void Awake()
        {
            ApplyStyle();
            SetCopyClickAction(OnCopyClicked);
        }

        public static TSDebugTextPairWidget Instantiate()
        {
            if (_prefab == null)
                _prefab = Resources.Load<TSDebugTextPairWidget>(PrefabResourcePath);

            return UnityEngine.Object.Instantiate(_prefab);
        }

        public void SetPair(string label, string value)
        {
            if (_label != null)
                _label.text = label;

            if (_value != null)
                _value.text = value;
        }

        public void SetCopyValue(string value)
        {
            _copyValue = value;
            SetCopyEnabled(!string.IsNullOrEmpty(value));
        }

        public void SetCopyEnabled(bool enabled)
        {
            if (_copyButton != null)
                _copyButton.gameObject.SetActive(enabled);

            SetTextCopyEnabled(_label, _labelCopyButton, enabled);
            SetTextCopyEnabled(_value, _valueCopyButton, enabled);
        }

        public void SetCopyAction(Action onCopy)
        {
            SetCopyClickAction(onCopy);
            SetCopyEnabled(onCopy != null);
        }

        private void OnCopyClicked()
        {
            if (!string.IsNullOrEmpty(_copyValue))
                _copyValue.CopyToClipboard();
        }

        private void ApplyStyle()
        {
            ScalePrefabLayout();

            var background = GetComponent<Image>();
            if (background != null)
                TSDebugBlueButtonStyle.ApplySurface(background, TSDebugBlueButtonStyle.CardColor);

            ApplyTextStyle(_label, FontStyle.Bold);
            ApplyTextStyle(_value, FontStyle.Normal);
            _labelCopyButton = CreateTextCopyButton(_label);
            _valueCopyButton = CreateTextCopyButton(_value);

            if (_copyButton == null)
                return;

            ApplyCopyButtonStyle();
        }

        private void ApplyCopyButtonStyle()
        {
            var background = _copyButton.GetComponent<Image>();
            if (background == null)
                background = _copyButton.gameObject.AddComponent<Image>();

            TSDebugBlueButtonStyle.ApplySurface(background, TSDebugBlueButtonStyle.SecondaryButtonColor);
            _copyButton.targetGraphic = background;
            _copyButton.transition = Selectable.Transition.ColorTint;
            _copyButton.colors = new ColorBlock
            {
                normalColor = TSDebugBlueButtonStyle.SecondaryButtonColor,
                highlightedColor = TSDebugBlueButtonStyle.PanelColor,
                pressedColor = TSDebugBlueButtonStyle.SecondaryButtonPressedColor,
                selectedColor = TSDebugBlueButtonStyle.PanelColor,
                disabledColor = TSDebugBlueButtonStyle.DisabledColor,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };

            foreach (var image in _copyButton.GetComponentsInChildren<Image>(true))
            {
                if (image == background)
                    continue;

                image.color = TSDebugBlueButtonStyle.PrimaryColor;
                image.preserveAspect = true;
                image.raycastTarget = false;
                CenterCopyIcon(image.transform as RectTransform);
            }

            foreach (var text in _copyButton.GetComponentsInChildren<Text>(true))
                text.gameObject.SetActive(false);
        }

        private void SetCopyClickAction(Action onClick)
        {
            SetButtonAction(_copyButton, onClick);
            SetButtonAction(_labelCopyButton, onClick);
            SetButtonAction(_valueCopyButton, onClick);
        }

        private static void SetButtonAction(Button button, Action onClick)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();

            if (onClick != null)
                button.onClick.AddListener(() => onClick.Invoke());
        }

        private static Button CreateTextCopyButton(Text text)
        {
            if (text == null)
                return null;

            var button = text.GetComponent<Button>();
            if (button == null)
                button = text.gameObject.AddComponent<Button>();

            button.targetGraphic = text;
            button.transition = Selectable.Transition.None;
            text.raycastTarget = false;
            return button;
        }

        private static void SetTextCopyEnabled(Text text, Selectable selectable, bool enabled)
        {
            if (text != null)
                text.raycastTarget = enabled;

            if (selectable != null)
                selectable.interactable = enabled;
        }

        private static void CenterCopyIcon(RectTransform iconRect)
        {
            if (iconRect == null)
                return;

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = ScaledVector(CopyIconSize, CopyIconSize);
        }

        private void ScalePrefabLayout()
        {
            var rectTransform = (RectTransform)transform;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Scaled(RowHeight));

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minHeight = Scaled(RowHeight);
                layoutElement.preferredHeight = layoutElement.preferredHeight >= 0f
                    ? Scaled(layoutElement.preferredHeight)
                    : layoutElement.preferredHeight;
            }

            foreach (var childLayoutElement in GetComponentsInChildren<LayoutElement>(true))
            {
                if (childLayoutElement.gameObject == gameObject)
                    continue;

                ScaleLayoutElement(childLayoutElement);
            }

            foreach (var layoutGroup in GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
                ScaleLayoutGroup(layoutGroup);
        }

        private static void ApplyTextStyle(Text text, FontStyle fontStyle)
        {
            if (text == null)
                return;

            text.color = TSDebugBlueButtonStyle.DarkTextColor;
            text.fontSize = Scaled(text.fontSize);
            text.resizeTextMinSize = Scaled(text.resizeTextMinSize);
            text.resizeTextMaxSize = Scaled(text.resizeTextMaxSize);
            text.fontStyle = fontStyle;
        }
    }
}
