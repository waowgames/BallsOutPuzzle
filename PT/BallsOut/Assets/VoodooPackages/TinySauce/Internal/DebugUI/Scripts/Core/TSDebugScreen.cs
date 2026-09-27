using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Voodoo.Tiny.Sauce.Internal.Debugger.Widgets;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public abstract class TSDebugScreen : TSDebugWidget
    {
        private static Canvas _debugCanvas;
        private static TSDebugScreen _activeScreen;

        private TSDebugScreenWidget _page;

        protected abstract string ScreenTitle { get; }

        protected virtual bool ShowBackButton => true;

        public virtual void ShowScreen()
        {
            if (_activeScreen != null && _activeScreen != this)
                _activeScreen.HideScreen();

            _activeScreen = this;

            if (_page == null)
            {
                var pageObject = new GameObject($"{name}_Page", typeof(RectTransform), typeof(TSDebugScreenWidget));
                pageObject.transform.SetParent(GetDebugUIRoot(), false);
                _page = pageObject.GetComponent<TSDebugScreenWidget>();
            }

            _page.gameObject.SetActive(true);
            _page.ConfigureHeader(ScreenTitle, ShowBackButton, OnBackPressed);
            OnScreenShow();
        }

        protected virtual void OnBackPressed() => TSDebugUIManager.Instance?.ShowMainMenu();

        public virtual void HideScreen()
        {
            if (_page != null)
                _page.gameObject.SetActive(false);

            if (_activeScreen == this)
                _activeScreen = null;
        }

        public static void CloseActiveScreen()
        {
            if (_activeScreen != null)
                _activeScreen.HideScreen();
        }

        protected virtual void OnScreenShow() { }

        private TSDebugCompositeWidget WidgetContainer
        {
            get
            {
                if (_page == null)
                    ShowScreen();

                return _page;
            }
        }

        protected void AddWidget(TSDebugWidget widget) => WidgetContainer.AddWidget(widget);

        protected void ClearWidgets()
        {
            if (_page != null)
                _page.ClearWidgets();
        }

        protected TSDebugTextWidget AddText(string text) => WidgetContainer.AddText(text);

        protected TSDebugTitleWidget AddTitle(string title) => WidgetContainer.AddTitle(title);

        protected TSDebugTitleWidget AddSubTitle(string title) => WidgetContainer.AddSubTitle(title);

        protected TSDebugTextPairWidget AddTextPair(string label, string value, string copyValue = null) =>
            WidgetContainer.AddTextPair(label, value, copyValue);

        protected TSDebugFoldableSectionWidget AddFoldableSection(string title, bool expanded = true) =>
            WidgetContainer.AddFoldableSection(title, expanded);

        protected TSDebugButtonWidget AddButton(string label, System.Action onClick = null) =>
            WidgetContainer.AddButton(label, onClick);

        protected TSDebugToggleWidget AddToggle(
            string label,
            bool defaultValue = false,
            System.Action<bool> onValueChanged = null) =>
            WidgetContainer.AddToggle(label, defaultValue, onValueChanged);

        protected TSDebugDropdownWidget AddDropdown(
            string label,
            IEnumerable<string> options,
            string defaultValue = null,
            System.Action<string> onValueChanged = null) =>
            WidgetContainer.AddDropdown(label, options, defaultValue, onValueChanged);

        protected TSDebugDropdownWidget AddEnumDropdown<TEnum>(
            string label,
            TEnum defaultValue,
            System.Action<TEnum> onValueChanged = null)
            where TEnum : struct =>
            WidgetContainer.AddEnumDropdown(label, defaultValue, onValueChanged);

        protected TSDebugTextInputWidget AddTextInput(
            string label,
            string defaultValue = null,
            System.Action<string> onValueChanged = null) =>
            WidgetContainer.AddTextInput(label, defaultValue, onValueChanged);

        protected TSDebugNumberInputWidget AddNumberInput(
            string label,
            float defaultValue = 0f,
            bool isInt = false,
            System.Action<float> onValueChanged = null) =>
            WidgetContainer.AddNumberInput(label, defaultValue, isInt, onValueChanged);

        protected TSDebugNumberInputWithSliderWidget AddNumberInputWithSlider(
            string label,
            float defaultValue = 0f,
            float minValue = 0f,
            float maxValue = 100f,
            bool isInt = false,
            System.Action<float> onValueChanged = null) =>
            WidgetContainer.AddNumberInputWithSlider(label, defaultValue, minValue, maxValue, isInt, onValueChanged);

        protected T CreateWidget<T>() where T : TSDebugWidget
        {
            var widgetObject = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T));
            var widget = widgetObject.GetComponent<T>();
            AddWidget(widget);
            return widget;
        }

        private static Transform GetDebugUIRoot()
        {
            if (_debugCanvas != null)
                return _debugCanvas.transform;

            var canvasObject = new GameObject("TSDebugUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _debugCanvas = canvasObject.GetComponent<Canvas>();
            _debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _debugCanvas.sortingOrder = short.MaxValue;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            return _debugCanvas.transform;
        }
    }
}
