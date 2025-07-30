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
        private readonly Queue<SongRequest> _songQueue;
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
            _songQueue = new Queue<SongRequest>();

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            // Listen for song requests from Twitch
            _twitchService.SongRequestReceived += OnTwitchSongRequest;
            _twitchService.ChannelPointRedemption += OnChannelPointRedemption;

            // Listen for Spotify track events
            _spotifyService.TrackStarted += OnSpotifyTrackStarted;
            _spotifyService.TrackEnded += OnSpotifyTrackEnded;

            // Listen for YouTube track events
            _youtubeMusicService.TrackStarted += OnYouTubeTrackStarted;
            _youtubeMusicService.TrackEnded += OnYouTubeTrackEnded;
        }

        public async Task ProcessSongRequest(string query, string requestedBy, string source = "chat")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    ErrorOccurred?.Invoke(this, "Song request cannot be empty");
                    return;
                }

                // Check queue length limit
                var settings = _settingsService.LoadSettings();
                if (_songQueue.Count >= settings.MaxQueueLength)
                {
                    ErrorOccurred?.Invoke(this, $"Queue is full (max {settings.MaxQueueLength} songs)");
                    _twitchService.SendChatMessage($"@{requestedBy} Sorry, the song queue is full! Please try again later.");
                    return;
                }

                // Search for the song using preferred music source
                List<SongRequest> searchResults = new List<SongRequest>();
                var preferredSource = settings.PreferredMusicSource;

                if (preferredSource == "Spotify" && _spotifyService.IsConnected)
                {
                    searchResults = await _spotifyService.SearchSongs(query, requestedBy, 1);
                }
                else if (preferredSource == "YouTube" && _youtubeMusicService.IsConnected)
                {
                    searchResults = await _youtubeMusicService.SearchSongs(query, requestedBy, 1);
                }

                // If preferred source failed, try the alternative
                if (!searchResults.Any())
                {
                    if (preferredSource == "Spotify" && _youtubeMusicService.IsConnected)
                    {
                        searchResults = await _youtubeMusicService.SearchSongs(query, requestedBy, 1);
                    }
                    else if (preferredSource == "YouTube" && _spotifyService.IsConnected)
                    {
                        searchResults = await _spotifyService.SearchSongs(query, requestedBy, 1);
                    }
                }

                if (!searchResults.Any())
                {
                    ErrorOccurred?.Invoke(this, $"No songs found for '{query}'");
                    _twitchService.SendChatMessage($"@{requestedBy} Sorry, I couldn't find '{query}' on any music service.");
                    return;
                }

                var song = searchResults.First();
                song.RequestedBy = requestedBy;
                song.Timestamp = DateTime.Now;

                // Add to queue
                _songQueue.Enqueue(song);
                SongRequested?.Invoke(this, song);

                // Send confirmation to chat
                var sourceIcon = song.SourcePlatform == "Spotify" ? "🎵" : "📺";
                _twitchService.SendChatMessage($"@{requestedBy} {sourceIcon} Added '{song.Title}' by {song.Artist} to the queue! Position: {_songQueue.Count}");

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
                while (_songQueue.Count > 0)
                {
                    // If there's already a song playing, wait
                    if (_currentSong != null && _currentSong.Status == SongRequestStatus.Playing)
                    {
                        break;
                    }

                    var nextSong = _songQueue.Dequeue();
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
                _currentSong = song;
                song.Status = SongRequestStatus.Playing;

                bool success = false;

                // Play song using the appropriate service
                if (song.SourcePlatform == "Spotify" && _spotifyService.IsConnected)
                {
                    success = await _spotifyService.PlaySong(song);
                }
                else if (song.SourcePlatform == "YouTube" && _youtubeMusicService.IsConnected)
                {
                    success = await _youtubeMusicService.PlaySong(song);
                }

                if (success)
                {
                    SongStarted?.Invoke(this, song);
                    var sourceIcon = song.SourcePlatform == "Spotify" ? "🎵" : "📺";
                    _twitchService.SendChatMessage($"{sourceIcon} Now playing: {song.Title} by {song.Artist} (requested by @{song.RequestedBy})");
                }
                else
                {
                    song.Status = SongRequestStatus.Failed;
                    ErrorOccurred?.Invoke(this, $"Failed to play '{song.Title}'");
                    _twitchService.SendChatMessage($"@{song.RequestedBy} Sorry, I couldn't play '{song.Title}'. Skipping to next song.");
                    
                    // Try to play next song
                    _currentSong = null;
                    await ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                song.Status = SongRequestStatus.Failed;
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
                    if (skippedSong.SourcePlatform == "Spotify" && _spotifyService.IsConnected)
                    {
                        await _spotifyService.SkipToNext();
                    }
                    else if (skippedSong.SourcePlatform == "YouTube" && _youtubeMusicService.IsConnected)
                    {
                        await _youtubeMusicService.SkipToNext();
                    }
                    
                    _twitchService.SendChatMessage($"⏭️ Skipped: {skippedSong.Title} by {skippedSong.Artist}");
                    
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
            _songQueue.Clear();
            _twitchService.SendChatMessage("🗑️ Song queue has been cleared!");
        }

        public void RemoveSongFromQueue(string songId)
        {
            var tempQueue = new Queue<SongRequest>();
            var removed = false;

            while (_songQueue.Count > 0)
            {
                var song = _songQueue.Dequeue();
                if (song.Id != songId)
                {
                    tempQueue.Enqueue(song);
                }
                else
                {
                    removed = true;
                }
            }

            // Restore the queue without the removed song
            while (tempQueue.Count > 0)
            {
                _songQueue.Enqueue(tempQueue.Dequeue());
            }

            if (removed)
            {
                _twitchService.SendChatMessage("🗑️ Song removed from queue!");
            }
        }

        public async Task<List<SongRequest>> SearchSongs(string query, int limit = 5)
        {
            try
            {
                var results = new List<SongRequest>();
                var settings = _settingsService.LoadSettings();

                // Search both services if available
                if (_spotifyService.IsConnected)
                {
                    var spotifyResults = await _spotifyService.SearchSongs(query, "System", limit);
                    results.AddRange(spotifyResults);
                }

                if (_youtubeMusicService.IsConnected)
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

        public async Task UpdateCurrentSongInfo()
        {
            try
            {
                SongRequest currentSong = null;

                // Check both services for currently playing song
                if (_spotifyService.IsConnected)
                {
                    currentSong = await _spotifyService.GetCurrentSongInfo();
                }

                if (currentSong == null && _youtubeMusicService.IsConnected)
                {
                    currentSong = await _youtubeMusicService.GetCurrentSongInfo();
                }

                if (currentSong != null && (_currentSong == null || _currentSong.SourceId != currentSong.SourceId))
                {
                    _currentSong = currentSong;
                    SongStarted?.Invoke(this, currentSong);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating current song info: {ex.Message}");
            }
        }

        #region Event Handlers

        private async void OnTwitchSongRequest(object sender, string requestData)
        {
            try
            {
                var parts = requestData.Split('|');
                if (parts.Length >= 2)
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
                var parts = redemptionData.Split('|');
                if (parts.Length >= 2)
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
            _currentSong = song;
            SongStarted?.Invoke(this, song);
        }

        private async void OnSpotifyTrackEnded(object sender, SongRequest song)
        {
            if (_currentSong != null && _currentSong.Id == song.Id)
            {
                _currentSong.Status = SongRequestStatus.Completed;
                SongCompleted?.Invoke(this, _currentSong);
                _currentSong = null;

                // Auto-play next song
                await ProcessQueue();
            }
        }

        private void OnYouTubeTrackStarted(object sender, SongRequest song)
        {
            _currentSong = song;
            SongStarted?.Invoke(this, song);
        }

        private async void OnYouTubeTrackEnded(object sender, SongRequest song)
        {
            if (_currentSong != null && _currentSong.Id == song.Id)
            {
                _currentSong.Status = SongRequestStatus.Completed;
                SongCompleted?.Invoke(this, _currentSong);
                _currentSong = null;

                // Auto-play next song
                await ProcessQueue();
            }
        }

        #endregion

        public void Dispose()
        {
            _twitchService.SongRequestReceived -= OnTwitchSongRequest;
            _twitchService.ChannelPointRedemption -= OnChannelPointRedemption;
            _spotifyService.TrackStarted -= OnSpotifyTrackStarted;
            _spotifyService.TrackEnded -= OnSpotifyTrackEnded;
            _youtubeMusicService.TrackStarted -= OnYouTubeTrackStarted;
            _youtubeMusicService.TrackEnded -= OnYouTubeTrackEnded;
        }
    }
}
