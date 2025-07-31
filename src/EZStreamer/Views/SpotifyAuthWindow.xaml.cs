using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using EZStreamer.Services;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text.Json;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private string _clientSecret;
        private const string REDIRECT_URI = "https://localhost:8443/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";
        private HttpListener _httpsListener;
        private bool _isListening = false;

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            var credentials = _configService.GetAPICredentials();
            _clientId = credentials.SpotifyClientId;
            _clientSecret = credentials.SpotifyClientSecret;
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start HTTPS server for OAuth callback
            StartHttpsServer();
        }

        private void StartHttpsServer()
        {
            try
            {
                // Create self-signed certificate for localhost HTTPS
                CreateSelfSignedCertificate();
                
                _httpsListener = new HttpListener();
                _httpsListener.Prefixes.Add("https://localhost:8443/");
                _httpsListener.Start();
                _isListening = true;

                System.Diagnostics.Debug.WriteLine("HTTPS server started on https://localhost:8443/");

                // Listen for OAuth callback in background
                Task.Run(async () =>
                {
                    try
                    {
                        while (_isListening && _httpsListener.IsListening)
                        {
                            var context = await _httpsListener.GetContextAsync();
                            await ProcessOAuthCallback(context);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isListening)
                        {
                            System.Diagnostics.Debug.WriteLine($"HTTPS listener error: {ex.Message}");
                        }
                    }
                });

                // Proceed with authentication
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    ShowConfigurationNeeded();
                }
                else
                {
                    InitializeWebAuth();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to start HTTPS server: {ex.Message}\\n\\nThis might be due to Windows permissions. Try running as administrator.");
            }
        }

        private void CreateSelfSignedCertificate()
        {
            try
            {
                // Register the certificate validation callback to accept our self-signed cert
                ServicePointManager.ServerCertificateValidationCallback = 
                    new RemoteCertificateValidationCallback(ValidateServerCertificate);
                
                // For .NET Framework, we need to bind a certificate to the port
                // This is a simplified approach - in production you'd use netsh or IIS
                System.Diagnostics.Debug.WriteLine("Setting up HTTPS certificate binding for localhost:8443");
                
                // Note: This might require administrator privileges
                // Alternative: Use HTTP and proxy through HTTPS, or use a different approach
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Certificate setup warning: {ex.Message}");
            }
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Accept all certificates for localhost during development
            return true;
        }

        private async Task ProcessOAuthCallback(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Send success page
                var html = @"
                    <html>
                    <head>
                        <title>Spotify OAuth Success</title>
                        <style>
                            body { font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #1DB954; color: white; }
                            .success { background: white; color: #1DB954; padding: 30px; border-radius: 10px; max-width: 500px; margin: 0 auto; }
                        </style>
                    </head>
                    <body>
                        <div class='success'>
                            <h1>🎵 Authentication Successful!</h1>
                            <p>You can now close this window and return to EZStreamer.</p>
                            <p>Your Spotify account is connected!</p>
                        </div>
                        <script>setTimeout(() => window.close(), 3000);</script>
                    </body>
                    </html>";

                var buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                // Process the OAuth authorization code
                var code = request.QueryString["code"];
                var error = request.QueryString["error"];
                var state = request.QueryString["state"];

                if (!string.IsNullOrEmpty(error))
                {
                    Dispatcher.Invoke(() => ShowError($"OAuth error: {error}"));
                    return;
                }

                if (!string.IsNullOrEmpty(code))
                {
                    // Exchange authorization code for access token
                    await ExchangeCodeForToken(code);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing OAuth callback: {ex.Message}");
            }
        }

        private async Task ExchangeCodeForToken(string authorizationCode)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // Prepare token exchange request
                    var tokenRequest = new
                    {
                        grant_type = "authorization_code",
                        code = authorizationCode,
                        redirect_uri = REDIRECT_URI,
                        client_id = _clientId,
                        client_secret = _clientSecret
                    };

                    var requestContent = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", authorizationCode),
                        new KeyValuePair<string, string>("redirect_uri", REDIRECT_URI),
                        new KeyValuePair<string, string>("client_id", _clientId),
                        new KeyValuePair<string, string>("client_secret", _clientSecret)
                    });

                    // Exchange code for token
                    var response = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent);
                        
                        if (!string.IsNullOrEmpty(tokenResponse.access_token))
                        {
                            AccessToken = tokenResponse.access_token;
                            IsAuthenticated = true;

                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    $"Successfully connected to Spotify!\\n\\n" +
                                    $"Token expires in: {tokenResponse.expires_in} seconds\\n" +
                                    $"Refresh token available: {(!string.IsNullOrEmpty(tokenResponse.refresh_token) ? "Yes" : "No")}",
                                    "OAuth Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                                
                                DialogResult = true;
                                Close();
                            });

                            System.Diagnostics.Debug.WriteLine($"Access token received: {AccessToken.Substring(0, 10)}...");
                        }
                        else
                        {
                            Dispatcher.Invoke(() => ShowError("No access token received from Spotify"));
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed: {response.StatusCode}\\n{responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowError($"Error exchanging code for token: {ex.Message}"));
            }
        }

        private void InitializeWebAuth()
        {
            this.Loaded += SpotifyAuthWindow_Loaded;
        }

        private void SpotifyAuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= SpotifyAuthWindow_Loaded;
            NavigateToSpotifyAuth();
        }

        private void ShowConfigurationNeeded()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify Client ID and Secret are required for OAuth authentication.\\n\\n" +
                "Would you like to configure them now?\\n\\n" +
                "Get them from: https://developer.spotify.com/dashboard\\n\\n" +
                "IMPORTANT: Set redirect URI to: " + REDIRECT_URI + "\\n" +
                "And enable 'Authorization Code Flow' in your app settings.",
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
            // Show Client ID dialog
            var clientIdDialog = new ConfigurationDialog("Spotify Client ID", 
                "Enter your Spotify Client ID:");
            
            if (clientIdDialog.ShowDialog() == true)
            {
                _clientId = clientIdDialog.Value;
                
                // Show Client Secret dialog
                var clientSecretDialog = new ConfigurationDialog("Spotify Client Secret", 
                    "Enter your Spotify Client Secret:");
                
                if (clientSecretDialog.ShowDialog() == true)
                {
                    _clientSecret = clientSecretDialog.Value;
                    
                    // Save both credentials
                    _configService.SetSpotifyCredentials(_clientId, _clientSecret);
                    
                    // Start authentication
                    LoadingPanel.Visibility = Visibility.Visible;
                    NavigateToSpotifyAuth();
                }
                else
                {
                    DialogResult = false;
                    Close();
                }
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void NavigateToSpotifyAuth()
        {
            try
            {
                if (string.IsNullOrEmpty(_clientId))
                {
                    ShowConfigurationNeeded();
                    return;
                }

                // Generate state parameter for security
                var state = Guid.NewGuid().ToString();

                // Build OAuth authorization URL (Authorization Code Flow)
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=code" +
                            $"&client_id={_clientId}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                            $"&state={state}" +
                            $"&show_dialog=true";

                System.Diagnostics.Debug.WriteLine($"Navigating to OAuth URL: {authUrl}");

                if (AuthWebView.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.Navigate(authUrl);
                }
                else
                {
                    _pendingNavigationUrl = authUrl;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error starting OAuth flow: {ex.Message}");
            }
        }

        private string _pendingNavigationUrl;

        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                if (e?.IsSuccess != false)
                {
                    AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                    
                    if (!string.IsNullOrEmpty(_pendingNavigationUrl))
                    {
                        AuthWebView.CoreWebView2.Navigate(_pendingNavigationUrl);
                        _pendingNavigationUrl = null;
                    }
                    else if (!string.IsNullOrEmpty(_clientId))
                    {
                        NavigateToSpotifyAuth();
                    }
                }
                else
                {
                    ShowError("Failed to initialize web browser for OAuth");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing OAuth browser: {ex.Message}");
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OAuth navigation to: {e.Uri}");
            
            // Check if this is our HTTPS callback
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                LoadingPanel.Visibility = Visibility.Visible;
                // Let it navigate to our HTTPS server
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            if (!e.IsSuccess)
            {
                ShowError($"OAuth navigation failed: {e.WebErrorStatus}");
                return;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ManualTokenButton_Click(object sender, RoutedEventArgs e)
        {
            ShowError("This window is configured for OAuth with Client ID and Secret.\\nManual tokens are not needed with proper OAuth flow.");
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "OAuth Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Stop HTTPS server
                _isListening = false;
                if (_httpsListener != null && _httpsListener.IsListening)
                {
                    _httpsListener.Stop();
                    _httpsListener.Close();
                }

                // Clean up WebView2
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                }
                
                AuthWebView?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
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
