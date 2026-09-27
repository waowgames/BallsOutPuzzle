using System.Collections.Generic;
using System.Linq;
using Voodoo.Tiny.Sauce.Internal.Analytics;
using Voodoo.Tiny.Sauce.Internal.Debugger.Widgets;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Screens
{
    public class TSDebugVanEventDetailsScreen : TSDebugScreen
    {
        private const string EmptyValue = "-";
        private const string EventDateFormat = "dd/MM/yyyy HH:mm:ss";

        private DebugAnalyticsLog _log;
        private bool _hasLog;

        protected override string ScreenTitle => "VAN Event Details";

        internal void SetLog(DebugAnalyticsLog log)
        {
            _log = log;
            _hasLog = true;
        }

        protected override void OnScreenShow()
        {
            ClearWidgets();

            if (!_hasLog)
            {
                AddText("No event selected.");
                return;
            }

            AddTitle(GetDisplayValue(_log.EventName));
            AddTextPair("Status", GetStatusText(_log.StateEnum));
            AddTextPair("Time", _log.Timestamp.ToString(EventDateFormat));
            AddTextPair("Wrapper", GetDisplayValue(_log.WrapperName));
            AddTextPair("Event ID", GetDisplayValue(_log.EventId), _log.EventId);
            AddTextPair("Session ID", GetDisplayValue(_log.SessionId), _log.SessionId);

            if (!string.IsNullOrEmpty(_log.AdditionalInformation))
                AddTextPair("Details", _log.AdditionalInformation, _log.AdditionalInformation);

            if (!string.IsNullOrEmpty(_log.Error))
                AddTextPair("Error", _log.Error, _log.Error);

            AddParameters(_log.Parameters);
        }

        protected override void OnBackPressed() => TSDebugUIManager.Instance?.ShowVanEventConsoleScreen();

        private void AddParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                AddText("No parameters.");
                return;
            }

            var parametersSection = AddFoldableSection("Parameters", true);

            foreach (var parameter in parameters.OrderBy(parameter => parameter.Key))
                AddParameter(parametersSection, parameter.Key, parameter.Value);
        }

        private static void AddParameter(TSDebugCompositeWidget parent, string label, object value)
        {
            if (value is Dictionary<string, object> nestedParameters)
            {
                var nestedSection = parent.AddFoldableSection(label, true);
                foreach (var nestedParameter in nestedParameters.OrderBy(parameter => parameter.Key))
                    AddParameter(nestedSection, nestedParameter.Key, nestedParameter.Value);

                return;
            }

            var displayValue = GetDisplayValue(value);
            parent.AddTextPair(label, displayValue, displayValue);
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
