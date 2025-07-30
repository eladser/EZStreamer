using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class SpotifyService
    {
        private SpotifyApi _spotify;
        private string _accessToken;
        private Device _activeDevice;

        public bool IsConnected => _spotify != null;
        public string ActiveDeviceName => _activeDevice?.Name ?? "No device selected";

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        public event EventHandler<SongRequest> TrackEnded;

        public SpotifyService()
        {
            
        }

        public async Task Connect(string accessToken)
        {
            try
            {
                _accessToken = accessToken;
                var config = SpotifyClientConfig.CreateDefault().WithToken(accessToken);
                _spotify = new SpotifyApi(config);

                // Test the connection
                var user = await _spotify.UserProfile.Current();
                if (user == null)
                {
                    throw new Exception("Failed to get user profile");
                }

                // Get available devices
                await RefreshDevices();

                Connected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _spotify = null;
                _accessToken = null;
                throw new Exception($"Failed to connect to Spotify: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            _spotify = null;
            _accessToken = null;
            _activeDevice = null;
            
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<Device>> GetAvailableDevices()
        {
            try
            {
                if (_spotify == null)
                    return new List<Device>();

                var devices = await _spotify.Player.GetAvailableDevices();
                return devices.Devices?.ToList() ?? new List<Device>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting devices: {ex.Message}");
                return new List<Device>();
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
                if (_spotify == null)
                    return new List<SongRequest>();

                var searchRequest = new SearchRequest(SearchRequest.Types.Track, query)
                {
                    Limit = limit
                };

                var searchResult = await _spotify.Search.Item(searchRequest);
                var songs = new List<SongRequest>();

                if (searchResult.Tracks?.Items != null)
                {
                    foreach (var track in searchResult.Tracks.Items)
                    {
                        var songRequest = new SongRequest
                        {
                            Title = track.Name,
                            Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
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
                if (_spotify == null || _activeDevice == null)
                    return false;

                var playRequest = new PlayerResumePlaybackRequest
                {
                    DeviceId = _activeDevice.Id,
                    Uris = new List<string> { $"spotify:track:{song.SourceId}" }
                };

                await _spotify.Player.ResumePlayback(playRequest);
                
                song.Status = SongRequestStatus.Playing;
                TrackStarted?.Invoke(this, song);
                
                return true;
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
                if (_spotify == null || _activeDevice == null)
                    return false;

                var addToQueueRequest = new PlayerAddToQueueRequest($"spotify:track:{song.SourceId}")
                {
                    DeviceId = _activeDevice.Id
                };

                await _spotify.Player.AddToQueue(addToQueueRequest);
                return true;
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
                if (_spotify == null || _activeDevice == null)
                    return false;

                var skipRequest = new PlayerSkipNextRequest
                {
                    DeviceId = _activeDevice.Id
                };

                await _spotify.Player.SkipNext(skipRequest);
                return true;
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
                if (_spotify == null || _activeDevice == null)
                    return false;

                var pauseRequest = new PlayerPausePlaybackRequest
                {
                    DeviceId = _activeDevice.Id
                };

                await _spotify.Player.PausePlayback(pauseRequest);
                return true;
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
                if (_spotify == null || _activeDevice == null)
                    return false;

                var resumeRequest = new PlayerResumePlaybackRequest
                {
                    DeviceId = _activeDevice.Id
                };

                await _spotify.Player.ResumePlayback(resumeRequest);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resuming playback: {ex.Message}");
                return false;
            }
        }

        public async Task<CurrentlyPlaying> GetCurrentlyPlaying()
        {
            try
            {
                if (_spotify == null)
                    return null;

                return await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting currently playing: {ex.Message}");
                return null;
            }
        }

        public async Task<SongRequest> GetCurrentSongInfo()
        {
            try
            {
                var currentlyPlaying = await GetCurrentlyPlaying();
                
                if (currentlyPlaying?.Item is FullTrack track)
                {
                    return new SongRequest
                    {
                        Title = track.Name,
                        Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
                        SourcePlatform = "Spotify",
                        SourceId = track.Id,
                        Duration = TimeSpan.FromMilliseconds(track.DurationMs),
                        AlbumArt = track.Album?.Images?.FirstOrDefault()?.Url ?? "",
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

        public async Task<bool> SetVolume(int volumePercent)
        {
            try
            {
                if (_spotify == null || _activeDevice == null)
                    return false;

                var volumeRequest = new PlayerVolumeRequest(volumePercent)
                {
                    DeviceId = _activeDevice.Id
                };

                await _spotify.Player.SetVolume(volumeRequest);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting volume: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetActiveDevice(string deviceId)
        {
            try
            {
                if (_spotify == null)
                    return false;

                var devices = await GetAvailableDevices();
                _activeDevice = devices.FirstOrDefault(d => d.Id == deviceId);
                
                if (_activeDevice != null)
                {
                    var transferRequest = new PlayerTransferPlaybackRequest(new List<string> { deviceId })
                    {
                        Play = false
                    };

                    await _spotify.Player.TransferPlayback(transferRequest);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting active device: {ex.Message}");
                return false;
            }
        }
    }
}
