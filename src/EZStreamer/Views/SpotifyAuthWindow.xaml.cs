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

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private const string REDIRECT_URI = "http://localhost:8888/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";
        private HttpListener _httpListener;
        private bool _isListening = false;

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _clientId = _configService.GetSpotifyClientId();
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start local HTTP server for callback
            StartLocalServer();
        }

        private void StartLocalServer()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:8888/");
                _httpListener.Start();
                _isListening = true;

                // Listen for the callback in a background thread
                Task.Run(async () =>
                {
                    try
                    {
                        while (_isListening && _httpListener.IsListening)
                        {
                            var context = await _httpListener.GetContextAsync();
                            ProcessCallback(context);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isListening) // Only log if we're supposed to be listening
                        {
                            System.Diagnostics.Debug.WriteLine($"HTTP listener error: {ex.Message}");
                        }
                    }
                });

                // Now proceed with authentication
                if (string.IsNullOrEmpty(_clientId))
                {
                    ShowConfigurationNeeded();
                }
                else
                {
                    InitializeWebView();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to start local server: {ex.Message}\\n\\nTrying manual token entry instead.");
                ShowManualTokenDialog();
            }
        }

        private void ProcessCallback(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Send a simple HTML response
                var html = @"
                    <html>
                    <head><title>Spotify Authentication</title></head>
                    <body style='font-family: Arial; text-align: center; padding: 50px;'>
                        <h1>✅ Authentication Successful!</h1>
                        <p>You can close this window and return to EZStreamer.</p>
                        <script>
                            // Extract token from URL fragment and send to parent
                            if (window.location.hash) {
                                var hash = window.location.hash.substring(1);
                                var params = new URLSearchParams(hash);
                                var token = params.get('access_token');
                                if (token) {
                                    // Try to close the window
                                    setTimeout(() => window.close(), 2000);
                                }
                            }
                        </script>
                    </body>
                    </html>";

                var buffer = Encoding.UTF8.GetBytes(html);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                // Parse the URL for the access token
                var url = request.Url.ToString();
                if (url.Contains("#"))
                {
                    // Handle fragment (OAuth implicit flow)
                    var fragment = url.Split('#')[1];
                    var queryParams = HttpUtility.ParseQueryString(fragment);
                    var accessToken = queryParams["access_token"];
                    var error = queryParams["error"];

                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(error))
                        {
                            ShowError($"Authentication failed: {error}");
                        }
                        else if (!string.IsNullOrEmpty(accessToken))
                        {
                            AccessToken = accessToken;
                            IsAuthenticated = true;
                            MessageBox.Show("Successfully connected to Spotify!", "Authentication Successful", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                            Close();
                        }
                    });
                }
                else if (request.QueryString["code"] != null)
                {
                    // Handle authorization code (OAuth authorization code flow)
                    var code = request.QueryString["code"];
                    var error = request.QueryString["error"];

                    Dispatcher.Invoke(() =>
                    {
                        if (!string.IsNullOrEmpty(error))
                        {
                            ShowError($"Authentication failed: {error}");
                        }
                        else if (!string.IsNullOrEmpty(code))
                        {
                            // Would need to exchange code for token, but for simplicity using implicit flow
                            ShowError("Received authorization code. Please configure your Spotify app to use 'Implicit Grant' flow.");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing callback: {ex.Message}");
            }
        }

        private void InitializeWebView()
        {
            try
            {
                this.Loaded += SpotifyAuthWindow_Loaded;
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing web view: {ex.Message}");
            }
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
                "You can get a Client ID from the Spotify Developer Dashboard:\\n" +
                "https://developer.spotify.com/dashboard\\n\\n" +
                "IMPORTANT: Use this EXACT redirect URI in your Spotify app:\\n" +
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
                $"Enter your Spotify Application Client ID:\\n\\nIMPORTANT: Make sure your Spotify app uses this EXACT redirect URI:\\n{REDIRECT_URI}\\n\\nAlso enable 'Implicit Grant' in your app settings.");
            
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

                // Navigate to Spotify OAuth URL with implicit grant
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
                    ShowError("Failed to initialize web browser. Trying manual token entry...");
                    ShowManualTokenDialog();
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
            
            // Check if this is our callback URL
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                // Let it navigate to our local server
                LoadingPanel.Visibility = Visibility.Visible;
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
                ShowError($"Navigation failed: {e.WebErrorStatus}\\n\\nTrying manual token entry instead.");
                ShowManualTokenDialog();
                return;
            }

            try
            {
                var currentUrl = AuthWebView.CoreWebView2.Source;
                System.Diagnostics.Debug.WriteLine($"Navigation completed to: {currentUrl}");
                
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
                // Stop the HTTP listener
                _isListening = false;
                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                }

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
