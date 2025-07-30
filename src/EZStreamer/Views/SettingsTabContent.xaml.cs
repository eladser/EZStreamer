using System;
using System.Windows;
using System.Windows.Controls;
using EZStreamer.Services;
using EZStreamer.Models;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;

namespace EZStreamer.Views
{
    /// <summary>
    /// Interaction logic for SettingsTabContent.xaml
    /// </summary>
    public partial class SettingsTabContent : UserControl
    {
        private readonly ConfigurationService _configService;
        private readonly SettingsService _settingsService;
        private readonly OverlayService _overlayService;

        public SettingsTabContent()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _settingsService = new SettingsService();
            _overlayService = new OverlayService();
            
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var credentials = _configService.GetAPICredentials();
                var settings = _settingsService.LoadSettings();

                // Display current API credentials status
                UpdateCredentialsStatus(credentials);
                
                // Load current settings into UI
                LoadSettingsIntoUI(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCredentialsStatus(APICredentials credentials)
        {
            // Update Twitch status
            if (!string.IsNullOrEmpty(credentials.TwitchClientId))
            {
                TwitchClientIdTextBox.Text = credentials.TwitchClientId;
                TwitchStatusSettings.Style = (Style)FindResource("ConnectedStatus");
            }
            else
            {
                TwitchClientIdTextBox.Text = "";
                TwitchStatusSettings.Style = (Style)FindResource("DisconnectedStatus");
            }

            // Update Spotify status
            if (!string.IsNullOrEmpty(credentials.SpotifyClientId))
            {
                SpotifyClientIdTextBox.Text = credentials.SpotifyClientId;
                SpotifyStatusSettings.Style = (Style)FindResource("ConnectedStatus");
            }
            else
            {
                SpotifyClientIdTextBox.Text = "";
                SpotifyStatusSettings.Style = (Style)FindResource("DisconnectedStatus");
            }

            // Update YouTube status
            if (!string.IsNullOrEmpty(credentials.YouTubeAPIKey))
            {
                YouTubeAPIKeyTextBox.Text = credentials.YouTubeAPIKey;
                YouTubeStatusSettings.Style = (Style)FindResource("ConnectedStatus");
            }
            else
            {
                YouTubeAPIKeyTextBox.Text = "";
                YouTubeStatusSettings.Style = (Style)FindResource("DisconnectedStatus");
            }
        }

        private void LoadSettingsIntoUI(AppSettings settings)
        {
            // Chat settings
            EnableChatCommandsCheckBox.IsChecked = settings.EnableChatCommands;
            EnableChannelPointsCheckBox.IsChecked = settings.EnableChannelPoints;
            MaxQueueLengthTextBox.Text = settings.MaxQueueLength.ToString();
            ChatCommandTextBox.Text = settings.ChatCommand;
            RequestCooldownTextBox.Text = settings.SongRequestCooldown.ToString();

            // Music settings
            PreferredMusicSourceComboBox.SelectedIndex = settings.PreferredMusicSource == "Spotify" ? 0 : 1;
            AutoPlayNextSongCheckBox.IsChecked = settings.AutoPlayNextSong;

            // OBS settings
            OBSServerTextBox.Text = settings.OBSServerIP;
            OBSPortTextBox.Text = settings.OBSServerPort.ToString();
            OBSAutoConnectCheckBox.IsChecked = settings.OBSAutoConnect;
            OBSSceneSwitchingCheckBox.IsChecked = settings.OBSSceneSwitchingEnabled;

            // Overlay settings
            var overlayThemeIndex = settings.OverlayTheme switch
            {
                "Minimal" => 1,
                "Neon" => 2,
                "Classic" => 3,
                _ => 0
            };
            OverlayThemeComboBox.SelectedIndex = overlayThemeIndex;
            OverlayShowAlbumArtCheckBox.IsChecked = settings.OverlayShowAlbumArt;
            OverlayShowRequesterCheckBox.IsChecked = settings.OverlayShowRequester;
            OverlayDurationTextBox.Text = settings.OverlayDisplayDuration.ToString();

            // Advanced settings
            AllowExplicitContentCheckBox.IsChecked = settings.AllowExplicitContent;
            RequireFollowersOnlyCheckBox.IsChecked = settings.RequireFollowersOnly;
            RequireSubscribersOnlyCheckBox.IsChecked = settings.RequireSubscribersOnly;
            MinDurationTextBox.Text = settings.MinStreamDuration.ToString();
            MaxDurationTextBox.Text = settings.MaxStreamDuration.ToString();

            // Set overlay path
            OverlayPathTextBox.Text = _overlayService.OverlayFolderPath;
        }

        private void SaveTwitchCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clientId = TwitchClientIdTextBox.Text.Trim();
                var clientSecret = TwitchClientSecretTextBox.Password.Trim();

                if (string.IsNullOrEmpty(clientId))
                {
                    MessageBox.Show("Please enter a Twitch Client ID.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _configService.SetTwitchCredentials(clientId, clientSecret);
                UpdateCredentialsStatus(_configService.GetAPICredentials());
                
                MessageBox.Show("Twitch credentials saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving Twitch credentials: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSpotifyCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clientId = SpotifyClientIdTextBox.Text.Trim();
                var clientSecret = SpotifyClientSecretTextBox.Password.Trim();

                if (string.IsNullOrEmpty(clientId))
                {
                    MessageBox.Show("Please enter a Spotify Client ID.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _configService.SetSpotifyCredentials(clientId, clientSecret);
                UpdateCredentialsStatus(_configService.GetAPICredentials());
                
                MessageBox.Show("Spotify credentials saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving Spotify credentials: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveYouTubeCredentials_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var apiKey = YouTubeAPIKeyTextBox.Text.Trim();

                if (string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("Please enter a YouTube API Key.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _configService.SetYouTubeAPIKey(apiKey);
                UpdateCredentialsStatus(_configService.GetAPICredentials());
                
                MessageBox.Show("YouTube API key saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving YouTube API key: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConnectTwitch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First check if we have credentials
                var clientId = _configService.GetTwitchClientId();
                if (string.IsNullOrEmpty(clientId))
                {
                    MessageBox.Show("Please configure your Twitch Client ID first by expanding the 'Twitch API Credentials' section above.", 
                        "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show options for authentication
                var result = MessageBox.Show(
                    "Choose authentication method:\n\n" +
                    "YES - Use web browser (may have issues)\n" +
                    "NO - Enter token manually (recommended)\n" +
                    "CANCEL - Cancel authentication",
                    "Twitch Authentication Method",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.No)
                {
                    // Manual token entry
                    var manualDialog = new ManualTokenDialog("Twitch");
                    if (manualDialog.ShowDialog() == true)
                    {
                        // Save the token and test connection
                        var settings = _settingsService.LoadSettings();
                        settings.TwitchAccessToken = manualDialog.Token;
                        _settingsService.SaveSettings(settings);
                        
                        MessageBox.Show("Twitch token saved successfully! Connection will be tested on next app restart.", 
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Web authentication
                    var authWindow = new TwitchAuthWindow();
                    if (authWindow.ShowDialog() == true)
                    {
                        // Save the token
                        var settings = _settingsService.LoadSettings();
                        settings.TwitchAccessToken = authWindow.AccessToken;
                        _settingsService.SaveSettings(settings);
                        
                        MessageBox.Show("Twitch connection successful!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to Twitch: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConnectSpotify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // First check if we have credentials
                var clientId = _configService.GetSpotifyClientId();
                if (string.IsNullOrEmpty(clientId))
                {
                    MessageBox.Show("Please configure your Spotify Client ID first by expanding the 'Spotify API Credentials' section above.", 
                        "Configuration Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show options for authentication
                var result = MessageBox.Show(
                    "Choose authentication method:\n\n" +
                    "YES - Use web browser (may have issues)\n" +
                    "NO - Enter token manually (recommended)\n" +
                    "CANCEL - Cancel authentication",
                    "Spotify Authentication Method",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.No)
                {
                    // Manual token entry
                    var manualDialog = new ManualTokenDialog("Spotify");
                    if (manualDialog.ShowDialog() == true)
                    {
                        // Save the token
                        var settings = _settingsService.LoadSettings();
                        settings.SpotifyAccessToken = manualDialog.Token;
                        _settingsService.SaveSettings(settings);
                        
                        MessageBox.Show("Spotify token saved successfully! Connection will be tested on next app restart.", 
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Web authentication
                    var authWindow = new SpotifyAuthWindow();
                    if (authWindow.ShowDialog() == true)
                    {
                        // Save the token
                        var settings = _settingsService.LoadSettings();
                        settings.SpotifyAccessToken = authWindow.AccessToken;
                        _settingsService.SaveSettings(settings);
                        
                        MessageBox.Show("Spotify connection successful!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to Spotify: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConnectYouTube_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // YouTube Music uses a player window instead of OAuth
                var playerWindow = new YouTubePlayerWindow();
                playerWindow.Show();
                
                MessageBox.Show("YouTube Music player opened!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening YouTube Music: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreferredMusicSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentSettings();
        }

        private async void ConnectOBS_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var obsService = new OBSService();
                var serverIP = OBSServerTextBox.Text.Trim();
                var serverPort = int.TryParse(OBSPortTextBox.Text, out int port) ? port : 4455;
                var password = OBSPasswordBox.Password;

                var success = await obsService.ConnectAsync(serverIP, serverPort, password);
                if (success)
                {
                    MessageBox.Show("Connected to OBS successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to connect to OBS. Please check your settings.", "Connection Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to OBS: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestOBSConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var obsService = new OBSService();
                var serverIP = OBSServerTextBox.Text.Trim();
                var serverPort = int.TryParse(OBSPortTextBox.Text, out int port) ? port : 4455;
                var password = OBSPasswordBox.Password;

                var success = await obsService.ConnectAsync(serverIP, serverPort, password);
                if (success)
                {
                    var testResult = await obsService.TestConnection();
                    await obsService.DisconnectAsync();
                    
                    MessageBox.Show(testResult ? "OBS connection test successful!" : "OBS connection test failed.",
                        "Connection Test", MessageBoxButton.OK, 
                        testResult ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show("Could not connect to OBS to test.", "Test Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error testing OBS connection: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OverlayTheme_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (OverlayThemeComboBox.SelectedItem is ComboBoxItem item)
                {
                    var theme = item.Tag?.ToString() ?? "Default";
                    _overlayService.CreateCustomOverlay(theme);
                    SaveCurrentSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing overlay theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewOverlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a test song for preview
                var testSong = new SongRequest
                {
                    Title = "Sample Song Title",
                    Artist = "Sample Artist",
                    RequestedBy = "TestViewer",
                    SourcePlatform = "Spotify"
                };
                
                _overlayService.UpdateNowPlaying(testSong);
                MessageBox.Show("Overlay preview updated with sample data.", "Preview",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error previewing overlay: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenOverlayFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", _overlayService.OverlayFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open overlay folder: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to reset all settings to default values?\n\nThis action cannot be undone.",
                    "Reset Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _settingsService.ClearSettings();
                    LoadCurrentSettings();
                    MessageBox.Show("Settings reset to defaults.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Export Settings",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = "EZStreamerSettings.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var settings = _settingsService.LoadSettings();
                    // Remove sensitive data before export
                    settings.TwitchAccessToken = "";
                    settings.SpotifyAccessToken = "";
                    
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(saveDialog.FileName, json);
                    
                    MessageBox.Show("Settings exported successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export settings: {ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "Import Settings",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(openDialog.FileName);
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        _settingsService.SaveSettings(settings);
                        LoadCurrentSettings();
                        MessageBox.Show("Settings imported successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import settings: {ex.Message}", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                
                // Update settings from UI
                settings.EnableChatCommands = EnableChatCommandsCheckBox.IsChecked ?? true;
                settings.EnableChannelPoints = EnableChannelPointsCheckBox.IsChecked ?? true;
                settings.ChatCommand = ChatCommandTextBox.Text;
                
                if (int.TryParse(MaxQueueLengthTextBox.Text, out int maxQueue))
                    settings.MaxQueueLength = maxQueue;
                if (int.TryParse(RequestCooldownTextBox.Text, out int cooldown))
                    settings.SongRequestCooldown = cooldown;
                
                // Music settings
                settings.PreferredMusicSource = PreferredMusicSourceComboBox.SelectedIndex == 0 ? "Spotify" : "YouTube";
                settings.AutoPlayNextSong = AutoPlayNextSongCheckBox.IsChecked ?? true;
                
                // OBS settings
                settings.OBSServerIP = OBSServerTextBox.Text;
                if (int.TryParse(OBSPortTextBox.Text, out int port))
                    settings.OBSServerPort = port;
                settings.OBSServerPassword = OBSPasswordBox.Password;
                settings.OBSAutoConnect = OBSAutoConnectCheckBox.IsChecked ?? false;
                settings.OBSSceneSwitchingEnabled = OBSSceneSwitchingCheckBox.IsChecked ?? false;
                
                // Overlay settings
                settings.OverlayTheme = OverlayThemeComboBox.SelectedIndex switch
                {
                    1 => "Minimal",
                    2 => "Neon", 
                    3 => "Classic",
                    _ => "Default"
                };
                settings.OverlayShowAlbumArt = OverlayShowAlbumArtCheckBox.IsChecked ?? true;
                settings.OverlayShowRequester = OverlayShowRequesterCheckBox.IsChecked ?? true;
                if (int.TryParse(OverlayDurationTextBox.Text, out int duration))
                    settings.OverlayDisplayDuration = duration;
                
                // Advanced settings
                settings.AllowExplicitContent = AllowExplicitContentCheckBox.IsChecked ?? true;
                settings.RequireFollowersOnly = RequireFollowersOnlyCheckBox.IsChecked ?? false;
                settings.RequireSubscribersOnly = RequireSubscribersOnlyCheckBox.IsChecked ?? false;
                if (int.TryParse(MinDurationTextBox.Text, out int minDur))
                    settings.MinStreamDuration = minDur;
                if (int.TryParse(MaxDurationTextBox.Text, out int maxDur))
                    settings.MaxStreamDuration = maxDur;
                
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        // Auto-save settings when text changes
        private void SettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveCurrentSettings();
        }

        private void SettingsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
        }
    }
}
