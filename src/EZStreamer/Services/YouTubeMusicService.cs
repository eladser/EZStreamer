using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

                // Simulate search delay
                await Task.Delay(500);

                // Generate YouTube-style search results
                var songs = new List<SongRequest>();
                var sampleSongs = new[]
                {
                    ("Bohemian Rhapsody - Queen", "Queen"),
                    ("Imagine - John Lennon", "John Lennon"),
                    ("Hotel California - Eagles", "Eagles"),
                    ("Stairway to Heaven - Led Zeppelin", "Led Zeppelin"),
                    ("Sweet Child O' Mine - Guns N' Roses", "Guns N' Roses"),
                    ("Thunderstruck - AC/DC", "AC/DC"),
                    ("Smells Like Teen Spirit - Nirvana", "Nirvana"),
                    ("Billie Jean - Michael Jackson", "Michael Jackson"),
                    ("Don't Stop Believin' - Journey", "Journey"),
                    ("Livin' on a Prayer - Bon Jovi", "Bon Jovi")
                };

                var random = new Random();
                var searchTerms = query.ToLower().Split(' ');
                
                // Find songs that match the search query
                var matchingSongs = sampleSongs.Where(s => 
                    searchTerms.Any(term => 
                        s.Item1.ToLower().Contains(term) || 
                        s.Item2.ToLower().Contains(term)
                    )
                ).Take(limit).ToArray();

                // If no matches, return first few songs as fallback
                if (!matchingSongs.Any())
                {
                    matchingSongs = sampleSongs.Take(limit).ToArray();
                }

                foreach (var song in matchingSongs)
                {
                    songs.Add(new SongRequest
                    {
                        Title = ExtractTitle(song.Item1),
                        Artist = song.Item2,
                        RequestedBy = requestedBy,
                        SourcePlatform = "YouTube",
                        SourceId = GenerateVideoId(),
                        Duration = TimeSpan.FromMinutes(3 + random.Next(0, 4)), // Random duration 3-7 minutes
                        Status = SongRequestStatus.Queued
                    });
                }

                System.Diagnostics.Debug.WriteLine($"YouTube found {songs.Count} songs for query: {query}");
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
                if (!_isConnected || song == null)
                    return false;

                // Simulate playback start delay
                await Task.Delay(300);
                
                _currentSong = song;
                song.Status = SongRequestStatus.Playing;
                TrackStarted?.Invoke(this, song);
                
                System.Diagnostics.Debug.WriteLine($"YouTube playing: {song.Title} by {song.Artist}");
                
                // Simulate song duration (for demo, use shorter duration)
                var playDuration = song.Duration > TimeSpan.Zero ? 
                                  TimeSpan.FromSeconds(12) : // Demo: 12 seconds instead of full song
                                  TimeSpan.FromSeconds(12);
                
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

        private string ExtractTitle(string fullTitle)
        {
            // Extract just the song title from "Title - Artist" format
            var parts = fullTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].Trim() : fullTitle;
        }

        private string GenerateVideoId()
        {
            // Generate a YouTube-like video ID for demo purposes
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 11)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Validate API key (for future implementation)
        public async Task<bool> ValidateAPIKeyAsync(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return false;

                // Simulate validation
                await Task.Delay(500);
                
                // Basic validation - check if it looks like a valid API key
                return apiKey.Length > 20;
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing YouTubeMusicService: {ex.Message}");
            }
        }
    }
}
