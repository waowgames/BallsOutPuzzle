using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal.Debugger;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugFoldableSectionWidget : TSDebugCompositeWidget
    {
        private const int HeaderHeight = 80;
        private const int HeaderTitleFontSize = 36;
        private const int HeaderIndicatorFontSize = 30;
        private const int HeaderIndicatorWidth = 32;
        private const int HeaderHorizontalPadding = 16;
        private const int ContentSpacing = 8;
        private const int SectionSpacing = 8;

        private static readonly Color HeaderTextColor = TSDebugBlueButtonStyle.DarkTextColor;

        private static readonly Color[] HeaderTints =
        {
            new Color32(232, 242, 255, 255),
            new Color32(222, 236, 255, 255),
            new Color32(212, 229, 255, 255),
            new Color32(202, 222, 255, 255),
        };

        private static readonly Color[] ContentBackgroundColors =
        {
            new Color32(230, 238, 249, 255),
            new Color32(221, 232, 248, 255),
            new Color32(213, 226, 247, 255),
            new Color32(205, 220, 246, 255),
        };

        private RectTransform _contentContainer;
        private Image _headerImage;
        private Image _contentImage;
        private Text _titleText;
        private Text _expandIndicator;
        private int _depth;
        private bool _isExpanded = true;

        public int Depth => _depth;

        protected override RectTransform ContentContainer => _contentContainer;

        private void Awake()
        {
            EnsureContainerReady();
        }

        protected override void EnsureContainerReady()
        {
            if (_contentContainer == null)
                BuildLayout();
        }

        protected override void OnWidgetAdded(TSDebugWidget widget) => RefreshLayout();

        public static TSDebugFoldableSectionWidget Instantiate()
        {
            var widgetObject = new GameObject(
                nameof(TSDebugFoldableSectionWidget),
                typeof(RectTransform),
                typeof(TSDebugFoldableSectionWidget));

            return widgetObject.GetComponent<TSDebugFoldableSectionWidget>();
        }

        public void SetTitle(string title)
        {
            if (_titleText != null)
                _titleText.text = title;
        }

        public void SetDepth(int depth)
        {
            _depth = Mathf.Max(0, depth);
            ApplyDepthStyle();
        }

        public void SetExpanded(bool expanded)
        {
            _isExpanded = expanded;

            if (_contentContainer != null)
                _contentContainer.gameObject.SetActive(expanded);

            UpdateExpandIndicator();
            RefreshLayout();
        }

        private void ToggleExpanded() => SetExpanded(!_isExpanded);

        private void UpdateExpandIndicator()
        {
            if (_expandIndicator != null)
                _expandIndicator.text = _isExpanded ? "\u25BC" : "\u25B6";
        }

        private void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        private void BuildLayout()
        {
            var rectTransform = (RectTransform)transform;

            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.minHeight = Scaled(HeaderHeight);

            var rootLayout = gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.spacing = 0;
            rootLayout.padding = ScaledOffset(0, 0, SectionSpacing, SectionSpacing);

            var sizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader(rectTransform);
            BuildContent(rectTransform);
            UpdateExpandIndicator();
            ApplyDepthStyle();
        }

        private void ApplyDepthStyle()
        {
            if (_headerImage != null)
            {
                TSDebugBlueButtonStyle.ApplySurface(_headerImage, GetColorForDepth(HeaderTints, _depth));
                _headerImage.color = GetColorForDepth(HeaderTints, _depth);
            }

            if (_contentImage != null)
            {
                TSDebugBlueButtonStyle.ApplySurface(_contentImage, GetColorForDepth(ContentBackgroundColors, _depth));
                _contentImage.color = GetColorForDepth(ContentBackgroundColors, _depth);
            }
        }

        private static Color GetColorForDepth(Color[] palette, int depth)
        {
            if (palette == null || palette.Length == 0)
                return Color.white;

            return palette[Mathf.Min(depth, palette.Length - 1)];
        }

        private void BuildHeader(RectTransform parent)
        {
            var headerGo = new GameObject(
                "Header",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.SetParent(parent, false);

            var headerLayout = headerGo.GetComponent<LayoutElement>();
            headerLayout.minHeight = Scaled(HeaderHeight);
            headerLayout.preferredHeight = Scaled(HeaderHeight);
            headerLayout.flexibleWidth = 1f;

            _headerImage = headerGo.GetComponent<Image>();
            var button = headerGo.GetComponent<Button>();
            TSDebugBlueButtonStyle.ApplySecondaryButton(_headerImage, button);

            button.targetGraphic = _headerImage;
            button.onClick.AddListener(ToggleExpanded);

            var horizontalLayout = headerGo.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.padding = ScaledOffset(HeaderHorizontalPadding, HeaderHorizontalPadding, 0, 0);
            horizontalLayout.spacing = Scaled(12);
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;

            _expandIndicator = CreateText(headerRect, "ExpandIndicator", HeaderIndicatorFontSize, FontStyle.Bold, HeaderTextColor);

            var indicatorLayout = _expandIndicator.gameObject.AddComponent<LayoutElement>();
            indicatorLayout.minWidth = Scaled(HeaderIndicatorWidth);
            indicatorLayout.preferredWidth = Scaled(HeaderIndicatorWidth);

            _titleText = CreateText(headerRect, "Title", HeaderTitleFontSize, FontStyle.Bold, HeaderTextColor);
            _titleText.alignment = TextAnchor.MiddleLeft;

            var titleLayout = _titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;
        }

        private void BuildContent(RectTransform parent)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(Image));
            _contentContainer = contentGo.GetComponent<RectTransform>();
            _contentContainer.SetParent(parent, false);

            _contentImage = contentGo.GetComponent<Image>();
            TSDebugBlueButtonStyle.ApplySurface(_contentImage, TSDebugBlueButtonStyle.CardColor);
            _contentImage.raycastTarget = false;

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = ScaledOffset(8, 8, 8, 8);
            contentLayout.spacing = Scaled(ContentSpacing);
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentSizeFitter = contentGo.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static Text CreateText(Transform parent, string name, int fontSize, FontStyle fontStyle, Color color)
        {
            var textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Scaled(fontSize);
            text.fontStyle = fontStyle;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}
