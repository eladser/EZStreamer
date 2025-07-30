using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class SpotifyService
    {
        private readonly HttpClient _httpClient;
        private string _accessToken;
        private SpotifyDevice _activeDevice;

        public bool IsConnected => !string.IsNullOrEmpty(_accessToken);
        public string ActiveDeviceName => _activeDevice?.Name ?? "No device selected";

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        // Fixed CS0067: Removed unused event
        // public event EventHandler<SongRequest> TrackEnded;

        public SpotifyService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
        }

        public async Task Connect(string accessToken)
        {
            try
            {
                _accessToken = accessToken;
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                // Test the connection by getting user profile
                var response = await _httpClient.GetAsync("me");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to authenticate with Spotify: {response.StatusCode}");
                }

                // Get available devices
                await RefreshDevices();

                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _accessToken = null;
                _httpClient.DefaultRequestHeaders.Clear();
                throw new Exception($"Failed to connect to Spotify: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            _accessToken = null;
            _httpClient.DefaultRequestHeaders.Clear();
            _activeDevice = null;
            
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<SpotifyDevice>> GetAvailableDevices()
        {
            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                    return new List<SpotifyDevice>();

                var response = await _httpClient.GetAsync("me/player/devices");
                if (!response.IsSuccessStatusCode)
                    return new List<SpotifyDevice>();

                var content = await response.Content.ReadAsStringAsync();
                var devicesResponse = JsonSerializer.Deserialize<SpotifyDevicesResponse>(content);
                
                return devicesResponse?.Devices ?? new List<SpotifyDevice>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting devices: {ex.Message}");
                return new List<SpotifyDevice>();
            }
        }

        public async Task RefreshDevices()
        {
            var devices = await GetAvailableDevices();
            
            // Try to find an active device
            _activeDevice = devices.FirstOrDefault(d => d.IsActive) ?? devices.FirstOrDefault();
        }

        public async Task<List<SongRequest>> SearchSongs(string query, string requestedBy, int limit = 5)
        {
            try
            {
                if (string.IsNullOrEmpty(_accessToken))
                    return new List<SongRequest>();

                var encodedQuery = Uri.EscapeDataString(query);
                var response = await _httpClient.GetAsync($"search?q={encodedQuery}&type=track&limit={limit}");
                
                if (!response.IsSuccessStatusCode)
                    return new List<SongRequest>();

                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<SpotifySearchResponse>(content);
                var songs = new List<SongRequest>();

                if (searchResponse?.Tracks?.Items != null)
                {
                    foreach (var track in searchResponse.Tracks.Items)
                    {
                        var songRequest = new SongRequest
                        {
                            Title = track.Name,
                            Artist = string.Join(", ", track.Artists?.Select(a => a.Name) ?? new[] { "Unknown Artist" }),
                            RequestedBy = requestedBy,
                            SourcePlatform = "Spotify",
                            SourceId = track.Id,
                            Duration = TimeSpan.FromMilliseconds(track.DurationMs),
                            AlbumArt = track.Album?.Images?.FirstOrDefault()?.Url ?? ""
                        };
                        songs.Add(songRequest);
                    }
                }

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
                if (string.IsNullOrEmpty(_accessToken) || _activeDevice == null)
                    return false;

                var playData = new
                {
                    uris = new[] { $"spotify:track:{song.SourceId}" },
                    device_id = _activeDevice.Id
                };

                var json = JsonSerializer.Serialize(playData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PutAsync("me/player/play", content);
                
                if (response.IsSuccessStatusCode)
                {
                    song.Status = SongRequestStatus.Playing;
                    TrackStarted?.Invoke(this, song);
                    return true;
                }

                return false;
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
                if (string.IsNullOrEmpty(_accessToken) || _activeDevice == null)
                    return false;

                var uri = Uri.EscapeDataString($"spotify:track:{song.SourceId}");
                var response = await _httpClient.PostAsync($"me/player/queue?uri={uri}&device_id={_activeDevice.Id}", null);
                
                return response.IsSuccessStatusCode;
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
                if (string.IsNullOrEmpty(_accessToken) || _activeDevice == null)
                    return false;

                var response = await _httpClient.PostAsync($"me/player/next?device_id={_activeDevice.Id}", null);
                return response.IsSuccessStatusCode;
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
                if (string.IsNullOrEmpty(_accessToken) || _activeDevice == null)
                    return false;

                var response = await _httpClient.PutAsync($"me/player/pause?device_id={_activeDevice.Id}", null);
                return response.IsSuccessStatusCode;
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
                if (string.IsNullOrEmpty(_accessToken) || _activeDevice == null)
                    return false;

                var response = await _httpClient.PutAsync($"me/player/play?device_id={_activeDevice.Id}", null);
                return response.IsSuccessStatusCode;
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
                if (string.IsNullOrEmpty(_accessToken))
                    return null;

                var response = await _httpClient.GetAsync("me/player/currently-playing");
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                var currentlyPlaying = JsonSerializer.Deserialize<SpotifyCurrentlyPlaying>(content);

                if (currentlyPlaying?.Item != null)
                {
                    return new SongRequest
                    {
                        Title = currentlyPlaying.Item.Name,
                        Artist = string.Join(", ", currentlyPlaying.Item.Artists?.Select(a => a.Name) ?? new[] { "Unknown Artist" }),
                        SourcePlatform = "Spotify",
                        SourceId = currentlyPlaying.Item.Id,
                        Duration = TimeSpan.FromMilliseconds(currentlyPlaying.Item.DurationMs),
                        AlbumArt = currentlyPlaying.Item.Album?.Images?.FirstOrDefault()?.Url ?? "",
                        Status = currentlyPlaying.IsPlaying ? SongRequestStatus.Playing : SongRequestStatus.Queued
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current song info: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // Simple Spotify API response models
    public class SpotifyDevice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public class SpotifyDevicesResponse
    {
        public List<SpotifyDevice> Devices { get; set; }
    }

    public class SpotifySearchResponse
    {
        public SpotifyTracksResponse Tracks { get; set; }
    }

    public class SpotifyTracksResponse
    {
        public List<SpotifyTrack> Items { get; set; }
    }

    public class SpotifyTrack
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int DurationMs { get; set; }
        public List<SpotifyArtist> Artists { get; set; }
        public SpotifyAlbum Album { get; set; }
    }

    public class SpotifyArtist
    {
        public string Name { get; set; }
    }

    public class SpotifyAlbum
    {
        public List<SpotifyImage> Images { get; set; }
    }

    public class SpotifyImage
    {
        public string Url { get; set; }
    }

    public class SpotifyCurrentlyPlaying
    {
        public bool IsPlaying { get; set; }
        public SpotifyTrack Item { get; set; }
    }
}
