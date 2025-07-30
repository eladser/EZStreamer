using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using EZStreamer.Models;
using EZStreamer.Services;

namespace EZStreamer.Views
{
    public partial class MainWindow : Window
    {
        private readonly TwitchService _twitchService;
        private readonly SpotifyService _spotifyService;
        private readonly SettingsService _settingsService;
        private readonly SongRequestService _songRequestService;
        private readonly OverlayService _overlayService;
        
        public ObservableCollection<SongRequest> SongQueue { get; set; }
        public ObservableCollection<SongRequest> RequestHistory { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _settingsService = new SettingsService();
            _twitchService = new TwitchService();
            _spotifyService = new SpotifyService();
            _songRequestService = new SongRequestService(_twitchService, _spotifyService);
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
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            
            // Apply settings to UI
            EnableChatCommandsCheckBox.IsChecked = settings.EnableChatCommands;
            EnableChannelPointsCheckBox.IsChecked = settings.EnableChannelPoints;
            MaxQueueLengthTextBox.Text = settings.MaxQueueLength.ToString();
            
            // Try to connect services if tokens exist
            if (!string.IsNullOrEmpty(settings.TwitchAccessToken))
            {
                ConnectTwitchWithToken(settings.TwitchAccessToken);
            }
            
            if (!string.IsNullOrEmpty(settings.SpotifyAccessToken))
            {
                ConnectSpotifyWithToken(settings.SpotifyAccessToken);
            }
        }

        private void SetupEventHandlers()
        {
            // Song request events
            _songRequestService.SongRequested += OnSongRequested;
            _songRequestService.SongStarted += OnSongStarted;
            _songRequestService.SongCompleted += OnSongCompleted;
            
            // Service connection events
            _twitchService.Connected += OnTwitchConnected;
            _twitchService.Disconnected += OnTwitchDisconnected;
            _spotifyService.Connected += OnSpotifyConnected;
            _spotifyService.Disconnected += OnSpotifyDisconnected;
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to settings tab
            var tabControl = (System.Windows.Controls.TabControl)FindName("TabControl");
            if (tabControl != null && tabControl.Items.Count > 3)
            {
                tabControl.SelectedIndex = 3; // Settings tab
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

        private void ConnectSpotifyWithToken(string accessToken)
        {
            try
            {
                _spotifyService.Connect(accessToken);
                
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

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // Save settings before closing
            var settings = _settingsService.LoadSettings();
            settings.EnableChatCommands = EnableChatCommandsCheckBox.IsChecked ?? true;
            settings.EnableChannelPoints = EnableChannelPointsCheckBox.IsChecked ?? true;
            
            if (int.TryParse(MaxQueueLengthTextBox.Text, out int maxQueue))
            {
                settings.MaxQueueLength = maxQueue;
            }
            
            _settingsService.SaveSettings(settings);
            
            // Disconnect services
            _twitchService?.Disconnect();
            _spotifyService?.Disconnect();
            
            base.OnClosed(e);
        }
    }
}
