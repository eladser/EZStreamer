using System;

namespace EZStreamer.Models
{
    public class SongRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public SongRequestStatus Status { get; set; } = SongRequestStatus.Queued;
        public string SourcePlatform { get; set; } = string.Empty; // "Spotify" or "YouTube"
        public string SourceId { get; set; } = string.Empty; // Track ID or YouTube URL
        public string AlbumArt { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    }

    public enum SongRequestStatus
    {
        Queued,
        Playing,
        Completed,
        Skipped,
        Failed
    }

    public class StreamInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsLive { get; set; } = false;
        public int ViewerCount { get; set; } = 0;
    }

    public class AppSettings
    {
        // Authentication
        public string TwitchAccessToken { get; set; } = string.Empty;
        public string SpotifyAccessToken { get; set; } = string.Empty;
        public string SpotifyRefreshToken { get; set; } = string.Empty;
        public DateTime SpotifyTokenExpiry { get; set; } = DateTime.MinValue;
        public string TwitchClientId { get; set; } = string.Empty;
        public string SpotifyClientId { get; set; } = string.Empty;
        
        // Song Request Settings
        public bool EnableChatCommands { get; set; } = true;
        public bool EnableChannelPoints { get; set; } = true;
        public int MaxQueueLength { get; set; } = 10;
        public string ChatCommand { get; set; } = "!songrequest";
        public string ChannelPointsRewardId { get; set; } = string.Empty;
        public bool AutoPlayNextSong { get; set; } = true;
        public string PreferredMusicSource { get; set; } = "Spotify"; // "Spotify" or "YouTube"
        
        // OBS Settings
        public string OBSServerIP { get; set; } = "localhost";
        public int OBSServerPort { get; set; } = 4455;
        public string OBSServerPassword { get; set; } = string.Empty;
        public bool OBSAutoConnect { get; set; } = false;
        public bool OBSSceneSwitchingEnabled { get; set; } = false;
        public string OBSMusicScene { get; set; } = string.Empty;
        public string OBSDefaultScene { get; set; } = string.Empty;
        
        // Overlay Settings
        public string OverlayTheme { get; set; } = "Default";
        public int OverlayDisplayDuration { get; set; } = 0; // 0 = always show, >0 = seconds to show
        public bool OverlayShowAlbumArt { get; set; } = true;
        public bool OverlayShowRequester { get; set; } = true;
        
        // Application Settings
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoStartServices { get; set; } = false;
        public string LastUsedVersion { get; set; } = "1.0.0";
        
        // Advanced Settings
        public int SongRequestCooldown { get; set; } = 0; // seconds between requests per user
        public bool AllowExplicitContent { get; set; } = true;
        public bool RequireFollowersOnly { get; set; } = false;
        public bool RequireSubscribersOnly { get; set; } = false;
        public int MinStreamDuration { get; set; } = 30; // minimum seconds for song requests
        public int MaxStreamDuration { get; set; } = 600; // maximum seconds for song requests
    }

    public class APICredentials
    {
        public string TwitchClientId { get; set; } = string.Empty;
        public string TwitchClientSecret { get; set; } = string.Empty;
        public string SpotifyClientId { get; set; } = string.Empty;
        public string SpotifyClientSecret { get; set; } = string.Empty;
        public string YouTubeAPIKey { get; set; } = string.Empty;
    }

    public class OverlaySettings
    {
        public string Theme { get; set; } = "Default";
        public string Position { get; set; } = "BottomLeft"; // TopLeft, TopRight, BottomLeft, BottomRight, Center
        public int Width { get; set; } = 450;
        public int Height { get; set; } = 150;
        public double Opacity { get; set; } = 1.0;
        public bool ShowAnimation { get; set; } = true;
        public bool ShowAlbumArt { get; set; } = true;
        public bool ShowRequester { get; set; } = true;
        public bool ShowProgress { get; set; } = false;
        public string FontFamily { get; set; } = "Segoe UI";
        public int FontSize { get; set; } = 14;
        public string BackgroundColor { get; set; } = "#673AB7";
        public string TextColor { get; set; } = "#FFFFFF";
    }
}
