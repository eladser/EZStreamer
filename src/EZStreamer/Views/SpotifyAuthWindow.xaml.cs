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

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        // Use a working HTTPS redirect that Spotify supports
        private const string REDIRECT_URI = "https://example.com/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _clientId = _configService.GetSpotifyClientId();
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Check if client ID is configured
            if (string.IsNullOrEmpty(_clientId))
            {
                ShowConfigurationNeeded();
            }
            else
            {
                // Show choice between web auth and manual token
                ShowAuthenticationChoice();
            }
        }

        private void ShowAuthenticationChoice()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Choose Spotify authentication method:\\n\\n" +
                "YES = Web authentication (requires app setup)\\n" +
                "NO = Manual token entry (easier, more reliable)\\n\\n" +
                "Manual token is recommended for first-time setup.",
                "Spotify Authentication",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                InitializeWebAuth();
            }
            else if (result == MessageBoxResult.No)
            {
                ShowManualTokenDialog();
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void InitializeWebAuth()
        {
            LoadingPanel.Visibility = Visibility.Visible;
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
                "Spotify Client ID is not configured.\\n\\n" +
                "Would you like to configure it now?\\n\\n" +
                "Get a Client ID from: https://developer.spotify.com/dashboard\\n\\n" +
                "For web auth, use redirect URI: " + REDIRECT_URI + "\\n" +
                "Or choose manual token entry (easier).",
                "Configuration Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
                
            if (result == MessageBoxResult.Yes)
            {
                ShowClientIdDialog();
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void ShowClientIdDialog()
        {
            var dialog = new ConfigurationDialog("Spotify Client ID", 
                $"Enter your Spotify Application Client ID:\\n\\nFor web authentication, set redirect URI to:\\n{REDIRECT_URI}\\n\\nOr use manual token entry instead.");
            
            if (dialog.ShowDialog() == true)
            {
                _clientId = dialog.Value;
                _configService.SetSpotifyCredentials(_clientId);
                
                ShowAuthenticationChoice();
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

                // Navigate to Spotify OAuth URL
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=token" +
                            $"&client_id={_clientId}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                            $"&show_dialog=true";

                System.Diagnostics.Debug.WriteLine($"Navigating to: {authUrl}");

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
                ShowError($"Error initializing authentication: {ex.Message}\\n\\nTrying manual token entry...");
                ShowManualTokenDialog();
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
                    ShowError("Failed to initialize web browser. Using manual token entry...");
                    ShowManualTokenDialog();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing web view: {ex.Message}\\nUsing manual token entry...");
                ShowManualTokenDialog();
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting to: {e.Uri}");
            
            // Check if this is our redirect URI or contains access token
            if (e.Uri.StartsWith(REDIRECT_URI) || e.Uri.Contains("access_token="))
            {
                e.Cancel = true;
                ProcessUrlForToken(e.Uri);
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            // Check the current URL for tokens
            try
            {
                var currentUrl = AuthWebView.CoreWebView2.Source;
                if (currentUrl.Contains("access_token=") || currentUrl.StartsWith(REDIRECT_URI))
                {
                    ProcessUrlForToken(currentUrl);
                }
                else if (currentUrl.Contains("error="))
                {
                    var uri = new Uri(currentUrl);
                    var queryParams = HttpUtility.ParseQueryString(uri.Query);
                    var error = queryParams["error"];
                    ShowError($"Spotify authentication error: {error}\\n\\nTry manual token entry instead.");
                }
                else
                {
                    // Show instruction to copy URL if needed
                    ShowUrlInstructionIfNeeded(currentUrl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking URL: {ex.Message}");
            }
        }

        private void ShowUrlInstructionIfNeeded(string currentUrl)
        {
            // If we're on the redirect page but no token, show instructions
            if (currentUrl.StartsWith(REDIRECT_URI) || currentUrl.Contains("example.com"))
            {
                var result = MessageBox.Show(
                    "The page has loaded but no access token was automatically detected.\\n\\n" +
                    "If you see an access token in the URL bar, you can:\\n" +
                    "1. Copy the entire URL\\n" +
                    "2. Click 'Manual Token' and paste it\\n\\n" +
                    "Or click 'Manual Token' for easier token generation.\\n\\n" +
                    "Continue with manual token entry?",
                    "Token Extraction",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    ShowManualTokenDialog();
                }
            }
        }

        private void ProcessUrlForToken(string url)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Processing URL for token: {url}");
                
                string accessToken = null;
                string error = null;

                // Check URL fragment first (most common for implicit flow)
                if (url.Contains("#"))
                {
                    var fragment = url.Split('#')[1];
                    var queryParams = HttpUtility.ParseQueryString(fragment);
                    accessToken = queryParams["access_token"];
                    error = queryParams["error"];
                }
                
                // Check query parameters as backup
                if (string.IsNullOrEmpty(accessToken) && url.Contains("?"))
                {
                    var uri = new Uri(url);
                    var queryParams = HttpUtility.ParseQueryString(uri.Query);
                    accessToken = queryParams["access_token"];
                    error = queryParams["error"];
                }

                if (!string.IsNullOrEmpty(error))
                {
                    ShowError($"Authentication failed: {error}\\n\\nTry manual token entry instead.");
                    return;
                }

                if (!string.IsNullOrEmpty(accessToken))
                {
                    AccessToken = accessToken;
                    IsAuthenticated = true;
                    
                    MessageBox.Show("Successfully connected to Spotify!", "Authentication Successful", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    DialogResult = true;
                    Close();
                    return;
                }
                
                // No token found
                ShowError("No access token found in the URL.\\n\\nTry manual token entry for more reliable authentication.");
            }
            catch (Exception ex)
            {
                ShowError($"Error processing authentication: {ex.Message}\\n\\nTry manual token entry instead.");
            }
        }

        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            if (!e.IsSuccess)
            {
                ShowError($"Navigation failed: {e.WebErrorStatus}\\n\\nTrying manual token entry...");
                ShowManualTokenDialog();
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
            ShowManualTokenDialog();
        }

        private void ShowManualTokenDialog()
        {
            var instructions = "To get a Spotify access token manually:\\n\\n" +
                "1. Go to your Spotify app in the Developer Dashboard\\n" +
                "2. Use an OAuth testing tool like:\\n" +
                "   • https://oauth.tools/spotify\\n" +
                "   • https://developer.spotify.com/console/\\n\\n" +
                "3. Generate a token with these scopes:\\n" +
                "   • user-read-playback-state\\n" +
                "   • user-modify-playback-state\\n" +
                "   • user-read-currently-playing\\n" +
                "   • playlist-read-private\\n\\n" +
                "4. Copy the access token and paste it below:";
                
            MessageBox.Show(instructions, "Manual Token Instructions", 
                MessageBoxButton.OK, MessageBoxImage.Information);

            var dialog = new ManualTokenDialog("Spotify");
            if (dialog.ShowDialog() == true)
            {
                AccessToken = dialog.Token;
                IsAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Authentication Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Clean up WebView2 resources
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                }
                
                AuthWebView?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors during window close
            }
            
            base.OnClosed(e);
        }
    }
}
