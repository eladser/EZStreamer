# EZStreamer GitHub Pages

This directory contains the GitHub Pages website for EZStreamer OAuth callbacks.

## OAuth Callback URLs

### Spotify
- **URL:** https://eladser.github.io/ezstreamer/auth/spotify/callback
- **File:** `auth/spotify/callback.html`
- **Purpose:** Handles Spotify OAuth callback with HTTPS support

## Setup Instructions

1. **Enable GitHub Pages** for this repository:
   - Go to repository Settings
   - Scroll down to "Pages" section
   - Set source to "Deploy from a branch"
   - Choose "main" branch and "/docs" folder
   - Save the settings

2. **Configure your Spotify app** to use:
   ```
   https://eladser.github.io/ezstreamer/auth/spotify/callback
   ```

3. **Test the callback URL** by visiting:
   ```
   https://eladser.github.io/ezstreamer/auth/spotify/callback.html
   ```

The callback page will handle the OAuth flow and provide instructions for manual token entry if needed.
