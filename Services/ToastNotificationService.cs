using Microsoft.Toolkit.Uwp.Notifications;
using System;
using Windows.UI.Notifications;

namespace FileOrganizer.Services
{
    /// <summary>
    /// Service for sending Windows Toast notifications
    /// </summary>
    public class ToastNotificationService
    {
        private const string AppName = "Portable File Organizer";
        private const string AppId = "FileOrganizer.PortableFileOrganizer.v5";

        /// <summary>
        /// Show a notification when an operation starts
        /// </summary>
        public void ShowOperationStarted(string operationName, string details = "")
        {
            try
            {
                var content = new ToastContentBuilder()
                    .AddText($"{operationName} Started")
                    .AddText(string.IsNullOrEmpty(details) ? $"{operationName} is now in progress..." : details)
                    .AddAttributionText(AppName)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml());
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                notifier.Show(toast);

                System.Diagnostics.Debug.WriteLine($"[Toast] Sent: {operationName} Started");
            }
            catch (Exception ex)
            {
                // Log but don't crash - toast notifications are nice-to-have
                System.Diagnostics.Debug.WriteLine($"[Toast] Failed to show notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Toast] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Show a notification when an operation completes successfully
        /// </summary>
        public void ShowOperationCompleted(string operationName, string details, TimeSpan duration)
        {
            try
            {
                var durationText = FormatDuration(duration);
                
                var content = new ToastContentBuilder()
                    .AddText($"{operationName} Completed")
                    .AddText(details)
                    .AddText($"Duration: {durationText}")
                    .AddAttributionText(AppName)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml());
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                notifier.Show(toast);

                System.Diagnostics.Debug.WriteLine($"[Toast] Sent: {operationName} Completed in {durationText}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Failed to show notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Toast] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Show a notification when an operation fails
        /// </summary>
        public void ShowOperationFailed(string operationName, string errorMessage)
        {
            try
            {
                var content = new ToastContentBuilder()
                    .AddText($"{operationName} Failed")
                    .AddText(errorMessage)
                    .AddAttributionText(AppName)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml());
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                notifier.Show(toast);

                System.Diagnostics.Debug.WriteLine($"[Toast] Sent: {operationName} Failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Failed to show notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Toast] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Show a notification with progress
        /// </summary>
        public void ShowOperationProgress(string operationName, int progressPercent, string details)
        {
            try
            {
                var content = new ToastContentBuilder()
                    .AddText(operationName)
                    .AddText(details)
                    .AddText($"{progressPercent}% complete")
                    .AddAttributionText(AppName)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml());
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                notifier.Show(toast);

                System.Diagnostics.Debug.WriteLine($"[Toast] Sent: {operationName} Progress {progressPercent}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Failed to show notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Toast] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Test if toast notifications are working
        /// </summary>
        public bool TestNotification()
        {
            try
            {
                var content = new ToastContentBuilder()
                    .AddText("Test Notification")
                    .AddText("Toast notifications are working correctly!")
                    .AddAttributionText(AppName)
                    .GetToastContent();

                var toast = new ToastNotification(content.GetXml());
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                notifier.Show(toast);

                System.Diagnostics.Debug.WriteLine("[Toast] Test notification sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Test notification failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Toast] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Format duration into readable string
        /// </summary>
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
            {
                return $"{duration.TotalMilliseconds:F0}ms";
            }
            else if (duration.TotalMinutes < 1)
            {
                return $"{duration.TotalSeconds:F1}s";
            }
            else if (duration.TotalHours < 1)
            {
                return $"{duration.Minutes}m {duration.Seconds}s";
            }
            else
            {
                return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
            }
        }
    }
}
