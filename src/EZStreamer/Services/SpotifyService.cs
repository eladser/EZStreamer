using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EZStreamer.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace EZStreamer.Services
{
    public class SpotifyService
    {
        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry;
        private bool _isConnected;
        private readonly HttpClient _httpClient;
        private string _activeDeviceId;
        private readonly SettingsService _settingsService;
        private readonly ConfigurationService _configService;

        public bool IsConnected => _isConnected;
        public string ActiveDeviceName => _isConnected ? "EZStreamer Player" : "No device selected";

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        public event EventHandler<SongRequest> TrackEnded;

        public SpotifyService()
        {
            _isConnected = false;
            _httpClient = new HttpClient();
            _settingsService = new SettingsService();
            _configService = new ConfigurationService();
            _tokenExpiry = DateTime.MinValue;

            // Try to load existing tokens from settings
            LoadTokensFromSettings();
        }

        public async Task Connect(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new ArgumentException("Access token is required");
                }

                _accessToken = accessToken;
                
                // Validate token by trying to get user profile
                if (!await ValidateTokenAsync(accessToken))
                {
                    throw new Exception("Invalid or expired Spotify access token");
                }
                
                // Get available devices
                await GetActiveDevice();
                
                _isConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
                
                System.Diagnostics.Debug.WriteLine("Connected to Spotify");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _accessToken = null;
                throw new Exception($"Failed to connect to Spotify: {ex.Message}", ex);
            }
        }

        private async Task GetActiveDevice()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player/devices");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var devicesResponse = JsonSerializer.Deserialize<SpotifyDevicesResponse>(content);
                    
                    var activeDevice = devicesResponse?.devices?.FirstOrDefault(d => d.is_active);
                    if (activeDevice != null)
                    {
                        _activeDeviceId = activeDevice.id;
                    }
                    else
                    {
                        // Use first available device
                        var firstDevice = devicesResponse?.devices?.FirstOrDefault();
                        if (firstDevice != null)
                        {
                            _activeDeviceId = firstDevice.id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Spotify devices: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                _accessToken = null;
                _activeDeviceId = null;
                
                Disconnected?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine("Disconnected from Spotify");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disconnecting from Spotify: {ex.Message}");
            }
        }

        public async Task<List<SongRequest>> SearchSongs(string query, string requestedBy, int limit = 5)
        {
            try
            {
                if (!_isConnected)
                {
                    return new List<SongRequest>();
                }

                if (string.IsNullOrEmpty(query))
                {
                    return new List<SongRequest>();
                }

                // Ensure token is valid before making API request
                await EnsureValidToken();

                var encodedQuery = Uri.EscapeDataString(query);
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://api.spotify.com/v1/search?q={encodedQuery}&type=track&limit={limit}");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Spotify search failed: {response.StatusCode}");
                    return new List<SongRequest>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(content);

                var songs = new List<SongRequest>();
                if (searchResponse?.tracks?.items != null)
                {
                    foreach (var track in searchResponse.tracks.items)
                    {
                        songs.Add(new SongRequest
                        {
                            Title = track.name,
                            Artist = string.Join(", ", track.artists.Select(a => a.name)),
                            RequestedBy = requestedBy,
                            SourcePlatform = "Spotify",
                            SourceId = track.id,
                            Duration = TimeSpan.FromMilliseconds(track.duration_ms),
                            Status = SongRequestStatus.Queued,
                            AlbumArt = track.album?.images?.FirstOrDefault()?.url ?? ""
                        });
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Found {songs.Count} songs for query: {query}");
                return songs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching songs: {ex.Message}");
                return new List<SongRequest>();
            }
        }

        public async Task<bool> PlaySong(SongRequest song)
        {
            try
            {
                if (!_isConnected || song == null)
                {
                    return false;
                }

                // Ensure token is valid before making API request
                await EnsureValidToken();

                var playData = new
                {
                    uris = new[] { $"spotify:track:{song.SourceId}" }
                };

                var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                
                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    request.RequestUri = new Uri($"https://api.spotify.com/v1/me/player/play?device_id={_activeDeviceId}");
                }

                request.Content = new StringContent(JsonSerializer.Serialize(playData), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    song.Status = SongRequestStatus.Playing;
                    TrackStarted?.Invoke(this, song);
                    
                    System.Diagnostics.Debug.WriteLine($"Started playing: {song.Title} by {song.Artist}");
                    
                    // Start a task to mark the song as ended after duration
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var duration = song.Duration > TimeSpan.Zero ? song.Duration : TimeSpan.FromSeconds(30);
                            await Task.Delay(duration);
                            song.Status = SongRequestStatus.Completed;
                            TrackEnded?.Invoke(this, song);
                            System.Diagnostics.Debug.WriteLine($"Finished playing: {song.Title}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error in song completion tracking: {ex.Message}");
                        }
                    });
                    
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to play song: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing song: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddToQueue(SongRequest song)
        {
            try
            {
                if (!_isConnected || song == null)
                {
                    return false;
                }

                var uri = $"spotify:track:{song.SourceId}";
                var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(uri)}");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    request.RequestUri = new Uri($"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(uri)}&device_id={_activeDeviceId}");
                }

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    System.Diagnostics.Debug.WriteLine($"Added to Spotify queue: {song.Title} by {song.Artist}");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to add to queue: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding to queue: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SkipToNext()
        {
            try
            {
                if (!_isConnected)
                {
                    return false;
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.spotify.com/v1/me/player/next");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    request.RequestUri = new Uri($"https://api.spotify.com/v1/me/player/next?device_id={_activeDeviceId}");
                }

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    System.Diagnostics.Debug.WriteLine("Skipped to next track");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to skip: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error skipping song: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PausePlayback()
        {
            try
            {
                if (!_isConnected)
                {
                    return false;
                }

                var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/pause");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    request.RequestUri = new Uri($"https://api.spotify.com/v1/me/player/pause?device_id={_activeDeviceId}");
                }

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    System.Diagnostics.Debug.WriteLine("Playback paused");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to pause: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pausing playback: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ResumePlayback()
        {
            try
            {
                if (!_isConnected)
                {
                    return false;
                }

                var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    request.RequestUri = new Uri($"https://api.spotify.com/v1/me/player/play?device_id={_activeDeviceId}");
                }

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    System.Diagnostics.Debug.WriteLine("Playback resumed");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to resume: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resuming playback: {ex.Message}");
                return false;
            }
        }

        public async Task<SongRequest> GetCurrentSongInfo()
        {
            try
            {
                if (!_isConnected)
                {
                    return null;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player/currently-playing");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(content))
                    {
                        return null; // Nothing playing
                    }

                    var currentlyPlaying = JsonSerializer.Deserialize<SpotifyCurrentlyPlayingResponse>(content);
                    
                    if (currentlyPlaying?.item != null)
                    {
                        return new SongRequest
                        {
                            Title = currentlyPlaying.item.name,
                            Artist = string.Join(", ", currentlyPlaying.item.artists.Select(a => a.name)),
                            SourcePlatform = "Spotify",
                            SourceId = currentlyPlaying.item.id,
                            Duration = TimeSpan.FromMilliseconds(currentlyPlaying.item.duration_ms),
                            Status = currentlyPlaying.is_playing ? SongRequestStatus.Playing : SongRequestStatus.Queued,
                            AlbumArt = currentlyPlaying.item.album?.images?.FirstOrDefault()?.url ?? ""
                        };
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current song info: {ex.Message}");
                return null;
            }
        }

        // Validate access token by trying to get user profile
        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    return false;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token validation error: {ex.Message}");
                return false;
            }
        }

        public bool ValidateToken(string accessToken)
        {
            try
            {
                var result = false;
                Task.Run(async () =>
                {
                    result = await ValidateTokenAsync(accessToken);
                }).Wait(5000); // 5 second timeout
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating token: {ex.Message}");
                return false;
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering track ended: {ex.Message}");
            }
        }

        private void LoadTokensFromSettings()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                if (!string.IsNullOrEmpty(settings.SpotifyAccessToken))
                {
                    _accessToken = settings.SpotifyAccessToken;
                    _refreshToken = settings.SpotifyRefreshToken;
                    _tokenExpiry = settings.SpotifyTokenExpiry;
                    System.Diagnostics.Debug.WriteLine("Loaded Spotify tokens from settings");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Spotify tokens: {ex.Message}");
            }
        }

        private async Task<bool> RefreshAccessToken()
        {
            try
            {
                if (string.IsNullOrEmpty(_refreshToken))
                {
                    System.Diagnostics.Debug.WriteLine("No refresh token available");
                    return false;
                }

                var credentials = _configService.GetAPICredentials();
                if (string.IsNullOrEmpty(credentials.SpotifyClientId) || string.IsNullOrEmpty(credentials.SpotifyClientSecret))
                {
                    System.Diagnostics.Debug.WriteLine("Spotify credentials not configured");
                    return false;
                }

                var requestData = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken,
                    ["client_id"] = credentials.SpotifyClientId,
                    ["client_secret"] = credentials.SpotifyClientSecret
                };

                var requestContent = new FormUrlEncodedContent(requestData);
                var response = await _httpClient.PostAsync("https://accounts.spotify.com/api/token", requestContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent);

                    if (!string.IsNullOrEmpty(tokenResponse?.access_token))
                    {
                        _accessToken = tokenResponse.access_token;
                        _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.expires_in);

                        // Update refresh token if a new one was provided
                        if (!string.IsNullOrEmpty(tokenResponse.refresh_token))
                        {
                            _refreshToken = tokenResponse.refresh_token;
                        }

                        // Save updated tokens to settings
                        var settings = _settingsService.LoadSettings();
                        settings.SpotifyAccessToken = _accessToken;
                        settings.SpotifyRefreshToken = _refreshToken;
                        settings.SpotifyTokenExpiry = _tokenExpiry;
                        _settingsService.SaveSettings(settings);

                        System.Diagnostics.Debug.WriteLine("Spotify access token refreshed successfully");
                        return true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Token refresh failed: {response.StatusCode}");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing Spotify token: {ex.Message}");
                return false;
            }
        }

        private async Task EnsureValidToken()
        {
            try
            {
                // Check if token is expired or about to expire (within 5 minutes)
                if (_tokenExpiry != DateTime.MinValue && DateTime.Now >= _tokenExpiry.AddMinutes(-5))
                {
                    System.Diagnostics.Debug.WriteLine("Spotify token expired or expiring soon, refreshing...");
                    await RefreshAccessToken();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring valid token: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                Disconnect();
                _httpClient?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing SpotifyService: {ex.Message}");
            }
        }
    }

    // Token response model
    public class SpotifyTokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
    }

    // Models for Spotify API responses
    public class SpotifySearchResponse
    {
        public SpotifyTracksResponse tracks { get; set; }
    }

    public class SpotifyTracksResponse
    {
        public SpotifyTrack[] items { get; set; }
    }

    public class SpotifyTrack
    {
        public string id { get; set; }
        public string name { get; set; }
        public SpotifyArtist[] artists { get; set; }
        public SpotifyAlbum album { get; set; }
        public int duration_ms { get; set; }
    }

    public class SpotifyArtist
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class SpotifyAlbum
    {
        public string id { get; set; }
        public string name { get; set; }
        public SpotifyImage[] images { get; set; }
    }

    public class SpotifyImage
    {
        public string url { get; set; }
        public int height { get; set; }
        public int width { get; set; }
    }

    public class SpotifyDevicesResponse
    {
        public SpotifyDevice[] devices { get; set; }
    }

    public class SpotifyDevice
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool is_active { get; set; }
        public bool is_private_session { get; set; }
        public bool is_restricted { get; set; }
        public int volume_percent { get; set; }
    }

    public class SpotifyCurrentlyPlayingResponse
    {
        public SpotifyTrack item { get; set; }
        public bool is_playing { get; set; }
        public int progress_ms { get; set; }
        public string currently_playing_type { get; set; }
    }
}
