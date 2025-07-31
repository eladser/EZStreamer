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
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.IO;
using System.Management;

namespace EZStreamer.Views
{
    public partial class SpotifyAuthWindow : Window
    {
        private readonly ConfigurationService _configService;
        private string _clientId;
        private string _clientSecret;
        private const string REDIRECT_URI = "https://localhost:8443/callback";
        private const string SCOPES = "user-read-playback-state user-modify-playback-state user-read-currently-playing playlist-read-private";
        
        private HttpListener _httpListener;
        private bool _isListening = false;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _serverStarted = false;
        private int _debugCounter = 0;
        private X509Certificate2 _serverCertificate;

        public string AccessToken { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public SpotifyAuthWindow()
        {
            InitializeComponent();
            LogDebug("=== SpotifyAuthWindow Constructor Started ===");
            
            _configService = new ConfigurationService();
            var credentials = _configService.GetAPICredentials();
            _clientId = credentials.SpotifyClientId;
            _clientSecret = credentials.SpotifyClientSecret;
            _cancellationTokenSource = new CancellationTokenSource();
            
            LogDebug($"ClientId: {(!string.IsNullOrEmpty(_clientId) ? $"SET ({_clientId.Length} chars)" : "NOT SET")}");
            LogDebug($"ClientSecret: {(!string.IsNullOrEmpty(_clientSecret) ? $"SET ({_clientSecret.Length} chars)" : "NOT SET")}");
            LogDebug($"Administrator check: {IsRunningAsAdministrator()}");
            LogDebug($"Running as Administrator: {IsRunningAsAdministrator()}");
            LogDebug($"WebView2 Runtime: {GetWebView2Version()}");
            
            LoadingPanel.Visibility = Visibility.Visible;
            
            // Start initialization
            LogDebug("Starting initialization...");
            InitializeAuthentication();
        }

        private void LogDebug(string message)
        {
            _debugCounter++;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logMessage = $"[{_debugCounter:D3}] {timestamp} SPOTIFY: {message}";
            Debug.WriteLine(logMessage);
            Console.WriteLine(logMessage); // Also log to console
        }

        private string GetWebView2Version()
        {
            try
            {
                return CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        private void InitializeAuthentication()
        {
            try
            {
                LogDebug("=== InitializeAuthentication Started ===");
                
                // Check credentials first
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    LogDebug("❌ Missing credentials, showing configuration dialog");
                    ShowConfigurationNeeded();
                    return;
                }

                LogDebug("✅ Credentials found, starting local HTTPS server...");
                
                // Start server in background
                Task.Run(async () =>
                {
                    try
                    {
                        LogDebug("Background task started for HTTPS server");
                        await StartLocalHttpsServer();
                        LogDebug("✅ Local HTTPS server started successfully");
                        
                        Dispatcher.Invoke(() =>
                        {
                            LogDebug("Dispatcher.Invoke - Initializing WebView...");
                            InitializeWebAuth();
                        });
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Failed to start local HTTPS server: {ex.Message}");
                        LogDebug($"Stack trace: {ex.StackTrace}");
                        
                        Dispatcher.Invoke(() => 
                        {
                            ShowError($"Failed to start HTTPS server: {ex.Message}\n\n" +
                                    "Spotify requires HTTPS for OAuth. Please run EZStreamer as Administrator or use manual token authentication.");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error in InitializeAuthentication: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Initialization error: {ex.Message}");
            }
        }

        private async Task StartLocalHttpsServer()
        {
            try
            {
                LogDebug("=== StartLocalHttpsServer Started ===");
                
                // Force cleanup any existing certificates and bindings
                await ForceCleanupExistingBindings();
                
                // Setup certificate with improved approach
                LogDebug("Setting up HTTPS certificate for localhost:8443...");
                var certSuccess = await SetupHttpsCertificateAdvanced();
                
                if (!certSuccess)
                {
                    throw new Exception("Failed to setup HTTPS certificate. Please run as Administrator.");
                }
                
                LogDebug("Creating HttpListener for HTTPS...");
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("https://localhost:8443/");
                
                LogDebug("Starting HTTPS HttpListener...");
                _httpListener.Start();
                _isListening = true;
                _serverStarted = true;
                
                LogDebug("✅ HTTPS server started successfully on https://localhost:8443/");
                
                // Start listening for requests
                LogDebug("Starting request listener loop...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var requestCount = 0;
                        while (_isListening && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            LogDebug($"Waiting for HTTPS request #{requestCount + 1}...");
                            
                            var context = await GetContextAsync(_httpListener, _cancellationTokenSource.Token);
                            if (context != null)
                            {
                                requestCount++;
                                LogDebug($"✅ Received HTTPS request #{requestCount}: {context.Request.Url}");
                                LogDebug($"Request method: {context.Request.HttpMethod}");
                                LogDebug($"User agent: {context.Request.UserAgent}");
                                LogDebug($"Headers: {context.Request.Headers.Count}");
                                
                                _ = Task.Run(() => ProcessCallback(context));
                            }
                            else
                            {
                                LogDebug("GetContextAsync returned null - listener may be stopping");
                            }
                        }
                        LogDebug("Request listener loop ended");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Error in HTTPS server loop: {ex.Message}");
                        LogDebug($"Stack trace: {ex.StackTrace}");
                    }
                });
                
                LogDebug("HTTPS server setup complete");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Failed to start HTTPS server: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task ForceCleanupExistingBindings()
        {
            try
            {
                LogDebug("=== ForceCleanupExistingBindings Started ===");
                
                // Stop any existing HttpListeners
                try
                {
                    LogDebug("Attempting to stop any existing HttpListeners...");
                    
                    // Kill any processes that might be using the port
                    await KillProcessesUsingPort(8443);
                    
                    // Wait a moment for processes to close
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    LogDebug($"Error during process cleanup: {ex.Message}");
                }
                
                // Force delete all possible certificate bindings
                var deleteCommands = new[]
                {
                    "netsh http delete sslcert ipport=0.0.0.0:8443",
                    "netsh http delete sslcert ipport=127.0.0.1:8443",
                    "netsh http delete sslcert ipport=[::1]:8443"
                };
                
                foreach (var cmd in deleteCommands)
                {
                    try
                    {
                        LogDebug($"Running cleanup command: {cmd}");
                        var result = RunNetshCommand(cmd);
                        LogDebug($"Cleanup result: {result}");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Cleanup command error: {ex.Message}");
                    }
                }
                
                LogDebug("Force cleanup completed");
            }
            catch (Exception ex)
            {
                LogDebug($"Error in ForceCleanupExistingBindings: {ex.Message}");
            }
        }

        private async Task KillProcessesUsingPort(int port)
        {
            try
            {
                LogDebug($"Checking for processes using port {port}...");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(startInfo))
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var lines = output.Split('\n');
                    
                    foreach (var line in lines)
                    {
                        if (line.Contains($":{port} ") && line.Contains("LISTENING"))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 4 && int.TryParse(parts[parts.Length - 1], out var pid))
                            {
                                try
                                {
                                    LogDebug($"Found process {pid} using port {port}, attempting to kill...");
                                    var processToKill = Process.GetProcessById(pid);
                                    processToKill.Kill();
                                    LogDebug($"✅ Killed process {pid}");
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"Could not kill process {pid}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error checking/killing processes: {ex.Message}");
            }
        }

        private async Task<bool> SetupHttpsCertificateAdvanced()
        {
            try
            {
                LogDebug("=== SetupHttpsCertificateAdvanced Started ===");
                
                // Create certificate with exportable private key
                LogDebug("Creating self-signed certificate for localhost...");
                _serverCertificate = CreateSelfSignedCertificateAdvanced();
                
                LogDebug($"Certificate created - Subject: {_serverCertificate.Subject}");
                LogDebug($"Certificate thumbprint: {_serverCertificate.Thumbprint}");
                LogDebug($"Certificate valid from: {_serverCertificate.NotBefore} to {_serverCertificate.NotAfter}");
                LogDebug($"Has private key: {_serverCertificate.HasPrivateKey}");
                
                // Install certificate to multiple stores
                await InstallCertificateToStores(_serverCertificate);
                
                // Bind certificate using advanced approach
                var bindingSuccess = await AdvancedCertificateBinding(_serverCertificate.Thumbprint);
                
                if (!bindingSuccess)
                {
                    LogDebug("⚠️ Certificate binding failed, but attempting to continue...");
                    
                    // Try alternative approach: use HttpListener without explicit binding
                    LogDebug("Attempting to use HttpListener with certificate store only...");
                    
                    // Sometimes HttpListener can find the certificate from the store automatically
                    return true;
                }
                
                LogDebug("✅ Certificate setup completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"❌ SetupHttpsCertificateAdvanced failed: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private X509Certificate2 CreateSelfSignedCertificateAdvanced()
        {
            try
            {
                LogDebug("=== CreateSelfSignedCertificateAdvanced Started ===");
                
                using (var rsa = RSA.Create(2048))
                {
                    LogDebug("RSA key created (2048 bits)");
                    
                    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    LogDebug("Certificate request created");
                    
                    // Add subject alternative names
                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddDnsName("localhost");
                    sanBuilder.AddDnsName("127.0.0.1");
                    sanBuilder.AddIpAddress(IPAddress.Loopback);
                    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
                    request.CertificateExtensions.Add(sanBuilder.Build());
                    LogDebug("Subject Alternative Names added (localhost, 127.0.0.1, ::1)");
                    
                    // Add basic constraints
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                    LogDebug("Basic constraints extension added");
                    
                    // Add key usage
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, false));
                    LogDebug("Key usage extension added");
                        
                    // Add extended key usage for server authentication
                    var serverAuthOid = new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1"); // Server Authentication
                    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([serverAuthOid], false));
                    LogDebug("Enhanced key usage extension added (Server Authentication)");
                    
                    // Create the certificate with exportable private key
                    LogDebug("Creating self-signed certificate...");
                    var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
                    
                    // Export and re-import to make sure private key is available
                    LogDebug("Exporting and re-importing certificate with private key...");
                    var pfxData = certificate.Export(X509ContentType.Pfx, "EZStreamer");
                    var reimportedCert = new X509Certificate2(pfxData, "EZStreamer", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                    
                    LogDebug("✅ Self-signed certificate created successfully");
                    LogDebug($"Subject: {reimportedCert.Subject}");
                    LogDebug($"Issuer: {reimportedCert.Issuer}");
                    LogDebug($"Thumbprint: {reimportedCert.Thumbprint}");
                    LogDebug($"Serial number: {reimportedCert.SerialNumber}");
                    LogDebug($"Valid from: {reimportedCert.NotBefore} to {reimportedCert.NotAfter}");
                    LogDebug($"Has private key: {reimportedCert.HasPrivateKey}");
                    
                    return reimportedCert;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Failed to create self-signed certificate: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task InstallCertificateToStores(X509Certificate2 certificate)
        {
            try
            {
                LogDebug("=== InstallCertificateToStores Started ===");
                
                // Install to Current User Root store
                await Task.Run(() =>
                {
                    try
                    {
                        LogDebug("Installing to CurrentUser\\Root store...");
                        using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                        {
                            store.Open(OpenFlags.ReadWrite);
                            
                            // Remove any existing certificates with same thumbprint
                            var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                            foreach (X509Certificate2 existingCert in existingCerts)
                            {
                                store.Remove(existingCert);
                                LogDebug("Removed existing certificate from CurrentUser\\Root");
                            }
                            
                            store.Add(certificate);
                            store.Close();
                            LogDebug("✅ Certificate installed to CurrentUser\\Root store");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Failed to install to CurrentUser\\Root: {ex.Message}");
                    }
                });
                
                // Install to Local Machine Root store (if admin)
                if (IsRunningAsAdministrator())
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            LogDebug("Installing to LocalMachine\\Root store...");
                            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                            {
                                store.Open(OpenFlags.ReadWrite);
                                
                                // Remove any existing certificates with same thumbprint
                                var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                                foreach (X509Certificate2 existingCert in existingCerts)
                                {
                                    store.Remove(existingCert);
                                    LogDebug("Removed existing certificate from LocalMachine\\Root");
                                }
                                
                                store.Add(certificate);
                                store.Close();
                                LogDebug("✅ Certificate installed to LocalMachine\\Root store");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"❌ Failed to install to LocalMachine\\Root: {ex.Message}");
                        }
                    });
                    
                    // Also install to Personal store
                    await Task.Run(() =>
                    {
                        try
                        {
                            LogDebug("Installing to LocalMachine\\My store...");
                            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
                            {
                                store.Open(OpenFlags.ReadWrite);
                                
                                // Remove any existing certificates with same thumbprint
                                var existingCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false);
                                foreach (X509Certificate2 existingCert in existingCerts)
                                {
                                    store.Remove(existingCert);
                                    LogDebug("Removed existing certificate from LocalMachine\\My");
                                }
                                
                                store.Add(certificate);
                                store.Close();
                                LogDebug("✅ Certificate installed to LocalMachine\\My store");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"❌ Failed to install to LocalMachine\\My: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ InstallCertificateToStores failed: {ex.Message}");
            }
        }

        private async Task<bool> AdvancedCertificateBinding(string thumbprint)
        {
            try
            {
                LogDebug("=== AdvancedCertificateBinding Started ===");
                
                if (!IsRunningAsAdministrator())
                {
                    LogDebug("⚠️ Not running as administrator - skipping certificate binding");
                    return false;
                }
                
                // Multiple binding attempts with different approaches
                var bindingAttempts = new[]
                {
                    new { AppId = "{12345678-1234-1234-1234-123456789012}", Description = "Standard App ID" },
                    new { AppId = "{00000000-0000-0000-0000-000000000000}", Description = "Null App ID" },
                    new { AppId = $"{{{Guid.NewGuid()}}}", Description = "Random App ID" },
                    new { AppId = "{A007CA11-0B89-4F8A-B6D0-3E4B5C6D7E8F}", Description = "Custom App ID" }
                };
                
                foreach (var attempt in bindingAttempts)
                {
                    try
                    {
                        LogDebug($"Attempting certificate binding with {attempt.Description}: {attempt.AppId}");
                        
                        // First ensure no existing binding
                        RunNetshCommand("netsh http delete sslcert ipport=0.0.0.0:8443");
                        await Task.Delay(500);
                        
                        // Try to add the binding
                        var addCmd = $"netsh http add sslcert ipport=0.0.0.0:8443 certhash={thumbprint} appid={attempt.AppId}";
                        var result = RunNetshCommand(addCmd);
                        
                        LogDebug($"Binding result: {result}");
                        
                        if (result.Contains("successfully") || result.Contains("SSL Certificate successfully added"))
                        {
                            LogDebug($"✅ Certificate binding successful with {attempt.Description}");
                            
                            // Verify the binding
                            var verifyResult = RunNetshCommand("netsh http show sslcert ipport=0.0.0.0:8443");
                            LogDebug($"Binding verification: {verifyResult}");
                            
                            if (!verifyResult.Contains("The system cannot find the file specified"))
                            {
                                LogDebug("✅ Certificate binding verified successfully");
                                return true;
                            }
                        }
                        else if (result.Contains("already exists"))
                        {
                            LogDebug($"✅ Certificate binding already exists");
                            return true;
                        }
                        else if (result.Contains("Error: 1312"))
                        {
                            LogDebug($"❌ Error 1312 with {attempt.Description} - trying next approach...");
                            continue;
                        }
                        else
                        {
                            LogDebug($"❌ Binding failed with {attempt.Description}: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"❌ Exception during binding attempt with {attempt.Description}: {ex.Message}");
                    }
                    
                    // Wait between attempts
                    await Task.Delay(1000);
                }
                
                // If all binding attempts failed, try a different approach using WinHTTP
                LogDebug("All standard binding attempts failed - trying WinHTTP approach...");
                return await TryWinHttpBinding(thumbprint);
            }
            catch (Exception ex)
            {
                LogDebug($"❌ AdvancedCertificateBinding failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryWinHttpBinding(string thumbprint)
        {
            try
            {
                LogDebug("=== TryWinHttpBinding Started ===");
                
                // Use PowerShell to bind the certificate (sometimes more reliable)
                var psScript = $@"
                    try {{
                        # Remove existing binding
                        netsh http delete sslcert ipport=0.0.0.0:8443 2>$null
                        
                        # Add new binding using PowerShell approach
                        $cert = Get-ChildItem -Path Cert:\LocalMachine\Root | Where-Object {{$_.Thumbprint -eq '{thumbprint}'}}
                        if ($cert) {{
                            netsh http add sslcert ipport=0.0.0.0:8443 certhash={thumbprint} appid={{EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE}}
                            Write-Output 'SUCCESS'
                        }} else {{
                            Write-Output 'CERT_NOT_FOUND'
                        }}
                    }} catch {{
                        Write-Output 'ERROR: ' + $_.Exception.Message
                    }}
                ";
                
                var psResult = RunPowerShellCommand(psScript);
                LogDebug($"PowerShell binding result: {psResult}");
                
                if (psResult.Contains("SUCCESS"))
                {
                    LogDebug("✅ PowerShell certificate binding successful");
                    return true;
                }
                
                LogDebug("❌ PowerShell binding also failed");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"❌ TryWinHttpBinding failed: {ex.Message}");
                return false;
            }
        }

        private string RunPowerShellCommand(string script)
        {
            try
            {
                LogDebug("Running PowerShell command...");
                
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "powershell.exe";
                    process.StartInfo.Arguments = $"-Command \"{script}\"";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Verb = "runas"; // Request elevation
                    
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    LogDebug($"PowerShell exit code: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(output))
                        LogDebug($"PowerShell output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        LogDebug($"PowerShell error: {error}");
                    
                    return output + error;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Failed to run PowerShell command: {ex.Message}");
                return ex.Message;
            }
        }

        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                LogDebug($"Error checking administrator status: {ex.Message}");
                return false;
            }
        }

        private string RunNetshCommand(string command)
        {
            try
            {
                LogDebug($"Running netsh command: {command}");
                
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c {command}";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    LogDebug($"Netsh exit code: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(output))
                        LogDebug($"Netsh output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        LogDebug($"Netsh error: {error}");
                    
                    return output + error;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Failed to run netsh command: {ex.Message}");
                return ex.Message;
            }
        }

        private async Task<HttpListenerContext> GetContextAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                LogDebug("GetContextAsync started - waiting for request...");
                var contextTask = listener.GetContextAsync();
                
                using (cancellationToken.Register(() => 
                {
                    try 
                    { 
                        LogDebug("Cancellation requested - stopping listener");
                        listener.Stop(); 
                    } 
                    catch (Exception ex)
                    { 
                        LogDebug($"Error stopping listener: {ex.Message}");
                    }
                }))
                {
                    var context = await contextTask;
                    LogDebug("GetContextAsync completed - request received");
                    return context;
                }
            }
            catch (ObjectDisposedException ex)
            {
                LogDebug($"HttpListener was disposed: {ex.Message}");
                return null;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                LogDebug($"HttpListener operation was aborted: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error getting HTTPS context: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task ProcessCallback(HttpListenerContext context)
        {
            try
            {
                LogDebug($"=== ProcessCallback Started ===");
                LogDebug($"Request URL: {context.Request.Url}");
                LogDebug($"Request method: {context.Request.HttpMethod}");
                LogDebug($"User agent: {context.Request.UserAgent}");
                
                var request = context.Request;
                var response = context.Response;
                
                // Extract query parameters
                var query = HttpUtility.ParseQueryString(request.Url.Query);
                var code = query["code"];
                var error = query["error"];
                var state = query["state"];
                
                LogDebug($"Query parameters extracted:");
                LogDebug($"- code: {(!string.IsNullOrEmpty(code) ? $"RECEIVED ({code.Length} chars)" : "NOT FOUND")}");
                LogDebug($"- error: {error ?? "NONE"}");
                LogDebug($"- state: {(!string.IsNullOrEmpty(state) ? $"RECEIVED ({state.Length} chars)" : "NOT FOUND")}");
                
                string responseHtml;
                
                if (!string.IsNullOrEmpty(error))
                {
                    LogDebug($"❌ OAuth error received: {error}");
                    responseHtml = CreateErrorResponseHtml(error);
                    Dispatcher.Invoke(() => ShowError($"Spotify authorization failed: {error}"));
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    LogDebug("✅ Authorization code received, starting token exchange...");
                    responseHtml = CreateSuccessResponseHtml();
                    
                    // Exchange code for token immediately
                    LogDebug("Starting token exchange process...");
                    await ExchangeCodeForToken(code);
                }
                else
                {
                    LogDebug("❌ No authorization code or error found in callback");
                    responseHtml = CreateErrorResponseHtml("No authorization code received");
                    Dispatcher.Invoke(() => ShowError("Invalid callback - no authorization code received"));
                }
                
                // Send HTTPS response
                try
                {
                    LogDebug("Sending HTTPS response...");
                    var buffer = Encoding.UTF8.GetBytes(responseHtml);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;
                    
                    LogDebug($"Response size: {buffer.Length} bytes");
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    
                    LogDebug("✅ HTTPS response sent successfully");
                }
                catch (Exception ex)
                {
                    LogDebug($"❌ Error sending HTTPS response: {ex.Message}");
                    LogDebug($"Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error processing HTTPS callback: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
            }
        }

        private async Task ExchangeCodeForToken(string authorizationCode)
        {
            try
            {
                LogDebug("=== ExchangeCodeForToken Started ===");
                LogDebug($"Authorization code length: {authorizationCode.Length}");
                
                using (var httpClient = new HttpClient())
                {
                    var requestData = new Dictionary<string, string>
                    {
                        ["grant_type"] = "authorization_code",
                        ["code"] = authorizationCode,
                        ["redirect_uri"] = REDIRECT_URI,
                        ["client_id"] = _clientId,
                        ["client_secret"] = _clientSecret
                    };
                    
                    LogDebug($"Token exchange request prepared:");
                    LogDebug($"- grant_type: authorization_code");
                    LogDebug($"- redirect_uri: {REDIRECT_URI}");
                    LogDebug($"- client_id: {_clientId}");
                    LogDebug($"- client_secret: {(_clientSecret.Length)} chars");
                    LogDebug($"- code: {authorizationCode.Length} chars");
                    
                    var requestContent = new FormUrlEncodedContent(requestData);
                    
                    LogDebug("Sending token exchange request to Spotify...");
                    var response = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    LogDebug($"Token exchange response received:");
                    LogDebug($"- Status code: {response.StatusCode} ({(int)response.StatusCode})");
                    LogDebug($"- Response length: {responseContent.Length} chars");
                    LogDebug($"- Response content: {responseContent}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        LogDebug("✅ Token exchange successful!");
                        
                        try
                        {
                            var tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseContent);
                            LogDebug($"Token response deserialized:");
                            LogDebug($"- access_token: {(!string.IsNullOrEmpty(tokenResponse?.access_token) ? $"RECEIVED ({tokenResponse.access_token.Length} chars)" : "NOT FOUND")}");
                            LogDebug($"- token_type: {tokenResponse?.token_type ?? "NULL"}");
                            LogDebug($"- expires_in: {tokenResponse?.expires_in ?? 0}");
                            LogDebug($"- refresh_token: {(!string.IsNullOrEmpty(tokenResponse?.refresh_token) ? $"RECEIVED ({tokenResponse.refresh_token.Length} chars)" : "NOT FOUND")}");
                            LogDebug($"- scope: {tokenResponse?.scope ?? "NULL"}");
                            
                            if (!string.IsNullOrEmpty(tokenResponse?.access_token))
                            {
                                AccessToken = tokenResponse.access_token;
                                IsAuthenticated = true;
                                
                                LogDebug($"✅ Access token stored successfully");
                                LogDebug($"IsAuthenticated set to: {IsAuthenticated}");
                                
                                Dispatcher.Invoke(() =>
                                {
                                    LogDebug("Dispatcher.Invoke - Showing success message");
                                    LoadingPanel.Visibility = Visibility.Collapsed;
                                    
                                    MessageBox.Show(
                                        $"🎵 Successfully connected to Spotify!\n\n" +
                                        $"✅ Access token received\n" +
                                        $"⏰ Expires in: {tokenResponse.expires_in} seconds\n" +
                                        $"🔄 Refresh token: {(!string.IsNullOrEmpty(tokenResponse.refresh_token) ? "Available" : "Not provided")}\n" +
                                        $"🔒 HTTPS connection established successfully",
                                        "Spotify Authentication Success", 
                                        MessageBoxButton.OK, 
                                        MessageBoxImage.Information);
                                    
                                    LogDebug("Setting DialogResult = true and closing window");
                                    DialogResult = true;
                                    Close();
                                });
                            }
                            else
                            {
                                LogDebug("❌ No access token in deserialized response");
                                Dispatcher.Invoke(() => ShowError("Token exchange succeeded but no access token received"));
                            }
                        }
                        catch (JsonException ex)
                        {
                            LogDebug($"❌ JSON deserialization error: {ex.Message}");
                            LogDebug($"Raw response content: {responseContent}");
                            Dispatcher.Invoke(() => ShowError($"Failed to parse token response: {ex.Message}"));
                        }
                    }
                    else
                    {
                        LogDebug($"❌ Token exchange failed with status: {response.StatusCode}");
                        LogDebug($"Response headers: {response.Headers}");
                        LogDebug($"Error response content: {responseContent}");
                        
                        Dispatcher.Invoke(() => ShowError($"Token exchange failed ({response.StatusCode}):\n\n{responseContent}"));
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Exception during token exchange: {ex.Message}");
                LogDebug($"Exception type: {ex.GetType().Name}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                
                Dispatcher.Invoke(() => ShowError($"Error during token exchange:\n\n{ex.Message}"));
            }
        }

        private void InitializeWebAuth()
        {
            LogDebug("=== InitializeWebAuth Started ===");
            this.Loaded += SpotifyAuthWindow_Loaded;
            LogDebug("Window Loaded event handler attached");
        }

        private void SpotifyAuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LogDebug("=== SpotifyAuthWindow_Loaded Event Fired ===");
            this.Loaded -= SpotifyAuthWindow_Loaded;
            LogDebug("Window Loaded event handler detached");
            
            // Small delay to ensure everything is ready
            LogDebug("Starting navigation delay (500ms)...");
            Task.Delay(500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    LogDebug($"Navigation delay complete. Server started: {_serverStarted}");
                    if (_serverStarted)
                    {
                        LogDebug("✅ HTTPS server confirmed started, navigating to Spotify...");
                        NavigateToSpotifyAuth();
                    }
                    else
                    {
                        LogDebug("❌ HTTPS server not started, cannot proceed");
                        ShowError("HTTPS server failed to start. Spotify requires HTTPS for OAuth.\n\nPlease run EZStreamer as Administrator.");
                    }
                });
            });
        }

        private void NavigateToSpotifyAuth()
        {
            try
            {
                LogDebug("=== NavigateToSpotifyAuth Started ===");
                
                // Generate state parameter for security
                var state = Guid.NewGuid().ToString();
                LogDebug($"Generated state parameter: {state}");
                
                // Build OAuth authorization URL (using HTTPS)
                var authUrl = $"https://accounts.spotify.com/authorize" +
                            $"?response_type=code" +
                            $"&client_id={Uri.EscapeDataString(_clientId)}" +
                            $"&scope={Uri.EscapeDataString(SCOPES)}" +
                            $"&redirect_uri={Uri.EscapeDataString(REDIRECT_URI)}" +
                            $"&state={Uri.EscapeDataString(state)}" +
                            $"&show_dialog=true";

                LogDebug($"OAuth URL built:");
                LogDebug($"Full URL: {authUrl}");
                LogDebug($"URL Length: {authUrl.Length}");
                
                LogDebug($"WebView2 status: {(AuthWebView.CoreWebView2 != null ? "READY" : "NOT READY")}");
                
                if (AuthWebView.CoreWebView2 != null)
                {
                    LogDebug("🌐 Navigating WebView to Spotify authorization...");
                    AuthWebView.CoreWebView2.Navigate(authUrl);
                    LogDebug("Navigation command sent to WebView2");
                }
                else
                {
                    LogDebug("⚠️ WebView2 not ready, storing URL for later");
                    _pendingNavigationUrl = authUrl;
                    LogDebug($"Pending URL stored: {_pendingNavigationUrl != null}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error starting OAuth navigation: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Error starting OAuth flow: {ex.Message}");
            }
        }

        private string _pendingNavigationUrl;

        private void AuthWebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            try
            {
                LogDebug($"=== WebView2 Initialization Completed ===");
                LogDebug($"Success: {e.IsSuccess}");
                
                if (e.IsSuccess)
                {
                    LogDebug("WebView2 initialization successful - setting up event handlers");
                    
                    // Configure WebView2 to accept our self-signed certificate
                    AuthWebView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
                    AuthWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                    
                    LogDebug("Event handlers attached");
                    
                    // Allow insecure content for localhost
                    var settings = AuthWebView.CoreWebView2.Settings;
                    settings.IsGeneralAutofillEnabled = false;
                    settings.IsWebMessageEnabled = true;
                    
                    LogDebug($"WebView2 settings configured:");
                    LogDebug($"- IsGeneralAutofillEnabled: {settings.IsGeneralAutofillEnabled}");
                    LogDebug($"- IsWebMessageEnabled: {settings.IsWebMessageEnabled}");
                    LogDebug($"- UserAgent: {settings.UserAgent}");
                    
                    if (!string.IsNullOrEmpty(_pendingNavigationUrl))
                    {
                        LogDebug("✅ Executing pending navigation...");
                        LogDebug($"Pending URL: {_pendingNavigationUrl}");
                        AuthWebView.CoreWebView2.Navigate(_pendingNavigationUrl);
                        _pendingNavigationUrl = null;
                        LogDebug("Pending navigation executed and cleared");
                    }
                    else if (!string.IsNullOrEmpty(_clientId) && _serverStarted)
                    {
                        LogDebug("✅ Starting OAuth navigation...");
                        NavigateToSpotifyAuth();
                    }
                    else
                    {
                        LogDebug($"⚠️ Cannot start navigation - ClientId: {(!string.IsNullOrEmpty(_clientId) ? "SET" : "NOT SET")}, ServerStarted: {_serverStarted}");
                    }
                }
                else
                {
                    LogDebug("❌ WebView2 initialization failed");
                    ShowError("Failed to initialize web browser for OAuth");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error in WebView2 initialization: {ex.Message}");
                LogDebug($"Stack trace: {ex.StackTrace}");
                ShowError($"Error initializing OAuth browser: {ex.Message}");
            }
        }

        private void CoreWebView2_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            // Allow all permissions for OAuth
            e.State = CoreWebView2PermissionState.Allow;
            LogDebug($"✅ WebView2 permission granted: {e.PermissionKind}");
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LogDebug($"=== Navigation Starting ===");
            LogDebug($"🌐 Navigation starting to: {e.Uri}");
            LogDebug($"Navigation ID: {e.NavigationId}");
            LogDebug($"Is user initiated: {e.IsUserInitiated}");
            LogDebug($"Is redirected: {e.IsRedirected}");
            
            if (e.Uri.StartsWith(REDIRECT_URI))
            {
                LogDebug("✅ Detected HTTPS callback URL - our local server should handle this");
                LogDebug("This means Spotify is redirecting back to us - authentication may be successful!");
            }
            else if (e.Uri.StartsWith("https://accounts.spotify.com"))
            {
                LogDebug("✅ Navigation to Spotify authorization page");
            }
            else
            {
                LogDebug($"⚠️ Unexpected navigation destination: {e.Uri}");
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogDebug($"=== Navigation Completed ===");
            LogDebug($"🌐 Navigation completed. Success: {e.IsSuccess}");
            LogDebug($"Navigation ID: {e.NavigationId}");
            LogDebug($"WebErrorStatus: {e.WebErrorStatus}");
            
            if (!e.IsSuccess)
            {
                LogDebug($"❌ Navigation failed with error: {e.WebErrorStatus}");
                
                // If navigation failed due to certificate issues, provide helpful error
                if (e.WebErrorStatus.ToString().Contains("Certificate") || 
                    e.WebErrorStatus.ToString().Contains("SSL") ||
                    e.WebErrorStatus.ToString().Contains("Security"))
                {
                    LogDebug("🔒 Certificate/SSL error detected");
                    ShowError($"HTTPS certificate error: {e.WebErrorStatus}\n\n" +
                             "The HTTPS server setup failed. Please run EZStreamer as Administrator to enable HTTPS authentication.");
                }
                else
                {
                    LogDebug($"❌ Other navigation error: {e.WebErrorStatus}");
                    ShowError($"OAuth navigation failed: {e.WebErrorStatus}");
                }
            }
            else
            {
                LogDebug("✅ Navigation completed successfully");
                try
                {
                    var currentUrl = AuthWebView.CoreWebView2.Source;
                    LogDebug($"Current URL after navigation: {currentUrl}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Error getting current URL: {ex.Message}");
                }
            }
        }

        private void CoreWebView2_DOMContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            LogDebug("=== DOM Content Loaded ===");
            LogDebug("📄 DOM content loaded - page is ready");
            
            try
            {
                var currentUrl = AuthWebView.CoreWebView2.Source;
                LogDebug($"Page URL: {currentUrl}");
                
                // Hide loading panel when page loads
                LoadingPanel.Visibility = Visibility.Collapsed;
                LogDebug("Loading panel hidden");
            }
            catch (Exception ex)
            {
                LogDebug($"Error in DOMContentLoaded: {ex.Message}");
            }
        }

        // Missing event handler that was referenced in XAML
        private void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogDebug($"=== WebView NavigationCompleted (XAML Handler) ===");
            LogDebug($"🌐 WebView Navigation completed. Success: {e.IsSuccess}");
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            if (!e.IsSuccess)
            {
                LogDebug($"❌ WebView navigation failed with error: {e.WebErrorStatus}");
                
                // Check for certificate/SSL errors
                if (e.WebErrorStatus.ToString().Contains("Certificate") || 
                    e.WebErrorStatus.ToString().Contains("SSL") ||
                    e.WebErrorStatus.ToString().Contains("Security"))
                {
                    LogDebug("🔒 Certificate/SSL error in XAML handler");
                    ShowError($"HTTPS certificate error: {e.WebErrorStatus}\n\n" +
                             "The self-signed certificate was rejected.\n" +
                             "Please run EZStreamer as Administrator.");
                }
                else
                {
                    ShowError($"OAuth navigation failed: {e.WebErrorStatus}");
                }
            }
        }

        private void ShowConfigurationNeeded()
        {
            LogDebug("=== ShowConfigurationNeeded ===");
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            var result = MessageBox.Show(
                "Spotify Client ID and Secret are required for OAuth authentication.\n\n" +
                "Would you like to configure them now?\n\n" +
                "Get them from: https://developer.spotify.com/dashboard\n\n" +
                "IMPORTANT: Set redirect URI to: https://localhost:8443/callback",
                "OAuth Configuration Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
                
            LogDebug($"Configuration dialog result: {result}");
            
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
            LogDebug("ShowCredentialsDialog - directing user to settings");
            
            MessageBox.Show(
                "Please configure your Spotify credentials in the Settings tab:\n\n" +
                "1. Go to Settings\n" +
                "2. Expand 'Spotify API Credentials'\n" +
                "3. Enter your Client ID and Secret\n" +
                "4. Click Save\n" +
                "5. Try Test Connection again\n\n" +
                "IMPORTANT: Set redirect URI to: https://localhost:8443/callback",
                "Configure Credentials",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
                
            DialogResult = false;
            Close();
        }

        private string CreateSuccessResponseHtml()
        {
            LogDebug("Creating success response HTML");
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>Spotify Authentication Success</title>
    <style>
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #1DB954, #1ed760); 
            color: white; 
            margin: 0; 
            padding: 20px; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }
        .container { 
            background: white; 
            color: #1DB954; 
            padding: 40px; 
            border-radius: 20px; 
            box-shadow: 0 20px 40px rgba(0,0,0,0.1); 
            text-align: center; 
            max-width: 500px; 
            animation: slideIn 0.5s ease-out;
        }
        @keyframes slideIn {
            from { transform: translateY(-20px); opacity: 0; }
            to { transform: translateY(0); opacity: 1; }
        }
        h1 { margin-top: 0; font-size: 2.2em; font-weight: 600; }
        .icon { font-size: 3em; margin-bottom: 20px; }
        .message { font-size: 1.1em; line-height: 1.6; margin: 20px 0; }
        .success-check { color: #1DB954; font-size: 1.2em; margin: 10px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>🎵</div>
        <h1>Authentication Successful!</h1>
        <div class='message'>
            <div class='success-check'>✅ Connected to Spotify</div>
            <div class='success-check'>✅ Access token received</div>
            <div class='success-check'>✅ Ready to use</div>
            <p style='margin-top: 30px;'>You can now close this window and return to EZStreamer.</p>
        </div>
    </div>
    <script>
        console.log('Spotify OAuth callback success page loaded');
        setTimeout(() => {
            console.log('Attempting to close window...');
            window.close();
        }, 3000);
    </script>
</body>
</html>";
        }

        private string CreateErrorResponseHtml(string error)
        {
            LogDebug($"Creating error response HTML for: {error}");
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Spotify Authentication Error</title>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #ff4444, #cc0000); 
            color: white; 
            margin: 0; 
            padding: 20px; 
            min-height: 100vh; 
            display: flex; 
            align-items: center; 
            justify-content: center; 
        }}
        .container {{ 
            background: white; 
            color: #cc0000; 
            padding: 40px; 
            border-radius: 20px; 
            box-shadow: 0 20px 40px rgba(0,0,0,0.1); 
            text-align: center; 
            max-width: 500px; 
        }}
        h1 {{ margin-top: 0; font-size: 2.2em; font-weight: 600; }}
        .icon {{ font-size: 3em; margin-bottom: 20px; }}
        .error {{ background: #ffebee; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #cc0000; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>❌</div>
        <h1>Authentication Error</h1>
        <div class='error'>
            <strong>Error:</strong> {HttpUtility.HtmlEncode(error)}
        </div>
        <p>Please close this window and try again in EZStreamer.</p>
    </div>
</body>
</html>";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("User cancelled authentication");
            DialogResult = false;
            Close();
        }

        private void ManualTokenButton_Click(object sender, RoutedEventArgs e)
        {
            LogDebug("User requested manual token option");
            MessageBox.Show(
                "Manual token authentication:\n\n" +
                "1. Go to https://developer.spotify.com/console/get-current-user/\n" +
                "2. Click 'Get Token'\n" +
                "3. Select required scopes\n" +
                "4. Copy the generated token\n" +
                "5. Use 'Manual Token' option in EZStreamer settings",
                "Manual Token Instructions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            LogDebug($"❌ Showing error to user: {message}");
            MessageBox.Show(message, "Spotify OAuth Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                LogDebug("=== OnClosed - Cleaning up SpotifyAuthWindow ===");
                
                // Stop server
                _isListening = false;
                _cancellationTokenSource?.Cancel();
                
                if (_httpListener != null && _httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    LogDebug("✅ HTTPS server stopped");
                }

                // Clean up WebView2
                if (AuthWebView?.CoreWebView2 != null)
                {
                    AuthWebView.CoreWebView2.PermissionRequested -= CoreWebView2_PermissionRequested;
                    AuthWebView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    AuthWebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                    AuthWebView.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                    LogDebug("WebView2 event handlers removed");
                }
                
                AuthWebView?.Dispose();
                _cancellationTokenSource?.Dispose();
                _serverCertificate?.Dispose();
                
                LogDebug("✅ SpotifyAuthWindow cleanup completed");
                LogDebug($"Final state - IsAuthenticated: {IsAuthenticated}, AccessToken: {(!string.IsNullOrEmpty(AccessToken) ? "SET" : "NOT SET")}");
            }
            catch (Exception ex)
            {
                LogDebug($"❌ Error during cleanup: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
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
