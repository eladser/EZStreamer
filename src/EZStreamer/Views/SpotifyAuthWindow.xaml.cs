using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using EZStreamer.Services;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private const string REDIRECT_URI = "http://localhost:3000/auth/spotify/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _clientId = _configService.GetSpotifyClientId();
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Defer client ID check until after window is shown
            this.Loaded += SpotifyAuthWindow_Loaded;
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
                "Spotify Client ID is not configured.\n\n" +
                "Would you like to configure it now?\n\n" +
                "You can get a Client ID from the Spotify Developer Dashboard:\n" +
                "https://developer.spotify.com/dashboard",
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
                "Enter your Spotify Application Client ID:");
            
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

        // Fixed CS1998: Removed async since no await is used
        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                if (e?.IsSuccess != false) // null or true
                {
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

        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            try
            {
                var uri = new Uri(AuthWebView.CoreWebView2.Source);
                
                // Check if this is the callback URL with access token
                if (uri.Host == "localhost" && uri.LocalPath == "/auth/spotify/callback")
                {
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
                }

                // Check for error in URL
                if (uri.Query.Contains("error="))
                {
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
            var dialog = new ManualTokenDialog("Spotify");
            if (dialog.ShowDialog() == true)
            {
                AccessToken = dialog.Token;
                IsAuthenticated = true;
                DialogResult = true;
                Close();
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Authentication Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }
}
