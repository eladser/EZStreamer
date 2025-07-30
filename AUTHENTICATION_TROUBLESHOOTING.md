# 🔧 EZStreamer Authentication Troubleshooting Guide

## **Current Issues & Solutions**

### **🔴 Issue 1: Twitch Scope Error**

**Error:** `"navigation failed : connection aborted into navigation failed : Unknown into -> {"status":400,"message":"invalid scope requested: 'chat:read+chat:edit+channel:manage:broadcast+channel:read:redemptions+user:read:email'}"`

**✅ FIXED:** Updated `TwitchAuthWindow.xaml.cs` to use space-separated scopes instead of plus signs.

**What was wrong:** Twitch OAuth expects scopes to be separated by spaces, not plus signs.

**Old scopes:** `"chat:read+chat:edit+channel:manage:broadcast+channel:read:redemptions+user:read:email"`

**Fixed scopes:** `"chat:read chat:edit channel:manage:broadcast channel:read:redemptions user:read:email"`

---

### **🔴 Issue 2: Spotify HTTPS Redirect URI Requirement**

**Error:** `"This redirect URI is not secure. Learn more here."`

**✅ FIXED:** Updated Spotify authentication to use HTTPS redirect URI via GitHub Pages.

**What was wrong:** Spotify now requires HTTPS redirect URIs for security, even in development.

**Old redirect URI:** `http://localhost:3000/auth/spotify/callback`

**New redirect URI:** `https://eladser.github.io/ezstreamer/auth/spotify/callback`

---

## **🚀 Complete Setup Guide**

### **1. Enable GitHub Pages (Required for Spotify)**

1. **Go to your repository Settings**
2. **Scroll to "Pages" section**
3. **Configure GitHub Pages:**
   - Source: "Deploy from a branch"
   - Branch: "main"
   - Folder: "/docs"
   - Click "Save"

4. **Verify the callback page is accessible:**
   - Visit: https://eladser.github.io/ezstreamer/auth/spotify/callback.html
   - You should see the EZStreamer callback page

### **2. Configure Twitch Application**

1. **Go to Twitch Developer Console:**
   - Visit: https://dev.twitch.tv/console
   - Login with your Twitch account

2. **Create New Application:**
   - Click "Register Your Application"
   - Name: `EZStreamer` (or your preferred name)
   - OAuth Redirect URLs: `http://localhost:3000/auth/twitch/callback`
   - Category: `Broadcasting Suite`

3. **Get Client ID:**
   - After creating the app, copy the **Client ID**

### **3. Configure Spotify Application**

1. **Go to Spotify Developer Dashboard:**
   - Visit: https://developer.spotify.com/dashboard
   - Login with your Spotify account

2. **Create New App:**
   - Click "Create App"
   - App name: `EZStreamer`
   - App description: `Stream overlay and music integration`
   - Website: `https://eladser.github.io/ezstreamer/`
   - **Redirect URIs:** `https://eladser.github.io/ezstreamer/auth/spotify/callback`
   - Which API/SDKs: `Web API`

3. **Get Client ID:**
   - After creating the app, copy the **Client ID**

### **4. Configure EZStreamer**

1. **Pull the latest code** from the repository
2. **Build and run** EZStreamer
3. **Go to Settings Tab**
4. **Configure API Credentials:**
   - Enter your **Twitch Client ID**
   - Enter your **Spotify Client ID**
   - Click **Save** for each

### **5. Test Authentication**

#### **For Twitch:**
1. Click **"Test Connection"** next to Twitch
2. Choose authentication method:
   - **Web Authentication:** Should work with the scope fix
   - **Manual Token:** Always reliable backup

#### **For Spotify:**
1. Click **"Test Connection"** next to Spotify
2. **No more security warnings!** The HTTPS redirect URI resolves this
3. Follow the OAuth flow normally

---

## **🎯 How the New Spotify Flow Works**

1. **EZStreamer opens** the Spotify authorization URL
2. **User authorizes** the application on Spotify
3. **Spotify redirects** to the HTTPS callback page on GitHub Pages
4. **Callback page extracts** the access token from the URL
5. **User copies** the token from the callback page
6. **User enters** the token manually in EZStreamer

This approach solves the HTTPS requirement while keeping the authentication flow simple and secure.

---

## **🎯 Manual Token Method (Backup Option)**

If web authentication has any issues, use the manual token method:

### **Twitch Manual Tokens:**
1. Go to: https://twitchtokengenerator.com
2. Select scopes:
   - `chat:read`
   - `chat:edit`
   - `channel:manage:broadcast`
   - `channel:read:redemptions`
   - `user:read:email`
3. Generate token
4. In EZStreamer, choose "Manual Token" and paste it

### **Spotify Manual Tokens:**
1. Go to: https://developer.spotify.com/console/get-current-user/
2. Click "Get Token"
3. Select scopes:
   - `user-read-currently-playing`
   - `user-read-playback-state`
   - `user-modify-playback-state`
   - `playlist-read-private`
4. Copy the token
5. In EZStreamer, choose "Manual Token" and paste it

---

## **🔍 Common Issues & Solutions**

### **Issue: GitHub Pages Not Working**
**Solution:**
1. Make sure GitHub Pages is enabled in repository settings
2. Use the `/docs` folder as the source
3. Wait a few minutes for GitHub Pages to deploy
4. Test the callback URL in your browser

### **Issue: "Invalid Redirect URI"**
**Solution:**
1. Make sure your Spotify app redirect URI exactly matches:
   ```
   https://eladser.github.io/ezstreamer/auth/spotify/callback
   ```
2. No trailing slashes or extra characters
3. Case-sensitive match required

### **Issue: WebView2 Not Working**
**Solution:**
1. Install Microsoft WebView2 Runtime
2. Download from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/
3. Restart EZStreamer after installation

### **Issue: "Client ID Not Configured"**
**Solution:**
1. Make sure you've entered the Client ID in Settings
2. Click "Save" after entering
3. Restart the application if needed

---

## **🛡️ Security Notes**

1. **HTTPS is now required** for Spotify (handled via GitHub Pages)
2. **Never share your Client Secret** (only Client ID is needed)
3. **Tokens expire** - you'll need to re-authenticate periodically
4. **GitHub Pages is public** - this is fine for OAuth callbacks
5. **Keep tokens secure** - don't share them publicly

---

## **✅ Updated Verification Checklist**

- [ ] GitHub Pages enabled and working
- [ ] Callback page accessible at: https://eladser.github.io/ezstreamer/auth/spotify/callback.html
- [ ] Twitch app created with: `http://localhost:3000/auth/twitch/callback`
- [ ] Spotify app created with: `https://eladser.github.io/ezstreamer/auth/spotify/callback`
- [ ] Client IDs entered in EZStreamer settings
- [ ] Settings saved successfully
- [ ] WebView2 runtime installed
- [ ] Latest EZStreamer code pulled and built
- [ ] Twitch authentication works (no scope error)
- [ ] Spotify authentication works (no HTTPS warning)
- [ ] Tokens received successfully

---

## **🆘 Still Having Issues?**

1. **Check the callback page** in your browser first
2. **Verify GitHub Pages** is properly configured
3. **Double-check redirect URIs** in your app configurations
4. **Try manual token authentication** as a workaround
5. **Create an issue** in the GitHub repository with:
   - Error messages
   - Steps to reproduce
   - Your configuration (without sensitive tokens)

---

**Both authentication issues have been resolved! The HTTPS redirect URI via GitHub Pages solves the Spotify security requirement, and the scope format fix resolves the Twitch error.** 🎉

## **Quick Summary of Changes:**

1. **Fixed Twitch scopes** - Changed from `+` to space separators
2. **Added HTTPS redirect** - Uses GitHub Pages for Spotify OAuth
3. **Created callback page** - Handles token extraction and display
4. **Updated documentation** - Complete setup guide with new URLs

Pull the latest changes and follow the setup guide above! 🚀
