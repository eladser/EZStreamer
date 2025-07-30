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

        public SongRequestService(TwitchService twitchService, SpotifyService spotifyService)
        {
            _twitchService = twitchService;
            _spotifyService = spotifyService;
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

                // Search for the song on Spotify
                var searchResults = await _spotifyService.SearchSongs(query, requestedBy, 1);
                
                if (!searchResults.Any())
                {
                    ErrorOccurred?.Invoke(this, $"No songs found for '{query}'");
                    _twitchService.SendChatMessage($"@{requestedBy} Sorry, I couldn't find '{query}' on Spotify.");
                    return;
                }

                var song = searchResults.First();
                song.RequestedBy = requestedBy;
                song.Timestamp = DateTime.Now;

                // Add to queue
                _songQueue.Enqueue(song);
                SongRequested?.Invoke(this, song);

                // Send confirmation to chat
                _twitchService.SendChatMessage($"@{requestedBy} Added '{song.Title}' by {song.Artist} to the queue! Position: {_songQueue.Count}");

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
            if (_isProcessingQueue || !_spotifyService.IsConnected)
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
                if (!_spotifyService.IsConnected)
                {
                    ErrorOccurred?.Invoke(this, "Spotify is not connected");
                    return;
                }

                _currentSong = song;
                song.Status = SongRequestStatus.Playing;

                var success = await _spotifyService.PlaySong(song);
                
                if (success)
                {
                    SongStarted?.Invoke(this, song);
                    _twitchService.SendChatMessage($"♪ Now playing: {song.Title} by {song.Artist} (requested by @{song.RequestedBy})");
                }
                else
                {
                    song.Status = SongRequestStatus.Failed;
                    ErrorOccurred?.Invoke(this, $"Failed to play '{song.Title}'");
                    _twitchService.SendChatMessage($"@{song.RequestedBy} Sorry, I couldn't play '{song.Title}'. Skipping to next song.");
                    
                    // Try to play next song
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
                    
                    await _spotifyService.SkipToNext();
                    
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
                if (_spotifyService.IsConnected)
                {
                    return await _spotifyService.SearchSongs(query, "System", limit);
                }
                
                return new List<SongRequest>();
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
                if (_spotifyService.IsConnected)
                {
                    var currentSong = await _spotifyService.GetCurrentSongInfo();
                    if (currentSong != null && (_currentSong == null || _currentSong.SourceId != currentSong.SourceId))
                    {
                        _currentSong = currentSong;
                        SongStarted?.Invoke(this, currentSong);
                    }
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

        #endregion

        public void Dispose()
        {
            _twitchService.SongRequestReceived -= OnTwitchSongRequest;
            _twitchService.ChannelPointRedemption -= OnChannelPointRedemption;
            _spotifyService.TrackStarted -= OnSpotifyTrackStarted;
            _spotifyService.TrackEnded -= OnSpotifyTrackEnded;
        }
    }
}
