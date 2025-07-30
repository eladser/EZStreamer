using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            UpdateQueueDisplay();
            
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
                "Go to the Settings tab to connect your Twitch and Spotify accounts.\n\n" +
                "For now, you can test the functionality using the test controls in the Now Playing tab.",
                "Welcome to EZStreamer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            
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
            
            // Collection change events
            SongQueue.CollectionChanged += (s, e) => UpdateQueueDisplay();
        }

        private void UpdateStatusIndicators()
        {
            // Update Twitch status
            var twitchConnected = _twitchService.IsConnected;
            TwitchStatusIndicator.Style = twitchConnected ? 
                (Style)FindResource("ConnectedStatus") : 
                (Style)FindResource("DisconnectedStatus");
            
            // Update Spotify status
            var spotifyConnected = _spotifyService.IsConnected;
            SpotifyStatusIndicator.Style = spotifyConnected ? 
                (Style)FindResource("ConnectedStatus") : 
                (Style)FindResource("DisconnectedStatus");
        }

        private void UpdateQueueDisplay()
        {
            var count = SongQueue.Count;
            QueueCountText.Text = count == 0 ? "Empty" : 
                                  count == 1 ? "1 song" : 
                                  $"{count} songs";
            
            EmptyQueueMessage.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                StatusText.Text = "YouTube Music player ready!";
            });
        }

        private void OnYouTubeDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "YouTube Music disconnected.";
            });
        }

        private void OnOBSConnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Connected to OBS! Scene switching available.";
            });
        }

        private void OnOBSDisconnected(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
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

        private void SkipSong_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await _songRequestService.SkipCurrentSong();
                    Dispatcher.Invoke(() => StatusText.Text = "Song skipped.");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => StatusText.Text = $"Error skipping song: {ex.Message}");
                }
            });
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
            
            try
            {
                _twitchService.UpdateStreamInfo(title, category);
                StatusText.Text = "Stream information updated.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error updating stream info: {ex.Message}";
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
                MessageBox.Show($"Could not open overlay folder: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to settings tab
            TabControl.SelectedIndex = 3; // Settings tab is the 4th tab (index 3)
        }

        // New test functionality
        private void AddTestSong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var title = TestSongTitleTextBox.Text.Trim();
                var artist = TestSongArtistTextBox.Text.Trim();
                var requester = TestRequesterTextBox.Text.Trim();
                var platform = TestPlatformComboBox.SelectedIndex == 0 ? "Spotify" : "YouTube";

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
                {
                    MessageBox.Show("Please enter both song title and artist.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var testSong = new SongRequest
                {
                    Title = title,
                    Artist = artist,
                    RequestedBy = string.IsNullOrEmpty(requester) ? "TestUser" : requester,
                    SourcePlatform = platform,
                    Timestamp = DateTime.Now,
                    Status = SongRequestStatus.Queued
                };

                SongQueue.Add(testSong);
                RequestHistory.Insert(0, testSong);
                
                StatusText.Text = $"Test song added: {title} by {artist}";
                
                // Generate random suggestions for next test
                var songSuggestions = new[]
                {
                    ("Bohemian Rhapsody", "Queen"),
                    ("Imagine", "John Lennon"),
                    ("Hotel California", "Eagles"),
                    ("Stairway to Heaven", "Led Zeppelin"),
                    ("Sweet Child O' Mine", "Guns N' Roses"),
                    ("Thunderstruck", "AC/DC"),
                    ("Smells Like Teen Spirit", "Nirvana"),
                    ("Billie Jean", "Michael Jackson")
                };
                
                var random = new Random();
                var suggestion = songSuggestions[random.Next(songSuggestions.Length)];
                TestSongTitleTextBox.Text = suggestion.Item1;
                TestSongArtistTextBox.Text = suggestion.Item2;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding test song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SongQueue.Count == 0)
                {
                    MessageBox.Show("Queue is already empty.", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to clear all {SongQueue.Count} songs from the queue?",
                    "Clear Queue",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SongQueue.Clear();
                    StatusText.Text = "Song queue cleared.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing queue: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlaySongNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is SongRequest song)
                {
                    // Simulate playing the song
                    OnSongStarted(this, song);
                    
                    // Move to top of queue if not already
                    if (SongQueue.Contains(song))
                    {
                        SongQueue.Remove(song);
                        SongQueue.Insert(0, song);
                    }
                    
                    StatusText.Text = $"Now playing: {song.Title} by {song.Artist}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is SongRequest song)
                {
                    SongQueue.Remove(song);
                    StatusText.Text = $"Removed {song.Title} from queue.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // Force proper cleanup before closing
            PerformCleanup();
            base.OnClosed(e);
        }

        private void PerformCleanup()
        {
            try
            {
                // Disconnect and dispose services properly
                _twitchService?.Disconnect();
                _spotifyService?.Disconnect();
                
                // Force close YouTube player window if it exists
                if (_youtubeMusicService?.IsConnected == true)
                {
                    var youtubeService = _youtubeMusicService as YouTubeMusicService;
                    if (youtubeService != null)
                    {
                        try
                        {
                            // Use reflection to access the private _playerWindow field
                            var playerWindowField = youtubeService.GetType().GetField("_playerWindow", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            
                            if (playerWindowField?.GetValue(youtubeService) is YouTubePlayerWindow playerWindow)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    playerWindow.ForceClose();
                                });
                            }
                        }
                        catch
                        {
                            // Ignore reflection errors, just disconnect normally
                        }
                    }
                    
                    _youtubeMusicService.Disconnect();
                }
                
                // Disconnect OBS with timeout
                if (_obsService?.IsConnected == true)
                {
                    var disconnectTask = _obsService.DisconnectAsync();
                    
                    // Wait up to 2 seconds for graceful disconnect
                    if (!disconnectTask.Wait(2000))
                    {
                        System.Diagnostics.Debug.WriteLine("OBS disconnect timed out");
                    }
                }
                
                // Dispose OBS service
                _obsService?.Dispose();
                
                // Force garbage collection to clean up any WebView2 processes
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
                // Don't throw exceptions during cleanup as it might prevent shutdown
            }
        }
    }
}
