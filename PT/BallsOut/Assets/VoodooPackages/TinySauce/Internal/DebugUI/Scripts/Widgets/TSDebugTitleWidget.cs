using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    [RequireComponent(typeof(RectTransform))]
    public class TSDebugTitleWidget : TSDebugWidget
    {
        public enum Level
        {
            Title,
            SubTitle
        }

        private const int TitleHeight = 56;
        private const int SubTitleHeight = 44;

        private static readonly Color TitleBackgroundColor = TSDebugBlueButtonStyle.PanelColor;
        private static readonly Color SubTitleBackgroundColor = TSDebugBlueButtonStyle.CardColor;
        private static readonly Color TextColor = TSDebugBlueButtonStyle.DarkTextColor;

        [SerializeField] private Text _text;

        private Level _level = Level.Title;

        private void Awake()
        {
            if (_text == null)
                BuildLayout();
        }

        public static TSDebugTitleWidget Instantiate(Level level = Level.Title)
        {
            var widgetObject = new GameObject(nameof(TSDebugTitleWidget), typeof(RectTransform), typeof(TSDebugTitleWidget));
            var widget = widgetObject.GetComponent<TSDebugTitleWidget>();
            widget.SetLevel(level);
            return widget;
        }

        public void SetText(string value)
        {
            if (_text != null)
                _text.text = value;
        }

        public void SetLevel(Level level)
        {
            _level = level;
            ApplyStyle();
        }

        private void BuildLayout()
        {
            var rectTransform = (RectTransform)transform;

            var layoutElement = gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;

            var background = gameObject.AddComponent<Image>();
            background.raycastTarget = false;

            var horizontalLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlWidth = true;
            horizontalLayout.childControlHeight = true;
            horizontalLayout.childForceExpandWidth = true;
            horizontalLayout.childForceExpandHeight = true;
            horizontalLayout.padding = ScaledOffset(12, 12, 0, 0);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rectTransform, false);

            _text = textGo.GetComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.alignment = TextAnchor.MiddleLeft;
            _text.color = TextColor;
            _text.raycastTarget = false;

            ApplyStyle();
        }

        private void ApplyStyle()
        {
            var layoutElement = GetComponent<LayoutElement>();
            var background = GetComponent<Image>();

            if (layoutElement == null || background == null || _text == null)
                return;

            TSDebugBlueButtonStyle.ApplySurface(background, _level == Level.Title ? TitleBackgroundColor : SubTitleBackgroundColor);

            switch (_level)
            {
                case Level.Title:
                    layoutElement.minHeight = Scaled(TitleHeight);
                    layoutElement.preferredHeight = Scaled(TitleHeight);
                    background.color = TitleBackgroundColor;
                    _text.fontSize = Scaled(28);
                    _text.fontStyle = FontStyle.Bold;
                    break;

                case Level.SubTitle:
                    layoutElement.minHeight = Scaled(SubTitleHeight);
                    layoutElement.preferredHeight = Scaled(SubTitleHeight);
                    background.color = SubTitleBackgroundColor;
                    _text.fontSize = Scaled(22);
                    _text.fontStyle = FontStyle.Bold;
                    break;
            }
        }
    }
}
