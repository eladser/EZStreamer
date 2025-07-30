using System;
using System.IO;
using EZStreamer.Models;
using Newtonsoft.Json;

namespace EZStreamer.Services
{
    public class OverlayService
    {
        private readonly string _overlayFolderPath;
        private readonly string _nowPlayingJsonPath;
        private readonly string _overlayHtmlPath;

        public string OverlayFolderPath => _overlayFolderPath;
        public string NowPlayingJsonPath => _nowPlayingJsonPath;
        public string OverlayHtmlPath => _overlayHtmlPath;

        public OverlayService()
        {
            // Create overlay folder in app data
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EZStreamer", "Overlay");
            
            _overlayFolderPath = appDataFolder;
            _nowPlayingJsonPath = Path.Combine(_overlayFolderPath, "now_playing.json");
            _overlayHtmlPath = Path.Combine(_overlayFolderPath, "overlay.html");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(_overlayFolderPath);
            
            // Create default overlay HTML file
            CreateDefaultOverlayHtml();
        }

        public void UpdateNowPlaying(SongRequest song)
        {
            try
            {
                var overlayData = new
                {
                    title = song?.Title ?? "",
                    artist = song?.Artist ?? "",
                    requestedBy = song?.RequestedBy ?? "",
                    albumArt = song?.AlbumArt ?? "",
                    isPlaying = song?.Status == SongRequestStatus.Playing,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    platform = song?.SourcePlatform ?? ""
                };

                var json = JsonConvert.SerializeObject(overlayData, Formatting.Indented);
                File.WriteAllText(_nowPlayingJsonPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating now playing overlay: {ex.Message}");
            }
        }

        public void ClearNowPlaying()
        {
            try
            {
                var overlayData = new
                {
                    title = "",
                    artist = "",
                    requestedBy = "",
                    albumArt = "",
                    isPlaying = false,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    platform = ""
                };

                var json = JsonConvert.SerializeObject(overlayData, Formatting.Indented);
                File.WriteAllText(_nowPlayingJsonPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing now playing overlay: {ex.Message}");
            }
        }

        private void CreateDefaultOverlayHtml()
        {
            try
            {
                if (File.Exists(_overlayHtmlPath))
                    return; // Don't overwrite existing file

                var htmlContent = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>EZStreamer - Now Playing</title>
    <style>
        body {
            margin: 0;
            padding: 20px;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: transparent;
            color: white;
            overflow: hidden;
        }

        .now-playing {
            background: linear-gradient(135deg, rgba(103, 58, 183, 0.9), rgba(63, 81, 181, 0.9));
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.1);
            max-width: 400px;
            opacity: 0;
            transform: translateY(20px);
            transition: all 0.5s ease;
        }

        .now-playing.visible {
            opacity: 1;
            transform: translateY(0);
        }

        .song-info {
            display: flex;
            align-items: center;
            gap: 15px;
        }

        .album-art {
            width: 60px;
            height: 60px;
            border-radius: 8px;
            background: rgba(255, 255, 255, 0.1);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            flex-shrink: 0;
        }

        .album-art img {
            width: 100%;
            height: 100%;
            border-radius: 8px;
            object-fit: cover;
        }

        .text-info {
            flex: 1;
            min-width: 0;
        }

        .song-title {
            font-size: 18px;
            font-weight: bold;
            margin: 0 0 5px 0;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .song-artist {
            font-size: 14px;
            opacity: 0.9;
            margin: 0 0 5px 0;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .requested-by {
            font-size: 12px;
            opacity: 0.7;
            font-style: italic;
            margin: 0;
        }

        .music-icon {
            position: absolute;
            top: 10px;
            right: 15px;
            font-size: 20px;
            opacity: 0.8;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { opacity: 0.8; }
            50% { opacity: 1; }
        }

        .platform-badge {
            position: absolute;
            bottom: 10px;
            right: 15px;
            background: rgba(255, 255, 255, 0.2);
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 10px;
            text-transform: uppercase;
            font-weight: bold;
        }

        /* Scrolling text for long titles */
        .scroll-text {
            animation: scroll-left 10s linear infinite;
        }

        @keyframes scroll-left {
            0% { transform: translateX(100%); }
            100% { transform: translateX(-100%); }
        }
    </style>
</head>
<body>
    <div id=""nowPlaying"" class=""now-playing"">
        <div class=""music-icon"">♪</div>
        <div class=""song-info"">
            <div class=""album-art"" id=""albumArt"">
                🎵
            </div>
            <div class=""text-info"">
                <div class=""song-title"" id=""songTitle"">No song playing</div>
                <div class=""song-artist"" id=""songArtist"">Connect Spotify to get started</div>
                <div class=""requested-by"" id=""requestedBy""></div>
            </div>
        </div>
        <div class=""platform-badge"" id=""platformBadge"">EZStreamer</div>
    </div>

    <script>
        let currentData = null;

        async function updateNowPlaying() {
            try {
                const response = await fetch('./now_playing.json?t=' + Date.now());
                const data = await response.json();
                
                // Only update if data has changed
                if (JSON.stringify(data) === JSON.stringify(currentData)) {
                    return;
                }
                
                currentData = data;
                
                const nowPlayingElement = document.getElementById('nowPlaying');
                const songTitle = document.getElementById('songTitle');
                const songArtist = document.getElementById('songArtist');
                const requestedBy = document.getElementById('requestedBy');
                const albumArt = document.getElementById('albumArt');
                const platformBadge = document.getElementById('platformBadge');
                
                if (data.isPlaying && data.title) {
                    // Update content
                    songTitle.textContent = data.title;
                    songArtist.textContent = data.artist;
                    requestedBy.textContent = data.requestedBy ? `Requested by ${data.requestedBy}` : '';
                    platformBadge.textContent = data.platform || 'EZStreamer';
                    
                    // Update album art
                    if (data.albumArt) {
                        albumArt.innerHTML = `<img src=""${data.albumArt}"" alt=""Album Art"">`;
                    } else {
                        albumArt.innerHTML = '🎵';
                    }
                    
                    // Show the overlay
                    nowPlayingElement.classList.add('visible');
                } else {
                    // Hide the overlay when no song is playing
                    nowPlayingElement.classList.remove('visible');
                }
            } catch (error) {
                console.error('Error updating now playing:', error);
                // Hide overlay on error
                document.getElementById('nowPlaying').classList.remove('visible');
            }
        }

        // Update every 2 seconds
        setInterval(updateNowPlaying, 2000);
        
        // Initial update
        updateNowPlaying();
    </script>
</body>
</html>";

                File.WriteAllText(_overlayHtmlPath, htmlContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating default overlay HTML: {ex.Message}");
            }
        }

        public void CreateCustomOverlay(string theme)
        {
            try
            {
                string customHtmlPath = Path.Combine(_overlayFolderPath, $"overlay_{theme.ToLower()}.html");
                
                string htmlContent = theme.ToLower() switch
                {
                    "minimal" => CreateMinimalOverlayHtml(),
                    "neon" => CreateNeonOverlayHtml(),
                    "classic" => CreateClassicOverlayHtml(),
                    _ => CreateDefaultOverlayContent()
                };

                File.WriteAllText(customHtmlPath, htmlContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating custom overlay: {ex.Message}");
            }
        }

        private string CreateMinimalOverlayHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { 
            margin: 0; 
            font-family: Arial, sans-serif; 
            background: transparent; 
            color: white; 
        }
        .minimal-overlay { 
            background: rgba(0, 0, 0, 0.7); 
            padding: 10px 20px; 
            border-radius: 25px; 
            display: inline-block;
            transition: opacity 0.3s;
        }
        .song-title { font-weight: bold; font-size: 14px; }
        .song-artist { font-size: 12px; opacity: 0.8; }
    </style>
</head>
<body>
    <div id=""overlay"" class=""minimal-overlay"">
        <div class=""song-title"" id=""title"">No song playing</div>
        <div class=""song-artist"" id=""artist""></div>
    </div>
    <script>
        setInterval(async () => {
            try {
                const response = await fetch('./now_playing.json?t=' + Date.now());
                const data = await response.json();
                document.getElementById('title').textContent = data.title || 'No song playing';
                document.getElementById('artist').textContent = data.artist || '';
                document.getElementById('overlay').style.opacity = data.isPlaying ? '1' : '0';
            } catch (e) {
                document.getElementById('overlay').style.opacity = '0';
            }
        }, 2000);
    </script>
</body>
</html>";
        }

        private string CreateNeonOverlayHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { 
            margin: 0; 
            font-family: 'Courier New', monospace; 
            background: transparent; 
            color: #00ff41; 
        }
        .neon-overlay { 
            border: 2px solid #00ff41; 
            border-radius: 10px; 
            padding: 15px; 
            background: rgba(0, 0, 0, 0.8);
            box-shadow: 0 0 20px #00ff41;
            animation: neon-glow 2s infinite alternate;
        }
        @keyframes neon-glow {
            from { box-shadow: 0 0 20px #00ff41; }
            to { box-shadow: 0 0 30px #00ff41, 0 0 40px #00ff41; }
        }
        .song-title { font-size: 16px; text-shadow: 0 0 10px #00ff41; }
        .song-artist { font-size: 14px; opacity: 0.8; }
    </style>
</head>
<body>
    <div id=""overlay"" class=""neon-overlay"">
        <div class=""song-title"" id=""title"">♪ No song playing ♪</div>
        <div class=""song-artist"" id=""artist""></div>
    </div>
    <script>
        setInterval(async () => {
            try {
                const response = await fetch('./now_playing.json?t=' + Date.now());
                const data = await response.json();
                document.getElementById('title').textContent = data.title ? `♪ ${data.title} ♪` : '♪ No song playing ♪';
                document.getElementById('artist').textContent = data.artist || '';
                document.getElementById('overlay').style.opacity = data.isPlaying ? '1' : '0';
            } catch (e) {
                document.getElementById('overlay').style.opacity = '0';
            }
        }, 2000);
    </script>
</body>
</html>";
        }

        private string CreateClassicOverlayHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { 
            margin: 0; 
            font-family: 'Times New Roman', serif; 
            background: transparent; 
            color: white; 
        }
        .classic-overlay { 
            background: linear-gradient(to bottom, #8B4513, #D2691E); 
            border: 3px solid #DAA520; 
            border-radius: 15px; 
            padding: 20px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.5);
        }
        .song-title { font-size: 18px; font-weight: bold; color: #FFD700; }
        .song-artist { font-size: 14px; color: #F5DEB3; }
        .requested-by { font-size: 12px; font-style: italic; color: #DDD; }
    </style>
</head>
<body>
    <div id=""overlay"" class=""classic-overlay"">
        <div class=""song-title"" id=""title"">Now Playing</div>
        <div class=""song-artist"" id=""artist"">Connect your music service</div>
        <div class=""requested-by"" id=""requester""></div>
    </div>
    <script>
        setInterval(async () => {
            try {
                const response = await fetch('./now_playing.json?t=' + Date.now());
                const data = await response.json();
                document.getElementById('title').textContent = data.title || 'Now Playing';
                document.getElementById('artist').textContent = data.artist || 'Connect your music service';
                document.getElementById('requester').textContent = data.requestedBy ? `Requested by ${data.requestedBy}` : '';
                document.getElementById('overlay').style.opacity = data.isPlaying ? '1' : '0.7';
            } catch (e) {
                document.getElementById('overlay').style.opacity = '0.7';
            }
        }, 2000);
    </script>
</body>
</html>";
        }

        private string CreateDefaultOverlayContent()
        {
            return File.ReadAllText(_overlayHtmlPath);
        }

        public string GetOverlayInstructions()
        {
            return $@"OBS Setup Instructions:

1. Add a Browser Source to your scene
2. Set the URL to: file:///{_overlayHtmlPath.Replace("\\", "/")}
3. Set Width: 450, Height: 150
4. Check 'Shutdown source when not visible'
5. Check 'Refresh browser when scene becomes active'

The overlay will automatically update when songs change!

Available overlay themes:
- overlay.html (default with animations)
- overlay_minimal.html (simple text only)
- overlay_neon.html (retro neon style)
- overlay_classic.html (elegant classic style)

JSON data is also available at: {_nowPlayingJsonPath}";
        }
    }
}
