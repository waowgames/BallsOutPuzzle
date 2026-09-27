using System;
using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugButtonWidget : TSDebugWidget
    {
        private const int ButtonHeight = 72;
        private const int FontSize = 28;
        private const int HorizontalPadding = 16;

        private static readonly Color TextColor = Color.white;

        private Button _button;
        private Text _text;

        private void Awake()
        {
            if (_button == null)
                BuildLayout();
        }

        public static TSDebugButtonWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugButtonWidget),
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(TSDebugButtonWidget));

            return widgetObject.GetComponent<TSDebugButtonWidget>();
        }

        public void SetText(string label)
        {
            if (_text != null)
                _text.text = label;
        }

        public void SetAction(Action onClick)
        {
            if (_button == null)
                return;

            _button.onClick.RemoveAllListeners();

            if (onClick != null)
                _button.onClick.AddListener(() => onClick.Invoke());
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null)
                _button.interactable = interactable;
        }

        private void BuildLayout()
        {
            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = Scaled(ButtonHeight);
            layoutElement.preferredHeight = Scaled(ButtonHeight);

            var image = GetComponent<Image>();
            _button = GetComponent<Button>();
            TSDebugBlueButtonStyle.Apply(image, _button);
            _button.targetGraphic = image;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(transform, false);
            StretchToParent(textRect);
            textRect.offsetMin = ScaledVector(HorizontalPadding, 0f);
            textRect.offsetMax = ScaledVector(-HorizontalPadding, 0f);

            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = Scaled(FontSize);
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = TextColor;
            _text.raycastTarget = false;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
