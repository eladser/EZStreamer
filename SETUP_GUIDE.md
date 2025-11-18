# EZStreamer - Complete Setup Guide

Welcome to EZStreamer! This guide will walk you through setting up your song request system for Twitch streaming.

## 📋 Prerequisites

- Windows 10/11
- .NET 8.0 Runtime
- OBS Studio (optional, for overlay)
- Twitch account
- Spotify account (for Spotify integration)
- Google account (for YouTube integration)

## 🚀 Quick Start

### Step 1: Initial Setup

1. Launch EZStreamer
2. The app will create its configuration folder at `%APPDATA%/EZStreamer/`
3. Navigate to the **Settings** tab

### Step 2: Configure Spotify (Recommended)

#### A. Create Spotify App

1. Go to https://developer.spotify.com/dashboard
2. Log in with your Spotify account
3. Click **"Create app"**
4. Fill in the form:
   - **App name:** EZStreamer (or your choice)
   - **App description:** Song request system for streaming
   - **Redirect URI:** `http://localhost:8888/callback` ⚠️ **IMPORTANT**
   - **Which API/SDKs are you planning to use?** Select "Web API"
5. Accept terms and click **Create**
6. You'll see your **Client ID** and **Client Secret** (click "View client secret")

#### B. Configure in EZStreamer

1. In EZStreamer, go to **Settings** tab
2. Expand **"Spotify API Credentials"** section
3. Paste your **Client ID**
4. Paste your **Client Secret**
5. Click **Save**
6. Click **"Test Connection"** or **"Connect Spotify"**
7. A browser window will open - log in to Spotify and authorize the app
8. You should see "Successfully connected to Spotify!" message

**Troubleshooting:**
- If you see "Failed to start callback server", ensure port 8888 is not in use
- If authorization fails, verify the redirect URI is exactly `http://localhost:8888/callback`
- Check that both Client ID and Secret are correct (no extra spaces)

### Step 3: Configure YouTube (Optional)

#### A. Create YouTube API Key

1. Go to https://console.cloud.google.com/
2. Create a new project (or select existing):
   - Click the project dropdown at the top
   - Click **"New Project"**
   - Name it "EZStreamer" and click **Create**
3. Enable YouTube Data API v3:
   - Go to **"APIs & Services"** → **"Library"**
   - Search for "YouTube Data API v3"
   - Click on it and press **"Enable"**
4. Create API Key:
   - Go to **"APIs & Services"** → **"Credentials"**
   - Click **"Create Credentials"** → **"API Key"**
   - Copy the generated API key
   - (Optional) Click **"Restrict Key"** to limit usage to YouTube Data API v3

#### B. Configure in EZStreamer

1. In EZStreamer, go to **Settings** tab
2. Expand **"YouTube API Configuration"** section
3. Paste your **API Key**
4. Click **Save**
5. Click **"Test Connection"** to validate

**Note:** YouTube has daily quota limits (10,000 units/day for free tier). Each search uses ~100 units.

### Step 4: Configure Twitch

#### A. Create Twitch Application

1. Go to https://dev.twitch.tv/console/apps
2. Log in with your Twitch account
3. Click **"Register Your Application"**
4. Fill in:
   - **Name:** EZStreamer
   - **OAuth Redirect URLs:** `http://localhost` (for now)
   - **Category:** Application Integration
5. Click **Create**
6. Copy your **Client ID**
7. Click **"New Secret"** and copy the **Client Secret**

#### B. Configure in EZStreamer

1. In EZStreamer, go to **Settings** tab
2. Expand **"Twitch Configuration"** section
3. Enter your **Twitch Channel Name** (without the #)
4. Paste **Client ID** and **Client Secret**
5. Click **Save**
6. Click **"Connect to Twitch"**
7. Authorize the application when prompted

### Step 5: Configure OBS Integration (Optional)

#### A. Install OBS WebSocket

Modern OBS Studio (28.0+) has WebSocket built-in. For older versions:
1. Download from https://github.com/obs-websocket-community/obs-websocket/releases
2. Install the plugin
3. Restart OBS

#### B. Configure WebSocket in OBS

1. In OBS, go to **Tools** → **WebSocket Server Settings**
2. Check **"Enable WebSocket server"**
3. Note the **Server Port** (default: 4455)
4. Set a **Server Password** (recommended)
5. Click **OK**

#### C. Configure in EZStreamer

1. In EZStreamer, go to **Settings** tab
2. Expand **"OBS Settings"** section
3. Enter:
   - **Server IP:** localhost (if OBS is on same PC)
   - **Port:** 4455 (or your configured port)
   - **Password:** (the password you set)
4. Click **Save**
5. Click **"Test Connection"**

### Step 6: Set Up Overlay (Optional)

#### A. EZStreamer Generates Overlay Files

EZStreamer automatically creates overlay files at:
```
%APPDATA%/EZStreamer/Overlay/
├── now_playing.json  (current song data)
└── overlay.html      (browser source HTML)
```

#### B. Add to OBS

1. In OBS, add a new **Browser Source**
2. Check **"Local file"**
3. Click **Browse** and navigate to:
   ```
   C:\Users\[YourName]\AppData\Roaming\EZStreamer\Overlay\overlay.html
   ```
4. Set dimensions:
   - **Width:** 450
   - **Height:** 150
5. Check **"Refresh browser when scene becomes active"** (recommended)
6. Click **OK**
7. Position and resize as needed

## 🎵 Using EZStreamer

### For Viewers (Song Requests)

Viewers can request songs in Twitch chat using:

```
!songrequest Bohemian Rhapsody
!sr Never Gonna Give You Up
```

The command searches your preferred music source (Spotify or YouTube) and adds the song to the queue.

### For Streamers

#### Now Playing Tab
- See the currently playing song
- View elapsed time and progress
- Skip to next song
- Pause/Resume playback

#### Song Queue Tab
- View all queued requests
- Remove specific songs
- Clear entire queue
- See request history
- Filter by platform (Spotify/YouTube)

#### Streaming Tools Tab
- Update stream title and category
- Control OBS scenes
- Refresh overlay
- Export queue as playlist

#### Settings Tab
- Configure all API credentials
- Set preferred music source
- Adjust queue limits
- Enable/disable features
- Auto-connect on startup

## 🎛️ Advanced Configuration

### Preferred Music Source

Choose between Spotify and YouTube:
1. Go to **Settings** → **Music Source Preference**
2. Select **Spotify** or **YouTube**
3. Song requests will search the selected platform first

**Recommendations:**
- **Spotify:** Better for studio recordings, higher quality
- **YouTube:** Wider selection, includes remixes and covers

### Queue Management

Configure queue behavior:
- **Max Queue Length:** Limit how many songs can be queued (default: 10)
- **Auto-Play Next:** Automatically play next song when current ends
- **Request Cooldown:** Seconds users must wait between requests
- **Min/Max Duration:** Filter songs by length

### Chat Commands

Customize the song request command:
1. Go to **Settings** → **Chat Command**
2. Change from `!songrequest` to your preferred command
3. Examples: `!sr`, `!music`, `!song`

## 🔒 Security & Privacy

### API Credentials
- All tokens are encrypted using Windows DPAPI
- Credentials stored in `%APPDATA%/EZStreamer/settings.json` (encrypted)
- Never share your API credentials or tokens
- Use the "Hide" buttons when streaming to avoid leaking keys

### Best Practices
1. Use your own API credentials (don't use defaults)
2. Set passwords on OBS WebSocket
3. Don't share your settings.json file
4. Regularly rotate Spotify/Twitch secrets if compromised
5. Limit API key usage to required APIs only

## 🐛 Troubleshooting

### Spotify Connection Issues

**"Failed to start callback server"**
- Port 8888 is in use by another application
- Close other applications or change the port in Windows Firewall

**"Invalid redirect URI"**
- Ensure redirect URI in Spotify dashboard is exactly: `http://localhost:8888/callback`
- No HTTPS, no trailing slash, correct port

**"Token expired"**
- This should auto-refresh, but you can reconnect manually
- Go to Settings → Spotify → Test Connection

### YouTube API Issues

**"API key not configured"**
- Enter your API key in Settings → YouTube API Configuration

**"API quota exceeded"**
- You've hit the free tier limit (10,000 units/day)
- Wait until quota resets (midnight Pacific Time)
- Or upgrade to paid tier in Google Cloud Console

**"Search returns no results"**
- Check API key is valid
- Ensure YouTube Data API v3 is enabled
- Verify no restrictions on the API key

### Twitch Connection Issues

**"Failed to connect to chat"**
- Verify channel name is correct (no # symbol)
- Check Twitch credentials
- Ensure you're not banned from the channel

**"Channel points not working"**
- Channel points integration requires additional setup
- Currently in development

### OBS Integration Issues

**"Failed to connect to OBS"**
- Ensure OBS is running
- Check WebSocket server is enabled in OBS
- Verify IP, port, and password are correct
- Try without password first to test connection

## 📊 Resource Usage

### API Rate Limits

**Spotify:**
- Rate limits are per app, not per user
- Generally very generous (hundreds of requests per second)
- Automatically handles rate limiting with retries

**YouTube:**
- 10,000 quota units per day (free tier)
- Each search: ~100 units
- Each video details fetch: ~1 unit
- Approximately 100 searches per day limit

**Twitch:**
- IRC has no practical rate limits for reading
- API calls: 800 requests per minute

### Performance

**CPU:** Low (~1-2% when idle, ~5-10% when playing)
**RAM:** ~100-150 MB
**Network:** Minimal (<1 MB/hour for API calls)
**Storage:** ~10 MB for application, ~1 MB for settings

## 🎯 Tips for Best Experience

1. **Test before going live**
   - Test all connections before stream
   - Do a trial song request to yourself
   - Verify overlay appears correctly

2. **Have backup plans**
   - Keep Spotify open in case EZStreamer has issues
   - Have a backup playlist ready
   - Know how to manually control playback

3. **Moderate requests**
   - Set reasonable queue limits
   - Use cooldowns to prevent spam
   - Consider min/max duration filters

4. **Engage with chat**
   - Thank users for song requests
   - Let chat vote to skip songs
   - Create themed request sessions

## 📞 Support & Community

**Issues & Bugs:**
- Open issues on GitHub repository
- Include error messages and logs
- Describe steps to reproduce

**Feature Requests:**
- Create enhancement requests on GitHub
- Describe use case and benefits
- Vote on existing requests

**Logs & Debugging:**
- Check Windows Event Viewer for crashes
- Look at Debug output in Visual Studio if developing
- Settings file location: `%APPDATA%\EZStreamer\settings.json`

## 🙏 Credits

EZStreamer uses these amazing technologies:
- **.NET 8.0** - Application framework
- **WPF & Material Design** - Beautiful UI
- **WebView2** - Modern web browser control
- **Spotify Web API** - Music playback
- **YouTube Data API v3** - Video search
- **Twitch API** - Chat integration
- **OBS WebSocket** - Scene control

---

**Happy Streaming! 🎮🎵**

If you find EZStreamer useful, please star the repository and share with fellow streamers!
