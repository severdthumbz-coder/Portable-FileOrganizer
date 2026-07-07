using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FileOrganizer.Controls
{
    /// <summary>
    /// In-app banner notification control (similar to Timer Suite)
    /// </summary>
    public partial class BannerNotification : UserControl
    {
        private DispatcherTimer _autoDismissTimer;
        private Storyboard _showAnimation;
        private Storyboard _hideAnimation;

        public event EventHandler Dismissed;

        public BannerNotification()
        {
            InitializeComponent();
            
            // Get animations from resources
            _showAnimation = (Storyboard)Resources["ShowBanner"];
            _hideAnimation = (Storyboard)Resources["HideBanner"];
            
            // Hook up hide animation completed event
            _hideAnimation.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                Dismissed?.Invoke(this, EventArgs.Empty);
            };
        }

        /// <summary>
        /// Show the banner with specified content
        /// </summary>
        public void Show(string title, string message, string icon = "🎉", int autoDismissSeconds = 10)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            IconText.Text = icon;

            Visibility = Visibility.Visible;
            _showAnimation.Begin(BannerContainer);

            // Setup auto-dismiss timer
            if (autoDismissSeconds > 0)
            {
                _autoDismissTimer?.Stop();
                _autoDismissTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(autoDismissSeconds)
                };
                _autoDismissTimer.Tick += (s, e) =>
                {
                    _autoDismissTimer.Stop();
                    Hide();
                };
                _autoDismissTimer.Start();
            }
        }

        /// <summary>
        /// Hide the banner with animation
        /// </summary>
        public void Hide()
        {
            _autoDismissTimer?.Stop();
            _hideAnimation.Begin(BannerContainer);
        }

        private void DismissButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
