using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EZStreamer.Models;
using EZStreamer.Services;
using Microsoft.Win32;

namespace EZStreamer.Views
{
    public partial class MainWindow : Window
    {
        private readonly TwitchService _twitchService;
        private readonly SpotifyService _spotifyService;
        private readonly YouTubeMusicService _youtubeMusicService;
        private readonly OBSService _obsService;
        private readonly SettingsService _settingsService;
        private readonly ConfigurationService _configurationService;
        private readonly SongRequestService _songRequestService;
        private readonly OverlayService _overlayService;
        
        public ObservableCollection<SongRequest> SongQueue { get; set; }
        public ObservableCollection<SongRequest> RequestHistory { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _settingsService = new SettingsService();
            _configurationService = new ConfigurationService();
            _twitchService = new TwitchService();
            _spotifyService = new SpotifyService();
            _youtubeMusicService = new YouTubeMusicService();
            _obsService = new OBSService();
            _songRequestService = new SongRequestService(_twitchService, _spotifyService, _youtubeMusicService, _settingsService);
            _overlayService = new OverlayService();
            
            // Initialize collections
            SongQueue = new ObservableCollection<SongRequest>();
            RequestHistory = new ObservableCollection<SongRequest>();
            
            // Bind data
            SongQueueList.ItemsSource = SongQueue;
            RequestHistoryGrid.ItemsSource = RequestHistory;
            
            // Set up overlay path
            OverlayPathTextBox.Text = _overlayService.OverlayFolderPath;
            
            // Load settings and initialize
            LoadSettings();
            SetupEventHandlers();
            UpdateStatusIndicators();
            
            StatusText.Text = "Welcome to EZStreamer! Connect your accounts to get started.";
            
            // Check if this is first run
            if (_configurationService.IsFirstRun())
            {
                ShowFirstRunMessage();
            }
        }

        private void ShowFirstRunMessage()
        {
            MessageBox.Show(
                "Welcome to EZStreamer!\n\n" +
                "This appears to be your first time running the application. " +
                "You'll need to configure your API credentials to get started.\n\n" +
                "Go to the Settings tab to connect your Twitch and Spotify accounts.",
                "Welcome to EZStreamer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            
            // Apply settings to UI - using settings object directly since controls are in UserControls
            // The actual UI control updates should be handled by the individual UserControls
            
            // Try to connect services if tokens exist
            if (!string.IsNullOrEmpty(settings.TwitchAccessToken))
            {
                ConnectTwitchWithToken(settings.TwitchAccessToken);
            }
            
            if (!string.IsNullOrEmpty(settings.SpotifyAccessToken))
            {
                ConnectSpotifyWithToken(settings.SpotifyAccessToken);
            }
            
            // Connect YouTube Music (no token needed)
            ConnectYouTube();
            
            // Auto-connect to OBS if enabled
            if (settings.OBSAutoConnect)
            {
                ConnectOBSWithSettings(settings);
            }
        }

        private void SetupEventHandlers()
        {
            // Song request events
            _songRequestService.SongRequested += OnSongRequested;
            _songRequestService.SongStarted += OnSongStarted;
            _songRequestService.SongCompleted += OnSongCompleted;
            _songRequestService.ErrorOccurred += OnSongRequestError;
            
            // Service connection events
            _twitchService.Connected += OnTwitchConnected;
            _twitchService.Disconnected += OnTwitchDisconnected;
            _spotifyService.Connected += OnSpotifyConnected;
            _spotifyService.Disconnected += OnSpotifyDisconnected;
            _youtubeMusicService.Connected += OnYouTubeConnected;
            _youtubeMusicService.Disconnected += OnYouTubeDisconnected;
            _obsService.Connected += OnOBSConnected;
            _obsService.Disconnected += OnOBSDisconnected;
            _obsService.ErrorOccurred += OnOBSError;
        }

        private void UpdateStatusIndicators()
        {
            // Update Twitch status
            var twitchConnected = _twitchService.IsConnected;
            TwitchStatusIndicator.Style = twitchConnected ? 
                (Style)FindResource("ConnectedStatus") : 
                (Style)FindResource("DisconnectedStatus");
            TwitchStatusSettings.Style = TwitchStatusIndicator.Style;
            TwitchConnectButton.Content = twitchConnected ? "Disconnect" : "Connect";
            
            // Update Spotify status
            var spotifyConnected = _spotifyService.IsConnected;
            SpotifyStatusIndicator.Style = spotifyConnected ? 
                (Style)FindResource("ConnectedStatus") : 
                (Style)FindResource("DisconnectedStatus");
            SpotifyStatusSettings.Style = SpotifyStatusIndicator.Style;
            SpotifyConnectButton.Content = spotifyConnected ? "Disconnect" : "Connect";
        }

        #region Event Handlers

        private void OnSongRequested(object sender, SongRequest request)
        {
            Dispatcher.Invoke(() =>
            {
                SongQueue.Add(request);
                RequestHistory.Insert(0, request);
                StatusText.Text = $"Song requested by {request.RequestedBy}: {request.Title}";
            });
        }

        private void OnSongStarted(object sender, SongRequest request)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentSongTitle.Text = request.Title;
                CurrentSongArtist.Text = request.Artist;
                RequestedBy.Text = $"Requested by {request.RequestedBy}";
                
                // Update overlay
                _overlayService.UpdateNowPlaying(request);
                
                StatusText.Text = $"Now playing: {request.Title} by {request.Artist}";
            });
        }

        private void OnSongCompleted(object sender, SongRequest request)
        {
            Dispatcher.Invoke(() =>
            {
                if (SongQueue.Contains(request))
                {
                    SongQueue.Remove(request);
                }
            });
        }

        private void OnSongRequestError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Error: {error}";
            });
        }

        private void OnTwitchConnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Connected to Twitch! You can now receive song requests.";
            });
        }

        private void OnTwitchDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Disconnected from Twitch.";
            });
        }

        private void OnSpotifyConnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Connected to Spotify! Songs can now be played automatically.";
            });
        }

        private void OnSpotifyDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Disconnected from Spotify.";
            });
        }

        private void OnYouTubeConnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "YouTube Music player ready!";
            });
        }

        private void OnYouTubeDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "YouTube Music disconnected.";
            });
        }

        private void OnOBSConnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Connected to OBS! Scene switching available.";
            });
        }

        private void OnOBSDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusIndicators();
                StatusText.Text = "Disconnected from OBS.";
            });
        }

        private void OnOBSError(object sender, string error)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"OBS Error: {error}";
            });
        }

        #endregion

        #region Button Click Handlers

        private void ConnectTwitch_Click(object sender, RoutedEventArgs e)
        {
            if (_twitchService.IsConnected)
            {
                _twitchService.Disconnect();
            }
            else
            {
                var authWindow = new TwitchAuthWindow();
                if (authWindow.ShowDialog() == true)
                {
                    ConnectTwitchWithToken(authWindow.AccessToken);
                }
            }
        }

        private void ConnectSpotify_Click(object sender, RoutedEventArgs e)
        {
            if (_spotifyService.IsConnected)
            {
                _spotifyService.Disconnect();
            }
            else
            {
                var authWindow = new SpotifyAuthWindow();
                if (authWindow.ShowDialog() == true)
                {
                    ConnectSpotifyWithToken(authWindow.AccessToken);
                }
            }
        }

        private void ConnectYouTube_Click(object sender, RoutedEventArgs e)
        {
            if (_youtubeMusicService.IsConnected)
            {
                _youtubeMusicService.Disconnect();
            }
            else
            {
                ConnectYouTube();
            }
        }

        private async void ConnectOBS_Click(object sender, RoutedEventArgs e)
        {
            if (_obsService.IsConnected)
            {
                await _obsService.DisconnectAsync();
            }
            else
            {
                // Use default settings for now since controls are in UserControls
                var success = await _obsService.ConnectAsync("localhost", 4455, "");
                if (!success)
                {
                    MessageBox.Show("Failed to connect to OBS. Please check your settings.", "Connection Failed",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void TestOBSConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_obsService.IsConnected)
            {
                var success = await _obsService.TestConnection();
                MessageBox.Show(success ? "OBS connection is working!" : "OBS connection test failed.",
                    "Connection Test", MessageBoxButton.OK, 
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("Please connect to OBS first.", "Not Connected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SkipSong_Click(object sender, RoutedEventArgs e)
        {
            _songRequestService.SkipCurrentSong();
            StatusText.Text = "Song skipped.";
        }

        private void UpdateStreamInfo_Click(object sender, RoutedEventArgs e)
        {
            var title = StreamTitleTextBox.Text;
            var category = StreamCategoryTextBox.Text;
            
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter a stream title.", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            _twitchService.UpdateStreamInfo(title, category);
            StatusText.Text = "Stream information updated.";
        }

        private void OpenOverlayFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", _overlayService.OverlayFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open overlay folder: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewOverlay_Click(object sender, RoutedEventArgs e)
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
            StatusText.Text = "Overlay preview updated with sample data.";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to settings tab (assuming it's the 4th tab)
            var tabControl = (System.Windows.Controls.TabControl)FindName("TabControl");
            if (tabControl != null && tabControl.Items.Count > 3)
            {
                tabControl.SelectedIndex = 3; // Settings tab
            }
        }

        #endregion

        #region Settings Event Handlers

        private void PreferredMusicSource_Changed(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentSettings();
        }

        private void OverlayTheme_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Placeholder - theme change should be handled by settings UserControl
            SaveCurrentSettings();
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?\n\nThis action cannot be undone.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settingsService.ClearSettings();
                LoadSettings();
                StatusText.Text = "Settings reset to defaults.";
            }
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e)
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
                try
                {
                    var settings = _settingsService.LoadSettings();
                    // Remove sensitive data before export
                    settings.TwitchAccessToken = "";
                    settings.SpotifyAccessToken = "";
                    
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(saveDialog.FileName, json);
                    
                    StatusText.Text = "Settings exported successfully.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export settings: {ex.Message}", "Export Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "Import Settings",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(openDialog.FileName);
                    var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        _settingsService.SaveSettings(settings);
                        LoadSettings();
                        StatusText.Text = "Settings imported successfully.";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import settings: {ex.Message}", "Import Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Helper Methods

        private void ConnectTwitchWithToken(string accessToken)
        {
            try
            {
                _twitchService.Connect(accessToken);
                
                // Save token
                var settings = _settingsService.LoadSettings();
                settings.TwitchAccessToken = accessToken;
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to Twitch: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ConnectSpotifyWithToken(string accessToken)
        {
            try
            {
                await _spotifyService.Connect(accessToken);
                
                // Save token
                var settings = _settingsService.LoadSettings();
                settings.SpotifyAccessToken = accessToken;
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to Spotify: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConnectYouTube()
        {
            try
            {
                _youtubeMusicService.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to YouTube Music: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ConnectOBSWithSettings(AppSettings settings)
        {
            try
            {
                await _obsService.ConnectAsync(settings.OBSServerIP, settings.OBSServerPort, settings.OBSServerPassword);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-connect to OBS failed: {ex.Message}");
            }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                // Since UI controls are now in UserControls, this is a placeholder
                // The actual saving should be handled by the individual UserControls
                var settings = _settingsService.LoadSettings();
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // Save settings before closing
            SaveCurrentSettings();
            
            // Disconnect services
            _twitchService?.Disconnect();
            _spotifyService?.Disconnect();
            _youtubeMusicService?.Disconnect();
            _obsService?.Dispose();
            
            base.OnClosed(e);
        }
    }
}
