using System;
using System.Collections.Generic;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Screens
{
    public class TSDebugMainMenuScreen : TSDebugScreen
    {
        private static readonly List<(string label, Action onClick)> CustomMenuItems = new();

        public static void RegisterMenuItem(string label, Action onClick)
        {
            if (string.IsNullOrWhiteSpace(label) || onClick == null)
                return;

            CustomMenuItems.Add((label, onClick));
        }

        protected override string ScreenTitle => "Debug Menu";

        protected override bool ShowBackButton => false;

        protected override void OnScreenShow()
        {
            ClearWidgets();
            AddButton("App Information", () => TSDebugUIManager.Instance?.ShowAppInformationScreen());
            AddButton("GameAnalytics", () => TSDebugUIManager.Instance?.ShowGameAnalyticsScreen());
            AddButton("VAN Event Console", () => TSDebugUIManager.Instance?.ShowVanEventConsoleScreen());

            foreach (var (label, onClick) in CustomMenuItems)
                AddButton(label, onClick);
        }
    }
}
