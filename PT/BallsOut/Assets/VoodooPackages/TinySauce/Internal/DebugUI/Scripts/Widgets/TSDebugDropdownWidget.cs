using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugDropdownWidget : TSDebugWidget
    {
        private const int DropdownHeight = 72;
        private const int OptionHeight = 56;
        private const int LabelFontSize = 24;
        private const int DropdownFontSize = 26;
        private const int LabelMinWidth = 200;
        private const int HorizontalPadding = 12;
        private const int TriangleButtonWidth = 48;
        private const int TriangleButtonInset = 4;
        private const int TriangleIconSize = 18;
        private const int TriangleTextureSize = 32;
        private const int OptionsSpacing = 2;
        private const int OptionsPadding = 14;

        private static readonly Color LabelColor = TSDebugBlueButtonStyle.DarkTextColor;
        private static readonly Color DropdownTextColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color PopupBackgroundColor = new Color32(214, 221, 233, 255);
        private static readonly Color ItemBackgroundColor = TSDebugBlueButtonStyle.FieldColor;
        private static readonly Color ItemHoverColor = new Color32(232, 242, 255, 255);
        private static readonly Color SelectedItemColor = new Color32(212, 229, 255, 255);
        private static Sprite _triangleSprite;

        private readonly List<string> _options = new List<string>();
        private readonly List<Button> _optionButtons = new List<Button>();

        private Text _labelText;
        private Text _captionText;
        private Button _valueButton;
        private RectTransform _optionsContainer;
        private RectTransform _localOptionsParent;
        private Action<string> _onValueChanged;
        private string _selectedValue = string.Empty;
        private bool _isOpen;
        private readonly Vector3[] _valueButtonCorners = new Vector3[4];

        private void Awake()
        {
            if (_valueButton == null)
                BuildLayout();
        }

        private void LateUpdate()
        {
            if (_isOpen)
                PositionOptionsContainer();
        }

        private void Update()
        {
            if (!_isOpen || !GetPointerDown())
                return;

            if (!IsPointerInsideDropdown(GetPointerScreenPosition()))
                SetOpen(false);
        }

        private void OnEnable()
        {
            if (!_isOpen)
                RestoreOptionsContainerParent();
        }

        private void OnDisable()
        {
            // Cannot reparent during deactivation; hide the popup and restore hierarchy on re-enable.
            _isOpen = false;

            if (_optionsContainer != null)
                _optionsContainer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_optionsContainer == null)
                return;

            // Options are only detached from the widget hierarchy while open.
            if (_optionsContainer.parent != _localOptionsParent)
                Destroy(_optionsContainer.gameObject);
        }

        public static TSDebugDropdownWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugDropdownWidget),
                typeof(RectTransform),
                typeof(TSDebugDropdownWidget));

            return widgetObject.GetComponent<TSDebugDropdownWidget>();
        }

        public void SetLabel(string label)
        {
            if (_labelText != null)
                _labelText.text = label;
        }

        public void SetOptions(IEnumerable<string> options, string defaultValue = null)
        {
            _options.Clear();
            ClearOptions();

            if (options != null)
            {
                foreach (var option in options)
                {
                    if (!string.IsNullOrEmpty(option))
                        _options.Add(option);
                }
            }

            BuildOptions();
            SetValue(defaultValue);
        }

        public void SetValue(string value)
        {
            if (_options.Count == 0)
                return;

            var optionIndex = GetOptionIndex(value);
            ApplyValue(_options[optionIndex >= 0 ? optionIndex : 0], notify: false);
        }

        public string GetValue() => _selectedValue;

        public void SetAction(Action<string> onValueChanged)
        {
            _onValueChanged = onValueChanged;
        }

        private void BuildLayout()
        {
            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = Scaled(DropdownHeight);
            layoutElement.preferredHeight = Scaled(DropdownHeight);

            var verticalLayout = gameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = ScaledOffset(HorizontalPadding, HorizontalPadding, 8, 8);
            verticalLayout.spacing = Scaled(4);
            verticalLayout.childAlignment = TextAnchor.UpperLeft;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            BuildValueRow();
            BuildOptionsContainer();
            SetOpen(false);
        }

        private void BuildValueRow()
        {
            var valueRowGo = new GameObject("ValueRow", typeof(RectTransform));
            valueRowGo.transform.SetParent(transform, false);

            var valueRowLayout = valueRowGo.AddComponent<LayoutElement>();
            valueRowLayout.flexibleWidth = 1f;
            valueRowLayout.minHeight = Scaled(DropdownHeight - 16);
            valueRowLayout.preferredHeight = Scaled(DropdownHeight - 16);

            var horizontalLayout = valueRowGo.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;

            _labelText = CreateText(valueRowGo.transform, "Label", LabelFontSize, FontStyle.Bold, LabelColor, TextAnchor.MiddleLeft);
            var labelLayout = _labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.minWidth = Scaled(LabelMinWidth);
            labelLayout.preferredWidth = Scaled(LabelMinWidth);
            labelLayout.flexibleWidth = 0f;

            _valueButton = CreateValueButton(valueRowGo.transform);
            _valueButton.onClick.AddListener(ToggleOpen);
        }

        private void BuildOptionsContainer()
        {
            var optionsGo = new GameObject(
                "Options",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(Image),
                typeof(VerticalLayoutGroup));
            _optionsContainer = optionsGo.GetComponent<RectTransform>();
            _optionsContainer.SetParent(_valueButton.transform, false);
            _localOptionsParent = (RectTransform)_valueButton.transform;
            _optionsContainer.anchorMin = new Vector2(0f, 0f);
            _optionsContainer.anchorMax = new Vector2(1f, 0f);
            _optionsContainer.pivot = new Vector2(0.5f, 1f);
            _optionsContainer.anchoredPosition = ScaledVector(0f, -OptionsSpacing);

            var popupCanvas = optionsGo.GetComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = short.MaxValue;

            var background = optionsGo.GetComponent<Image>();
            background.color = PopupBackgroundColor;
            background.raycastTarget = true;

            var optionsLayout = optionsGo.GetComponent<VerticalLayoutGroup>();
            optionsLayout.padding = ScaledOffset(OptionsPadding, OptionsPadding, OptionsPadding, OptionsPadding);
            optionsLayout.spacing = Scaled(OptionsSpacing);
            optionsLayout.childAlignment = TextAnchor.UpperLeft;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
        }

        private void ToggleOpen()
        {
            if (_options.Count == 0)
                return;

            SetOpen(!_isOpen);
        }

        private void SetOpen(bool isOpen)
        {
            _isOpen = isOpen;

            if (_optionsContainer != null)
            {
                RefreshOptionsContainerSize();
                if (isOpen)
                    PositionOptionsContainer();
                else if (isActiveAndEnabled)
                    RestoreOptionsContainerParent();

                _optionsContainer.gameObject.SetActive(isOpen);
                _optionsContainer.SetAsLastSibling();
            }

            Canvas.ForceUpdateCanvases();
        }

        private void PositionOptionsContainer()
        {
            if (_optionsContainer == null || _valueButton == null)
                return;

            var rootCanvas = GetComponentInParent<Canvas>();
            var rootCanvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            if (rootCanvasRect == null)
            {
                RestoreOptionsContainerParent();
                return;
            }

            if (_optionsContainer.parent != rootCanvasRect)
                _optionsContainer.SetParent(rootCanvasRect, false);

            _optionsContainer.SetAsLastSibling();
            _optionsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _optionsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _optionsContainer.pivot = new Vector2(0.5f, 1f);

            var buttonRect = (RectTransform)_valueButton.transform;
            buttonRect.GetWorldCorners(_valueButtonCorners);

            var canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            var bottomLeftScreenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, _valueButtonCorners[0]);
            var topRightScreenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, _valueButtonCorners[2]);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                bottomLeftScreenPosition,
                canvasCamera,
                out var bottomLeftPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvasRect,
                topRightScreenPosition,
                canvasCamera,
                out var topRightPosition);

            _optionsContainer.anchoredPosition = new Vector2(
                (bottomLeftPosition.x + topRightPosition.x) * 0.5f,
                bottomLeftPosition.y - Scaled(OptionsSpacing));
            _optionsContainer.sizeDelta = new Vector2(topRightPosition.x - bottomLeftPosition.x, _optionsContainer.sizeDelta.y);
        }

        private void RestoreOptionsContainerParent()
        {
            if (_optionsContainer == null || _localOptionsParent == null || _optionsContainer.parent == _localOptionsParent)
                return;

            _optionsContainer.SetParent(_localOptionsParent, false);
            _optionsContainer.anchorMin = new Vector2(0f, 0f);
            _optionsContainer.anchorMax = new Vector2(1f, 0f);
            _optionsContainer.pivot = new Vector2(0.5f, 1f);
            _optionsContainer.anchoredPosition = ScaledVector(0f, -OptionsSpacing);
        }

        private bool IsPointerInsideDropdown(Vector2 screenPosition)
        {
            var rootCanvas = GetComponentInParent<Canvas>();
            var canvasCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            return IsScreenPointInside(_valueButton.transform as RectTransform, screenPosition, canvasCamera)
                || IsScreenPointInside(_optionsContainer, screenPosition, canvasCamera);
        }

        private static bool IsScreenPointInside(RectTransform rectTransform, Vector2 screenPosition, Camera canvasCamera) =>
            rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, canvasCamera);

        private static bool GetPointerDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
#else
            if (Input.GetMouseButtonDown(0))
                return true;

            for (var touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
            {
                if (Input.GetTouch(touchIndex).phase == TouchPhase.Began)
                    return true;
            }

            return false;
#endif
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
#else
            if (Input.touchCount > 0)
                return Input.GetTouch(0).position;

            return Input.mousePosition;
#endif
        }

        private void ApplyValue(string value, bool notify)
        {
            var selectedValue = value ?? string.Empty;
            var hasChanged = !string.Equals(_selectedValue, selectedValue, StringComparison.Ordinal);
            _selectedValue = selectedValue;
            RefreshCaption();
            RefreshOptionsVisuals();
            SetOpen(false);

            if (notify && hasChanged)
                _onValueChanged?.Invoke(_selectedValue);
        }

        private void RefreshCaption()
        {
            if (_captionText != null)
                _captionText.text = string.IsNullOrEmpty(_selectedValue) ? "Select..." : _selectedValue;
        }

        private void RefreshOptionsVisuals()
        {
            for (var index = 0; index < _optionButtons.Count; index++)
            {
                var isSelected = _options[index] == _selectedValue;
                ApplyOptionButtonColors(_optionButtons[index], isSelected);
            }
        }

        private void BuildOptions()
        {
            if (_optionsContainer == null)
                return;

            foreach (var option in _options)
                _optionButtons.Add(CreateOptionButton(_optionsContainer, option));

            RefreshOptionsVisuals();
            RefreshOptionsContainerSize();
        }

        private void RefreshOptionsContainerSize()
        {
            if (_optionsContainer == null)
                return;

            var optionCount = Mathf.Max(0, _options.Count);
            var optionsHeight = optionCount * Scaled(OptionHeight)
                + Mathf.Max(0, optionCount - 1) * Scaled(OptionsSpacing)
                + Scaled(OptionsPadding * 2);
            _optionsContainer.sizeDelta = new Vector2(0f, optionsHeight);
        }

        private void ClearOptions()
        {
            _optionButtons.Clear();

            if (_optionsContainer == null)
                return;

            for (var index = _optionsContainer.childCount - 1; index >= 0; index--)
                Destroy(_optionsContainer.GetChild(index).gameObject);
        }

        private int GetOptionIndex(string value)
        {
            if (string.IsNullOrEmpty(value))
                return -1;

            for (var index = 0; index < _options.Count; index++)
            {
                if (string.Equals(_options[index], value, StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }

        private Button CreateValueButton(Transform parent)
        {
            var buttonGo = new GameObject("ValueButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.flexibleWidth = 1f;
            buttonLayout.minHeight = Scaled(DropdownHeight - 16);
            buttonLayout.preferredHeight = Scaled(DropdownHeight - 16);

            var image = buttonGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplyField(image);

            _captionText = CreateText(buttonGo.transform, "Text", DropdownFontSize, FontStyle.Normal, DropdownTextColor, TextAnchor.MiddleLeft);
            var textRect = (RectTransform)_captionText.transform;
            StretchToParent(textRect);
            textRect.offsetMin = ScaledVector(10f, 0f);
            textRect.offsetMax = ScaledVector(-(TriangleButtonWidth + 16f), 0f);

            var triangleButton = CreateTriangleButton(buttonGo.transform);
            triangleButton.onClick.AddListener(ToggleOpen);

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private Button CreateOptionButton(Transform parent, string option)
        {
            var buttonGo = new GameObject($"Option_{option}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);

            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.flexibleWidth = 1f;
            buttonLayout.minHeight = Scaled(OptionHeight);
            buttonLayout.preferredHeight = Scaled(OptionHeight);

            var image = buttonGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplyField(image);

            var text = CreateText(buttonGo.transform, "Text", DropdownFontSize, FontStyle.Normal, DropdownTextColor, TextAnchor.MiddleLeft);
            var textRect = (RectTransform)text.transform;
            StretchToParent(textRect);
            textRect.offsetMin = ScaledVector(10f, 0f);
            textRect.offsetMax = ScaledVector(-10f, 0f);
            text.text = option;

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            ApplyOptionButtonColors(button, isSelected: false);
            button.onClick.AddListener(() => ApplyValue(option, notify: true));
            return button;
        }

        private static void ApplyOptionButtonColors(Button button, bool isSelected)
        {
            if (button == null)
                return;

            var normalColor = isSelected ? SelectedItemColor : ItemBackgroundColor;
            var highlightedColor = isSelected ? TSDebugBlueButtonStyle.PrimaryHighlightedColor : ItemHoverColor;
            var pressedColor = isSelected ? TSDebugBlueButtonStyle.PrimaryPressedColor : PopupBackgroundColor;

            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = normalColor,
                highlightedColor = highlightedColor,
                pressedColor = pressedColor,
                selectedColor = highlightedColor,
                disabledColor = TSDebugBlueButtonStyle.DisabledColor,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            if (button.targetGraphic != null)
                button.targetGraphic.color = normalColor;
        }

        private Button CreateTriangleButton(Transform parent)
        {
            var buttonGo = new GameObject("TriangleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.offsetMin = ScaledVector(-(TriangleButtonWidth + TriangleButtonInset), TriangleButtonInset);
            buttonRect.offsetMax = ScaledVector(-TriangleButtonInset, -TriangleButtonInset);

            var buttonImage = buttonGo.GetComponent<Image>();
            var button = buttonGo.GetComponent<Button>();
            TSDebugBlueButtonStyle.ApplyPrimaryFieldButton(buttonImage, button);
            button.targetGraphic = buttonImage;

            var iconGo = new GameObject("TriangleIcon", typeof(RectTransform), typeof(Image));
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.SetParent(buttonGo.transform, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = ScaledVector(TriangleIconSize, TriangleIconSize);
            iconRect.anchoredPosition = Vector2.zero;

            var iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = GetTriangleSprite();
            iconImage.type = Image.Type.Simple;
            iconImage.color = TSDebugBlueButtonStyle.LightTextColor;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            return button;
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
            text.supportRichText = false;
            return text;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static Sprite GetTriangleSprite()
        {
            if (_triangleSprite == null)
                _triangleSprite = CreateDownTriangleSprite();

            return _triangleSprite;
        }

        private static Sprite CreateDownTriangleSprite()
        {
            var texture = new Texture2D(TriangleTextureSize, TriangleTextureSize, TextureFormat.RGBA32, false)
            {
                name = "TSDebugDropdownTriangle",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[TriangleTextureSize * TriangleTextureSize];
            var center = (TriangleTextureSize - 1) * 0.5f;
            var pointY = TriangleTextureSize * 0.28f;
            var baseY = TriangleTextureSize * 0.72f;

            for (var y = 0; y < TriangleTextureSize; y++)
            {
                for (var x = 0; x < TriangleTextureSize; x++)
                    pixels[y * TriangleTextureSize + x] = IsInsideDownTriangle(x, y, center, pointY, baseY) ? Color.white : Color.clear;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, TriangleTextureSize, TriangleTextureSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool IsInsideDownTriangle(float x, float y, float center, float pointY, float baseY)
        {
            if (y < pointY || y > baseY)
                return false;

            var halfWidthAtY = y - pointY;
            return Mathf.Abs(x - center) <= halfWidthAtY;
        }
    }
}
