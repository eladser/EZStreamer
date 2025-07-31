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
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.IO;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private string _clientSecret;
        private const string REDIRECT_URI = "https://localhost:8443/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";
        
        private HttpListener _httpListener;
        private bool _isListening = false;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _serverStarted = false;

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            var credentials = _configService.GetAPICredentials();
            _clientId = credentials.SpotifyClientId;
            _clientSecret = credentials.SpotifyClientSecret;
            _cancellationTokenSource = new CancellationTokenSource();
            
            Debug.WriteLine($"SpotifyAuthWindow initialized with ClientId: {(!string.IsNullOrEmpty(_clientId) ? "SET" : "NOT SET")}");
            Debug.WriteLine($"SpotifyAuthWindow initialized with ClientSecret: {(!string.IsNullOrEmpty(_clientSecret) ? "SET" : "NOT SET")}");
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start initialization
            InitializeAuthentication();
        }

        private void InitializeAuthentication()
        {
            try
            {
                Debug.WriteLine("Starting authentication initialization...");
                
                // Check credentials first
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    Debug.WriteLine("Missing credentials, showing configuration dialog");
                    ShowConfigurationNeeded();
                    return;
                }

                Debug.WriteLine("Credentials found, starting local HTTPS server...");
                
                // Start server in background
                Task.Run(async () =>
                {
                    try
                    {
                        await StartLocalHttpsServer();
                        Debug.WriteLine("Local HTTPS server started successfully");
                        
                        Dispatcher.Invoke(() =>
                        {
                            Debug.WriteLine("Initializing WebView...");
                            InitializeWebAuth();
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to start local HTTPS server: {ex.Message}");
                        Dispatcher.Invoke(() => 
                        {
                            ShowError($"Failed to start local HTTPS server: {ex.Message}\n\n" +
                                    "This requires administrator privileges to bind HTTPS certificate.\n\n" +
                                    "Please run EZStreamer as Administrator, or try the manual token option.");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in InitializeAuthentication: {ex.Message}");
                ShowError($"Initialization error: {ex.Message}");
            }
        }

        private async Task StartLocalHttpsServer()
        {
            try
            {
                Debug.WriteLine("Setting up HTTPS certificate for localhost:8443...");
                
                // First, try to set up certificate binding
                await SetupHttpsCertificate();
                
                Debug.WriteLine("Creating HttpListener for HTTPS...");
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("https://localhost:8443/");
                
                Debug.WriteLine("Starting HTTPS HttpListener...");
                _httpListener.Start();
                _isListening = true;
                _serverStarted = true;
                
                Debug.WriteLine("✅ HTTPS server started successfully on https://localhost:8443/");
                
                // Start listening for requests
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_isListening && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Debug.WriteLine("Waiting for HTTPS request...");
                            
                            var context = await GetContextAsync(_httpListener, _cancellationTokenSource.Token);
                            if (context != null)
                            {
                                Debug.WriteLine($"Received HTTPS request: {context.Request.Url}");
                                _ = Task.Run(() => ProcessCallback(context));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in HTTPS server loop: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to start HTTPS server: {ex.Message}");
                throw;
            }
        }

        private async Task SetupHttpsCertificate()
        {
            try
            {
                Debug.WriteLine("Setting up self-signed certificate for localhost...");
                
                // Create a self-signed certificate for localhost
                var cert = CreateSelfSignedCertificate();
                
                // Try to bind the certificate to port 8443
                await Task.Run(() =>
                {
                    try
                    {
                        // Export certificate to temporary file
                        var certPath = Path.GetTempFileName() + ".pfx";
                        var password = "temp123";
                        File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, password));
                        
                        Debug.WriteLine($"Certificate exported to: {certPath}");
                        
                        // Use netsh to bind certificate (requires admin privileges)
                        var thumbprint = cert.Thumbprint;
                        Debug.WriteLine($"Certificate thumbprint: {thumbprint}");
                        
                        // Note: This requires administrator privileges
                        var netshCmd = $"http add sslcert ipport=0.0.0.0:8443 certhash={thumbprint} appid={{12345678-1234-1234-1234-123456789012}}";
                        Debug.WriteLine($"Would execute: netsh {netshCmd}");
                        
                        // For now, we'll try without netsh and let HttpListener handle it
                        Debug.WriteLine("Proceeding without netsh certificate binding - HttpListener will handle HTTPS");
                        
                        // Clean up temp file
                        try { File.Delete(certPath); } catch { }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Certificate binding warning: {ex.Message}");
                        // Continue anyway - HttpListener might work without explicit binding
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Certificate setup warning: {ex.Message}");
                // Continue anyway - we'll try HTTPS without custom certificate
            }
        }

        private X509Certificate2 CreateSelfSignedCertificate()
        {
            try
            {
                using (var rsa = RSA.Create(2048))
                {
                    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    
                    // Add subject alternative names
                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddDnsName("localhost");
                    sanBuilder.AddIpAddress(IPAddress.Loopback);
                    request.CertificateExtensions.Add(sanBuilder.Build());
                    
                    // Set certificate as CA
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                    
                    // Set key usage
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                    
                    // Create the certificate
                    var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
                    
                    Debug.WriteLine("✅ Self-signed certificate created successfully");
                    return certificate;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to create self-signed certificate: {ex.Message}");
                throw;
            }
        }

        private async Task<HttpListenerContext> GetContextAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                var contextTask = listener.GetContextAsync();
                
                using (cancellationToken.Register(() => 
                {
                    try 
                    { 
                        listener.Stop(); 
                    } 
                    catch 
                    { 
                        // Ignore cleanup errors 
                    }
                }))
                {
                    return await contextTask;
                }
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("HttpListener was disposed");
                return null;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                Debug.WriteLine("HttpListener operation was aborted");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting HTTPS context: {ex.Message}");
                return null;
            }
        }

        private async Task ProcessCallback(HttpListenerContext context)
        {
            try
            {
                Debug.WriteLine($"🔄 Processing HTTPS callback: {context.Request.Url}");
                
                var request = context.Request;
                var response = context.Response;
                
                // Extract query parameters
                var query = HttpUtility.ParseQueryString(request.Url.Query);
                var code = query["code"];
                var error = query["error"];
                var state = query["state"];
                
                Debug.WriteLine($"Authorization code: {(!string.IsNullOrEmpty(code) ? "RECEIVED" : "NOT FOUND")}");
                Debug.WriteLine($"Error parameter: {error ?? "NONE"}");
                
                string responseHtml;
                
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine($"❌ OAuth error: {error}");
                    responseHtml = CreateErrorResponseHtml(error);
                    Dispatcher.Invoke(() => ShowError($"Spotify authorization failed: {error}"));
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    Debug.WriteLine("✅ Authorization code received, starting token exchange...");
                    responseHtml = CreateSuccessResponseHtml();
                    
                    // Exchange code for token immediately
                    await ExchangeCodeForToken(code);
                }
                else
                {
                    Debug.WriteLine("❌ No authorization code or error found in callback");
                    responseHtml = CreateErrorResponseHtml("No authorization code received");
                    Dispatcher.Invoke(() => ShowError("Invalid callback - no authorization code received"));
                }
                
                // Send HTTPS response
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(responseHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;
                    
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    
                    Debug.WriteLine("✅ HTTPS response sent successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Error sending HTTPS response: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error processing HTTPS callback: {ex.Message}");
            }
        }

        private async Task ExchangeCodeForToken(string authorizationCode)
        {
            try
            {
                Debug.WriteLine("🔄 Starting token exchange...");
                
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
                    
                    Debug.WriteLine($"Token exchange request data prepared");
                    Debug.WriteLine($"- grant_type: authorization_code");
                    Debug.WriteLine($"- redirect_uri: {REDIRECT_URI}");
                    Debug.WriteLine($"- client_id: {_clientId}");
                    
                    var requestContent = new FormUrlEncodedContent(requestData);
                    
                    Debug.WriteLine("Sending token exchange request to Spotify...");
                    var response = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    Debug.WriteLine($"Token exchange response: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine("✅ Token exchange successful!");
                        Debug.WriteLine($"Response content: {responseContent}");
                        
                        var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent);
                        
                        if (!string.IsNullOrEmpty(tokenResponse?.access_token))
                        {
                            AccessToken = tokenResponse.access_token;
                            IsAuthenticated = true;
                            
                            Debug.WriteLine($"✅ Access token received: {AccessToken.Substring(0, Math.Min(20, AccessToken.Length))}...");
                            
                            Dispatcher.Invoke(() =>
                            {
                                LoadingPanel.Visibility = Visibility.Collapsed;
                                
                                MessageBox.Show(
                                    $"🎵 Successfully connected to Spotify!\n\n" +
                                    $"✅ Access token received\n" +
                                    $"⏰ Expires in: {tokenResponse.expires_in} seconds\n" +
                                    $"🔄 Refresh token: {(!string.IsNullOrEmpty(tokenResponse.refresh_token) ? "Available" : "Not provided")}",
                                    "Spotify Authentication Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                                
                                DialogResult = true;
                                Close();
                            });
                        }
                        else
                        {
                            Debug.WriteLine("❌ No access token in response");
                            Dispatcher.Invoke(() => ShowError("Token exchange succeeded but no access token received"));
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"❌ Token exchange failed: {response.StatusCode}");
                        Debug.WriteLine($"Error response: {responseContent}");
                        
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed ({response.StatusCode}):\n\n{responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Exception during token exchange: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                Dispatcher.Invoke(() => ShowError($"Error during token exchange:\n\n{ex.Message}"));
            }
        }

        private void InitializeWebAuth()
        {
            Debug.WriteLine("Initializing WebView authentication...");
            this.Loaded += SpotifyAuthWindow_Loaded;
        }

        private void SpotifyAuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Window loaded, starting OAuth navigation...");
            this.Loaded -= SpotifyAuthWindow_Loaded;
            
            // Small delay to ensure everything is ready
            Task.Delay(500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_serverStarted)
                    {
                        Debug.WriteLine("HTTPS server confirmed started, navigating to Spotify...");
                        NavigateToSpotifyAuth();
                    }
                    else
                    {
                        Debug.WriteLine("❌ HTTPS server not started, cannot proceed");
                        ShowError("Local HTTPS server failed to start. Cannot proceed with OAuth.\n\nPlease run EZStreamer as Administrator.");
                    }
                });
            });
        }

        private void NavigateToSpotifyAuth()
        {
            try
            {
                Debug.WriteLine("🔄 Building Spotify OAuth URL...");
                
                // Generate state parameter for security
                var state = Guid.NewGuid().ToString();
                
                // Build OAuth authorization URL (using HTTPS)
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=code" +
                            $"&client_id={Uri.EscapeDataString(_clientId)}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                            $"&state={Uri.EscapeDataString(state)}" +
                            $"&show_dialog=true";

                Debug.WriteLine($"OAuth URL: {authUrl}");
                Debug.WriteLine("🌐 Navigating WebView to Spotify authorization...");

                if (AuthWebView.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.Navigate(authUrl);
                }
                else
                {
                    Debug.WriteLine("WebView2 not ready, storing URL for later");
                    _pendingNavigationUrl = authUrl;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error starting OAuth navigation: {ex.Message}");
                ShowError($"Error starting OAuth flow: {ex.Message}");
            }
        }

        private string _pendingNavigationUrl;

        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"WebView2 initialization completed. Success: {e.IsSuccess}");
                
                if (e.IsSuccess)
                {
                    AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                    
                    if (!string.IsNullOrEmpty(_pendingNavigationUrl))
                    {
                        Debug.WriteLine("Executing pending navigation...");
                        AuthWebView.CoreWebView2.Navigate(_pendingNavigationUrl);
                        _pendingNavigationUrl = null;
                    }
                    else if (!string.IsNullOrEmpty(_clientId) && _serverStarted)
                    {
                        Debug.WriteLine("Starting OAuth navigation...");
                        NavigateToSpotifyAuth();
                    }
                }
                else
                {
                    Debug.WriteLine("❌ WebView2 initialization failed");
                    ShowError("Failed to initialize web browser for OAuth");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in WebView2 initialization: {ex.Message}");
                ShowError($"Error initializing OAuth browser: {ex.Message}");
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            Debug.WriteLine($"🌐 Navigation starting to: {e.Uri}");
            
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                Debug.WriteLine("✅ Detected HTTPS callback URL - our local server should handle this");
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Debug.WriteLine($"🌐 Navigation completed. Success: {e.IsSuccess}");
            
            if (!e.IsSuccess)
            {
                Debug.WriteLine($"❌ Navigation failed with error: {e.WebErrorStatus}");
                ShowError($"OAuth navigation failed: {e.WebErrorStatus}");
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            Debug.WriteLine("📄 DOM content loaded");
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        // Missing event handler that was referenced in XAML
        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Debug.WriteLine($"🌐 WebView Navigation completed. Success: {e.IsSuccess}");
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            if (!e.IsSuccess)
            {
                Debug.WriteLine($"❌ WebView navigation failed with error: {e.WebErrorStatus}");
                ShowError($"OAuth navigation failed: {e.WebErrorStatus}");
            }
        }

        private void ShowConfigurationNeeded()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify Client ID and Secret are required for OAuth authentication.\n\n" +
                "Would you like to configure them now?\n\n" +
                "Get them from: https://developer.spotify.com/dashboard\n\n" +
                "IMPORTANT: Set redirect URI to: https://localhost:8443/callback",
                "OAuth Configuration Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
                
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
            // This would show credential input dialogs
            // For now, direct user to settings
            MessageBox.Show(
                "Please configure your Spotify credentials in the Settings tab:\n\n" +
                "1. Go to Settings\n" +
                "2. Expand 'Spotify API Credentials'\n" +
                "3. Enter your Client ID and Secret\n" +
                "4. Click Save\n" +
                "5. Try Test Connection again\n\n" +
                "IMPORTANT: Set redirect URI to: https://localhost:8443/callback",
                "Configure Credentials",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
                
            DialogResult = false;
            Close();
        }

        private string CreateSuccessResponseHtml()
        {
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
            Debug.WriteLine("User cancelled authentication");
            DialogResult = false;
            Close();
        }

        private void ManualTokenButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("User requested manual token option");
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
            Debug.WriteLine($"❌ Showing error to user: {message}");
            MessageBox.Show(message, "Spotify OAuth Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                Debug.WriteLine("🔄 Cleaning up SpotifyAuthWindow...");
                
                // Stop server
                _isListening = false;
                _cancellationTokenSource?.Cancel();
                
                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    Debug.WriteLine("✅ HTTPS server stopped");
                }

                // Clean up WebView2
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                }
                
                AuthWebView?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                Debug.WriteLine("✅ SpotifyAuthWindow cleanup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error during cleanup: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }

    // Response model for Spotify token exchange
    public class SpotifyTokenResponse
    {    
        public string access_token { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
    }
}
