# EZStreamer Setup Wizard - Testing Checklist

## Critical Tests Required

### 1. Build Test
- [ ] Project builds without errors
- [ ] No TypeConverter errors
- [ ] No XAML parsing errors

### 2. Wizard Launch Test
- [ ] App launches successfully
- [ ] Setup Wizard appears on first run (when LastUsedVersion is empty or "1.0.0")
- [ ] Wizard window opens without crashing
- [ ] All text is readable (no yellow/unreadable backgrounds)

### 3. Color Scheme Test
- [ ] Header matches app theme (purple/Material Design primary color)
- [ ] Redirect URI `http://127.0.0.1:8888/callback` is visible in BOLD PURPLE text
- [ ] Warning text uses red/validation error color (readable)
- [ ] All backgrounds match app theme (not random colors)
- [ ] Buttons use default Material Design colors

### 4. Step 1: Spotify Setup
- [ ] Can paste Client ID and Client Secret
- [ ] "Copy to Clipboard" button copies `http://127.0.0.1:8888/callback`
- [ ] "Test Spotify Connection" button works
- [ ] SpotifyAuthWindow opens when testing
- [ ] Success message appears if auth succeeds
- [ ] Error message appears if auth fails

### 5. Step 2: YouTube Setup
- [ ] Can paste YouTube API Key
- [ ] "Test YouTube Connection" validates the API key
- [ ] Success message appears if valid
- [ ] Error message appears if invalid
- [ ] Can skip this step

### 6. Step 3: Twitch Setup
- [ ] Can enter Twitch channel name
- [ ] "Connect to Twitch" opens TwitchAuthWindow
- [ ] Success message appears if connection succeeds
- [ ] Can skip this step

### 7. Navigation Tests
- [ ] "Next" button advances to next step
- [ ] "Back" button goes to previous step
- [ ] "Skip This Step" button skips current step
- [ ] "Finish" button shows completion screen
- [ ] "Start Using EZStreamer" closes wizard

### 8. Settings Tab Integration
- [ ] "Setup Wizard" button in Settings tab works
- [ ] Opens wizard when clicked
- [ ] Redirect URI box in Settings is readable
- [ ] Copy button in Settings works

### 9. Hyperlinks Test
- [ ] Clicking Spotify dashboard link opens browser
- [ ] Clicking Google Cloud Console link opens browser
- [ ] All hyperlinks are clickable and work

## Known Potential Issues to Watch For:

1. **Services might fail to initialize** - If ConfigurationService, SpotifyService, etc. require dependencies
2. **ValidateAPIKeyAsync** might not exist in YouTubeMusicService
3. **SpotifyAuthWindow** or **TwitchAuthWindow** might not open correctly
4. **Theme colors** might not resolve if Material Design theme isn't loaded

## If You Get Errors:

Please share:
1. The exact error message
2. Which button/action caused it
3. The stack trace if available

I'll fix any errors immediately based on your feedback.
