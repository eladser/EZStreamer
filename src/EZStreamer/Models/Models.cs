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
        public string TwitchAccessToken { get; set; } = string.Empty;
        public string SpotifyAccessToken { get; set; } = string.Empty;
        public bool EnableChatCommands { get; set; } = true;
        public bool EnableChannelPoints { get; set; } = true;
        public int MaxQueueLength { get; set; } = 10;
        public string ChatCommand { get; set; } = "!songrequest";
        public string ChannelPointsRewardId { get; set; } = string.Empty;
        public bool AutoPlayNextSong { get; set; } = true;
        public string PreferredMusicSource { get; set; } = "Spotify"; // "Spotify" or "YouTube"
        public string OverlayTheme { get; set; } = "Default";
    }
}
