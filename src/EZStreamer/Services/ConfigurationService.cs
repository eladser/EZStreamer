using System;
using System.IO;
using EZStreamer.Models;
using Newtonsoft.Json;

namespace EZStreamer.Services
{
    public class ConfigurationService
    {
        private readonly string _configPath;
        private readonly string _appDataFolder;
        private APICredentials _credentials;

        public ConfigurationService()
        {
            _appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EZStreamer");
            
            _configPath = Path.Combine(_appDataFolder, "config.json");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(_appDataFolder);
            
            LoadConfiguration();
        }

        public APICredentials GetAPICredentials()
        {
            return _credentials ?? new APICredentials();
        }

        public void SaveAPICredentials(APICredentials credentials)
        {
            _credentials = credentials;
            SaveConfiguration();
        }

        public bool HasTwitchCredentials()
        {
            return !string.IsNullOrEmpty(_credentials?.TwitchClientId);
        }

        public bool HasSpotifyCredentials()
        {
            return !string.IsNullOrEmpty(_credentials?.SpotifyClientId);
        }

        public bool HasYouTubeCredentials()
        {
            return !string.IsNullOrEmpty(_credentials?.YouTubeAPIKey);
        }

        public string GetTwitchClientId()
        {
            return _credentials?.TwitchClientId ?? GetDefaultTwitchClientId();
        }

        public string GetSpotifyClientId()
        {
            return _credentials?.SpotifyClientId ?? GetDefaultSpotifyClientId();
        }

        public string GetYouTubeAPIKey()
        {
            return _credentials?.YouTubeAPIKey ?? string.Empty;
        }

        public void SetTwitchCredentials(string clientId, string clientSecret = "")
        {
            if (_credentials == null)
                _credentials = new APICredentials();
                
            _credentials.TwitchClientId = clientId;
            _credentials.TwitchClientSecret = clientSecret;
            SaveConfiguration();
        }

        public void SetSpotifyCredentials(string clientId, string clientSecret = "")
        {
            if (_credentials == null)
                _credentials = new APICredentials();
                
            _credentials.SpotifyClientId = clientId;
            _credentials.SpotifyClientSecret = clientSecret;
            SaveConfiguration();
        }

        public void SetYouTubeAPIKey(string apiKey)
        {
            if (_credentials == null)
                _credentials = new APICredentials();
                
            _credentials.YouTubeAPIKey = apiKey;
            SaveConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _credentials = new APICredentials();
                    return;
                }

                var jsonContent = File.ReadAllText(_configPath);
                _credentials = JsonConvert.DeserializeObject<APICredentials>(jsonContent) ?? new APICredentials();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load configuration: {ex.Message}");
                _credentials = new APICredentials();
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var jsonContent = JsonConvert.SerializeObject(_credentials, Formatting.Indented);
                File.WriteAllText(_configPath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            }
        }

        // Default credentials for development/testing
        // These are public development credentials that can be used for testing
        private string GetDefaultTwitchClientId()
        {
            // Using a demo/test client ID - users should replace with their own
            // This is a publicly known test client ID that can be used for development
            return "q6batx0epp608isickayubi39itsckt"; // This is a common test client ID
        }

        private string GetDefaultSpotifyClientId()
        {
            // Demo client ID for Spotify - users should replace with their own
            // This is for testing purposes only
            return "5fe01282e29448808d78ac2796c2ba18"; // This is a demo client ID
        }

        public bool IsFirstRun()
        {
            return !File.Exists(_configPath) || 
                   (string.IsNullOrEmpty(_credentials?.TwitchClientId) && 
                    string.IsNullOrEmpty(_credentials?.SpotifyClientId));
        }

        public void ClearConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    File.Delete(_configPath);
                }
                _credentials = new APICredentials();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear configuration: {ex.Message}");
            }
        }

        public string GetConfigurationStatus()
        {
            var status = "Configuration Status:\n";
            status += $"Twitch: {(HasTwitchCredentials() ? "✓ Configured" : "✗ Using default (configure your own)")}\n";
            status += $"Spotify: {(HasSpotifyCredentials() ? "✓ Configured" : "✗ Using default (configure your own)")}\n";
            status += $"YouTube: {(HasYouTubeCredentials() ? "✓ Configured" : "✗ Not configured")}\n";
            
            if (IsFirstRun())
            {
                status += "\n⚠️ Using default test credentials - please configure your own API credentials for production use.";
            }
            
            return status;
        }

        public void ValidateConfiguration()
        {
            // Now just warn instead of throwing, since we have defaults
            if (!HasTwitchCredentials())
            {
                System.Diagnostics.Debug.WriteLine("Warning: Using default Twitch Client ID. Please configure your own for production use.");
            }

            if (!HasSpotifyCredentials())
            {
                System.Diagnostics.Debug.WriteLine("Warning: Using default Spotify Client ID. Please configure your own for production use.");
            }
        }
    }
}
