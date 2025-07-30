using System;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using Microsoft.Web.WebView2.Core;
using EZStreamer.Models;

namespace EZStreamer.Views
{
    public partial class YouTubePlayerWindow : Window
    {
        private SongRequest _currentSong;
        private bool _isPlaying = false;
        private bool _isInitialized = false;
        private bool _isShuttingDown = false;

        public event EventHandler<SongRequest> SongStarted;
        public event EventHandler<SongRequest> SongEnded;
        public event EventHandler<SongRequest> SongPaused;
        public event EventHandler<SongRequest> SongResumed;

        public YouTubePlayerWindow()
        {
            InitializeComponent();
            LoadingPanel.Visibility = Visibility.Visible;
        }

        private async void YouTubeWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                if (e.IsSuccess)
                {
                    // Set up message handling for JavaScript communication
                    YouTubeWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                    
                    // Load the YouTube player HTML
                    await LoadYouTubePlayer();
                    _isInitialized = true;
                }
                else
                {
                    ShowError("Failed to initialize YouTube player");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing YouTube player: {ex.Message}");
            }
        }

        private void YouTubeWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            if (e.IsSuccess)
            {
                StatusText.Text = "YouTube player ready";
            }
            else
            {
                StatusText.Text = "Failed to load YouTube player";
            }
        }

        private async Task LoadYouTubePlayer()
        {
            var playerHtml = GetYouTubePlayerHtml();
            // FIXED: NavigateToString is synchronous, not async
            YouTubeWebView.CoreWebView2.NavigateToString(playerHtml);
            // Add a small delay to allow navigation to start
            await Task.Delay(100);
        }

        public async Task PlaySong(SongRequest song)
        {
            try
            {
                if (!_isInitialized)
                {
                    await Task.Delay(1000); // Wait for initialization
                }

                _currentSong = song;
                SongInfoText.Text = $"{song.Title} by {song.Artist}";
                
                // Load the video in the YouTube player
                var videoId = ExtractVideoId(song.SourceId);
                // This should now work correctly
                await ExecuteScript($"loadVideo('{videoId}')");
                
                _isPlaying = true;
                UpdatePlayPauseButton();
                
                StatusText.Text = $"Playing: {song.Title}";
                SongStarted?.Invoke(this, song);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to play song: {ex.Message}");
            }
        }

        public async Task PausePlayback()
        {
            try
            {
                await ExecuteScript("pauseVideo()");
                _isPlaying = false;
                UpdatePlayPauseButton();
                StatusText.Text = "Paused";
                
                if (_currentSong != null)
                {
                    SongPaused?.Invoke(this, _currentSong);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to pause: {ex.Message}");
            }
        }

        public async Task ResumePlayback()
        {
            try
            {
                await ExecuteScript("playVideo()");
                _isPlaying = true;
                UpdatePlayPauseButton();
                StatusText.Text = "Playing";
                
                if (_currentSong != null)
                {
                    SongResumed?.Invoke(this, _currentSong);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to resume: {ex.Message}");
            }
        }

        public async Task Stop()
        {
            try
            {
                await ExecuteScript("stopVideo()");
                _isPlaying = false;
                UpdatePlayPauseButton();
                StatusText.Text = "Stopped";
                
                if (_currentSong != null)
                {
                    SongEnded?.Invoke(this, _currentSong);
                    _currentSong = null;
                    SongInfoText.Text = "YouTube Music Player";
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to stop: {ex.Message}");
            }
        }

        // ExecuteScript properly returns Task
        private async Task ExecuteScript(string script)
        {
            if (_isInitialized && YouTubeWebView.CoreWebView2 != null)
            {
                await YouTubeWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private void UpdatePlayPauseButton()
        {
            PlayPauseIcon.Kind = _isPlaying ? MaterialDesignThemes.Wpf.PackIconKind.Pause : MaterialDesignThemes.Wpf.PackIconKind.Play;
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                
                // Handle messages from YouTube player
                if (message.Contains("onStateChange"))
                {
                    if (message.Contains("ended"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isPlaying = false;
                            UpdatePlayPauseButton();
                            StatusText.Text = "Song ended";
                            
                            if (_currentSong != null)
                            {
                                SongEnded?.Invoke(this, _currentSong);
                                _currentSong = null;
                                SongInfoText.Text = "YouTube Music Player";
                            }
                        });
                    }
                    else if (message.Contains("playing"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isPlaying = true;
                            UpdatePlayPauseButton();
                            StatusText.Text = "Playing";
                        });
                    }
                    else if (message.Contains("paused"))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _isPlaying = false;
                            UpdatePlayPauseButton();
                            StatusText.Text = "Paused";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling YouTube message: {ex.Message}");
            }
        }

        private string ExtractVideoId(string sourceId)
        {
            // If it's already a video ID, return it
            if (!sourceId.Contains("/") && !sourceId.Contains("="))
            {
                return sourceId;
            }
            
            // Extract from YouTube URL
            if (sourceId.Contains("youtube.com/watch?v="))
            {
                var start = sourceId.IndexOf("v=") + 2;
                var end = sourceId.IndexOf("&", start);
                return end > start ? sourceId.Substring(start, end - start) : sourceId.Substring(start);
            }
            
            if (sourceId.Contains("youtu.be/"))
            {
                var start = sourceId.LastIndexOf("/") + 1;
                var end = sourceId.IndexOf("?", start);
                return end > start ? sourceId.Substring(start, end - start) : sourceId.Substring(start);
            }
            
            return sourceId;
        }

        private string GetYouTubePlayerHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>YouTube Player</title>
    <style>
        body { margin: 0; padding: 0; background: #000; }
        #player { width: 100%; height: 100vh; }
    </style>
</head>
<body>
    <div id=""player""></div>
    
    <script>
        var tag = document.createElement('script');
        tag.src = 'https://www.youtube.com/iframe_api';
        var firstScriptTag = document.getElementsByTagName('script')[0];
        firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
        
        var player;
        function onYouTubeIframeAPIReady() {
            player = new YT.Player('player', {
                height: '100%',
                width: '100%',
                playerVars: {
                    'playsinline': 1,
                    'controls': 1,
                    'rel': 0,
                    'showinfo': 0,
                    'iv_load_policy': 3
                },
                events: {
                    'onReady': onPlayerReady,
                    'onStateChange': onPlayerStateChange
                }
            });
        }
        
        function onPlayerReady(event) {
            window.chrome.webview?.postMessage('Player ready');
        }
        
        function onPlayerStateChange(event) {
            var state = '';
            if (event.data == YT.PlayerState.ENDED) {
                state = 'ended';
            } else if (event.data == YT.PlayerState.PLAYING) {
                state = 'playing';
            } else if (event.data == YT.PlayerState.PAUSED) {
                state = 'paused';
            } else if (event.data == YT.PlayerState.BUFFERING) {
                state = 'buffering';
            }
            
            window.chrome.webview?.postMessage('onStateChange: ' + state);
        }
        
        function loadVideo(videoId) {
            if (player && player.loadVideoById) {
                player.loadVideoById(videoId);
            }
        }
        
        function playVideo() {
            if (player && player.playVideo) {
                player.playVideo();
            }
        }
        
        function pauseVideo() {
            if (player && player.pauseVideo) {
                player.pauseVideo();
            }
        }
        
        function stopVideo() {
            if (player && player.stopVideo) {
                player.stopVideo();
            }
        }
    </script>
</body>
</html>";
        }

        #region Event Handlers

        private async void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                await PausePlayback();
            }
            else
            {
                await ResumePlayback();
            }
        }

        private async void Skip_Click(object sender, RoutedEventArgs e)
        {
            await Stop();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        #endregion

        private void ShowError(string message)
        {
            StatusText.Text = $"Error: {message}";
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        // Method to force close the window (called during application shutdown)
        public void ForceClose()
        {
            _isShuttingDown = true;
            
            try
            {
                // Clean up WebView2 resources
                if (YouTubeWebView?.CoreWebView2 != null)
                {
                    YouTubeWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }
            }
            catch
            {
                // Ignore cleanup errors during shutdown
            }
            
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Only prevent closing if it's not during application shutdown
            if (!_isShuttingDown && Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                // Don't actually close, just hide (when main app is still running)
                e.Cancel = true;
                Hide();
            }
            else
            {
                // Allow closing during shutdown or if main window is closed
                try
                {
                    // Clean up WebView2 resources properly
                    if (YouTubeWebView != null)
                    {
                        // Dispose the WebView2 control which will clean up the CoreWebView2
                        YouTubeWebView.Dispose();
                    }
                }
                catch
                {
                    // Ignore cleanup errors during shutdown
                }
                
                base.OnClosing(e);
            }
        }
    }
}
