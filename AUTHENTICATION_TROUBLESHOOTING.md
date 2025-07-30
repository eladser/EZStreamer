# 🔧 EZStreamer Authentication Troubleshooting Guide

## **Current Issues & Solutions**

### **🔴 Issue 1: Twitch Scope Error**

**Error:** `"navigation failed : connection aborted into navigation failed : Unknown into -> {"status":400,"message":"invalid scope requested: 'chat:read+chat:edit+channel:manage:broadcast+channel:read:redemptions+user:read:email'}"`

**✅ FIXED:** Updated `TwitchAuthWindow.xaml.cs` to use space-separated scopes instead of plus signs.

**What was wrong:** Twitch OAuth expects scopes to be separated by spaces, not plus signs.

**Old scopes:** `"chat:read+chat:edit+channel:manage:broadcast+channel:read:redemptions+user:read:email"`

**Fixed scopes:** `"chat:read chat:edit channel:manage:broadcast channel:read:redemptions user:read:email"`

---

### **🟡 Issue 2: Spotify Redirect URI Security Warning**

**Warning:** `"This redirect URI is not secure. Learn more here."`

**✅ EXPLANATION:** This is expected behavior during development. Spotify shows this warning for localhost HTTP URLs but still allows them for development purposes.

**Solution Options:**

#### **Option A: Ignore the Warning (Recommended for Development)**
- The warning doesn't prevent authentication
- Click "Continue" or "I Understand" in Spotify's interface
- This is normal for development environments

#### **Option B: Use HTTPS for Production**
- For production deployments, use HTTPS redirect URLs
- Example: `https://yourdomain.com/auth/spotify/callback`
- This requires a proper domain and SSL certificate

---

## **🚀 Step-by-Step Setup Guide**

### **1. Configure Twitch Application**

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
   - Keep the **Client Secret** secure (not needed for this OAuth flow)

### **2. Configure Spotify Application**

1. **Go to Spotify Developer Dashboard:**
   - Visit: https://developer.spotify.com/dashboard
   - Login with your Spotify account

2. **Create New App:**
   - Click "Create App"
   - App name: `EZStreamer`
   - App description: `Stream overlay and music integration`
   - Website: `http://localhost:3000` (or leave blank)
   - Redirect URIs: `http://localhost:3000/auth/spotify/callback`
   - Which API/SDKs are you planning to use: `Web API`

3. **Get Client ID:**
   - After creating the app, copy the **Client ID**
   - Click "Settings" to view/edit your app details

### **3. Configure EZStreamer**

1. **Open EZStreamer Application**
2. **Go to Settings Tab**
3. **Expand API Credentials Sections:**
   - Enter your **Twitch Client ID**
   - Enter your **Spotify Client ID**
   - Click **Save** for each

### **4. Test Authentication**

#### **For Twitch:**
1. Click **"Test Connection"** next to Twitch
2. Choose authentication method:
   - **Web Authentication:** Uses browser (should work now with the fix)
   - **Manual Token:** Uses external token generator (always reliable)

#### **For Spotify:**
1. Click **"Test Connection"** next to Spotify
2. When you see the security warning, click **"Continue"** or **"I Understand"**
3. Authorize the application

---

## **🎯 Manual Token Method (Recommended Backup)**

If web authentication still has issues, use the manual token method:

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

### **Issue: "Invalid Client ID"**
**Solution:**
1. Double-check the Client ID from your developer console
2. Make sure there are no extra spaces or characters
3. Verify the redirect URI matches exactly

### **Issue: Authentication Window Stuck Loading**
**Solution:**
1. Close the authentication window
2. Use the "Manual Token" option instead
3. This bypasses all browser-related issues

---

## **🛡️ Security Notes**

1. **Never share your Client Secret** (only Client ID is needed)
2. **Tokens expire** - you'll need to re-authenticate periodically
3. **For production apps**, use HTTPS redirect URLs
4. **Keep tokens secure** - don't share them publicly

---

## **✅ Verification Checklist**

- [ ] Twitch app created with correct redirect URI
- [ ] Spotify app created with correct redirect URI  
- [ ] Client IDs entered in EZStreamer settings
- [ ] Settings saved successfully
- [ ] WebView2 runtime installed
- [ ] Can access Twitch authentication (no scope error)
- [ ] Can access Spotify authentication (ignoring security warning)
- [ ] Tokens received successfully

---

## **🆘 Still Having Issues?**

1. **Check Windows Event Viewer** for WebView2 errors
2. **Try manual token authentication** as a workaround
3. **Restart EZStreamer** after making configuration changes
4. **Update to the latest version** of EZStreamer
5. **Create an issue** in the GitHub repository with:
   - Error messages
   - Steps to reproduce
   - Your configuration (without sensitive tokens)

---

**The fixes have been applied to your repository. Pull the latest changes and the Twitch authentication should work properly now!** 🎉
