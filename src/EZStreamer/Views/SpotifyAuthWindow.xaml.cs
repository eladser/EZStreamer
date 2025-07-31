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
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

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
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start local HTTPS server and then initialize auth
            StartLocalHttpsServer();
        }

        private void StartLocalHttpsServer()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Try to bind to HTTPS port using HttpListener with certificate
                    await SetupHttpsListener();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                        {
                            ShowConfigurationNeeded();
                        }
                        else
                        {
                            InitializeWebAuth();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => 
                    {
                        ShowError($"Failed to start local HTTPS server: {ex.Message}\n\n" +
                                "This may be due to:\n" +
                                "1. Port 8443 is already in use\n" +
                                "2. Administrator privileges are required\n" +
                                "3. Windows firewall is blocking the connection\n\n" +
                                "Try running EZStreamer as Administrator, or use manual token authentication instead.");
                    });
                }
            });
        }

        private async Task SetupHttpsListener()
        {
            // Create a simple HTTP listener (we'll handle HTTPS through WebView2's security context)
            // For local development, we'll use HTTP and rely on Spotify's localhost exception
            const string HTTP_REDIRECT_URI = "http://localhost:8443/callback";
            
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add("http://localhost:8443/");
            
            try
            {
                _httpListener.Start();
                _isListening = true;
                
                System.Diagnostics.Debug.WriteLine("Local HTTP server started on http://localhost:8443/");
                
                // Update redirect URI to use HTTP for local development
                // Spotify allows localhost HTTP redirects for development
                
                // Listen for requests
                while (_isListening && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await GetContextAsync(_httpListener, _cancellationTokenSource.Token);
                        if (context != null)
                        {
                            _ = Task.Run(() => ProcessCallback(context));
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Listener was disposed, exit gracefully
                        break;
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                    {
                        // Operation was aborted, exit gracefully
                        break;
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                throw new Exception($"Failed to start HTTP listener on port 8443: {ex.Message}");
            }
        }

        private async Task<HttpListenerContext> GetContextAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                var contextTask = listener.GetContextAsync();
                var tcs = new TaskCompletionSource<bool>();
                
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    var completedTask = await Task.WhenAny(contextTask, tcs.Task);
                    if (completedTask == contextTask)
                    {
                        return await contextTask;
                    }
                    else
                    {
                        return null; // Cancelled
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task ProcessCallback(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                
                System.Diagnostics.Debug.WriteLine($"Received callback request: {request.Url}");
                
                // Extract authorization code or error from query parameters
                var query = HttpUtility.ParseQueryString(request.Url.Query);
                var code = query["code"];
                var error = query["error"];
                var state = query["state"];
                
                // Send response page
                string responseHtml;
                if (!string.IsNullOrEmpty(error))
                {
                    responseHtml = CreateErrorResponseHtml(error);
                    Dispatcher.Invoke(() => ShowError($"OAuth error: {error}"));
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    responseHtml = CreateSuccessResponseHtml();
                    // Process the authorization code
                    await ExchangeCodeForToken(code);
                }
                else
                {
                    responseHtml = CreateErrorResponseHtml("No authorization code received");
                    Dispatcher.Invoke(() => ShowError("No authorization code received from Spotify"));
                }
                
                // Send HTML response
                var buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing callback: {ex.Message}");
            }
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
            font-family: Arial, sans-serif; 
            background: linear-gradient(135deg, #1DB954, #1ed760); 
            color: white; 
            margin: 0; 
            padding: 0; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }
        .container { 
            background: white; 
            color: #1DB954; 
            padding: 40px; 
            border-radius: 15px; 
            box-shadow: 0 10px 30px rgba(0,0,0,0.2); 
            text-align: center; 
            max-width: 500px; 
            margin: 20px;
        }
        h1 { margin-top: 0; font-size: 2.5em; }
        .icon { font-size: 4em; margin-bottom: 20px; }
        .message { font-size: 1.2em; margin: 20px 0; }
        .footer { font-size: 0.9em; color: #666; margin-top: 30px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>🎵</div>
        <h1>Success!</h1>
        <div class='message'>
            <p><strong>Spotify authentication completed successfully!</strong></p>
            <p>You can now close this window and return to EZStreamer.</p>
            <p>Your Spotify account is connected and ready to use.</p>
        </div>
        <div class='footer'>
            <p>This window will close automatically in a few seconds...</p>
        </div>
    </div>
    <script>
        setTimeout(function() {
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
            font-family: Arial, sans-serif; 
            background: linear-gradient(135deg, #ff4444, #cc0000); 
            color: white; 
            margin: 0; 
            padding: 0; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }}
        .container {{ 
            background: white; 
            color: #cc0000; 
            padding: 40px; 
            border-radius: 15px; 
            box-shadow: 0 10px 30px rgba(0,0,0,0.2); 
            text-align: center; 
            max-width: 500px; 
            margin: 20px;
        }}
        h1 {{ margin-top: 0; font-size: 2.5em; }}
        .icon {{ font-size: 4em; margin-bottom: 20px; }}
        .error {{ background: #ffebee; padding: 15px; border-radius: 5px; margin: 20px 0; }}
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

        private async Task ExchangeCodeForToken(string authorizationCode)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // Prepare token exchange request
                    var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "authorization_code",
                        ["code"] = authorizationCode,
                        ["redirect_uri"] = "http://localhost:8443/callback", // Use HTTP for local development
                        ["client_id"] = _clientId,
                        ["client_secret"] = _clientSecret
                    });

                    System.Diagnostics.Debug.WriteLine("Exchanging authorization code for access token...");

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

                            System.Diagnostics.Debug.WriteLine($"Access token received successfully");

                            Dispatcher.Invoke(() =>
                            {
                                LoadingPanel.Visibility = Visibility.Collapsed;
                                
                                MessageBox.Show(
                                    $"✅ Successfully connected to Spotify!\n\n" +
                                    $"🔑 Access token received\n" +
                                    $"⏰ Expires in: {tokenResponse.expires_in} seconds\n" +
                                    $"🔄 Refresh token: {(!string.IsNullOrEmpty(tokenResponse.refresh_token) ? "Available" : "Not provided")}",
                                    "Spotify OAuth Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                                
                                DialogResult = true;
                                Close();
                            });
                        }
                        else
                        {
                            Dispatcher.Invoke(() => ShowError("No access token received from Spotify"));
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Token exchange failed: {response.StatusCode} - {responseContent}");
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed: {response.StatusCode}\n\nDetails: {responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exchanging code for token: {ex.Message}");
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
                "Spotify Client ID and Secret are required for OAuth authentication.\n\n" +
                "Would you like to configure them now?\n\n" +
                "Get them from: https://developer.spotify.com/dashboard\n\n" +
                "IMPORTANT: Set redirect URI to: http://localhost:8443/callback\n" +
                "(Spotify allows HTTP for localhost development)",
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

                // Build OAuth authorization URL (using HTTP for local development)
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=code" +
                            $"&client_id={_clientId}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString("http://localhost:8443/callback")}" +
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
            
            // Check if this is our local callback
            if (e.Uri.StartsWith("http://localhost:8443/callback"))
            {
                System.Diagnostics.Debug.WriteLine("Detected callback URL, processing...");
                // Let it proceed to our local server
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
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
            var result = MessageBox.Show(
                "Do you want to use manual token authentication instead?\n\n" +
                "This will open the Spotify Web Console where you can generate a token manually.",
                "Manual Token Authentication",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://developer.spotify.com/console/get-current-user/",
                        UseShellExecute = true
                    });
                    
                    MessageBox.Show(
                        "Manual token steps:\n\n" +
                        "1. Click 'Get Token' on the opened webpage\n" +
                        "2. Select the required scopes\n" +
                        "3. Copy the generated token\n" +
                        "4. Use the 'Manual Token' option in EZStreamer settings",
                        "Manual Token Instructions",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowError($"Could not open browser: {ex.Message}");
                }
            }
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
                // Stop the HTTP listener
                _isListening = false;
                _cancellationTokenSource?.Cancel();
                
                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                }

                // Clean up WebView2
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                }
                
                AuthWebView?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            base.OnClosed(e);
        }
    }

    // Helper classes
    public class AuthResult
    {
        public bool success { get; set; }
        public string code { get; set; }
        public string error { get; set; }
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
