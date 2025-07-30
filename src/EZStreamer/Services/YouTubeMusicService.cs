using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using EZStreamer.Models;
using EZStreamer.Views;

namespace EZStreamer.Services
{
    public class YouTubeMusicService
    {
        private bool _isConnected;
        private SongRequest _currentSong;
        private YouTubePlayerWindow _playerWindow;

        public bool IsConnected => _isConnected;
        public SongRequest CurrentSong => _currentSong;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        public event EventHandler<SongRequest> TrackEnded;

        public YouTubeMusicService()
        {
            
        }

        public void Connect()
        {
            try
            {
                // Show immediate feedback to user
                var result = MessageBox.Show(
                    "YouTube Music Player will be initialized.\n\n" +
                    "This service allows you to play YouTube videos for song requests. " +
                    "A player window will open when songs are requested.\n\n" +
                    "Would you like to enable YouTube Music?",
                    "YouTube Music Service",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // Initialize the YouTube player window (but don't show it yet)
                if (_playerWindow == null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _playerWindow = new YouTubePlayerWindow();
                        _playerWindow.SongStarted += OnPlayerSongStarted;
                        _playerWindow.SongEnded += OnPlayerSongEnded;
                        _playerWindow.SongPaused += OnPlayerSongPaused;
                        _playerWindow.SongResumed += OnPlayerSongResumed;
                        
                        // Hide the window initially - it will show when a song is played
                        _playerWindow.Hide();
                    });
                }

                _isConnected = true;
                
                // Show success message
                MessageBox.Show(
                    "YouTube Music service enabled successfully!\n\n" +
                    "The player window will appear when you play YouTube songs.",
                    "YouTube Music Connected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize YouTube Music service:\n\n{ex.Message}",
                    "YouTube Music Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                    
                throw new Exception($"Failed to connect to YouTube Music: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _currentSong = null;
            
            if (_playerWindow != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _playerWindow.Close();
                    _playerWindow = null;
                });
            }
            
            MessageBox.Show(
                "YouTube Music service disconnected.",
                "YouTube Music Disconnected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        // Fixed CS1998: Added proper async implementation with Task.Run
        public async Task<List<SongRequest>> SearchSongs(string query, string requestedBy, int limit = 5)
        {
            try
            {
                if (!_isConnected)
                    return new List<SongRequest>();

                // Simulate async search operation
                return await Task.Run(() =>
                {
                    // For MVP, we'll simulate YouTube search results
                    // In production, this would use YouTube Data API
                    var songs = new List<SongRequest>();
                    
                    // Generate realistic YouTube video IDs for search results
                    var searchTerms = query.Split(' ');
                    var baseVideoIds = new[]
                    {
                        "dQw4w9WgXcQ", // Never Gonna Give You Up (for demo)
                        "kJQP7kiw5Fk", // Despacito
                        "9bZkp7q19f0", // Gangnam Style
                        "OPf0YbXqDm0", // Uptown Funk
                        "CevxZvSJLk8"  // Katy Perry - Roar
                    };

                    for (int i = 0; i < Math.Min(limit, 3); i++)
                    {
                        var songRequest = new SongRequest
                        {
                            Title = ExtractLikelyTitle(query) + (i > 0 ? $" (Version {i + 1})" : ""),
                            Artist = ExtractLikelyArtist(query),
                            RequestedBy = requestedBy,
                            SourcePlatform = "YouTube",
                            SourceId = baseVideoIds[i % baseVideoIds.Length], // Use demo video IDs
                            Duration = TimeSpan.FromMinutes(3 + i), // Estimated duration
                            AlbumArt = $"https://img.youtube.com/vi/{baseVideoIds[i % baseVideoIds.Length]}/hqdefault.jpg"
                        };
                        songs.Add(songRequest);
                    }

                    return songs;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching YouTube songs: {ex.Message}");
                return new List<SongRequest>();
            }
        }

        public async Task<bool> PlaySong(SongRequest song)
        {
            try
            {
                if (!_isConnected || _playerWindow == null)
                    return false;

                _currentSong = song;
                song.Status = SongRequestStatus.Playing;

                // Show and play in the YouTube player window
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    _playerWindow.Show();
                    _playerWindow.Activate();
                    await _playerWindow.PlaySong(song);
                });
                
                TrackStarted?.Invoke(this, song);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing YouTube song: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SkipToNext()
        {
            try
            {
                if (_playerWindow != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await _playerWindow.Stop();
                    });
                }
                
                if (_currentSong != null)
                {
                    _currentSong.Status = SongRequestStatus.Skipped;
                    TrackEnded?.Invoke(this, _currentSong);
                    _currentSong = null;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error skipping YouTube song: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PausePlayback()
        {
            try
            {
                if (_playerWindow != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await _playerWindow.PausePlayback();
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pausing YouTube playback: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResumePlayback()
        {
            try
            {
                if (_playerWindow != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await _playerWindow.ResumePlayback();
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resuming YouTube playback: {ex.Message}");
                return false;
            }
        }

        // Fixed CS1998: Made this method truly async with Task.FromResult
        public async Task<SongRequest> GetCurrentSongInfo()
        {
            return await Task.FromResult(_currentSong);
        }

        public void ShowPlayer()
        {
            if (_playerWindow != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _playerWindow.Show();
                    _playerWindow.Activate();
                });
            }
        }

        public void HidePlayer()
        {
            if (_playerWindow != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _playerWindow.Hide();
                });
            }
        }

        #region Event Handlers

        private void OnPlayerSongStarted(object sender, SongRequest song)
        {
            _currentSong = song;
            TrackStarted?.Invoke(this, song);
        }

        private void OnPlayerSongEnded(object sender, SongRequest song)
        {
            if (_currentSong != null && _currentSong.Id == song.Id)
            {
                _currentSong.Status = SongRequestStatus.Completed;
                TrackEnded?.Invoke(this, _currentSong);
                _currentSong = null;
            }
        }

        private void OnPlayerSongPaused(object sender, SongRequest song)
        {
            // Handle pause event if needed
        }

        private void OnPlayerSongResumed(object sender, SongRequest song)
        {
            // Handle resume event if needed
        }

        #endregion

        #region Helper Methods

        private string ExtractLikelyTitle(string query)
        {
            // Simple heuristic to extract song title from search query
            var parts = query.Split(new[] { " by ", " - ", " | " }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : query;
        }

        private string ExtractLikelyArtist(string query)
        {
            // Simple heuristic to extract artist from search query
            var parts = query.Split(new[] { " by ", " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                return parts[1].Trim();
            }
            
            // Try to extract from common patterns
            var artistMatch = Regex.Match(query, @"(.+?)\s+[-–—]\s+(.+)", RegexOptions.IgnoreCase);
            if (artistMatch.Success)
            {
                return artistMatch.Groups[1].Value.Trim();
            }
            
            return "Unknown Artist";
        }

        private string GetYouTubeWatchUrl(string videoId)
        {
            return $"https://www.youtube.com/watch?v={videoId}";
        }

        private string GetYouTubeEmbedUrl(string videoId)
        {
            return $"https://www.youtube.com/embed/{videoId}?autoplay=1&controls=1";
        }

        #endregion

        #region YouTube Data API Integration (Future Enhancement)
        
        // TODO: Implement real YouTube Data API search
        // This would require YouTube API key and proper search implementation
        // Fixed CS1998: Made this method properly async
        public async Task<List<SongRequest>> SearchYouTubeAPI(string query, string requestedBy, int limit = 5)
        {
            // Placeholder for YouTube Data API integration
            // Would use Google.Apis.YouTube.v3 NuGet package
            /*
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "YOUR_API_KEY",
                ApplicationName = "EZStreamer"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = query;
            searchListRequest.MaxResults = limit;
            searchListRequest.Type = "video";
            searchListRequest.VideoCategoryId = "10"; // Music category

            var searchListResponse = await searchListRequest.ExecuteAsync();
            
            return searchListResponse.Items.Select(item => new SongRequest
            {
                Title = item.Snippet.Title,
                Artist = item.Snippet.ChannelTitle,
                RequestedBy = requestedBy,
                SourcePlatform = "YouTube",
                SourceId = item.Id.VideoId,
                AlbumArt = item.Snippet.Thumbnails.High?.Url ?? ""
            }).ToList();
            */
            
            return await SearchSongs(query, requestedBy, limit);
        }

        #endregion
    }
}
