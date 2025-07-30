using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace EZStreamer.Services
{
    public class TwitchService
    {
        private string _channelName;
        private string _accessToken;
        private string _clientId;
        private bool _isConnected;
        private System.Net.WebSockets.ClientWebSocket _webSocket;
        private bool _isListening;
        private readonly HttpClient _httpClient;

        public bool IsConnected => _isConnected;
        public string ChannelName => _channelName;

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> SongRequestReceived;
        public event EventHandler<string> ChannelPointRedemption;

        public TwitchService()
        {
            _isConnected = false;
            _httpClient = new HttpClient();
        }

        public async Task ConnectAsync(string accessToken, string channelName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new ArgumentException("Access token is required");
                }

                _accessToken = accessToken;
                
                // Validate token and get user info
                var userInfo = await ValidateTokenAndGetUserAsync(accessToken);
                if (userInfo == null)
                {
                    throw new Exception("Invalid access token or failed to get user information");
                }

                _channelName = userInfo.login.ToLower();
                
                // Connect to Twitch IRC for chat
                await ConnectToIRC();
                
                _isConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
                
                System.Diagnostics.Debug.WriteLine($"Connected to Twitch as {_channelName}");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                throw new Exception($"Failed to connect to Twitch: {ex.Message}", ex);
            }
        }

        private async Task<TwitchUser> ValidateTokenAndGetUserAsync(string accessToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
                request.Headers.Add("Authorization", $"Bearer {accessToken}");
                request.Headers.Add("Client-ID", _clientId ?? ""); // Will be set from config

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Token validation failed: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var userResponse = JsonSerializer.Deserialize<TwitchUserResponse>(content);
                
                return userResponse?.data?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating token: {ex.Message}");
                return null;
            }
        }

        private async Task ConnectToIRC()
        {
            try
            {
                _webSocket = new System.Net.WebSockets.ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv:443"), System.Threading.CancellationToken.None);

                // Authenticate
                await SendIRCMessage($"PASS oauth:{_accessToken}");
                await SendIRCMessage($"NICK {_channelName}");
                await SendIRCMessage($"JOIN #{_channelName}");

                // Start listening for messages
                _isListening = true;
                _ = Task.Run(ListenForMessages);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IRC connection failed: {ex.Message}");
                throw;
            }
        }

        private async Task SendIRCMessage(string message)
        {
            try
            {
                if (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(message + "\r\n");
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), 
                        System.Net.WebSockets.WebSocketMessageType.Text, true, System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send IRC message: {ex.Message}");
            }
        }

        private async Task ListenForMessages()
        {
            try
            {
                var buffer = new byte[4096];
                
                while (_isListening && _webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
                    
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessIRCMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listening for messages: {ex.Message}");
                _isListening = false;
            }
        }

        private async Task ProcessIRCMessage(string message)
        {
            try
            {
                var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Handle PING
                    if (trimmed.StartsWith("PING"))
                    {
                        await SendIRCMessage(trimmed.Replace("PING", "PONG"));
                        continue;
                    }
                    
                    // Handle PRIVMSG (chat messages)
                    if (trimmed.Contains("PRIVMSG"))
                    {
                        ProcessChatMessage(trimmed);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing IRC message: {ex.Message}");
            }
        }

        private void ProcessChatMessage(string ircMessage)
        {
            try
            {
                // Parse IRC message format: :username!username@username.tmi.twitch.tv PRIVMSG #channel :message
                var parts = ircMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) return;

                var userPart = parts[0].TrimStart(':');
                var username = userPart.Split('!')[0];
                
                var messageStart = ircMessage.IndexOf(" :", ircMessage.IndexOf("PRIVMSG"));
                if (messageStart == -1) return;
                
                var chatMessage = ircMessage.Substring(messageStart + 2);
                
                System.Diagnostics.Debug.WriteLine($"Chat: {username}: {chatMessage}");
                
                // Raise message received event
                MessageReceived?.Invoke(this, $"{username}: {chatMessage}");
                
                // Check for song request commands
                if (chatMessage.StartsWith("!songrequest ", StringComparison.OrdinalIgnoreCase) ||
                    chatMessage.StartsWith("!sr ", StringComparison.OrdinalIgnoreCase))
                {
                    var command = chatMessage.StartsWith("!songrequest ", StringComparison.OrdinalIgnoreCase) ? "!songrequest " : "!sr ";
                    var songQuery = chatMessage.Substring(command.Length).Trim();
                    
                    if (!string.IsNullOrEmpty(songQuery))
                    {
                        System.Diagnostics.Debug.WriteLine($"Song request from {username}: {songQuery}");
                        SongRequestReceived?.Invoke(this, $"{username}|{songQuery}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing chat message: {ex.Message}");
            }
        }

        public void Connect(string accessToken)
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await ConnectAsync(accessToken, null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Twitch connection failed: {ex.Message}");
                        _isConnected = false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting Twitch connection: {ex.Message}");
                _isConnected = false;
            }
        }

        public void Disconnect()
        {
            try
            {
                _isListening = false;
                _isConnected = false;
                
                if (_webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _webSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, 
                                "Disconnecting", System.Threading.CancellationToken.None);
                        }
                        catch { }
                    });
                }
                
                _channelName = null;
                _accessToken = null;
                
                Disconnected?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine("Disconnected from Twitch");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disconnecting from Twitch: {ex.Message}");
            }
        }

        public async Task UpdateStreamInfoAsync(string title, string categoryName = null)
        {
            try
            {
                if (!_isConnected || string.IsNullOrEmpty(_accessToken))
                {
                    throw new Exception("Not connected to Twitch");
                }

                if (string.IsNullOrEmpty(title))
                {
                    throw new ArgumentException("Title is required");
                }

                // Get user ID first
                var userInfo = await ValidateTokenAndGetUserAsync(_accessToken);
                if (userInfo == null)
                {
                    throw new Exception("Failed to get user information");
                }

                // Update channel information
                var updateData = new
                {
                    game_id = "",  // Would need to look up category ID
                    title = title
                };

                var request = new HttpRequestMessage(HttpMethod.Patch, $"https://api.twitch.tv/helix/channels?broadcaster_id={userInfo.id}");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                request.Headers.Add("Client-ID", _clientId ?? "");
                request.Content = new StringContent(JsonSerializer.Serialize(updateData), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Stream info updated: Title='{title}', Category='{categoryName ?? "No category"}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Stream info update failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stream info update failed: {ex.Message}");
                throw;
            }
        }

        public void UpdateStreamInfo(string title, string category)
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await UpdateStreamInfoAsync(title, category);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Stream info update error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating stream info: {ex.Message}");
            }
        }

        public void SendChatMessage(string message)
        {
            try
            {
                if (!_isConnected)
                {
                    throw new Exception("Not connected to Twitch");
                }

                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                Task.Run(async () =>
                {
                    try
                    {
                        await SendIRCMessage($"PRIVMSG #{_channelName} :{message}");
                        System.Diagnostics.Debug.WriteLine($"Chat message sent: {message}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to send chat message: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending chat message: {ex.Message}");
            }
        }

        // Method to simulate receiving a song request (for testing)
        public void SimulateSongRequest(string username, string songQuery)
        {
            try
            {
                if (_isConnected)
                {
                    SongRequestReceived?.Invoke(this, $"{username}|{songQuery}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error simulating song request: {ex.Message}");
            }
        }

        // Method to simulate receiving a channel point redemption (for testing)
        public void SimulateChannelPointRedemption(string username, string userInput)
        {
            try
            {
                if (_isConnected)
                {
                    ChannelPointRedemption?.Invoke(this, $"{username}|{userInput}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error simulating channel point redemption: {ex.Message}");
            }
        }

        // Validate access token (now actually validates)
        public async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    return false;
                }

                var userInfo = await ValidateTokenAndGetUserAsync(accessToken);
                return userInfo != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token validation error: {ex.Message}");
                return false;
            }
        }

        public bool ValidateToken(string accessToken)
        {
            try
            {
                var result = false;
                Task.Run(async () =>
                {
                    result = await ValidateTokenAsync(accessToken);
                }).Wait(5000); // 5 second timeout
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating token: {ex.Message}");
                return false;
            }
        }

        public void SetClientId(string clientId)
        {
            _clientId = clientId;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isListening = false;
                _webSocket?.Dispose();
                _httpClient?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    // Models for Twitch API responses
    public class TwitchUserResponse
    {
        public TwitchUser[] data { get; set; }
    }

    public class TwitchUser
    {
        public string id { get; set; }
        public string login { get; set; }
        public string display_name { get; set; }
        public string type { get; set; }
        public string broadcaster_type { get; set; }
        public string description { get; set; }
        public string profile_image_url { get; set; }
        public string offline_image_url { get; set; }
        public int view_count { get; set; }
        public string email { get; set; }
        public DateTime created_at { get; set; }
    }
}
