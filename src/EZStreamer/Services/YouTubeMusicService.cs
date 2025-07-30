using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class YouTubeMusicService
    {
        private bool _isConnected;
        private SongRequest _currentSong;

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
                _isConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to YouTube Music: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _currentSong = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<SongRequest>> SearchSongs(string query, string requestedBy, int limit = 5)
        {
            try
            {
                if (!_isConnected)
                    return new List<SongRequest>();

                // For MVP, we'll create a simple YouTube search URL approach
                // In a production app, you'd integrate with YouTube Data API
                var songs = new List<SongRequest>();
                
                // Simulate search results with YouTube URLs
                // This is a simplified approach - real implementation would use YouTube Data API
                var searchQuery = Uri.EscapeDataString(query);
                var youtubeSearchUrl = $"https://www.youtube.com/results?search_query={searchQuery}";
                
                // For MVP demonstration, create sample results
                // In real implementation, parse YouTube search results or use API
                for (int i = 0; i < Math.Min(limit, 3); i++)
                {
                    var songRequest = new SongRequest
                    {
                        Title = ExtractLikelyTitle(query),
                        Artist = ExtractLikelyArtist(query),
                        RequestedBy = requestedBy,
                        SourcePlatform = "YouTube",
                        SourceId = GenerateYouTubeVideoId(query, i), // Simplified for MVP
                        Duration = TimeSpan.FromMinutes(3 + i), // Estimated duration
                        AlbumArt = "" // YouTube thumbnails would be extracted in real implementation
                    };
                    songs.Add(songRequest);
                }

                return songs;
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
                if (!_isConnected)
                    return false;

                _currentSong = song;
                song.Status = SongRequestStatus.Playing;

                // In a real implementation, this would:
                // 1. Open YouTube video in embedded WebView2
                // 2. Control playback via JavaScript API
                // 3. Handle autoplay and playlists
                
                // For MVP demonstration
                var youtubeUrl = GetYouTubeWatchUrl(song.SourceId);
                System.Diagnostics.Debug.WriteLine($"Would play YouTube video: {youtubeUrl}");
                
                TrackStarted?.Invoke(this, song);
                
                // Simulate playback completion after duration
                _ = Task.Delay(song.Duration).ContinueWith(t =>
                {
                    if (_currentSong?.Id == song.Id)
                    {
                        song.Status = SongRequestStatus.Completed;
                        TrackEnded?.Invoke(this, song);
                        _currentSong = null;
                    }
                });
                
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
                if (_currentSong != null)
                {
                    _currentSong.Status = SongRequestStatus.Skipped;
                    TrackEnded?.Invoke(this, _currentSong);
                    _currentSong = null;
                    return true;
                }
                return false;
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
                // Would send pause command to embedded YouTube player
                System.Diagnostics.Debug.WriteLine("YouTube playback paused");
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
                // Would send resume command to embedded YouTube player
                System.Diagnostics.Debug.WriteLine("YouTube playback resumed");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resuming YouTube playback: {ex.Message}");
                return false;
            }
        }

        public async Task<SongRequest> GetCurrentSongInfo()
        {
            return _currentSong;
        }

        #region Helper Methods

        private string ExtractLikelyTitle(string query)
        {
            // Simple heuristic to extract song title from search query
            // In real implementation, this would come from YouTube API response
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

        private string GenerateYouTubeVideoId(string query, int index)
        {
            // For MVP demonstration - generates fake video IDs
            // Real implementation would get actual video IDs from YouTube API
            var hash = Math.Abs(query.GetHashCode() + index);
            return $"demo{hash % 100000:D5}";
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

        #region WebView2 Integration (for future implementation)
        
        public string GetEmbeddedPlayerHtml(string videoId)
        {
            // This would be used with WebView2 to create an embedded YouTube player
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>YouTube Music Player</title>
    <style>
        body {{ margin: 0; padding: 0; background: black; }}
        iframe {{ width: 100%; height: 100vh; border: none; }}
    </style>
</head>
<body>
    <iframe 
        src=""{GetYouTubeEmbedUrl(videoId)}"" 
        allow=""accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"" 
        allowfullscreen>
    </iframe>
    
    <script>
        // JavaScript API integration for controlling playback
        // Would communicate with WPF application via WebView2 bridge
        window.chrome.webview?.postMessage({{ 
            type: 'playerReady', 
            videoId: '{videoId}' 
        }});
    </script>
</body>
</html>";
        }

        #endregion
    }
}
