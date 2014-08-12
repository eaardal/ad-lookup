using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Label = System.Windows.Controls.Label;

namespace AdLookup
{
    static class Log
    {
        public enum Severity
        {
            Info, Warning, Error
        }

        private static readonly List<KeyValuePair<string, Severity>> _log;
        private static Label _logForDisplayPanel;
        private static Label _statusBar;
        private static Grid _displayPanel;

        static Log()
        {
            _log = new List<KeyValuePair<string, Severity>>();
        }

        public static void Initialize(Label logForDisplayPanel, Label statusBar, Grid displayPanel)
        {
            _logForDisplayPanel = logForDisplayPanel;
            _statusBar = statusBar;
            _displayPanel = displayPanel;
        }

        public static void ToDisplayPanel(Severity severity, string message)
        {
            _log.Add(new KeyValuePair<string, Severity>(message, severity));
        }

        public static void Show()
        {
            if (!_log.Any()) return;

            string formattedLog = _log.Aggregate(String.Empty, (current, entry) => current + (entry.Value + ": " + entry.Key + "\n"));
            _logForDisplayPanel.Content = formattedLog;
            _displayPanel.Visibility = Visibility.Visible;
        }

        public static void ToStatusBar(string message)
        {
            string logMsg = String.Format("{0} - {1}", DateTime.Now, message);
            _statusBar.Content = logMsg;
        }

        public static void Clear()
        {
            _log.Clear();
        }
    }
}
