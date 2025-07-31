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

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private string _clientSecret;
        private const string REDIRECT_URI = "https://eladser.github.io/ezstreamer/auth/spotify/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";

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
            
            // Check credentials and start authentication
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                ShowConfigurationNeeded();
            }
            else
            {
                InitializeWebAuth();
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
                "IMPORTANT: Set redirect URI to: " + REDIRECT_URI + "\n" +
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
            
            // Check if this is our GitHub Pages callback
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                LoadingPanel.Visibility = Visibility.Visible;
                // Allow navigation to GitHub Pages callback
                return;
            }
        }

        private async void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            try
            {
                // Check if we're on the callback page
                var currentUrl = AuthWebView.CoreWebView2.Source;
                if (currentUrl.StartsWith(REDIRECT_URI))
                {
                    // The callback page will display the authorization code
                    // We need to extract it from the page
                    await Task.Delay(1000); // Give the page time to load and extract the code
                    
                    // Try to get the authorization code from the page
                    var result = await AuthWebView.CoreWebView2.ExecuteScriptAsync(@"
                        try {
                            const urlParams = new URLSearchParams(window.location.search);
                            const code = urlParams.get('code');
                            const error = urlParams.get('error');
                            
                            if (error) {
                                JSON.stringify({ success: false, error: error });
                            } else if (code) {
                                // Display the code to the user
                                document.body.innerHTML = `
                                    <div style='font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #1DB954; color: white;'>
                                        <div style='background: white; color: #1DB954; padding: 30px; border-radius: 10px; max-width: 600px; margin: 0 auto;'>
                                            <h1>🎵 Authorization Code Received!</h1>
                                            <p>Copy this authorization code:</p>
                                            <div style='background: #f0f0f0; padding: 15px; border-radius: 5px; font-family: monospace; margin: 20px 0; word-break: break-all;'>
                                                ${code}
                                            </div>
                                            <p>Click the button below in EZStreamer to continue...</p>
                                        </div>
                                    </div>
                                `;
                                JSON.stringify({ success: true, code: code });
                            } else {
                                JSON.stringify({ success: false, error: 'No code or error found' });
                            }
                        } catch (e) {
                            JSON.stringify({ success: false, error: e.toString() });
                        }
                    ");

                    if (!string.IsNullOrEmpty(result))
                    {
                        try
                        {
                            var authResult = JsonSerializer.Deserialize<AuthResult>(result.Trim('"').Replace("\\\"", "\""));
                            if (authResult.success && !string.IsNullOrEmpty(authResult.code))
                            {
                                // Show code input dialog
                                ShowCodeInputDialog(authResult.code);
                            }
                            else
                            {
                                ShowError($"Authentication failed: {authResult.error}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error parsing auth result: {ex.Message}");
                            ShowCodeInputDialog(); // Fallback to manual input
                        }
                    }
                    else
                    {
                        ShowCodeInputDialog(); // Fallback to manual input
                    }
                }
                else
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DOMContentLoaded: {ex.Message}");
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowCodeInputDialog(string prefilledCode = null)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var dialog = new AuthCodeInputDialog(prefilledCode);
            if (dialog.ShowDialog() == true)
            {
                LoadingPanel.Visibility = Visibility.Visible;
                _ = Task.Run(async () => await ExchangeCodeForToken(dialog.AuthorizationCode));
            }
            else
            {
                DialogResult = false;
                Close();
            }
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
                        ["redirect_uri"] = REDIRECT_URI,
                        ["client_id"] = _clientId,
                        ["client_secret"] = _clientSecret
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
                                    $"Successfully connected to Spotify!\n\n" +
                                    $"Token expires in: {tokenResponse.expires_in} seconds\n" +
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
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed: {response.StatusCode}\n{responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => ShowError($"Error exchanging code for token: {ex.Message}"));
            }
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
            ShowError("This window is configured for OAuth with Client ID and Secret.\nManual tokens are not needed with proper OAuth flow.");
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
