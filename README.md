# EZStreamer 🎵

A powerful and easy-to-use Windows desktop app for Twitch streamers to make their streams more interactive with music requests!

## ✨ What's New in v2.0

**Major improvements have been made!** See [IMPROVEMENTS.md](IMPROVEMENTS.md) for detailed changelog.

### 🚀 Key Improvements:
- ✅ **Simplified Spotify Authentication** - No more admin privileges required!
- ✅ **Automatic Token Refresh** - Never manually re-authenticate again
- ✅ **Real YouTube Integration** - Actual YouTube Data API v3 support
- ✅ **Better Reliability** - Removed 500+ lines of problematic certificate code
- ✅ **Improved Performance** - Faster, cleaner, more responsive

## What does EZStreamer do?

EZStreamer helps you manage your Twitch stream by letting your viewers request songs and controlling your stream settings, all from one easy-to-use application.

**Key Features:**
- ✨ **Viewer Song Requests** - Through Twitch chat or channel points
- 🎵 **Dual Platform Support** - Spotify AND YouTube Music
- 🔄 **Automatic Token Management** - No more expired sessions!
- 🎨 **Beautiful OBS Overlays** - Show what's currently playing
- 🌙 **Modern Dark Theme** - Easy on the eyes
- 🔌 **OBS Integration** - Control scenes and sources
- 📊 **Queue Management** - Full control over song requests
- 🎯 **Easy Setup** - Detailed guides included

## 📋 Quick Start

**For detailed setup instructions, see [SETUP_GUIDE.md](SETUP_GUIDE.md)**

### Basic Steps:
1. Download and run EZStreamer
2. Configure Spotify:
   - Create app at https://developer.spotify.com/dashboard
   - **Important:** Set redirect URI to `http://localhost:8888/callback`
   - Enter credentials in Settings
3. Configure YouTube (optional):
   - Get API key from https://console.cloud.google.com/
   - Enable YouTube Data API v3
   - Enter key in Settings
4. Connect to Twitch
5. Set up OBS overlay (optional)
6. Start accepting song requests!

## Requirements

- **Windows 10/11** - 64-bit
- **.NET 8.0 Runtime** - Will auto-prompt if missing
- **Twitch Account** - For chat integration
- **Spotify Account** - For Spotify integration (Premium not required!)
- **Google Account** - For YouTube integration
- **OBS Studio** - Optional, for overlays

## How Viewers Request Songs

Your viewers can request songs in two ways:

**Chat Command:**
```
!songrequest Bohemian Rhapsody Queen
```

**Channel Points Redemption:**
Set up a custom reward and viewers can redeem it with their song request

## 📚 Documentation

- **[SETUP_GUIDE.md](SETUP_GUIDE.md)** - Complete step-by-step setup instructions
- **[IMPROVEMENTS.md](IMPROVEMENTS.md)** - Detailed changelog and technical improvements
- **Settings Tab** - In-app configuration help

## 🐛 Troubleshooting

### Common Issues:

**Spotify "Failed to start callback server"**
- Ensure port 8888 is available
- Check Windows Firewall settings

**Spotify "Invalid redirect URI"**
- Must be exactly: `http://localhost:8888/callback`
- Update in Spotify dashboard if changed

**YouTube "API quota exceeded"**
- Free tier: 10,000 units/day
- Each search ~100 units
- Resets midnight PT

**More help:** See [SETUP_GUIDE.md](SETUP_GUIDE.md) troubleshooting section

## 🤝 Getting Help

If you run into any issues or have questions:
- Check [SETUP_GUIDE.md](SETUP_GUIDE.md) for detailed help
- Review [IMPROVEMENTS.md](IMPROVEMENTS.md) for recent changes
- Check the [Issues](https://github.com/eladser/EZStreamer/issues) page
- Create a new issue with error logs and details

## 🙏 Contributing

Want to help make EZStreamer better? Contributions are welcome!
- Report bugs with detailed reproduction steps
- Suggest features with use cases
- Submit pull requests with improvements
- Help with documentation

## 📜 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

**Made with ❤️ for the streaming community**

⭐ Star this repo if you find it useful!
