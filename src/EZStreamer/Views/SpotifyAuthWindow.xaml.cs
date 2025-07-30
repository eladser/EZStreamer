using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using EZStreamer.Services;
using System.Threading.Tasks;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        // Use the working redirect URI - Spotify allows this specific localhost URL
        private const string REDIRECT_URI = "http://redirect.spotify.com/redirect";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _clientId = _configService.GetSpotifyClientId();
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Show manual token option immediately if no client ID
            if (string.IsNullOrEmpty(_clientId))
            {
                ShowConfigurationNeeded();
            }
            else
            {
                // For now, go straight to manual token entry since it's more reliable
                ShowManualTokenOption();
            }
        }

        private void ShowManualTokenOption()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify authentication can be done manually for the most reliable results.\\n\\n" +
                "Click YES to enter a token manually, or NO to try web authentication.\\n\\n" +
                "For manual token:\\n" +
                "1. Go to: https://developer.spotify.com/console/get-current-user/\\n" +
                "2. Click 'Get Token'\\n" +
                "3. Select the required scopes\\n" +
                "4. Copy the access token",
                "Spotify Authentication Method",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                ShowManualTokenDialog();
            }
            else if (result == MessageBoxResult.No)
            {
                // Try web authentication
                this.Loaded += SpotifyAuthWindow_Loaded;
                InitializeWebAuth();
            }
            else
            {
                DialogResult = false;
                Close();
            }
        }

        private void ShowManualTokenDialog()
        {
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

        private void InitializeWebAuth()
        {
            LoadingPanel.Visibility = Visibility.Visible;
        }

        private void SpotifyAuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= SpotifyAuthWindow_Loaded; // Unsubscribe
            
            // Check if client ID is configured
            if (string.IsNullOrEmpty(_clientId))
            {
                ShowConfigurationNeeded();
            }
            else
            {
                // Initialize WebView if client ID is available
                if (AuthWebView.CoreWebView2 == null)
                {
                    // WebView2 will automatically initialize and trigger the initialization event
                }
                else
                {
                    // WebView2 is already initialized, navigate directly
                    NavigateToSpotifyAuth();
                }
            }
        }

        private void ShowConfigurationNeeded()
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify Client ID is not configured.\\n\\n" +
                "Would you like to configure it now?\\n\\n" +
                "You can get a Client ID from the Spotify Developer Dashboard:\\n" +
                "https://developer.spotify.com/dashboard\\n\\n" +
                "Use this redirect URI in your Spotify app:\\n" +
                REDIRECT_URI,
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
                $"Enter your Spotify Application Client ID:\\n\\nMake sure your Spotify app uses this redirect URI:\\n{REDIRECT_URI}");
            
            if (dialog.ShowDialog() == true)
            {
                _clientId = dialog.Value;
                _configService.SetSpotifyCredentials(_clientId);
                
                // Restart the authentication process
                LoadingPanel.Visibility = Visibility.Visible;
                NavigateToSpotifyAuth();
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
                    // Store the URL to navigate after WebView2 initializes
                    _pendingNavigationUrl = authUrl;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing authentication: {ex.Message}");
            }
        }

        private string _pendingNavigationUrl;

        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                if (e?.IsSuccess != false) // null or true
                {
                    // Set up navigation event handler to intercept the callback
                    AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                    
                    // If we have a pending navigation URL, navigate now
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
                    ShowError("Failed to initialize web browser");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing authentication: {ex.Message}");
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation starting to: {e.Uri}");
            
            // Check if this is our callback URL or if it contains access_token
            if (e.Uri.StartsWith(REDIRECT_URI) || e.Uri.Contains("access_token="))
            {
                e.Cancel = true; // Cancel the navigation
                ProcessCallback(e.Uri);
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            // Check if we're on the redirect page and extract token from URL
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000); // Wait for page to load
                    
                    var currentUrl = AuthWebView.CoreWebView2.Source;
                    if (currentUrl.Contains("access_token=") || currentUrl.StartsWith(REDIRECT_URI))
                    {
                        Dispatcher.Invoke(() => ProcessCallback(currentUrl));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking for token: {ex.Message}");
                }
            });
        }

        private void ProcessCallback(string callbackUrl)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Processing callback: {callbackUrl}");
                
                var uri = new Uri(callbackUrl);
                
                // Parse the fragment for access token
                var fragment = uri.Fragment.TrimStart('#');
                var queryParams = HttpUtility.ParseQueryString(fragment);
                
                var accessToken = queryParams["access_token"];
                var error = queryParams["error"];

                if (!string.IsNullOrEmpty(error))
                {
                    ShowError($"Authentication failed: {error}");
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
                
                // If no token found, suggest manual entry
                var result = MessageBox.Show(
                    "No access token was found in the callback URL.\\n\\n" +
                    "Would you like to try manual token entry instead?",
                    "Token Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    ShowManualTokenDialog();
                }
                else
                {
                    ShowError("Authentication failed - no access token received.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error processing authentication callback: {ex.Message}");
            }
        }

        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            if (!e.IsSuccess)
            {
                ShowError($"Navigation failed: {e.WebErrorStatus}");
                return;
            }

            try
            {
                var currentUrl = AuthWebView.CoreWebView2.Source;
                System.Diagnostics.Debug.WriteLine($"Navigation completed to: {currentUrl}");
                
                // Check if we're at the callback URL (shouldn't happen due to NavigationStarting handler)
                if (currentUrl.StartsWith(REDIRECT_URI) || currentUrl.Contains("access_token="))
                {
                    ProcessCallback(currentUrl);
                }
                
                // Check for error in URL
                if (currentUrl.Contains("error="))
                {
                    var uri = new Uri(currentUrl);
                    var queryParams = HttpUtility.ParseQueryString(uri.Query);
                    var error = queryParams["error"];
                    
                    ShowError($"Authentication failed: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
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

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Authentication Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Clean up WebView2 resources properly
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
