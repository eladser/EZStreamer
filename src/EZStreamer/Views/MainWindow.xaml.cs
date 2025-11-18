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
            
            // Set Client IDs for services
            _twitchService.SetClientId(_configurationService.GetTwitchClientId());
            
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
            
            StatusText.Text = "EZStreamer ready! Connect to Twitch and type !songrequest <song name> in chat to test.";

            // Show setup wizard on first run
            CheckAndShowSetupWizard();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                
                // Try to connect services if tokens exist and are valid
                if (!string.IsNullOrEmpty(settings.TwitchAccessToken))
                {
                    Task.Run(() => ConnectTwitchWithToken(settings.TwitchAccessToken));
                }
                
                if (!string.IsNullOrEmpty(settings.SpotifyAccessToken))
                {
                    Task.Run(() => ConnectSpotifyWithToken(settings.SpotifyAccessToken));
                }
                
                // Auto-connect to OBS if enabled
                if (settings.OBSAutoConnect)
                {
                    Task.Run(() => ConnectOBSWithSettings(settings));
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading settings: {ex.Message}";
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
            _twitchService.MessageReceived += OnTwitchMessageReceived;
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

        private void OnTwitchMessageReceived(object sender, string message)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Update status to show recent chat activity
                    StatusText.Text = $"Chat: {message}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Twitch message: {ex.Message}");
            }
        }

        private void UpdateStatusIndicators()
        {
            try
            {
                // Update Twitch status
                var twitchConnected = _twitchService?.IsConnected ?? false;
                TwitchStatusIndicator.Style = twitchConnected ? 
                    (Style)FindResource("ConnectedStatus") : 
                    (Style)FindResource("DisconnectedStatus");
                
                // Update Spotify status
                var spotifyConnected = _spotifyService?.IsConnected ?? false;
                SpotifyStatusIndicator.Style = spotifyConnected ? 
                    (Style)FindResource("ConnectedStatus") : 
                    (Style)FindResource("DisconnectedStatus");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating status indicators: {ex.Message}");
            }
        }

        private void UpdateQueueDisplay()
        {
            try
            {
                var count = SongQueue?.Count ?? 0;
                QueueCountText.Text = count == 0 ? "Empty" : 
                                      count == 1 ? "1 song" : 
                                      $"{count} songs";
                
                EmptyQueueMessage.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating queue display: {ex.Message}");
            }
        }

        #region Event Handlers

        private void OnSongRequested(object sender, SongRequest request)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    SongQueue.Add(request);
                    RequestHistory.Insert(0, request);
                    StatusText.Text = $"Song requested by {request.RequestedBy}: {request.Title}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling song request: {ex.Message}");
            }
        }

        private void OnSongStarted(object sender, SongRequest request)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    CurrentSongTitle.Text = request.Title;
                    CurrentSongArtist.Text = request.Artist;
                    RequestedBy.Text = $"Requested by {request.RequestedBy}";
                    
                    // Update request status
                    request.Status = SongRequestStatus.Playing;
                    
                    // Update overlay
                    try
                    {
                        _overlayService.UpdateNowPlaying(request);
                    }
                    catch (Exception overlayEx)
                    {
                        Debug.WriteLine($"Overlay update failed: {overlayEx.Message}");
                    }
                    
                    StatusText.Text = $"Now playing: {request.Title} by {request.Artist}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling song start: {ex.Message}");
            }
        }

        private void OnSongCompleted(object sender, SongRequest request)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    request.Status = SongRequestStatus.Completed;
                    if (SongQueue.Contains(request))
                    {
                        SongQueue.Remove(request);
                    }
                    
                    // Auto-play next song if enabled
                    var settings = _settingsService.LoadSettings();
                    if (settings.AutoPlayNextSong && SongQueue.Count > 0)
                    {
                        var nextSong = SongQueue.First();
                        Task.Run(() => PlaySong(nextSong));
                    }
                    else
                    {
                        // Clear current song display if queue is empty
                        if (SongQueue.Count == 0)
                        {
                            CurrentSongTitle.Text = "No song playing";
                            CurrentSongArtist.Text = "Use !songrequest <song> in Twitch chat or add test songs";
                            RequestedBy.Text = "";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling song completion: {ex.Message}");
            }
        }

        private void OnSongRequestError(object sender, string error)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Error: {error}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling song request error: {ex.Message}");
            }
        }

        private void OnTwitchConnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusIndicators();
                    StatusText.Text = $"Connected to Twitch as {_twitchService.ChannelName}! You can now receive song requests with !songrequest <song>";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Twitch connection: {ex.Message}");
            }
        }

        private void OnTwitchDisconnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusIndicators();
                    StatusText.Text = "Disconnected from Twitch.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Twitch disconnection: {ex.Message}");
            }
        }

        private void OnSpotifyConnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusIndicators();
                    StatusText.Text = "Connected to Spotify! Songs can now be played automatically.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Spotify connection: {ex.Message}");
            }
        }

        private void OnSpotifyDisconnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusIndicators();
                    StatusText.Text = "Disconnected from Spotify.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Spotify disconnection: {ex.Message}");
            }
        }

        private void OnYouTubeConnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "YouTube Music player ready!";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling YouTube connection: {ex.Message}");
            }
        }

        private void OnYouTubeDisconnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "YouTube Music disconnected.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling YouTube disconnection: {ex.Message}");
            }
        }

        private void OnOBSConnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Connected to OBS! Scene switching available.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling OBS connection: {ex.Message}");
            }
        }

        private void OnOBSDisconnected(object sender, EventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Disconnected from OBS.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling OBS disconnection: {ex.Message}");
            }
        }

        private void OnOBSError(object sender, string error)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"OBS Error: {error}";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling OBS error: {ex.Message}");
            }
        }

        #endregion

        #region Button Click Handlers

        private async void SkipSong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _songRequestService.SkipCurrentSong();
                StatusText.Text = "Song skipped.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error skipping song: {ex.Message}";
            }
        }

        private void UpdateStreamInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var title = StreamTitleTextBox.Text?.Trim();
                var category = StreamCategoryTextBox.Text?.Trim();
                
                if (string.IsNullOrWhiteSpace(title))
                {
                    MessageBox.Show("Please enter a stream title.", "EZStreamer", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!_twitchService.IsConnected)
                {
                    MessageBox.Show("Please connect to Twitch first.", "Not Connected", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
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
                if (Directory.Exists(_overlayService.OverlayFolderPath))
                {
                    Process.Start("explorer.exe", _overlayService.OverlayFolderPath);
                }
                else
                {
                    Directory.CreateDirectory(_overlayService.OverlayFolderPath);
                    Process.Start("explorer.exe", _overlayService.OverlayFolderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open overlay folder: {ex.Message}", "EZStreamer", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Switch to settings tab
                TabControl.SelectedIndex = 3; // Settings tab is the 4th tab (index 3)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error switching to settings: {ex.Message}");
            }
        }

        // Test functionality
        private async void AddTestSong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var title = TestSongTitleTextBox.Text?.Trim();
                var artist = TestSongArtistTextBox.Text?.Trim();
                var requester = TestRequesterTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
                {
                    MessageBox.Show("Please enter both song title and artist.", "Missing Information",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var query = $"{title} {artist}";
                await _songRequestService.ProcessSongRequest(query, string.IsNullOrEmpty(requester) ? "TestUser" : requester, "manual");
                
                StatusText.Text = $"Test song requested: {title} by {artist}";
                
                // Generate random suggestions for next test
                GenerateRandomSongSuggestion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding test song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateRandomSongSuggestion()
        {
            try
            {
                var songSuggestions = new[]
                {
                    ("Bohemian Rhapsody", "Queen"),
                    ("Imagine", "John Lennon"),
                    ("Hotel California", "Eagles"),
                    ("Stairway to Heaven", "Led Zeppelin"),
                    ("Sweet Child O' Mine", "Guns N' Roses"),
                    ("Thunderstruck", "AC/DC"),
                    ("Smells Like Teen Spirit", "Nirvana"),
                    ("Billie Jean", "Michael Jackson"),
                    ("Don't Stop Believin'", "Journey"),
                    ("Livin' on a Prayer", "Bon Jovi")
                };
                
                var random = new Random();
                var suggestion = songSuggestions[random.Next(songSuggestions.Length)];
                TestSongTitleTextBox.Text = suggestion.Item1;
                TestSongArtistTextBox.Text = suggestion.Item2;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating song suggestion: {ex.Message}");
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
                    _songRequestService.ClearQueue();
                    
                    // Clear current song display
                    CurrentSongTitle.Text = "No song playing";
                    CurrentSongArtist.Text = "Queue cleared";
                    RequestedBy.Text = "";
                    
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
                    Task.Run(() => _songRequestService.PlaySong(song));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlaySong(SongRequest song)
        {
            try
            {
                Task.Run(() => _songRequestService.PlaySong(song));
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error playing song: {ex.Message}";
            }
        }

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is SongRequest song)
                {
                    song.Status = SongRequestStatus.Skipped;
                    SongQueue.Remove(song);
                    _songRequestService.RemoveSongFromQueue(song);
                    StatusText.Text = $"Removed {song.Title} from queue.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing song: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add test Twitch message button for debugging
        private void TestTwitchMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _twitchService.SimulateSongRequest("TestUser", "bohemian rhapsody queen");
                StatusText.Text = "Test song request simulated.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error simulating message: {ex.Message}";
            }
        }

        #endregion

        #region Helper Methods

        private void ConnectTwitchWithToken(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                    return;
                    
                _twitchService.Connect(accessToken);
                
                // Save token
                var settings = _settingsService.LoadSettings();
                settings.TwitchAccessToken = accessToken;
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to connect to Twitch: {ex.Message}");
                Dispatcher.Invoke(() => StatusText.Text = "Twitch connection failed. Check your token in Settings.");
            }
        }

        private async void ConnectSpotifyWithToken(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                    return;
                    
                await _spotifyService.Connect(accessToken);
                
                // Save token
                var settings = _settingsService.LoadSettings();
                settings.SpotifyAccessToken = accessToken;
                _settingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to connect to Spotify: {ex.Message}");
                Dispatcher.Invoke(() => StatusText.Text = "Spotify connection failed. Check your token in Settings.");
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
                Debug.WriteLine($"Auto-connect to OBS failed: {ex.Message}");
            }
        }

        private void CheckAndShowSetupWizard()
        {
            try
            {
                var settings = _settingsService.LoadSettings();

                // Check if first run (version not set or old version)
                // Show wizard if LastUsedVersion is empty or still at default "1.0.0"
                if (string.IsNullOrEmpty(settings.LastUsedVersion) || settings.LastUsedVersion == "1.0.0")
                {
                    // Show setup wizard
                    var wizard = new SetupWizardWindow();
                    wizard.ShowDialog();

                    // Update version to indicate setup wizard has been shown
                    settings.LastUsedVersion = "2.0.0";
                    _settingsService.SaveSettings(settings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for first run: {ex.Message}");
                // Don't show error to user - just skip wizard if there's an issue
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
                // Disconnect services without showing messages
                _twitchService?.Disconnect();
                _spotifyService?.Disconnect();
                
                // Silently close YouTube player if it exists
                try
                {
                    if (_youtubeMusicService?.IsConnected == true)
                    {
                        _youtubeMusicService.Disconnect();
                    }
                }
                catch
                {
                    // Ignore YouTube cleanup errors
                }
                
                // Disconnect OBS with timeout
                try
                {
                    if (_obsService?.IsConnected == true)
                    {
                        var disconnectTask = _obsService.DisconnectAsync();
                        if (!disconnectTask.Wait(1000)) // Reduced timeout
                        {
                            Debug.WriteLine("OBS disconnect timed out");
                        }
                    }
                }
                catch
                {
                    // Ignore OBS cleanup errors
                }
                
                // Dispose services
                try
                {
                    _obsService?.Dispose();
                    _twitchService?.Dispose();
                    _spotifyService?.Dispose();
                    _songRequestService?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during cleanup: {ex.Message}");
                // Don't show error messages during shutdown
            }
        }
    }
}
