using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EZStreamer.Models;
using System.Net.Http;
using System.Text.Json;

namespace EZStreamer.Services
{
    public class YouTubeMusicService
    {
        private bool _isConnected;
        private SongRequest _currentSong;
        private readonly HttpClient _httpClient;
        private readonly ConfigurationService _configService;
        private string _apiKey;

        public bool IsConnected => _isConnected;
        public SongRequest CurrentSong => _currentSong;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        public event EventHandler<SongRequest> TrackEnded;

        public YouTubeMusicService()
        {
            _httpClient = new HttpClient();
            _configService = new ConfigurationService();
        }

        public void Connect()
        {
            try
            {
                // Load API key from configuration
                var credentials = _configService.GetAPICredentials();
                _apiKey = credentials.YouTubeAPIKey;

                if (string.IsNullOrEmpty(_apiKey))
                {
                    System.Diagnostics.Debug.WriteLine("YouTube API key not configured - using limited functionality");
                }

                _isConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine("YouTube Music service connected");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                throw new Exception($"Failed to connect to YouTube Music: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                _currentSong = null;
                Disconnected?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine("YouTube Music service disconnected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disconnecting YouTube Music: {ex.Message}");
            }
        }

        public async Task<List<SongRequest>> SearchSongs(string query, string requestedBy, int limit = 5)
        {
            try
            {
                if (!_isConnected)
                    return new List<SongRequest>();

                if (string.IsNullOrEmpty(query))
                    return new List<SongRequest>();

                // Check if API key is configured
                if (string.IsNullOrEmpty(_apiKey))
                {
                    System.Diagnostics.Debug.WriteLine("YouTube API key not configured");
                    return new List<SongRequest>();
                }

                // Use YouTube Data API v3 to search for videos
                var encodedQuery = Uri.EscapeDataString(query + " music");
                var apiUrl = $"https://www.googleapis.com/youtube/v3/search" +
                            $"?part=snippet" +
                            $"&q={encodedQuery}" +
                            $"&type=video" +
                            $"&videoCategoryId=10" + // Music category
                            $"&maxResults={limit}" +
                            $"&key={_apiKey}";

                var response = await _httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"YouTube API search failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error details: {errorContent}");
                    return new List<SongRequest>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<YouTubeSearchResponse>(content);

                var songs = new List<SongRequest>();

                if (searchResponse?.items != null)
                {
                    // Get video details to fetch duration
                    var videoIds = string.Join(",", searchResponse.items.Select(item => item.id.videoId));
                    var detailsUrl = $"https://www.googleapis.com/youtube/v3/videos" +
                                    $"?part=contentDetails,snippet" +
                                    $"&id={videoIds}" +
                                    $"&key={_apiKey}";

                    var detailsResponse = await _httpClient.GetAsync(detailsUrl);
                    if (detailsResponse.IsSuccessStatusCode)
                    {
                        var detailsContent = await detailsResponse.Content.ReadAsStringAsync();
                        var videoDetails = JsonSerializer.Deserialize<YouTubeVideoDetailsResponse>(detailsContent);

                        foreach (var video in videoDetails?.items ?? new List<YouTubeVideoDetail>())
                        {
                            // Parse ISO 8601 duration (e.g., PT4M13S = 4 minutes 13 seconds)
                            var duration = ParseYouTubeDuration(video.contentDetails?.duration ?? "PT0S");

                            songs.Add(new SongRequest
                            {
                                Title = video.snippet?.title ?? "Unknown Title",
                                Artist = video.snippet?.channelTitle ?? "Unknown Artist",
                                RequestedBy = requestedBy,
                                SourcePlatform = "YouTube",
                                SourceId = video.id,
                                Duration = duration,
                                Status = SongRequestStatus.Queued,
                                AlbumArt = video.snippet?.thumbnails?.medium?.url ??
                                           video.snippet?.thumbnails?.default_thumb?.url ?? ""
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"YouTube found {songs.Count} songs for query: {query}");
                return songs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching YouTube songs: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<SongRequest>();
            }
        }

        public async Task<bool> PlaySong(SongRequest song)
        {
            try
            {
                if (!_isConnected || song == null)
                    return false;

                // Simulate playback start delay
                await Task.Delay(300);
                
                _currentSong = song;
                song.Status = SongRequestStatus.Playing;
                TrackStarted?.Invoke(this, song);
                
                System.Diagnostics.Debug.WriteLine($"YouTube playing: {song.Title} by {song.Artist}");

                // Use actual song duration
                var playDuration = song.Duration > TimeSpan.Zero ?
                                  song.Duration :
                                  TimeSpan.FromMinutes(3); // Default to 3 minutes if duration unknown
                
                // Start a task to mark the song as ended after duration - FIXED: Added await
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(playDuration);
                        if (_currentSong != null && _currentSong.Id == song.Id)
                        {
                            song.Status = SongRequestStatus.Completed;
                            TrackEnded?.Invoke(this, song);
                            _currentSong = null;
                            System.Diagnostics.Debug.WriteLine($"YouTube finished playing: {song.Title}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in YouTube song completion: {ex.Message}");
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
                if (!_isConnected)
                    return false;

                // Simulate API call delay
                await Task.Delay(200);
                
                if (_currentSong != null)
                {
                    _currentSong.Status = SongRequestStatus.Skipped;
                    TrackEnded?.Invoke(this, _currentSong);
                    _currentSong = null;
                }
                
                System.Diagnostics.Debug.WriteLine("YouTube skipped to next track");
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
                if (!_isConnected)
                    return false;

                // Simulate API call delay
                await Task.Delay(200);
                
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
                if (!_isConnected)
                    return false;

                // Simulate API call delay
                await Task.Delay(200);
                
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
            try
            {
                if (!_isConnected)
                    return null;

                // Simulate API call delay
                await Task.Delay(200);
                
                return _currentSong;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting YouTube current song info: {ex.Message}");
                return null;
            }
        }

        // Method to trigger TrackEnded event when needed by SongRequestService
        public void TriggerTrackEnded(SongRequest song)
        {
            try
            {
                if (song != null)
                {
                    song.Status = SongRequestStatus.Completed;
                    TrackEnded?.Invoke(this, song);
                    if (_currentSong != null && _currentSong.Id == song.Id)
                    {
                        _currentSong = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering YouTube track ended: {ex.Message}");
            }
        }

        #region Helper Methods

        private TimeSpan ParseYouTubeDuration(string isoDuration)
        {
            try
            {
                // Parse ISO 8601 duration format (e.g., PT4M13S, PT1H2M30S)
                // PT = Period of Time
                // H = Hours, M = Minutes, S = Seconds

                if (string.IsNullOrEmpty(isoDuration) || !isoDuration.StartsWith("PT"))
                    return TimeSpan.Zero;

                var duration = isoDuration.Substring(2); // Remove "PT"
                int hours = 0, minutes = 0, seconds = 0;

                // Parse hours
                var hIndex = duration.IndexOf('H');
                if (hIndex > 0)
                {
                    hours = int.Parse(duration.Substring(0, hIndex));
                    duration = duration.Substring(hIndex + 1);
                }

                // Parse minutes
                var mIndex = duration.IndexOf('M');
                if (mIndex > 0)
                {
                    minutes = int.Parse(duration.Substring(0, mIndex));
                    duration = duration.Substring(mIndex + 1);
                }

                // Parse seconds
                var sIndex = duration.IndexOf('S');
                if (sIndex > 0)
                {
                    seconds = int.Parse(duration.Substring(0, sIndex));
                }

                return new TimeSpan(hours, minutes, seconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing YouTube duration '{isoDuration}': {ex.Message}");
                return TimeSpan.Zero;
            }
        }

        // Validate API key by making a test request
        public async Task<bool> ValidateAPIKeyAsync(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return false;

                // Make a simple test request to validate the API key
                var testUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&q=test&maxResults=1&key={apiKey}";
                var response = await _httpClient.GetAsync(testUrl);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YouTube API key validation error: {ex.Message}");
                return false;
            }
        }

        public bool ValidateAPIKey(string apiKey)
        {
            try
            {
                var result = false;
                Task.Run(async () =>
                {
                    result = await ValidateAPIKeyAsync(apiKey);
                }).Wait(5000); // 5 second timeout
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating YouTube API key: {ex.Message}");
                return false;
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                Disconnect();
                _httpClient?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing YouTubeMusicService: {ex.Message}");
            }
        }
    }

    // YouTube Data API v3 Response Models
    public class YouTubeSearchResponse
    {
        public List<YouTubeSearchItem> items { get; set; }
    }

    public class YouTubeSearchItem
    {
        public YouTubeVideoId id { get; set; }
        public YouTubeSnippet snippet { get; set; }
    }

    public class YouTubeVideoId
    {
        public string videoId { get; set; }
    }

    public class YouTubeVideoDetailsResponse
    {
        public List<YouTubeVideoDetail> items { get; set; }
    }

    public class YouTubeVideoDetail
    {
        public string id { get; set; }
        public YouTubeSnippet snippet { get; set; }
        public YouTubeContentDetails contentDetails { get; set; }
    }

    public class YouTubeSnippet
    {
        public string title { get; set; }
        public string channelTitle { get; set; }
        public YouTubeThumbnails thumbnails { get; set; }
    }

    public class YouTubeContentDetails
    {
        public string duration { get; set; }
    }

    public class YouTubeThumbnails
    {
        public YouTubeThumbnail default_thumb { get; set; }
        public YouTubeThumbnail medium { get; set; }
        public YouTubeThumbnail high { get; set; }
    }

    public class YouTubeThumbnail
    {
        public string url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
}
