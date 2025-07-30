using System;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchLib.Api;
using EZStreamer.Models;

namespace EZStreamer.Services
{
    public class TwitchService
    {
        private TwitchClient _client;
        private TwitchPubSub _pubSub;
        private TwitchAPI _api;
        private string _channelName;
        private string _accessToken;

        public bool IsConnected => _client?.IsConnected ?? false;
        public string ChannelName => _channelName;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> SongRequestReceived;
        public event EventHandler<string> ChannelPointRedemption;

        public TwitchService()
        {
            
        }

        public async Task Connect(string accessToken, string channelName = null)
        {
            try
            {
                _accessToken = accessToken;
                
                // Initialize API to get user info if channel name not provided
                _api = new TwitchAPI();
                _api.Settings.AccessToken = accessToken;
                
                if (string.IsNullOrEmpty(channelName))
                {
                    var user = await _api.Helix.Users.GetUsersAsync();
                    if (user.Users.Length > 0)
                    {
                        _channelName = user.Users[0].Login;
                    }
                    else
                    {
                        throw new Exception("Could not get user information from Twitch");
                    }
                }
                else
                {
                    _channelName = channelName.ToLower();
                }

                // Set up chat client
                var clientOptions = new ClientOptions
                {
                    MessagesAllowedInPeriod = 750,
                    ThrottlingPeriod = TimeSpan.FromSeconds(30)
                };
                
                var customClient = new WebSocketClient(clientOptions);
                _client = new TwitchClient(customClient);
                
                var credentials = new ConnectionCredentials(_channelName, accessToken);
                _client.Initialize(credentials, _channelName);

                // Set up event handlers
                _client.OnConnected += OnClientConnected;
                _client.OnDisconnected += OnClientDisconnected;
                _client.OnMessageReceived += OnClientMessageReceived;
                _client.OnJoinedChannel += OnClientJoinedChannel;

                // Set up PubSub for channel point redemptions
                _pubSub = new TwitchPubSub();
                _pubSub.OnChannelPointsRewardRedeemed += OnChannelPointsRewardRedeemed;
                _pubSub.OnPubSubServiceConnected += OnPubSubConnected;
                _pubSub.OnPubSubServiceError += OnPubSubError;

                // Connect
                _client.Connect();
                _pubSub.Connect();

                // Listen to channel points (need broadcaster ID)
                var broadcaster = await _api.Helix.Users.GetUsersAsync(logins: new[] { _channelName });
                if (broadcaster.Users.Length > 0)
                {
                    _pubSub.ListenToChannelPoints(broadcaster.Users[0].Id);
                    _pubSub.SendTopics(accessToken);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to Twitch: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                _client?.Disconnect();
                _pubSub?.Disconnect();
                _channelName = null;
                _accessToken = null;
                
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disconnecting from Twitch: {ex.Message}");
            }
        }

        public async Task UpdateStreamInfo(string title, string categoryName = null)
        {
            try
            {
                if (_api == null || string.IsNullOrEmpty(_accessToken))
                {
                    throw new Exception("Not connected to Twitch");
                }

                var broadcaster = await _api.Helix.Users.GetUsersAsync(logins: new[] { _channelName });
                if (broadcaster.Users.Length == 0)
                {
                    throw new Exception("Could not find broadcaster information");
                }

                var broadcasterId = broadcaster.Users[0].Id;
                
                // Get category ID if category name provided
                string categoryId = null;
                if (!string.IsNullOrEmpty(categoryName))
                {
                    var categories = await _api.Helix.Games.GetGamesAsync(gameNames: new[] { categoryName });
                    if (categories.Games.Length > 0)
                    {
                        categoryId = categories.Games[0].Id;
                    }
                }

                // Update channel information
                await _api.Helix.Channels.ModifyChannelInformationAsync(
                    broadcasterId: broadcasterId,
                    title: title,
                    gameId: categoryId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update stream info: {ex.Message}", ex);
            }
        }

        public void SendChatMessage(string message)
        {
            try
            {
                if (_client?.IsConnected == true)
                {
                    _client.SendMessage(_channelName, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending chat message: {ex.Message}");
            }
        }

        #region Event Handlers

        private void OnClientConnected(object sender, OnConnectedArgs e)
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        private void OnClientDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void OnClientJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Joined channel: {e.Channel}");
        }

        private void OnClientMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var message = e.ChatMessage.Message;
            var username = e.ChatMessage.Username;
            
            MessageReceived?.Invoke(this, $"{username}: {message}");

            // Check for song request command
            if (message.StartsWith("!songrequest", StringComparison.OrdinalIgnoreCase))
            {
                var songQuery = message.Substring(12).Trim(); // Remove "!songrequest"
                if (!string.IsNullOrEmpty(songQuery))
                {
                    SongRequestReceived?.Invoke(this, $"{username}|{songQuery}");
                }
            }
        }

        private void OnPubSubConnected(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("PubSub connected");
        }

        private void OnPubSubError(object sender, OnPubSubServiceErrorArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"PubSub error: {e.Exception?.Message}");
        }

        private void OnChannelPointsRewardRedeemed(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            var redemption = e.RewardRedeemed.Redemption;
            var username = redemption.User.DisplayName;
            var userInput = redemption.UserInput ?? "";
            
            ChannelPointRedemption?.Invoke(this, $"{username}|{userInput}");
        }

        #endregion
    }
}
