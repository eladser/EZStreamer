using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class SpotifyService
    {
        private string _accessToken;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string ActiveDeviceName => _isConnected ? "EZStreamer Player" : "No device selected";

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<SongRequest> TrackStarted;
        public event EventHandler<SongRequest> TrackEnded;

        public SpotifyService()
        {
            _isConnected = false;
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
                
                // Simulate connection delay
                await Task.Delay(1000);
                
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

        public void Disconnect()
        {
            try
            {
                _isConnected = false;
                _accessToken = null;
                
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

                // Simulate search delay
                await Task.Delay(500);

                // Generate some sample search results
                var songs = new List<SongRequest>();
                var sampleSongs = new[]
                {
                    ("Bohemian Rhapsody", "Queen"),
                    ("Imagine", "John Lennon"),
                    ("Hotel California", "Eagles"),
                    ("Stairway to Heaven", "Led Zeppelin"),
                    ("Sweet Child O' Mine", "Guns N' Roses"),
                    ("Thunderstruck", "AC/DC"),
                    ("Smells Like Teen Spirit", "Nirvana"),
                    ("Billie Jean", "Michael Jackson"),
                    ("Don't Stop Believin'", "Journey"),
                    ("Livin' on a Prayer", "Bon Jovi")
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
                        Title = song.Item1,
                        Artist = song.Item2,
                        RequestedBy = requestedBy,
                        SourcePlatform = "Spotify",
                        SourceId = Guid.NewGuid().ToString(),
                        Duration = TimeSpan.FromMinutes(3 + random.Next(0, 3)), // Random duration 3-6 minutes
                        Status = SongRequestStatus.Queued
                    });
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

                // Simulate playback start delay
                await Task.Delay(200);
                
                song.Status = SongRequestStatus.Playing;
                TrackStarted?.Invoke(this, song);
                
                System.Diagnostics.Debug.WriteLine($"Started playing: {song.Title} by {song.Artist}");
                
                // Simulate song duration (for demo, use shorter duration)
                var playDuration = song.Duration > TimeSpan.Zero ? 
                                  TimeSpan.FromSeconds(10) : // Demo: 10 seconds instead of full song
                                  TimeSpan.FromSeconds(10);
                
                // Start a task to mark the song as ended after duration
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(playDuration);
                        song.Status = SongRequestStatus.Completed;
                        TrackEnded?.Invoke(this, song);
                        System.Diagnostics.Debug.WriteLine($"Finished playing: {song.Title}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in song completion: {ex.Message}");
                    }
                });
                
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
                if (!_isConnected || song == null)
                {
                    return false;
                }

                // Simulate API call delay
                await Task.Delay(200);
                
                System.Diagnostics.Debug.WriteLine($"Added to Spotify queue: {song.Title} by {song.Artist}");
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
                if (!_isConnected)
                {
                    return false;
                }

                // Simulate API call delay
                await Task.Delay(200);
                
                System.Diagnostics.Debug.WriteLine("Skipped to next track");
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
                if (!_isConnected)
                {
                    return false;
                }

                // Simulate API call delay
                await Task.Delay(200);
                
                System.Diagnostics.Debug.WriteLine("Playback paused");
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
                if (!_isConnected)
                {
                    return false;
                }

                // Simulate API call delay
                await Task.Delay(200);
                
                System.Diagnostics.Debug.WriteLine("Playback resumed");
                return true;
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

                // Simulate API call delay
                await Task.Delay(200);
                
                // For demo purposes, return null (no current song)
                // In real implementation, this would return the currently playing song
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current song info: {ex.Message}");
                return null;
            }
        }

        // Validate access token (simplified)
        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    return false;
                }

                // Simulate token validation
                await Task.Delay(500);
                
                // Basic validation - check if it looks like a valid token
                return accessToken.Length > 10;
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

        public void Dispose()
        {
            try
            {
                Disconnect();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing SpotifyService: {ex.Message}");
            }
        }
    }
}
