using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    public class TSDebugTextWidget : TSDebugWidget
    {
        private const string PrefabResourcePath = "Prefabs/TSDebugTextWidget";
        private const int TextHeight = 70;
        private const int FontSize = 24;

        private static TSDebugTextWidget _prefab;

        [SerializeField] private Text _text;

        private void Awake()
        {
            if (_text == null)
                return;

            var scaledTextHeight = Scaled(TextHeight);
            var rectTransform = (RectTransform)transform;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, scaledTextHeight);
            SetLayoutHeight(GetComponent<LayoutElement>(), scaledTextHeight);
            _text.color = TSDebugBlueButtonStyle.DarkTextColor;
            _text.fontSize = Scaled(FontSize);
            _text.alignment = TextAnchor.UpperLeft;
        }

        public static TSDebugTextWidget Instantiate()
        {
            if (_prefab == null)
                _prefab = Resources.Load<TSDebugTextWidget>(PrefabResourcePath);

            return Object.Instantiate(_prefab);
        }

        public void SetText(string value)
        {
            if (_text != null)
                _text.text = value;
        }

        private static void SetLayoutHeight(LayoutElement layoutElement, int height)
        {
            if (layoutElement == null)
                return;

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }
    }
}
