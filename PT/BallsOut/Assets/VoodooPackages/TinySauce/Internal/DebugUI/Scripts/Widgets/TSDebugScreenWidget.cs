using System;
using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal.Debugger;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugScreenWidget : TSDebugCompositeWidget
    {
        private const int DefaultPadding = 20;
        private const int HeaderHeight = 80;
        private const int FooterHeight = 150;
        private const int CloseButtonSize = 100;
        private const int BackButtonWidth = 72;
        private const int BackButtonHeight = 56;
        private const int HeaderTitleFontSize = 32;
        private const int BackArrowShaftWidth = 34;
        private const int BackArrowStrokeWidth = 8;
        private const int BackArrowHeadLength = 26;
        private const int ContentSpacing = 12;

        private RectTransform _contentContainer;
        private ScrollRect _scrollRect;
        private Text _headerTitle;
        private GameObject _backButton;
        private Button _backButtonComponent;
        private Rect _lastSafeArea;

        protected override RectTransform ContentContainer => _contentContainer;

        private void Awake()
        {
            EnsureContainerReady();
        }

        private void OnEnable()
        {
            ApplySafeAreaToRoot();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea)
                ApplySafeAreaToRoot();
        }

        protected override void EnsureContainerReady()
        {
            if (_contentContainer == null)
                BuildLayout();
        }

        public void ClearWidgets()
        {
            if (_contentContainer == null)
                return;

            for (int i = _contentContainer.childCount - 1; i >= 0; i--)
                Destroy(_contentContainer.GetChild(i).gameObject);

            ResetScrollPosition();
        }

        public void ConfigureHeader(string title, bool showBackButton, Action onBack)
        {
            EnsureContainerReady();

            if (_headerTitle != null)
                _headerTitle.text = title;

            if (_backButton != null)
                _backButton.SetActive(showBackButton);

            if (_backButtonComponent != null)
            {
                _backButtonComponent.onClick.RemoveAllListeners();

                if (showBackButton && onBack != null)
                    _backButtonComponent.onClick.AddListener(() => onBack.Invoke());
            }
        }

        private void BuildLayout()
        {
            var rectTransform = (RectTransform)transform;
            ApplySafeAreaToRoot(rectTransform);

            var background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = TSDebugBlueButtonStyle.ScreenBackgroundColor;
            background.raycastTarget = true;

            BuildHeader(rectTransform);
            BuildFooter(rectTransform);

            BuildScrollableContent(rectTransform);
        }

        protected override void OnWidgetAdded(TSDebugWidget widget)
        {
            RefreshContentLayout();
        }

        private void BuildScrollableContent(RectTransform parent)
        {
            var scrollViewGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
            var scrollViewRect = scrollViewGo.GetComponent<RectTransform>();
            scrollViewRect.SetParent(parent, false);
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.offsetMin = ScaledVector(0f, FooterHeight);
            scrollViewRect.offsetMax = ScaledVector(0f, -HeaderHeight);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.SetParent(scrollViewRect, false);
            StretchToParent(viewportRect);

            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            _contentContainer = contentGo.GetComponent<RectTransform>();
            _contentContainer.SetParent(viewportRect, false);
            _contentContainer.anchorMin = new Vector2(0f, 1f);
            _contentContainer.anchorMax = Vector2.one;
            _contentContainer.pivot = new Vector2(0.5f, 1f);
            _contentContainer.offsetMin = Vector2.zero;
            _contentContainer.offsetMax = Vector2.zero;

            var layoutGroup = contentGo.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.spacing = Scaled(ContentSpacing);
            layoutGroup.padding = ScaledOffset(DefaultPadding, DefaultPadding, DefaultPadding, DefaultPadding);

            var contentSizeFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = viewportRect;
            _scrollRect.content = _contentContainer;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = Scaled(40f);
        }

        private void BuildHeader(RectTransform parent)
        {
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Image));
            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.SetParent(parent, false);
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = Vector2.one;
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = ScaledVector(0f, HeaderHeight);

            TSDebugBlueButtonStyle.ApplySurface(headerGo.GetComponent<Image>(), TSDebugBlueButtonStyle.PanelColor);

            var horizontalLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = ScaledOffset(16, 24, 8, 8);
            horizontalLayout.spacing = Scaled(8);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;

            BuildBackButton(headerRect);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.SetParent(headerRect, false);

            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
            titleLayout.minHeight = Scaled(HeaderHeight);
            titleLayout.preferredHeight = Scaled(HeaderHeight);

            _headerTitle = titleGo.GetComponent<Text>();
            _headerTitle.alignment = TextAnchor.MiddleLeft;
            _headerTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _headerTitle.fontSize = Scaled(HeaderTitleFontSize);
            _headerTitle.fontStyle = FontStyle.Bold;
            _headerTitle.color = TSDebugBlueButtonStyle.DarkTextColor;
            _headerTitle.raycastTarget = false;
        }

        private void BuildBackButton(RectTransform parent)
        {
            _backButton = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var backButtonRect = _backButton.GetComponent<RectTransform>();
            backButtonRect.SetParent(parent, false);

            var backButtonLayout = _backButton.AddComponent<LayoutElement>();
            backButtonLayout.minWidth = Scaled(BackButtonWidth);
            backButtonLayout.preferredWidth = Scaled(BackButtonWidth);
            backButtonLayout.minHeight = Scaled(BackButtonHeight);
            backButtonLayout.preferredHeight = Scaled(BackButtonHeight);

            var backButtonImage = _backButton.GetComponent<Image>();
            _backButtonComponent = _backButton.GetComponent<Button>();
            TSDebugBlueButtonStyle.ApplyPrimaryButton(backButtonImage, _backButtonComponent);
            _backButtonComponent.targetGraphic = backButtonImage;

            BuildBackArrow(backButtonRect);
        }

        private void BuildBackArrow(RectTransform parent)
        {
            CreateBackArrowSegment(parent, "Shaft", new Vector2(7f, 0f), new Vector2(BackArrowShaftWidth, BackArrowStrokeWidth), 0f);
            CreateBackArrowSegment(parent, "HeadTop", new Vector2(-11f, 8f), new Vector2(BackArrowHeadLength, BackArrowStrokeWidth), 45f);
            CreateBackArrowSegment(parent, "HeadBottom", new Vector2(-11f, -8f), new Vector2(BackArrowHeadLength, BackArrowStrokeWidth), -45f);
        }

        private void CreateBackArrowSegment(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float rotation)
        {
            var segmentGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            var segmentRect = segmentGo.GetComponent<RectTransform>();
            segmentRect.SetParent(parent, false);
            segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
            segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
            segmentRect.pivot = new Vector2(0.5f, 0.5f);
            segmentRect.anchoredPosition = ScaledVector(anchoredPosition.x, anchoredPosition.y);
            segmentRect.sizeDelta = ScaledVector(size.x, size.y);
            segmentRect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var segmentImage = segmentGo.GetComponent<Image>();
            segmentImage.color = TSDebugBlueButtonStyle.LightTextColor;
            segmentImage.raycastTarget = false;
        }

        private static void BuildFooter(RectTransform parent)
        {
            var footerGo = new GameObject("Footer", typeof(RectTransform), typeof(Image));
            var footerRect = footerGo.GetComponent<RectTransform>();
            footerRect.SetParent(parent, false);
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.sizeDelta = ScaledVector(0f, FooterHeight);

            footerGo.GetComponent<Image>().color = TSDebugBlueButtonStyle.ScreenBackgroundColor;

            var buttonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.SetParent(footerRect, false);
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = ScaledVector(CloseButtonSize, CloseButtonSize);

            var button = buttonGo.GetComponent<Button>();
            var buttonImage = buttonGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplyPrimaryButton(buttonImage, button);
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => TSDebugUIManager.Instance?.CloseDebugUI());

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(buttonRect, false);
            StretchToParent(textRect);

            var text = textGo.GetComponent<Text>();
            text.text = "X";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Scaled(48);
            text.fontStyle = FontStyle.Bold;
            text.color = TSDebugBlueButtonStyle.LightTextColor;
            text.raycastTarget = false;
        }

        private void ApplySafeAreaToRoot(RectTransform rectTransform = null)
        {
            rectTransform ??= (RectTransform)transform;

            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void RefreshContentLayout()
        {
            if (_contentContainer == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentContainer);
        }

        private void ResetScrollPosition()
        {
            if (_scrollRect == null)
                return;

            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
