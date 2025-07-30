using System;
using System.Web;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace EZStreamer.Views
{
    public partial class TwitchAuthWindow : Window
    {
        private const string CLIENT_ID = "your_twitch_client_id"; // This should be configured
        private const string REDIRECT_URI = "http://localhost:3000/auth/twitch/callback";
        private const string SCOPES = "chat:read+chat:edit+channel:manage:broadcast+channel:read:redemptions+user:read:email";

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public TwitchAuthWindow()
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
                    // Navigate to Twitch OAuth URL
                    var authUrl = $"https://id.twitch.tv/oauth2/authorize" +
                                $"?response_type=token" +
                                $"&client_id={CLIENT_ID}" +
                                $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                                $"&scope={Uri.EscapeDataString(SCOPES)}";

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
                if (uri.Host == "localhost" && uri.LocalPath == "/auth/twitch/callback")
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
                        
                        MessageBox.Show("Successfully connected to Twitch!", "Authentication Successful", 
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
                    var errorDescription = queryParams["error_description"];
                    
                    ShowError($"Authentication failed: {error} - {errorDescription}");
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
            var dialog = new ManualTokenDialog("Twitch");
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

    // Simple dialog for manual token entry
    public partial class ManualTokenDialog : Window
    {
        public string Token { get; private set; }

        public ManualTokenDialog(string serviceName)
        {
            InitializeComponent();
            Title = $"Enter {serviceName} Token";
            ServiceLabel.Content = $"{serviceName} Access Token:";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TokenTextBox.Text))
            {
                MessageBox.Show("Please enter a valid token.", "Invalid Token", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Token = TokenTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InitializeComponent()
        {
            Width = 400;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center
            };

            ServiceLabel = new System.Windows.Controls.Label
            {
                Content = "Access Token:",
                FontWeight = FontWeights.Bold
            };
            stackPanel.Children.Add(ServiceLabel);

            TokenTextBox = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 10, 0, 20),
                Height = 25
            };
            stackPanel.Children.Add(TokenTextBox);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            cancelButton.Click += CancelButton_Click;
            buttonPanel.Children.Add(cancelButton);

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                IsDefault = true
            };
            okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(okButton);

            stackPanel.Children.Add(buttonPanel);
            
            System.Windows.Controls.Grid.SetRow(stackPanel, 0);
            grid.Children.Add(stackPanel);

            Content = grid;
        }

        private System.Windows.Controls.Label ServiceLabel;
        private System.Windows.Controls.TextBox TokenTextBox;
    }
}
