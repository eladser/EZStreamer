using System;
using System.Threading.Tasks;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using Microsoft.Extensions.Logging;

namespace EZStreamer.Services
{
    public class OBSService
    {
        private OBSWebsocket _obs;
        private bool _isConnected;
        private string _serverIP;
        private int _serverPort;
        private string _serverPassword;

        public bool IsConnected => _isConnected && _obs != null;
        public string ConnectionInfo => $"{_serverIP}:{_serverPort}";

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<string> ErrorOccurred;

        public OBSService()
        {
            _obs = new OBSWebsocket();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            _obs.Connected += OnObsConnected;
            _obs.Disconnected += OnObsDisconnected;
        }

        public async Task<bool> ConnectAsync(string serverIP = "localhost", int serverPort = 4455, string password = "")
        {
            try
            {
                _serverIP = serverIP;
                _serverPort = serverPort;
                _serverPassword = password;

                if (_isConnected)
                {
                    await DisconnectAsync();
                }

                // Connect to OBS WebSocket
                await Task.Run(() =>
                {
                    try
                    {
                        _obs.ConnectAsync($"ws://{serverIP}:{serverPort}", password);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to connect to OBS: {ex.Message}", ex);
                    }
                });

                // Wait a moment for connection to establish
                await Task.Delay(1000);

                if (_isConnected)
                {
                    return true;
                }
                else
                {
                    throw new Exception("Connection to OBS timed out");
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to connect to OBS: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_obs != null && _isConnected)
                {
                    await Task.Run(() => _obs.Disconnect());
                }
                _isConnected = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disconnecting from OBS: {ex.Message}");
            }
        }

        public async Task<bool> SwitchToScene(string sceneName)
        {
            try
            {
                if (!_isConnected)
                    return false;

                await Task.Run(() => _obs.SetCurrentProgramScene(sceneName));
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to switch scene: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ToggleSource(string sourceName, bool? visible = null)
        {
            try
            {
                if (!_isConnected)
                    return false;

                // Get current scene to find the source
                var currentScene = await Task.Run(() => _obs.GetCurrentProgramScene());
                if (currentScene == null)
                    return false;

                // Toggle or set source visibility
                var sourceVisible = visible ?? !await IsSourceVisible(sourceName);
                await Task.Run(() => _obs.SetSceneItemEnabled(currentScene.Name, sourceName, sourceVisible));
                
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to toggle source: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsSourceVisible(string sourceName)
        {
            try
            {
                if (!_isConnected)
                    return false;

                var currentScene = await Task.Run(() => _obs.GetCurrentProgramScene());
                if (currentScene?.SceneItems == null)
                    return false;

                foreach (var item in currentScene.SceneItems)
                {
                    if (item.SourceName == sourceName)
                    {
                        return item.Enabled;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking source visibility: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetSourceText(string sourceName, string text)
        {
            try
            {
                if (!_isConnected)
                    return false;

                // Set text for text sources
                await Task.Run(() => _obs.SetInputSettings(sourceName, new { text = text }));
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to set source text: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RefreshBrowserSource(string sourceName)
        {
            try
            {
                if (!_isConnected)
                    return false;

                // Refresh browser source
                await Task.Run(() => _obs.PressInputPropertiesButton(sourceName, "refreshnocache"));
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to refresh browser source: {ex.Message}");
                return false;
            }
        }

        public async Task<SceneList> GetScenes()
        {
            try
            {
                if (!_isConnected)
                    return null;

                return await Task.Run(() => _obs.GetSceneList());
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to get scenes: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetCurrentSceneName()
        {
            try
            {
                if (!_isConnected)
                    return string.Empty;

                var scene = await Task.Run(() => _obs.GetCurrentProgramScene());
                return scene?.Name ?? string.Empty;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to get current scene: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<bool> StartStreaming()
        {
            try
            {
                if (!_isConnected)
                    return false;

                await Task.Run(() => _obs.StartStream());
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start streaming: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopStreaming()
        {
            try
            {
                if (!_isConnected)
                    return false;

                await Task.Run(() => _obs.StopStream());
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to stop streaming: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartRecording()
        {
            try
            {
                if (!_isConnected)
                    return false;

                await Task.Run(() => _obs.StartRecord());
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopRecording()
        {
            try
            {
                if (!_isConnected)
                    return false;

                await Task.Run(() => _obs.StopRecord());
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to stop recording: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestConnection()
        {
            try
            {
                if (!_isConnected)
                    return false;

                // Try to get OBS version as a simple test
                var version = await Task.Run(() => _obs.GetVersion());
                return version != null;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Connection test failed: {ex.Message}");
                return false;
            }
        }

        #region Event Handlers

        private void OnObsConnected(object sender, EventArgs e)
        {
            _isConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
        }

        private void OnObsDisconnected(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            _isConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        public void Dispose()
        {
            Task.Run(async () => await DisconnectAsync());
            _obs?.Dispose();
        }
    }
}
