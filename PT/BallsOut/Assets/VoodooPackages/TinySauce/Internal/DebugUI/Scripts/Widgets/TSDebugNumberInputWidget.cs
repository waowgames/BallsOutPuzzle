using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugNumberInputWidget : TSDebugWidget
    {
        private const int InputHeight = 72;
        private const int LabelFontSize = 24;
        private const int InputFontSize = 26;
        private const int LabelMinWidth = 200;
        private const int HorizontalPadding = 12;

        private static readonly Color LabelColor = TSDebugBlueButtonStyle.DarkTextColor;
        private static readonly Color InputTextColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color PlaceholderColor = TSDebugBlueButtonStyle.MutedTextColor;

        private Text _labelText;
        private InputField _inputField;
        private Action<float> _onValueChanged;
        private bool _isInt;
        private float _lastValue;

        private void Awake()
        {
            if (_inputField == null)
                BuildLayout();
        }

        public static TSDebugNumberInputWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugNumberInputWidget),
                typeof(RectTransform),
                typeof(TSDebugNumberInputWidget));

            return widgetObject.GetComponent<TSDebugNumberInputWidget>();
        }

        public void SetLabel(string label)
        {
            if (_labelText != null)
                _labelText.text = label;
        }

        public void SetIsInt(bool isInt)
        {
            _isInt = isInt;

            if (_inputField != null)
                _inputField.contentType = GetInputContentType();
        }

        public void SetValue(float value)
        {
            ApplyValue(value, notify: false);
        }

        public float GetValue()
        {
            if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text))
                return 0f;

            if (_isInt)
            {
                return int.TryParse(_inputField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                    ? intValue
                    : 0f;
            }

            return float.TryParse(_inputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0f;
        }

        public void SetPlaceholder(string placeholder)
        {
            if (_inputField?.placeholder is Text placeholderText)
                placeholderText.text = placeholder ?? string.Empty;
        }

        public void SetAction(Action<float> onValueChanged)
        {
            _onValueChanged = onValueChanged;
        }

        private void BuildLayout()
        {
            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = Scaled(InputHeight);
            layoutElement.preferredHeight = Scaled(InputHeight);

            var horizontalLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = ScaledOffset(HorizontalPadding, HorizontalPadding, 8, 8);
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            _labelText = CreateText("Label", LabelFontSize, FontStyle.Bold, LabelColor, TextAnchor.MiddleLeft);
            var labelLayout = _labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = Scaled(LabelMinWidth);
            labelLayout.preferredWidth = Scaled(LabelMinWidth);
            labelLayout.flexibleWidth = 0f;

            _inputField = CreateInputField();
            _inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        private void OnInputEndEdit(string text)
        {
            ApplyValue(GetValue(), notify: true);
        }

        private void ApplyValue(float value, bool notify)
        {
            var normalizedValue = NormalizeValue(value);

            if (_inputField != null)
                _inputField.text = FormatValue(normalizedValue);

            if (notify && Mathf.Approximately(_lastValue, normalizedValue))
                return;

            _lastValue = normalizedValue;

            if (notify)
                _onValueChanged?.Invoke(normalizedValue);
        }

        private Text CreateText(string name, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(transform, false);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Scaled(fontSize);
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private InputField CreateInputField()
        {
            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(transform, false);

            var inputLayout = inputGo.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1f;
            inputLayout.minHeight = Scaled(InputHeight - 16);
            inputLayout.preferredHeight = Scaled(InputHeight - 16);

            var background = inputGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplyField(background);

            var inputRect = (RectTransform)inputGo.transform;

            var textComponent = CreateInputChildText(inputRect, "Text", InputTextColor, false);
            var placeholderComponent = CreateInputChildText(inputRect, "Placeholder", PlaceholderColor, true);

            var inputField = inputGo.GetComponent<InputField>();
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderComponent;
            inputField.contentType = GetInputContentType();
            inputField.lineType = InputField.LineType.SingleLine;

            return inputField;
        }

        private InputField.ContentType GetInputContentType() =>
            _isInt ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;

        private string FormatValue(float value) =>
            _isInt
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);

        private float NormalizeValue(float value) => _isInt ? Mathf.RoundToInt(value) : value;

        private static Text CreateInputChildText(RectTransform parent, string name, Color color, bool isPlaceholder)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            StretchToParent(textRect);
            textRect.offsetMin = ScaledVector(8f, 4f);
            textRect.offsetMax = ScaledVector(-8f, -4f);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Scaled(InputFontSize);
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = !isPlaceholder;
            text.fontStyle = isPlaceholder ? FontStyle.Italic : FontStyle.Normal;
            return text;
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
