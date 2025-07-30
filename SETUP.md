# Development Setup Guide

This guide will help you set up the development environment and configure API keys for EZStreamer.

## Prerequisites

- Visual Studio 2022 or Visual Studio Code
- .NET 7 SDK
- Git
- Twitch Developer Account
- Spotify Developer Account

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/eladser/EZStreamer.git
   cd EZStreamer
   ```

2. **Open the solution**
   ```bash
   # Visual Studio
   start EZStreamer.sln
   
   # VS Code
   code .
   ```

3. **Restore NuGet packages**
   ```bash
   dotnet restore
   ```

## API Configuration

### Twitch Setup

1. Go to the [Twitch Developers Console](https://dev.twitch.tv/console)
2. Create a new application with these settings:
   - **Name**: EZStreamer (or your preferred name)
   - **OAuth Redirect URLs**: `http://localhost:3000/auth/twitch/callback`
   - **Category**: Application Integration

3. Copy your **Client ID** and update the following file:
   ```
   src/EZStreamer/Views/TwitchAuthWindow.xaml.cs
   ```
   Replace `your_twitch_client_id` with your actual Client ID.

### Spotify Setup

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Create a new app with these settings:
   - **App name**: EZStreamer
   - **App description**: Twitch streamer helper app
   - **Redirect URIs**: `http://localhost:3000/auth/spotify/callback`

3. Copy your **Client ID** and update the following file:
   ```
   src/EZStreamer/Views/SpotifyAuthWindow.xaml.cs
   ```
   Replace `your_spotify_client_id` with your actual Client ID.

## Building the Application

### Debug Build
```bash
dotnet build --configuration Debug
```

### Release Build
```bash
dotnet build --configuration Release
```

### Run the Application
```bash
dotnet run --project src/EZStreamer
```

## Project Structure

```
EZStreamer/
├── src/EZStreamer/
│   ├── Views/                 # UI Windows and Views
│   │   ├── MainWindow.xaml    # Main application window
│   │   ├── TwitchAuthWindow.xaml
│   │   └── SpotifyAuthWindow.xaml
│   ├── Services/              # Business logic services
│   │   ├── TwitchService.cs   # Twitch integration
│   │   ├── SpotifyService.cs  # Spotify integration
│   │   ├── SongRequestService.cs
│   │   ├── OverlayService.cs  # OBS overlay generation
│   │   └── SettingsService.cs # Configuration management
│   ├── Models/                # Data models
│   │   └── Models.cs          # Song, Settings, and other models
│   ├── App.xaml               # Application entry point
│   └── EZStreamer.csproj      # Project file
├── README.md
├── SETUP.md                   # This file
└── EZStreamer.sln            # Solution file
```

## Key Features

- **Twitch Integration**: Chat commands and channel point redemptions
- **Spotify Integration**: Music search, playback control, and queue management
- **OBS Overlays**: Automatic generation of "Now Playing" overlays
- **Modern UI**: Material Design with dark theme
- **Secure Storage**: Encrypted token storage using Windows Data Protection

## Debugging Tips

1. **Enable debug output** by checking the Output window in Visual Studio
2. **Test Twitch connection** by sending `!songrequest test` in your chat
3. **Check Spotify permissions** - ensure you have Spotify Premium for playback control
4. **Verify overlay files** are created in `%AppData%/EZStreamer/Overlay/`

## Common Issues

### Twitch Authentication Fails
- Verify your Client ID is correct
- Check that the redirect URI matches exactly: `http://localhost:3000/auth/twitch/callback`
- Ensure your Twitch app has the correct scopes

### Spotify Playback Doesn't Work
- Spotify Premium is required for playback control
- Make sure Spotify is running and has an active device
- Check that the Spotify app permissions include playback control

### OBS Overlay Not Updating
- Verify the overlay file path: `%AppData%/EZStreamer/Overlay/overlay.html`
- Check that the browser source is set to refresh automatically
- Ensure the JSON file is being updated: `%AppData%/EZStreamer/Overlay/now_playing.json`

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a pull request

## Building for Distribution

### Prerequisites for Release
- Code signing certificate (optional but recommended)
- Inno Setup or MSIX packaging tools

### Release Process
1. Update version numbers in `EZStreamer.csproj`
2. Build in Release configuration
3. Create installer using preferred method
4. Test on clean Windows machine

## Support

If you encounter issues:
1. Check the [Issues](https://github.com/eladser/EZStreamer/issues) page
2. Create a new issue with detailed information
3. Include log files from `%AppData%/EZStreamer/Logs/` if available

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
