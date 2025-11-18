using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using EZStreamer.Services;

namespace EZStreamer.Views
{
    public partial class SetupWizardWindow : Window
    {
        private int _currentStep = 1;
        private readonly ConfigurationService _configService;
        private readonly SpotifyService _spotifyService;
        private readonly YouTubeMusicService _youtubeService;
        private readonly TwitchService _twitchService;
        private readonly SettingsService _settingsService;

        private bool _spotifyConfigured = false;
        private bool _youtubeConfigured = false;
        private bool _twitchConfigured = false;

        public SetupWizardWindow()
        {
            InitializeComponent();

            _configService = new ConfigurationService();
            _spotifyService = new SpotifyService();
            _youtubeService = new YouTubeMusicService();
            _twitchService = new TwitchService();
            _settingsService = new SettingsService();

            // Load existing credentials if any
            LoadExistingCredentials();
        }

        private void LoadExistingCredentials()
        {
            try
            {
                var credentials = _configService.GetAPICredentials();

                if (!string.IsNullOrEmpty(credentials.SpotifyClientId))
                {
                    SpotifyClientIdBox.Text = credentials.SpotifyClientId;
                }

                if (!string.IsNullOrEmpty(credentials.YouTubeAPIKey))
                {
                    YouTubeAPIKeyBox.Text = credentials.YouTubeAPIKey;
                }

                // Check if services are already connected
                if (_spotifyService.IsConnected)
                {
                    _spotifyConfigured = true;
                    ShowSpotifySuccess();
                }

                if (_youtubeService.IsConnected)
                {
                    _youtubeConfigured = true;
                    ShowYouTubeSuccess();
                }

                if (_twitchService.IsConnected)
                {
                    _twitchConfigured = true;
                    ShowTwitchSuccess();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading existing credentials: {ex.Message}");
            }
        }

        private void UpdateStepIndicators()
        {
            // Reset all
            Step1Circle.Opacity = 0.3;
            Step2Circle.Opacity = 0.3;
            Step3Circle.Opacity = 0.3;

            // Highlight current
            switch (_currentStep)
            {
                case 1:
                    Step1Circle.Opacity = 1.0;
                    break;
                case 2:
                    Step2Circle.Opacity = 1.0;
                    break;
                case 3:
                    Step3Circle.Opacity = 1.0;
                    break;
            }
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            UpdateStepIndicators();

            // Hide all panels
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Collapsed;
            Step3Panel.Visibility = Visibility.Collapsed;
            CompletionPanel.Visibility = Visibility.Collapsed;

            // Show current step
            switch (step)
            {
                case 1:
                    Step1Panel.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Content = "Next ▶️";
                    SkipButton.Visibility = Visibility.Visible;
                    break;
                case 2:
                    Step2Panel.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Visible;
                    NextButton.Content = "Next ▶️";
                    SkipButton.Visibility = Visibility.Visible;
                    break;
                case 3:
                    Step3Panel.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Visible;
                    NextButton.Content = "Finish ✓";
                    SkipButton.Visibility = Visibility.Visible;
                    break;
                case 4:
                    CompletionPanel.Visibility = Visibility.Visible;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Visibility = Visibility.Collapsed;
                    SkipButton.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async void TestSpotify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SpotifyClientIdError.Visibility = Visibility.Collapsed;
                SpotifyClientSecretError.Visibility = Visibility.Collapsed;
                SpotifySuccessMessage.Visibility = Visibility.Collapsed;

                var clientId = SpotifyClientIdBox.Text.Trim();
                var clientSecret = SpotifyClientSecretBox.Password.Trim();

                // Validation
                if (string.IsNullOrEmpty(clientId))
                {
                    SpotifyClientIdError.Text = "❌ Client ID is required!";
                    SpotifyClientIdError.Visibility = Visibility.Visible;
                    return;
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    SpotifyClientSecretError.Text = "❌ Client Secret is required!";
                    SpotifyClientSecretError.Visibility = Visibility.Visible;
                    return;
                }

                // Save credentials
                _configService.SetSpotifyCredentials(clientId, clientSecret);

                // Show auth window
                var authWindow = new SpotifyAuthWindow();
                var result = authWindow.ShowDialog();

                if (result == true && authWindow.IsAuthenticated)
                {
                    _spotifyConfigured = true;
                    ShowSpotifySuccess();
                    MessageBox.Show("✅ Spotify connected successfully!\n\nYou can now use Spotify for song requests.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("❌ Spotify authentication was cancelled or failed.\n\nPlease try again.",
                        "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error connecting to Spotify:\n\n{ex.Message}\n\nPlease check your credentials and try again.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSpotifySuccess()
        {
            SpotifySuccessMessage.Text = "✅ Spotify is connected! You're ready to play music!";
            SpotifySuccessMessage.Visibility = Visibility.Visible;
        }

        private async void TestYouTube_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                YouTubeAPIKeyError.Visibility = Visibility.Collapsed;
                YouTubeSuccessMessage.Visibility = Visibility.Collapsed;

                var apiKey = YouTubeAPIKeyBox.Text.Trim();

                if (string.IsNullOrEmpty(apiKey))
                {
                    YouTubeAPIKeyError.Text = "❌ API Key is required!";
                    YouTubeAPIKeyError.Visibility = Visibility.Visible;
                    return;
                }

                // Save API key
                _configService.SetYouTubeAPIKey(apiKey);

                // Test the API key
                var isValid = await _youtubeService.ValidateAPIKeyAsync(apiKey);

                if (isValid)
                {
                    _youtubeConfigured = true;
                    ShowYouTubeSuccess();
                    MessageBox.Show("✅ YouTube API key is valid!\n\nYou can now search YouTube for songs.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("❌ Invalid YouTube API key!\n\nPlease check your API key and try again.\n\nMake sure YouTube Data API v3 is enabled in your Google Cloud project.",
                        "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error validating YouTube API key:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowYouTubeSuccess()
        {
            YouTubeSuccessMessage.Text = "✅ YouTube is connected! You can search for any video!";
            YouTubeSuccessMessage.Visibility = Visibility.Visible;
        }

        private async void ConnectTwitch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TwitchSuccessMessage.Visibility = Visibility.Collapsed;

                var channelName = TwitchChannelBox.Text.Trim();

                if (string.IsNullOrEmpty(channelName))
                {
                    MessageBox.Show("❌ Please enter your Twitch channel name!",
                        "Channel Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show Twitch auth window
                var authWindow = new TwitchAuthWindow();
                var result = authWindow.ShowDialog();

                if (result == true)
                {
                    // Save channel name and connect
                    var settings = _settingsService.LoadSettings();
                    settings.TwitchClientId = channelName;
                    _settingsService.SaveSettings(settings);

                    _twitchConfigured = true;
                    ShowTwitchSuccess();
                    MessageBox.Show($"✅ Connected to Twitch channel: {channelName}\n\nViewers can now request songs in your chat!",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("❌ Twitch authentication was cancelled.\n\nYou can try again later from the Settings tab.",
                        "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error connecting to Twitch:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTwitchSuccess()
        {
            TwitchSuccessMessage.Text = "✅ Twitch is connected! Viewers can request songs!";
            TwitchSuccessMessage.Visibility = Visibility.Visible;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 3)
            {
                ShowStep(_currentStep + 1);
            }
            else
            {
                // Finish button
                ShowStep(4);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                ShowStep(_currentStep - 1);
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 3)
            {
                ShowStep(_currentStep + 1);
            }
            else
            {
                ShowStep(4);
            }
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            // Mark setup as completed
            var settings = _settingsService.LoadSettings();
            settings.LastUsedVersion = "2.0"; // Indicate setup was completed
            _settingsService.SaveSettings(settings);

            DialogResult = true;
            Close();
        }

        private void CopySpotifyURI_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText("http://127.0.0.1:8888/callback");
                MessageBox.Show("✅ Redirect URI copied to clipboard!\n\nYou can now paste it in Spotify's dashboard.",
                    "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
