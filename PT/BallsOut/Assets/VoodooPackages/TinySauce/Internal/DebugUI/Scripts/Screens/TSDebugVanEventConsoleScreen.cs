using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Voodoo.Tiny.Sauce.Internal.Analytics;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Screens
{
    public class TSDebugVanEventConsoleScreen : TSDebugScreen
    {
        private const string VoodooAnalyticsWrapperName = "VoodooAnalytics";
        private const string AllEventsFilter = "All Events";
        private const string EmptyValue = "-";
        private const string EventDateFormat = "HH:mm:ss";

        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private string _selectedEventNameFilter = AllEventsFilter;
        private bool _isSubscribed;
        private bool _isVisible;

        protected override string ScreenTitle => "VAN Event Console";

        protected override void OnScreenShow()
        {
            _isVisible = true;
            EnsureSubscribed();
            RefreshScreen();
        }

        public override void HideScreen()
        {
            _isVisible = false;
            base.HideScreen();
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void EnsureSubscribed()
        {
            if (_isSubscribed)
                return;

            AnalyticsEventLogger.OnAnalyticsEventStateChanged += OnAnalyticsEventStateChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            AnalyticsEventLogger.OnAnalyticsEventStateChanged -= OnAnalyticsEventStateChanged;
            _isSubscribed = false;
        }

        private void OnAnalyticsEventStateChanged(DebugAnalyticsLog log, bool isUpdateFromExisting)
        {
            if (!IsVoodooAnalyticsLog(log))
                return;

            _mainThreadActions.Enqueue(() =>
            {
                if (_isVisible)
                    RefreshScreen();
            });
        }

        private void RefreshScreen()
        {
            ClearWidgets();
            AddControls();

            var logs = GetFilteredLogs().ToList();
            AddText($"Showing {logs.Count} VAN event(s).");

            if (logs.Count == 0)
            {
                AddText("No VAN events recorded yet.");
                return;
            }

            foreach (var sessionGroup in logs.GroupBy(log => GetDisplayValue(log.SessionId)))
                AddSessionSection(sessionGroup.Key, sessionGroup);
        }

        private void AddControls()
        {
            var logger = AnalyticsEventLogger.GetInstance();
            AddToggle("Recording", logger.IsRecordingEvents, isRecording =>
            {
                logger.SetAnalyticsEventRecording(isRecording);
                RefreshScreen();
            });
            AddToggle("Record at startup", logger.IsRecordingAtStartup, shouldRecordAtStartup =>
            {
                logger.IsRecordingAtStartup = shouldRecordAtStartup;
                RefreshScreen();
            });
            AddDropdown("Event filter", GetEventFilterOptions(), _selectedEventNameFilter, selectedFilter =>
            {
                _selectedEventNameFilter = selectedFilter;
                RefreshScreen();
            });
            AddButton("Flush VAN Events", () =>
            {
                logger.FlushAnalyticsLogs();
                _selectedEventNameFilter = AllEventsFilter;
                RefreshScreen();
            });
        }

        private IEnumerable<string> GetEventFilterOptions()
        {
            yield return AllEventsFilter;

            foreach (var eventName in GetVoodooAnalyticsLogs()
                         .Select(log => log.EventName)
                         .Where(eventName => !string.IsNullOrEmpty(eventName))
                         .Distinct()
                         .OrderBy(eventName => eventName))
            {
                yield return eventName;
            }
        }

        private IEnumerable<DebugAnalyticsLog> GetFilteredLogs()
        {
            var logs = GetVoodooAnalyticsLogs();

            if (!string.IsNullOrEmpty(_selectedEventNameFilter) && _selectedEventNameFilter != AllEventsFilter)
                logs = logs.Where(log => log.EventName == _selectedEventNameFilter);

            return logs.OrderByDescending(log => log.Timestamp);
        }

        private IEnumerable<DebugAnalyticsLog> GetVoodooAnalyticsLogs()
        {
            return AnalyticsEventLogger.GetInstance().GetLocalAnalyticsLog(VoodooAnalyticsWrapperName);
        }

        private void AddSessionSection(string sessionId, IEnumerable<DebugAnalyticsLog> sessionLogs)
        {
            var sessionSection = AddFoldableSection($"Session {sessionId}", true);

            foreach (var log in sessionLogs)
                sessionSection.AddButton(GetEventTitle(log), () => TSDebugUIManager.Instance?.ShowVanEventDetailsScreen(log));
        }

        private static bool IsVoodooAnalyticsLog(DebugAnalyticsLog log)
        {
            return !string.IsNullOrEmpty(log.WrapperName)
                   && log.WrapperName.Contains(VoodooAnalyticsWrapperName);
        }

        private static string GetEventTitle(DebugAnalyticsLog log)
        {
            return $"{log.Timestamp.ToString(EventDateFormat)} - {GetDisplayValue(log.EventName)} - {GetStatusText(log.StateEnum)}";
        }

        private static string GetStatusText(DebugAnalyticsStateEnum state)
        {
            switch (state)
            {
                case DebugAnalyticsStateEnum.ForwardedTo3rdParty:
                    return "Forwarded";
                case DebugAnalyticsStateEnum.Sent:
                    return "Sent";
                case DebugAnalyticsStateEnum.SentButErrorFromServer:
                    return "Server Error";
                case DebugAnalyticsStateEnum.ErrorSending:
                    return "Sending Error";
                case DebugAnalyticsStateEnum.Error:
                    return "Error";
                default:
                    return state.ToString();
            }
        }

        private static string GetDisplayValue(object value)
        {
            if (value == null)
                return EmptyValue;

            return value.ToString();
        }
    }
}
