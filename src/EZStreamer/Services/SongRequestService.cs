using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class SongRequestService
    {
        private readonly TwitchService _twitchService;
        private readonly SpotifyService _spotifyService;
        private readonly YouTubeMusicService _youtubeMusicService;
        private readonly SettingsService _settingsService;
        private readonly List<SongRequest> _songQueue;
        private SongRequest _currentSong;
        private bool _isProcessingQueue;

        public event EventHandler<SongRequest> SongRequested;
        public event EventHandler<SongRequest> SongStarted;
        public event EventHandler<SongRequest> SongCompleted;
        public event EventHandler<string> ErrorOccurred;

        public SongRequest CurrentSong => _currentSong;
        public IEnumerable<SongRequest> Queue => _songQueue.ToList();
        public int QueueCount => _songQueue.Count;

        public SongRequestService(TwitchService twitchService, SpotifyService spotifyService, 
            YouTubeMusicService youtubeMusicService, SettingsService settingsService)
        {
            _twitchService = twitchService;
            _spotifyService = spotifyService;
            _youtubeMusicService = youtubeMusicService;
            _settingsService = settingsService;
            _songQueue = new List<SongRequest>();

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            try
            {
                // Listen for song requests from Twitch
                if (_twitchService != null)
                {
                    _twitchService.SongRequestReceived += OnTwitchSongRequest;
                    _twitchService.ChannelPointRedemption += OnChannelPointRedemption;
                }

                // Listen for Spotify track events
                if (_spotifyService != null)
                {
                    _spotifyService.TrackStarted += OnSpotifyTrackStarted;
                    _spotifyService.TrackEnded += OnSpotifyTrackEnded;
                }

                // Listen for YouTube track events
                if (_youtubeMusicService != null)
                {
                    _youtubeMusicService.TrackStarted += OnYouTubeTrackStarted;
                    _youtubeMusicService.TrackEnded += OnYouTubeTrackEnded;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up event handlers: {ex.Message}");
            }
        }

        public async Task ProcessSongRequest(string query, string requestedBy, string source = "manual")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    ErrorOccurred?.Invoke(this, "Song request cannot be empty");
                    return;
                }

                // Check queue length limit
                var settings = _settingsService?.LoadSettings() ?? new AppSettings();
                if (_songQueue.Count >= settings.MaxQueueLength)
                {
                    ErrorOccurred?.Invoke(this, $"Queue is full (max {settings.MaxQueueLength} songs)");
                    return;
                }

                // Search for the song using preferred music source
                List<SongRequest> searchResults = new List<SongRequest>();
                var preferredSource = settings.PreferredMusicSource ?? "Spotify";

                // Try Spotify first if preferred or available
                if ((preferredSource == "Spotify" || preferredSource == "") && _spotifyService?.IsConnected == true)
                {
                    searchResults = await _spotifyService.SearchSongs(query, requestedBy, 1);
                }

                // If no results from Spotify, try YouTube
                if (!searchResults.Any() && _youtubeMusicService?.IsConnected == true)
                {
                    searchResults = await _youtubeMusicService.SearchSongs(query, requestedBy, 1);
                }

                if (!searchResults.Any())
                {
                    ErrorOccurred?.Invoke(this, $"No songs found for '{query}'");
                    return;
                }

                var song = searchResults.First();
                song.RequestedBy = requestedBy;
                song.Timestamp = DateTime.Now;
                song.Status = SongRequestStatus.Queued;

                // Add to queue
                _songQueue.Add(song);
                SongRequested?.Invoke(this, song);

                System.Diagnostics.Debug.WriteLine($"Added to queue: {song.Title} by {song.Artist} (requested by {requestedBy})");

                // Start processing queue if not already processing
                if (!_isProcessingQueue)
                {
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to process song request: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error processing song request: {ex.Message}");
            }
        }

        public async Task ProcessQueue()
        {
            if (_isProcessingQueue)
                return;

            _isProcessingQueue = true;

            try
            {
                // Check if there's a song currently playing
                if (_currentSong?.Status == SongRequestStatus.Playing)
                {
                    return;
                }

                // Get next song from queue
                var nextSong = _songQueue.FirstOrDefault(s => s.Status == SongRequestStatus.Queued);
                if (nextSong != null)
                {
                    await PlaySong(nextSong);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error processing queue: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error processing queue: {ex.Message}");
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        public async Task PlaySong(SongRequest song)
        {
            try
            {
                if (song == null)
                    return;

                _currentSong = song;
                song.Status = SongRequestStatus.Playing;

                bool success = false;

                // Play song using the appropriate service
                if (song.SourcePlatform == "Spotify" && _spotifyService?.IsConnected == true)
                {
                    success = await _spotifyService.PlaySong(song);
                }
                else if (song.SourcePlatform == "YouTube" && _youtubeMusicService?.IsConnected == true)
                {
                    success = await _youtubeMusicService.PlaySong(song);
                }

                if (success)
                {
                    SongStarted?.Invoke(this, song);
                    System.Diagnostics.Debug.WriteLine($"Now playing: {song.Title} by {song.Artist}");
                }
                else
                {
                    song.Status = SongRequestStatus.Failed;
                    ErrorOccurred?.Invoke(this, $"Failed to play '{song.Title}'");
                    
                    // Try to play next song
                    _currentSong = null;
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                if (song != null)
                {
                    song.Status = SongRequestStatus.Failed;
                }
                ErrorOccurred?.Invoke(this, $"Error playing song: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error playing song: {ex.Message}");
            }
        }

        public async Task SkipCurrentSong()
        {
            try
            {
                if (_currentSong != null)
                {
                    var skippedSong = _currentSong;
                    skippedSong.Status = SongRequestStatus.Skipped;
                    
                    // Skip using the appropriate service
                    if (skippedSong.SourcePlatform == "Spotify" && _spotifyService?.IsConnected == true)
                    {
                        await _spotifyService.SkipToNext();
                    }
                    else if (skippedSong.SourcePlatform == "YouTube" && _youtubeMusicService?.IsConnected == true)
                    {
                        await _youtubeMusicService.SkipToNext();
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Skipped: {skippedSong.Title} by {skippedSong.Artist}");
                    
                    SongCompleted?.Invoke(this, skippedSong);
                    _currentSong = null;

                    // Process next song in queue
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error skipping song: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error skipping song: {ex.Message}");
            }
        }

        public void ClearQueue()
        {
            try
            {
                _songQueue.Clear();
                System.Diagnostics.Debug.WriteLine("Song queue cleared");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing queue: {ex.Message}");
            }
        }

        public void RemoveSongFromQueue(SongRequest song)
        {
            try
            {
                if (song != null && _songQueue.Contains(song))
                {
                    _songQueue.Remove(song);
                    System.Diagnostics.Debug.WriteLine($"Removed from queue: {song.Title}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing song from queue: {ex.Message}");
            }
        }

        public async Task<List<SongRequest>> SearchSongs(string query, int limit = 5)
        {
            try
            {
                var results = new List<SongRequest>();
                var settings = _settingsService?.LoadSettings() ?? new AppSettings();

                // Search both services if available
                if (_spotifyService?.IsConnected == true)
                {
                    var spotifyResults = await _spotifyService.SearchSongs(query, "System", limit);
                    results.AddRange(spotifyResults);
                }

                if (_youtubeMusicService?.IsConnected == true)
                {
                    var youtubeResults = await _youtubeMusicService.SearchSongs(query, "System", limit);
                    results.AddRange(youtubeResults);
                }

                // Sort by preferred source
                if (settings.PreferredMusicSource == "Spotify")
                {
                    results = results.OrderBy(r => r.SourcePlatform == "Spotify" ? 0 : 1).Take(limit).ToList();
                }
                else
                {
                    results = results.OrderBy(r => r.SourcePlatform == "YouTube" ? 0 : 1).Take(limit).ToList();
                }

                return results;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error searching songs: {ex.Message}");
                return new List<SongRequest>();
            }
        }

        #region Event Handlers

        private async void OnTwitchSongRequest(object sender, string requestData)
        {
            try
            {
                var parts = requestData?.Split('|');
                if (parts?.Length >= 2)
                {
                    var username = parts[0];
                    var query = parts[1];
                    
                    await ProcessSongRequest(query, username, "chat");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error handling Twitch song request: {ex.Message}");
            }
        }

        private async void OnChannelPointRedemption(object sender, string redemptionData)
        {
            try
            {
                var parts = redemptionData?.Split('|');
                if (parts?.Length >= 2)
                {
                    var username = parts[0];
                    var query = parts[1];
                    
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        await ProcessSongRequest(query, username, "channel_points");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error handling channel point redemption: {ex.Message}");
            }
        }

        private void OnSpotifyTrackStarted(object sender, SongRequest song)
        {
            try
            {
                if (song != null)
                {
                    _currentSong = song;
                    SongStarted?.Invoke(this, song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling Spotify track started: {ex.Message}");
            }
        }

        private async void OnSpotifyTrackEnded(object sender, SongRequest song)
        {
            try
            {
                if (_currentSong != null && song != null && _currentSong.Id == song.Id)
                {
                    _currentSong.Status = SongRequestStatus.Completed;
                    SongCompleted?.Invoke(this, _currentSong);
                    _currentSong = null;

                    // Auto-play next song
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling Spotify track ended: {ex.Message}");
            }
        }

        private void OnYouTubeTrackStarted(object sender, SongRequest song)
        {
            try
            {
                if (song != null)
                {
                    _currentSong = song;
                    SongStarted?.Invoke(this, song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling YouTube track started: {ex.Message}");
            }
        }

        private async void OnYouTubeTrackEnded(object sender, SongRequest song)
        {
            try
            {
                if (_currentSong != null && song != null && _currentSong.Id == song.Id)
                {
                    _currentSong.Status = SongRequestStatus.Completed;
                    SongCompleted?.Invoke(this, _currentSong);
                    _currentSong = null;

                    // Auto-play next song
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling YouTube track ended: {ex.Message}");
            }
        }

        #endregion

        public void Dispose()
        {
            try
            {
                if (_twitchService != null)
                {
                    _twitchService.SongRequestReceived -= OnTwitchSongRequest;
                    _twitchService.ChannelPointRedemption -= OnChannelPointRedemption;
                }
                
                if (_spotifyService != null)
                {
                    _spotifyService.TrackStarted -= OnSpotifyTrackStarted;
                    _spotifyService.TrackEnded -= OnSpotifyTrackEnded;
                }
                
                if (_youtubeMusicService != null)
                {
                    _youtubeMusicService.TrackStarted -= OnYouTubeTrackStarted;
                    _youtubeMusicService.TrackEnded -= OnYouTubeTrackEnded;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing SongRequestService: {ex.Message}");
            }
        }
    }
}
