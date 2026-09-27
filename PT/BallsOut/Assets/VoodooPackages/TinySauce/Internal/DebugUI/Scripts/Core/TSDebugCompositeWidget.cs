using System;
using System.Collections.Generic;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal.Debugger.Widgets;

namespace Voodoo.Tiny.Sauce.Internal.Debugger
{
    public abstract class TSDebugCompositeWidget : TSDebugWidget
    {
        protected abstract RectTransform ContentContainer { get; }

        protected virtual void EnsureContainerReady() { }

        protected virtual void OnWidgetAdded(TSDebugWidget widget) { }

        public void AddWidget(TSDebugWidget widget)
        {
            EnsureContainerReady();
            widget.transform.SetParent(ContentContainer, false);
            OnWidgetAdded(widget);
        }

        public TSDebugTextWidget AddText(string text)
        {
            var widget = TSDebugTextWidget.Instantiate();
            AddWidget(widget);
            widget.SetText(text);
            return widget;
        }

        public TSDebugTitleWidget AddTitle(string title)
        {
            var widget = TSDebugTitleWidget.Instantiate(TSDebugTitleWidget.Level.Title);
            AddWidget(widget);
            widget.SetText(title);
            return widget;
        }

        public TSDebugTitleWidget AddSubTitle(string title)
        {
            var widget = TSDebugTitleWidget.Instantiate(TSDebugTitleWidget.Level.SubTitle);
            AddWidget(widget);
            widget.SetText(title);
            return widget;
        }

        public TSDebugTextPairWidget AddTextPair(string label, string value, string copyValue = null)
        {
            var widget = TSDebugTextPairWidget.Instantiate();
            AddWidget(widget);
            widget.SetPair(label, value);

            if (!string.IsNullOrEmpty(copyValue))
                widget.SetCopyValue(copyValue);

            return widget;
        }

        public TSDebugFoldableSectionWidget AddFoldableSection(string title, bool expanded = true)
        {
            var section = TSDebugFoldableSectionWidget.Instantiate();
            var depth = this is TSDebugFoldableSectionWidget parentSection ? parentSection.Depth + 1 : 0;
            AddWidget(section);
            section.SetDepth(depth);
            section.SetTitle(title);
            section.SetExpanded(expanded);
            return section;
        }

        public TSDebugButtonWidget AddButton(string label, Action onClick = null)
        {
            var widget = TSDebugButtonWidget.Instantiate();
            AddWidget(widget);
            widget.SetText(label);

            if (onClick != null)
                widget.SetAction(onClick);

            return widget;
        }

        public TSDebugToggleWidget AddToggle(string label, bool defaultValue = false, Action<bool> onValueChanged = null)
        {
            var widget = TSDebugToggleWidget.Instantiate();
            AddWidget(widget);
            widget.SetLabel(label);
            widget.SetValue(defaultValue);

            if (onValueChanged != null)
                widget.SetAction(onValueChanged);

            return widget;
        }

        public TSDebugDropdownWidget AddDropdown(
            string label,
            IEnumerable<string> options,
            string defaultValue = null,
            Action<string> onValueChanged = null)
        {
            var widget = TSDebugDropdownWidget.Instantiate();
            AddWidget(widget);
            widget.SetLabel(label);
            widget.SetOptions(options, defaultValue);

            if (onValueChanged != null)
                widget.SetAction(onValueChanged);

            return widget;
        }

        public TSDebugDropdownWidget AddEnumDropdown<TEnum>(
            string label,
            TEnum defaultValue,
            Action<TEnum> onValueChanged = null)
            where TEnum : struct
        {
            var widget = AddDropdown(label, Enum.GetNames(typeof(TEnum)), defaultValue.ToString());

            if (onValueChanged != null)
                widget.SetAction(value =>
                {
                    if (Enum.TryParse(value, out TEnum parsedValue))
                        onValueChanged.Invoke(parsedValue);
                });

            return widget;
        }

        public TSDebugTextInputWidget AddTextInput(
            string label,
            string defaultValue = null,
            Action<string> onValueChanged = null)
        {
            var widget = TSDebugTextInputWidget.Instantiate();
            AddWidget(widget);
            widget.SetLabel(label);

            if (defaultValue != null)
                widget.SetText(defaultValue);

            if (onValueChanged != null)
                widget.SetAction(onValueChanged);

            return widget;
        }

        public TSDebugNumberInputWidget AddNumberInput(
            string label,
            float defaultValue = 0f,
            bool isInt = false,
            Action<float> onValueChanged = null)
        {
            var widget = TSDebugNumberInputWidget.Instantiate();
            AddWidget(widget);
            widget.SetLabel(label);
            widget.SetIsInt(isInt);
            widget.SetValue(defaultValue);

            if (onValueChanged != null)
                widget.SetAction(onValueChanged);

            return widget;
        }

        public TSDebugNumberInputWithSliderWidget AddNumberInputWithSlider(
            string label,
            float defaultValue = 0f,
            float minValue = 0f,
            float maxValue = 100f,
            bool isInt = false,
            Action<float> onValueChanged = null)
        {
            var widget = TSDebugNumberInputWithSliderWidget.Instantiate();
            AddWidget(widget);
            widget.SetLabel(label);
            widget.SetIsInt(isInt);
            widget.SetRange(minValue, maxValue);
            widget.SetValue(defaultValue);

            if (onValueChanged != null)
                widget.SetAction(onValueChanged);

            return widget;
        }
    }
}
