using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugNumberInputWithSliderWidget : TSDebugWidget
    {
        private const int InputRowHeight = 72;
        private const int SliderHeight = 40;
        private const int LabelFontSize = 24;
        private const int InputFontSize = 26;
        private const int LabelMinWidth = 200;
        private const int HorizontalPadding = 12;
        private const int VerticalSpacing = 4;

        private static readonly Color LabelColor = TSDebugBlueButtonStyle.DarkTextColor;
        private static readonly Color InputTextColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color PlaceholderColor = TSDebugBlueButtonStyle.MutedTextColor;
        private static readonly Color SliderBackgroundColor = TSDebugBlueButtonStyle.SecondaryButtonColor;
        private static readonly Color SliderFillColor = TSDebugBlueButtonStyle.PrimaryColor;
        private static readonly Color SliderHandleColor = Color.white;

        private Text _labelText;
        private InputField _inputField;
        private Slider _slider;
        private Action<float> _onValueChanged;
        private float _minValue;
        private float _maxValue = 100f;
        private float _lastValue;
        private bool _isInt;
        private bool _isSyncing;
        private bool _isDraggingSlider;

        private void Awake()
        {
            if (_inputField == null)
                BuildLayout();
        }

        public static TSDebugNumberInputWithSliderWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugNumberInputWithSliderWidget),
                typeof(RectTransform),
                typeof(TSDebugNumberInputWithSliderWidget));

            return widgetObject.GetComponent<TSDebugNumberInputWithSliderWidget>();
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

            SetValue(GetValue());
        }

        public void SetRange(float minValue, float maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue > minValue ? maxValue : minValue + 1f;

            if (_slider != null)
            {
                _slider.minValue = _minValue;
                _slider.maxValue = _maxValue;
            }

            SetValue(GetValue());
        }

        public void SetValue(float value)
        {
            var clampedValue = Mathf.Clamp(value, _minValue, _maxValue);
            ApplyValue(clampedValue, notify: false);
        }

        public float GetValue()
        {
            if (_inputField == null || string.IsNullOrWhiteSpace(_inputField.text))
                return NormalizeValue(_minValue);

            if (_isInt)
            {
                return int.TryParse(_inputField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                    ? NormalizeValue(intValue)
                    : NormalizeValue(_minValue);
            }

            return float.TryParse(_inputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? NormalizeValue(value)
                : NormalizeValue(_minValue);
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
            layoutElement.minHeight = Scaled(InputRowHeight + SliderHeight + VerticalSpacing);
            layoutElement.preferredHeight = Scaled(InputRowHeight + SliderHeight + VerticalSpacing);

            var verticalLayout = gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = ScaledOffset(HorizontalPadding, HorizontalPadding, 8, 8);
            verticalLayout.spacing = Scaled(VerticalSpacing);
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            BuildInputRow();
            BuildSliderRow();
            BindEvents();
        }

        private void BuildInputRow()
        {
            var inputRowGo = new GameObject("InputRow", typeof(RectTransform));
            inputRowGo.transform.SetParent(transform, false);

            var inputRowLayout = inputRowGo.AddComponent<LayoutElement>();
            inputRowLayout.flexibleWidth = 1f;
            inputRowLayout.minHeight = Scaled(InputRowHeight);
            inputRowLayout.preferredHeight = Scaled(InputRowHeight);

            var horizontalLayout = inputRowGo.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            _labelText = CreateText(inputRowGo.transform, "Label", LabelFontSize, FontStyle.Bold, LabelColor, TextAnchor.MiddleLeft);
            var labelLayout = _labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = Scaled(LabelMinWidth);
            labelLayout.preferredWidth = Scaled(LabelMinWidth);
            labelLayout.flexibleWidth = 0f;

            _inputField = CreateInputField(inputRowGo.transform);
        }

        private void BuildSliderRow()
        {
            var sliderRowGo = new GameObject("SliderRow", typeof(RectTransform));
            sliderRowGo.transform.SetParent(transform, false);

            var sliderRowLayout = sliderRowGo.AddComponent<LayoutElement>();
            sliderRowLayout.flexibleWidth = 1f;
            sliderRowLayout.minHeight = Scaled(SliderHeight);
            sliderRowLayout.preferredHeight = Scaled(SliderHeight);

            var horizontalLayout = sliderRowGo.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            var spacerGo = new GameObject("Spacer", typeof(RectTransform));
            spacerGo.transform.SetParent(sliderRowGo.transform, false);
            var spacerLayout = spacerGo.AddComponent<LayoutElement>();
            spacerLayout.minWidth = Scaled(LabelMinWidth);
            spacerLayout.preferredWidth = Scaled(LabelMinWidth);
            spacerLayout.flexibleWidth = 0f;

            _slider = CreateSlider(sliderRowGo.transform);
        }

        private void BindEvents()
        {
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

            var releaseListener = _slider.gameObject.AddComponent<TSDebugSliderReleaseListener>();
            releaseListener.SetActions(OnSliderPointerDown, OnSliderPointerReleased);
        }

        private void OnSliderValueChanged(float value)
        {
            if (_isSyncing)
                return;

            var shouldNotify = !IsPointerPressed();
            ApplyValue(value, notify: shouldNotify, commitSilently: shouldNotify);
        }

        private void OnInputEndEdit(string text)
        {
            if (_isSyncing)
                return;

            ApplyValue(GetValue(), notify: true);
        }

        private void OnSliderPointerDown()
        {
            _isDraggingSlider = true;
        }

        private void OnSliderPointerReleased()
        {
            if (!_isDraggingSlider)
                return;

            _isDraggingSlider = false;
            NotifyValueChanged(GetValue());
        }

        private void ApplyValue(float value, bool notify, bool commitSilently = true)
        {
            var normalizedValue = NormalizeValue(value);

            _isSyncing = true;

            if (_inputField != null)
                _inputField.text = FormatValue(normalizedValue);

            if (_slider != null)
                _slider.SetValueWithoutNotify(normalizedValue);

            _isSyncing = false;

            if (notify)
            {
                NotifyValueChanged(normalizedValue);
                return;
            }

            if (commitSilently)
                _lastValue = normalizedValue;
        }

        private void NotifyValueChanged(float value)
        {
            var normalizedValue = NormalizeValue(value);

            if (Mathf.Approximately(_lastValue, normalizedValue))
                return;

            _lastValue = normalizedValue;
            _onValueChanged?.Invoke(normalizedValue);
        }

        private float NormalizeValue(float value)
        {
            var clampedValue = Mathf.Clamp(value, _minValue, _maxValue);
            return _isInt ? Mathf.RoundToInt(clampedValue) : clampedValue;
        }

        private static bool IsPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Pointer.current != null && Pointer.current.press.isPressed;
#else
            if (Input.GetMouseButton(0))
                return true;

            for (var touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
            {
                var touchPhase = Input.GetTouch(touchIndex).phase;
                if (touchPhase != TouchPhase.Canceled && touchPhase != TouchPhase.Ended)
                    return true;
            }

            return false;
#endif
        }

        private InputField.ContentType GetInputContentType() =>
            _isInt ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;

        private string FormatValue(float value) =>
            _isInt
                ? Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);

        private static Text CreateText(Transform parent, string name, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
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

        private InputField CreateInputField(Transform parent)
        {
            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(parent, false);

            var inputLayout = inputGo.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1f;
            inputLayout.minHeight = Scaled(InputRowHeight - 16);
            inputLayout.preferredHeight = Scaled(InputRowHeight - 16);

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

        private static Slider CreateSlider(Transform parent)
        {
            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);

            var sliderLayout = sliderGo.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minHeight = Scaled(SliderHeight);
            sliderLayout.preferredHeight = Scaled(SliderHeight);

            var sliderRect = (RectTransform)sliderGo.transform;

            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.SetParent(sliderRect, false);
            StretchToParent(backgroundRect);
            TSDebugBlueButtonStyle.ApplyField(backgroundGo.GetComponent<Image>());
            backgroundGo.GetComponent<Image>().color = SliderBackgroundColor;

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.SetParent(sliderRect, false);
            StretchToParent(fillAreaRect);
            fillAreaRect.offsetMin = ScaledVector(10f, 0f);
            fillAreaRect.offsetMax = ScaledVector(-10f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            TSDebugBlueButtonStyle.ApplyAccentFill(fillGo.GetComponent<Image>());
            fillGo.GetComponent<Image>().color = SliderFillColor;

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            var handleAreaRect = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRect.SetParent(sliderRect, false);
            StretchToParent(handleAreaRect);
            handleAreaRect.offsetMin = ScaledVector(10f, 0f);
            handleAreaRect.offsetMax = ScaledVector(-10f, 0f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.SetParent(handleAreaRect, false);
            handleRect.sizeDelta = ScaledVector(20f, 0f);
            TSDebugBlueButtonStyle.ApplyField(handleGo.GetComponent<Image>());
            handleGo.GetComponent<Image>().color = SliderHandleColor;

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }

    internal sealed class TSDebugSliderReleaseListener : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        private Action _onPointerDown;
        private Action _onPointerReleased;

        public void SetActions(Action onPointerDown, Action onPointerReleased)
        {
            _onPointerDown = onPointerDown;
            _onPointerReleased = onPointerReleased;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _onPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _onPointerReleased?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _onPointerReleased?.Invoke();
        }
    }
}
