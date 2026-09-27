using System;
using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugToggleWidget : TSDebugWidget
    {
        private const int ToggleHeight = 72;
        private const int LabelFontSize = 24;
        private const int LabelMinWidth = 200;
        private const int SwitchWidth = 112;
        private const int SwitchHeight = 64;
        private const int HorizontalPadding = 12;

        private static readonly Color LabelColor = TSDebugBlueButtonStyle.DarkTextColor;

        private Text _labelText;
        private Toggle _toggle;
        private Image _toggleImage;
        private Action<bool> _onValueChanged;
        private bool _isApplyingValue;

        private void Awake()
        {
            if (_toggle == null)
                BuildLayout();
        }

        public static TSDebugToggleWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugToggleWidget),
                typeof(RectTransform),
                typeof(TSDebugToggleWidget));

            return widgetObject.GetComponent<TSDebugToggleWidget>();
        }

        public void SetLabel(string label)
        {
            if (_labelText != null)
                _labelText.text = label;
        }

        public void SetValue(bool isOn)
        {
            ApplyValue(isOn, notify: false);
        }

        public bool GetValue() => _toggle != null && _toggle.isOn;

        public void SetAction(Action<bool> onValueChanged)
        {
            _onValueChanged = onValueChanged;
        }

        public void SetInteractable(bool interactable)
        {
            if (_toggle != null)
                _toggle.interactable = interactable;
        }

        private void BuildLayout()
        {
            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = Scaled(ToggleHeight);
            layoutElement.preferredHeight = Scaled(ToggleHeight);

            var horizontalLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = ScaledOffset(HorizontalPadding, HorizontalPadding, 4, 4);
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            _labelText = CreateText(transform, "Label", LabelFontSize, FontStyle.Bold, LabelColor, TextAnchor.MiddleLeft);
            var labelLayout = _labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = Scaled(LabelMinWidth);
            labelLayout.preferredWidth = Scaled(LabelMinWidth);
            labelLayout.flexibleWidth = 0f;

            _toggle = CreateToggle(transform);
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);

            RefreshVisuals(_toggle.isOn);
        }

        private void OnToggleValueChanged(bool isOn)
        {
            RefreshVisuals(isOn);

            if (!_isApplyingValue)
                _onValueChanged?.Invoke(isOn);
        }

        private void ApplyValue(bool isOn, bool notify)
        {
            if (_toggle == null)
                return;

            _isApplyingValue = !notify;
            _toggle.isOn = isOn;
            _isApplyingValue = false;
            RefreshVisuals(isOn);
        }

        private void RefreshVisuals(bool isOn)
        {
            TSDebugBlueButtonStyle.ApplyToggle(_toggleImage, isOn);
        }

        private static Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            TextAnchor alignment)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Scaled(fontSize);
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private Toggle CreateToggle(Transform parent)
        {
            var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            toggleGo.transform.SetParent(parent, false);

            var toggleLayout = toggleGo.AddComponent<LayoutElement>();
            toggleLayout.minWidth = Scaled(SwitchWidth);
            toggleLayout.preferredWidth = Scaled(SwitchWidth);
            toggleLayout.minHeight = Scaled(SwitchHeight);
            toggleLayout.preferredHeight = Scaled(SwitchHeight);
            toggleLayout.flexibleWidth = 0f;

            _toggleImage = toggleGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplyToggle(_toggleImage, false);

            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = _toggleImage;
            toggle.transition = Selectable.Transition.None;

            return toggle;
        }

    }
}
