using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private const string CLIENT_ID = "your_spotify_client_id"; // This should be configured
        private const string REDIRECT_URI = "http://localhost:3000/auth/spotify/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            LoadingPanel.Visibility = Visibility.Visible;
        }

        private async void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                if (e.IsSuccess)
                {
                    // Navigate to Spotify OAuth URL
                    var authUrl = $"https://accounts.spotify.com/authorize" +
                                $"?response_type=token" +
                                $"&client_id={CLIENT_ID}" +
                                $"&scope={Uri.EscapeDataString(SCOPES)}" +
                                $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                                $"&show_dialog=true";

                    AuthWebView.CoreWebView2.Navigate(authUrl);
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
