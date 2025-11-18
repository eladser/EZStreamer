using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using EZStreamer.Services;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private string _clientSecret;
        private const string REDIRECT_URI = "http://localhost:8888/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private user-read-email";

        private HttpListener _httpListener;
        private bool _isListening = false;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _serverStarted = false;
        private int _debugCounter = 0;
        private string _pendingNavigationUrl;
        private string _refreshToken;

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            LogDebug("=== SpotifyAuthWindow Constructor Started ===");
            
            _configService = new ConfigurationService();
            var credentials = _configService.GetAPICredentials();
            _clientId = credentials.SpotifyClientId;
            _clientSecret = credentials.SpotifyClientSecret;
            _cancellationTokenSource = new CancellationTokenSource();
            
            LogDebug($"ClientId: {(!string.IsNullOrEmpty(_clientId) ? $"SET ({_clientId.Length} chars)" : "NOT SET")}");
            LogDebug($"ClientSecret: {(!string.IsNullOrEmpty(_clientSecret) ? $"SET ({_clientSecret.Length} chars)" : "NOT SET")}");
            LogDebug($"WebView2 Runtime: {GetWebView2Version()}");

            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start initialization
            LogDebug("Starting initialization...");
            InitializeAuthentication();
        }

        private void LogDebug(string message)
        {
            _debugCounter++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{_debugCounter:D3}] {timestamp} SPOTIFY: {message}";
            Debug.WriteLine(logMessage);
            Console.WriteLine(logMessage); // Also log to console
        }

        private string GetWebView2Version()
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private void InitializeAuthentication()
        {
            try
            {
                LogDebug("=== InitializeAuthentication Started ===");
                
                // Check credentials first
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    LogDebug("❌ Missing credentials, showing configuration dialog");
                    ShowConfigurationNeeded();
                    return;
                }

                LogDebug("✅ Credentials found, starting local HTTP callback server...");
                
                // Start server in background
                Task.Run(async () =>
                {
                    try
                    {
                        LogDebug("Background task started for HTTP callback server");
                        await StartLocalHttpServer();
                        LogDebug("✅ Local HTTP server started successfully");

                        Dispatcher.Invoke(() =>
                        {
                            LogDebug("Dispatcher.Invoke - Initializing WebView...");
                            InitializeWebAuth();
                        });
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Failed to start local HTTP server: {ex.Message}");
                        LogDebug($"Stack trace: {ex.StackTrace}");

                        Dispatcher.Invoke(() =>
                        {
                            ShowError($"Failed to start callback server: {ex.Message}\n\n" +
                                    "Please ensure port 8888 is not in use by another application.");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error in InitializeAuthentication: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Initialization error: {ex.Message}");
            }
        }

        private async Task StartLocalHttpServer()
        {
            try
            {
                LogDebug("=== StartLocalHttpServer Started ===");

                LogDebug("Creating HttpListener for HTTP...");
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:8888/");

                LogDebug("Starting HTTP HttpListener...");
                _httpListener.Start();
                _isListening = true;
                _serverStarted = true;

                LogDebug("✅ HTTP server started successfully on http://localhost:8888/");

                // Start listening for requests
                LogDebug("Starting request listener loop...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var requestCount = 0;
                        while (_isListening && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            LogDebug($"Waiting for HTTP request #{requestCount + 1}...");

                            var context = await GetContextAsync(_httpListener, _cancellationTokenSource.Token);
                            if (context != null)
                            {
                                requestCount++;
                                LogDebug($"✅ Received HTTP request #{requestCount}: {context.Request.Url}");
                                LogDebug($"Request method: {context.Request.HttpMethod}");
                                LogDebug($"User agent: {context.Request.UserAgent}");

                                _ = Task.Run(() => ProcessCallback(context));
                            }
                            else
                            {
                                LogDebug("GetContextAsync returned null - listener may be stopping");
                            }
                        }
                        LogDebug("Request listener loop ended");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Error in HTTP server loop: {ex.Message}");
                        LogDebug($"Stack trace: {ex.StackTrace}");
                    }
                });

                LogDebug("HTTP server setup complete");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Failed to start HTTP server: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }


        private async Task<HttpListenerContext> GetContextAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                LogDebug("GetContextAsync started - waiting for request...");
                var contextTask = listener.GetContextAsync();
                
                using (cancellationToken.Register(() => 
                {
                    try 
                    { 
                        LogDebug("Cancellation requested - stopping listener");
                        listener.Stop(); 
                    } 
                    catch (Exception ex)
                    { 
                        LogDebug($"Error stopping listener: {ex.Message}");
                    }
                }))
                {
                    var context = await contextTask;
                    LogDebug("GetContextAsync completed - request received");
                    return context;
                }
            }
            catch (ObjectDisposedException ex)
            {
                LogDebug($"HttpListener was disposed: {ex.Message}");
                return null;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                LogDebug($"HttpListener operation was aborted: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error getting HTTPS context: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task ProcessCallback(HttpListenerContext context)
        {
            try
            {
                LogDebug($"=== ProcessCallback Started ===");
                LogDebug($"Request URL: {context.Request.Url}");
                LogDebug($"Request method: {context.Request.HttpMethod}");
                LogDebug($"User agent: {context.Request.UserAgent}");
                
                var request = context.Request;
                var response = context.Response;
                
                // Extract query parameters
                var query = HttpUtility.ParseQueryString(request.Url.Query);
                var code = query["code"];
                var error = query["error"];
                var state = query["state"];
                
                LogDebug($"Query parameters extracted:");
                LogDebug($"- code: {(!string.IsNullOrEmpty(code) ? $"RECEIVED ({code.Length} chars)" : "NOT FOUND")}");
                LogDebug($"- error: {error ?? "NONE"}");
                LogDebug($"- state: {(!string.IsNullOrEmpty(state) ? $"RECEIVED ({state.Length} chars)" : "NOT FOUND")}");
                
                string responseHtml;
                
                if (!string.IsNullOrEmpty(error))
                {
                    LogDebug($"❌ OAuth error received: {error}");
                    responseHtml = CreateErrorResponseHtml(error);
                    Dispatcher.Invoke(() => ShowError($"Spotify authorization failed: {error}"));
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    LogDebug("✅ Authorization code received, starting token exchange...");
                    responseHtml = CreateSuccessResponseHtml();
                    
                    // Exchange code for token immediately
                    LogDebug("Starting token exchange process...");
                    await ExchangeCodeForToken(code);
                }
                else
                {
                    LogDebug("❌ No authorization code or error found in callback");
                    responseHtml = CreateErrorResponseHtml("No authorization code received");
                    Dispatcher.Invoke(() => ShowError("Invalid callback - no authorization code received"));
                }
                
                // Send HTTPS response
                try
                {
                    LogDebug("Sending HTTPS response...");
                    var buffer = Encoding.UTF8.GetBytes(responseHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;
                    
                    LogDebug($"Response size: {buffer.Length} bytes");
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    
                    LogDebug("✅ HTTPS response sent successfully");
                }
                catch (Exception ex)
                {
                    LogDebug($"❌ Error sending HTTPS response: {ex.Message}");
                    LogDebug($"Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error processing HTTPS callback: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
            }
        }

        private async Task ExchangeCodeForToken(string authorizationCode)
        {
            try
            {
                LogDebug("=== ExchangeCodeForToken Started ===");
                LogDebug($"Authorization code length: {authorizationCode.Length}");
                
                using (var httpClient = new HttpClient())
                {
                    var requestData = new Dictionary<string, string>
                    {
                        ["grant_type"] = "authorization_code",
                        ["code"] = authorizationCode,
                        ["redirect_uri"] = REDIRECT_URI,
                        ["client_id"] = _clientId,
                        ["client_secret"] = _clientSecret
                    };
                    
                    LogDebug($"Token exchange request prepared:");
                    LogDebug($"- grant_type: authorization_code");
                    LogDebug($"- redirect_uri: {REDIRECT_URI}");
                    LogDebug($"- client_id: {_clientId}");
                    LogDebug($"- client_secret: {(_clientSecret.Length)} chars");
                    LogDebug($"- code: {authorizationCode.Length} chars");
                    
                    var requestContent = new FormUrlEncodedContent(requestData);
                    
                    LogDebug("Sending token exchange request to Spotify...");
                    var response = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    LogDebug($"Token exchange response received:");
                    LogDebug($"- Status code: {response.StatusCode} ({(int)response.StatusCode})");
                    LogDebug($"- Response length: {responseContent.Length} chars");
                    LogDebug($"- Response content: {responseContent}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        LogDebug("✅ Token exchange successful!");
                        
                        try
                        {
                            var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent);
                            LogDebug($"Token response deserialized:");
                            LogDebug($"- access_token: {(!string.IsNullOrEmpty(tokenResponse?.access_token) ? $"RECEIVED ({tokenResponse.access_token.Length} chars)" : "NOT FOUND")}");
                            LogDebug($"- token_type: {tokenResponse?.token_type ?? "NULL"}");
                            LogDebug($"- expires_in: {tokenResponse?.expires_in ?? 0}");
                            LogDebug($"- refresh_token: {(!string.IsNullOrEmpty(tokenResponse?.refresh_token) ? $"RECEIVED ({tokenResponse.refresh_token.Length} chars)" : "NOT FOUND")}");
                            LogDebug($"- scope: {tokenResponse?.scope ?? "NULL"}");
                            
                            if (!string.IsNullOrEmpty(tokenResponse?.access_token))
                            {
                                AccessToken = tokenResponse.access_token;
                                _refreshToken = tokenResponse.refresh_token;
                                IsAuthenticated = true;

                                // Save refresh token to settings
                                var settingsService = new SettingsService();
                                var settings = settingsService.LoadSettings();
                                settings.SpotifyRefreshToken = _refreshToken;
                                settings.SpotifyAccessToken = AccessToken;
                                settings.SpotifyTokenExpiry = DateTime.Now.AddSeconds(tokenResponse.expires_in);
                                settingsService.SaveSettings(settings);

                                LogDebug($"✅ Access token stored successfully");
                                LogDebug($"✅ Refresh token saved: {!string.IsNullOrEmpty(_refreshToken)}");
                                LogDebug($"IsAuthenticated set to: {IsAuthenticated}");

                                Dispatcher.Invoke(() =>
                                {
                                    LogDebug("Dispatcher.Invoke - Showing success message");
                                    LoadingPanel.Visibility = Visibility.Collapsed;

                                    MessageBox.Show(
                                        $"🎵 Successfully connected to Spotify!\n\n" +
                                        $"✅ Access token received\n" +
                                        $"⏰ Expires in: {tokenResponse.expires_in / 3600} hours\n" +
                                        $"🔄 Refresh token: {(!string.IsNullOrEmpty(tokenResponse.refresh_token) ? "Saved for automatic renewal" : "Not provided")}",
                                        "Spotify Authentication Success",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);

                                    LogDebug("Setting DialogResult = true and closing window");
                                    DialogResult = true;
                                    Close();
                                });
                            }
                            else
                            {
                                LogDebug("❌ No access token in deserialized response");
                                Dispatcher.Invoke(() => ShowError("Token exchange succeeded but no access token received"));
                            }
                        }
                        catch (JsonException ex)
                        {
                            LogDebug($"❌ JSON deserialization error: {ex.Message}");
                            LogDebug($"Raw response content: {responseContent}");
                            Dispatcher.Invoke(() => ShowError($"Failed to parse token response: {ex.Message}"));
                        }
                    }
                    else
                    {
                        LogDebug($"❌ Token exchange failed with status: {response.StatusCode}");
                        LogDebug($"Response headers: {response.Headers}");
                        LogDebug($"Error response content: {responseContent}");
                        
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed ({response.StatusCode}):\n\n{responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Exception during token exchange: {ex.Message}");
                LogDebug($"Exception type: {ex.GetType().Name}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                
                Dispatcher.Invoke(() => ShowError($"Error during token exchange:\n\n{ex.Message}"));
            }
        }

        private void InitializeWebAuth()
        {
            LogDebug("=== InitializeWebAuth Started ===");
            this.Loaded += SpotifyAuthWindow_Loaded;
            LogDebug("Window Loaded event handler attached");
        }

        private void SpotifyAuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogDebug("=== SpotifyAuthWindow_Loaded Event Fired ===");
            this.Loaded -= SpotifyAuthWindow_Loaded;
            LogDebug("Window Loaded event handler detached");
            
            // Initialize WebView2 immediately if server is ready
            if (_serverStarted)
            {
                LogDebug("✅ HTTP server confirmed started, initializing WebView2...");
                InitializeWebView2();
            }
            else
            {
                LogDebug("❌ HTTP server not started, cannot proceed");
                ShowError("HTTP callback server failed to start.\n\nPlease ensure port 8888 is not in use by another application.");
            }
        }

        private async void InitializeWebView2()
        {
            try
            {
                LogDebug("=== InitializeWebView2 Started ===");
                
                // Ensure WebView2 is properly initialized
                if (AuthWebView.CoreWebView2 == null)
                {
                    LogDebug("WebView2 not initialized, starting initialization...");
                    
                    // Set up initialization completion handler
                    AuthWebView.CoreWebView2InitializationCompleted += AuthWebView_CoreWebView2InitializationCompleted;
                    
                    // Force WebView2 initialization
                    LogDebug("Calling EnsureCoreWebView2Async...");
                    await AuthWebView.EnsureCoreWebView2Async();
                    LogDebug("EnsureCoreWebView2Async completed");
                }
                else
                {
                    LogDebug("WebView2 already initialized, setting up immediately");
                    SetupWebView2AndNavigate();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error initializing WebView2: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Failed to initialize web browser: {ex.Message}\n\nPlease try again or use manual token authentication.");
            }
        }

        private void SetupWebView2AndNavigate()
        {
            try
            {
                LogDebug("=== SetupWebView2AndNavigate Started ===");
                
                // Configure WebView2 settings
                var settings = AuthWebView.CoreWebView2.Settings;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsWebMessageEnabled = true;
                settings.UserAgent = "EZStreamer/1.0 Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";
                
                LogDebug($"WebView2 settings configured:");
                LogDebug($"- IsGeneralAutofillEnabled: {settings.IsGeneralAutofillEnabled}");
                LogDebug($"- IsWebMessageEnabled: {settings.IsWebMessageEnabled}");
                LogDebug($"- UserAgent: {settings.UserAgent}");
                
                // Hide loading panel
                LoadingPanel.Visibility = Visibility.Collapsed;
                LogDebug("Loading panel hidden");
                
                // Navigate to Spotify OAuth
                LogDebug("Starting navigation to Spotify OAuth...");
                NavigateToSpotifyAuth();
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error in SetupWebView2AndNavigate: {ex.Message}");
                ShowError($"Error setting up web browser: {ex.Message}");
            }
        }

        private void NavigateToSpotifyAuth()
        {
            try
            {
                LogDebug("=== NavigateToSpotifyAuth Started ===");
                
                // Generate state parameter for security
                var state = Guid.NewGuid().ToString();
                LogDebug($"Generated state parameter: {state}");
                
                // Build OAuth authorization URL (using HTTPS)
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=code" +
                            $"&client_id={Uri.EscapeDataString(_clientId)}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                            $"&state={Uri.EscapeDataString(state)}" +
                            $"&show_dialog=true";

                LogDebug($"OAuth URL built:");
                LogDebug($"Full URL: {authUrl}");
                LogDebug($"URL Length: {authUrl.Length}");
                
                LogDebug($"WebView2 status: {(AuthWebView.CoreWebView2 != null ? "READY" : "NOT READY")}");
                
                if (AuthWebView.CoreWebView2 != null)
                {
                    LogDebug("🌐 Navigating WebView to Spotify authorization...");
                    AuthWebView.CoreWebView2.Navigate(authUrl);
                    LogDebug("Navigation command sent to WebView2");
                }
                else
                {
                    LogDebug("⚠️ WebView2 not ready, storing URL for later");
                    _pendingNavigationUrl = authUrl;
                    LogDebug($"Pending URL stored: {_pendingNavigationUrl != null}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error starting OAuth navigation: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Error starting OAuth flow: {ex.Message}");
            }
        }

        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                LogDebug($"=== WebView2 Initialization Completed ===");
                LogDebug($"Success: {e.IsSuccess}");
                
                if (e.IsSuccess)
                {
                    LogDebug("WebView2 initialization successful - setting up event handlers");
                    
                    // Configure WebView2 to accept our self-signed certificate
                    AuthWebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
                    AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                    
                    LogDebug("Event handlers attached");
                    
                    // Now setup and navigate
                    SetupWebView2AndNavigate();
                }
                else
                {
                    LogDebug("❌ WebView2 initialization failed");
                    ShowError("Failed to initialize web browser for OAuth");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error in WebView2 initialization: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Error initializing OAuth browser: {ex.Message}");
            }
        }

        private void CoreWebView2_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            // Allow all permissions for OAuth
            e.State = CoreWebView2PermissionState.Allow;
            LogDebug($"✅ WebView2 permission granted: {e.PermissionKind}");
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LogDebug($"=== Navigation Starting ===");
            LogDebug($"🌐 Navigation starting to: {e.Uri}");
            LogDebug($"Navigation ID: {e.NavigationId}");
            LogDebug($"Is user initiated: {e.IsUserInitiated}");
            LogDebug($"Is redirected: {e.IsRedirected}");
            
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                LogDebug("✅ Detected HTTPS callback URL - our local server should handle this");
                LogDebug("This means Spotify is redirecting back to us - authentication may be successful!");
            }
            else if (e.Uri.StartsWith("https://accounts.spotify.com"))
            {
                LogDebug("✅ Navigation to Spotify authorization page");
            }
            else
            {
                LogDebug($"⚠️ Unexpected navigation destination: {e.Uri}");
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogDebug($"=== Navigation Completed ===");
            LogDebug($"🌐 Navigation completed. Success: {e.IsSuccess}");
            LogDebug($"Navigation ID: {e.NavigationId}");
            LogDebug($"WebErrorStatus: {e.WebErrorStatus}");
            
            if (!e.IsSuccess)
            {
                LogDebug($"❌ Navigation failed with error: {e.WebErrorStatus}");
                ShowError($"OAuth navigation failed: {e.WebErrorStatus}\n\nPlease ensure you're connected to the internet.");
            }
            else
            {
                LogDebug("✅ Navigation completed successfully");
                try
                {
                    var currentUrl = AuthWebView.CoreWebView2.Source;
                    LogDebug($"Current URL after navigation: {currentUrl}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Error getting current URL: {ex.Message}");
                }
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LogDebug("=== DOM Content Loaded ===");
            LogDebug("📄 DOM content loaded - page is ready");
            
            try
            {
                var currentUrl = AuthWebView.CoreWebView2.Source;
                LogDebug($"Page URL: {currentUrl}");
                
                // Hide loading panel when page loads
                LoadingPanel.Visibility = Visibility.Collapsed;
                LogDebug("Loading panel hidden");
            }
            catch (Exception ex)
            {
                LogDebug($"Error in DOMContentLoaded: {ex.Message}");
            }
        }

        // Missing event handler that was referenced in XAML
        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogDebug($"=== WebView NavigationCompleted (XAML Handler) ===");
            LogDebug($"🌐 WebView Navigation completed. Success: {e.IsSuccess}");
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            if (!e.IsSuccess)
            {
                LogDebug($"❌ WebView navigation failed with error: {e.WebErrorStatus}");
                ShowError($"OAuth navigation failed: {e.WebErrorStatus}\n\nPlease ensure you're connected to the internet.");
            }
        }

        private void ShowConfigurationNeeded()
        {
            LogDebug("=== ShowConfigurationNeeded ===");
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify Client ID and Secret are required for OAuth authentication.\n\n" +
                "Would you like to configure them now?\n\n" +
                "Get them from: https://developer.spotify.com/dashboard\n\n" +
                "IMPORTANT: Set redirect URI to: http://localhost:8888/callback",
                "OAuth Configuration Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
                
            LogDebug($"Configuration dialog result: {result}");
            
            if (result == MessageBoxResult.Yes)
            {
                ShowCredentialsDialog();
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void ShowCredentialsDialog()
        {
            LogDebug("ShowCredentialsDialog - directing user to settings");
            
            MessageBox.Show(
                "Please configure your Spotify credentials in the Settings tab:\n\n" +
                "1. Go to Settings\n" +
                "2. Expand 'Spotify API Credentials'\n" +
                "3. Enter your Client ID and Secret\n" +
                "4. Click Save\n" +
                "5. Try Test Connection again\n\n" +
                "IMPORTANT: Set redirect URI to: http://localhost:8888/callback",
                "Configure Credentials",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
                
            DialogResult = false;
            Close();
        }

        private string CreateSuccessResponseHtml()
        {
            LogDebug("Creating success response HTML");
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>Spotify Authentication Success</title>
    <style>
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #1DB954, #1ed760); 
            color: white; 
            margin: 0; 
            padding: 20px; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }
        .container { 
            background: white; 
            color: #1DB954; 
            padding: 40px; 
            border-radius: 20px; 
            box-shadow: 0 20px 40px rgba(0,0,0,0.1); 
            text-align: center; 
            max-width: 500px; 
            animation: slideIn 0.5s ease-out;
        }
        @keyframes slideIn {
            from { transform: translateY(-20px); opacity: 0; }
            to { transform: translateY(0); opacity: 1; }
        }
        h1 { margin-top: 0; font-size: 2.2em; font-weight: 600; }
        .icon { font-size: 3em; margin-bottom: 20px; }
        .message { font-size: 1.1em; line-height: 1.6; margin: 20px 0; }
        .success-check { color: #1DB954; font-size: 1.2em; margin: 10px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>🎵</div>
        <h1>Authentication Successful!</h1>
        <div class='message'>
            <div class='success-check'>✅ Connected to Spotify</div>
            <div class='success-check'>✅ Access token received</div>
            <div class='success-check'>✅ Ready to use</div>
            <p style='margin-top: 30px;'>You can now close this window and return to EZStreamer.</p>
        </div>
    </div>
    <script>
        console.log('Spotify OAuth callback success page loaded');
        setTimeout(() => {
            console.log('Attempting to close window...');
            window.close();
        }, 3000);
    </script>
</body>
</html>";
        }

        private string CreateErrorResponseHtml(string error)
        {
            LogDebug($"Creating error response HTML for: {error}");
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Spotify Authentication Error</title>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #ff4444, #cc0000); 
            color: white; 
            margin: 0; 
            padding: 20px; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }}
        .container {{ 
            background: white; 
            color: #cc0000; 
            padding: 40px; 
            border-radius: 20px; 
            box-shadow: 0 20px 40px rgba(0,0,0,0.1); 
            text-align: center; 
            max-width: 500px; 
        }}
        h1 {{ margin-top: 0; font-size: 2.2em; font-weight: 600; }}
        .icon {{ font-size: 3em; margin-bottom: 20px; }}
        .error {{ background: #ffebee; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #cc0000; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>❌</div>
        <h1>Authentication Error</h1>
        <div class='error'>
            <strong>Error:</strong> {HttpUtility.HtmlEncode(error)}
        </div>
        <p>Please close this window and try again in EZStreamer.</p>
    </div>
</body>
</html>";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("User cancelled authentication");
            DialogResult = false;
            Close();
        }

        private void ManualTokenButton_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("User requested manual token option");
            MessageBox.Show(
                "Manual token authentication:\n\n" +
                "1. Go to https://developer.spotify.com/console/get-current-user/\n" +
                "2. Click 'Get Token'\n" +
                "3. Select required scopes\n" +
                "4. Copy the generated token\n" +
                "5. Use 'Manual Token' option in EZStreamer settings",
                "Manual Token Instructions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            LogDebug($"❌ Showing error to user: {message}");
            MessageBox.Show(message, "Spotify OAuth Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LogDebug("=== OnClosed - Cleaning up SpotifyAuthWindow ===");
                
                // Stop server
                _isListening = false;
                _cancellationTokenSource?.Cancel();
                
                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    LogDebug("✅ HTTP server stopped");
                }

                // Clean up WebView2
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.PermissionRequested -= CoreWebView2_PermissionRequested;
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                    LogDebug("WebView2 event handlers removed");
                }

                AuthWebView?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                LogDebug("✅ SpotifyAuthWindow cleanup completed");
                LogDebug($"Final state - IsAuthenticated: {IsAuthenticated}, AccessToken: {(!string.IsNullOrEmpty(AccessToken) ? "SET" : "NOT SET")}");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error during cleanup: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}
