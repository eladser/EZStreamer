using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using EZStreamer.Models;
using Newtonsoft.Json;

namespace EZStreamer.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private readonly string _appDataFolder;
        private AppSettings _cachedSettings;
        private readonly object _lockObject = new object();

        public SettingsService()
        {
            try
            {
                _appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EZStreamer");
                
                _settingsPath = Path.Combine(_appDataFolder, "settings.json");
                
                // Create directory if it doesn't exist
                Directory.CreateDirectory(_appDataFolder);
                
                // Initialize cached settings
                _cachedSettings = LoadSettingsInternal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsService initialization error: {ex.Message}");
                
                // Fallback initialization
                _appDataFolder = Path.GetTempPath();
                _settingsPath = Path.Combine(_appDataFolder, "ezstreamer_settings.json");
                _cachedSettings = new AppSettings();
            }
        }

        public AppSettings LoadSettings()
        {
            lock (_lockObject)
            {
                try
                {
                    // Return cached settings if available
                    if (_cachedSettings != null)
                    {
                        return _cachedSettings;
                    }
                    
                    // Load from file
                    _cachedSettings = LoadSettingsInternal();
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                    
                    // Always return a valid AppSettings object
                    _cachedSettings = new AppSettings();
                    return _cachedSettings;
                }
            }
        }

        private AppSettings LoadSettingsInternal()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new AppSettings();
                }

                var encryptedContent = File.ReadAllText(_settingsPath);
                if (string.IsNullOrEmpty(encryptedContent))
                {
                    return new AppSettings();
                }
                
                var decryptedContent = DecryptString(encryptedContent);
                if (string.IsNullOrEmpty(decryptedContent))
                {
                    return new AppSettings();
                }
                
                var settings = JsonConvert.DeserializeObject<AppSettings>(decryptedContent);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings from file: {ex.Message}");
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            lock (_lockObject)
            {
                try
                {
                    if (settings == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Cannot save null settings");
                        return;
                    }

                    var jsonContent = JsonConvert.SerializeObject(settings, Formatting.Indented);
                    var encryptedContent = EncryptString(jsonContent);
                    
                    // Ensure directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
                    
                    File.WriteAllText(_settingsPath, encryptedContent);
                    
                    // Update cached settings
                    _cachedSettings = settings;
                    
                    System.Diagnostics.Debug.WriteLine("Settings saved successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                    // Don't throw - just log the error
                }
            }
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder ?? Path.GetTempPath();
        }

        private string EncryptString(string plainText)
        {
            try
            {
                if (string.IsNullOrEmpty(plainText))
                {
                    return string.Empty;
                }
                
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encryption failed: {ex.Message}");
                
                try
                {
                    // If encryption fails, return plain text encoded as base64 (less secure but functional)
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
                }
                catch
                {
                    // Last resort - return empty string
                    return string.Empty;
                }
            }
        }

        private string DecryptString(string encryptedText)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedText))
                {
                    return string.Empty;
                }
                
                var data = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Decryption failed: {ex.Message}");
                
                try
                {
                    // Try to decode as plain base64 (fallback for non-encrypted data)
                    var data = Convert.FromBase64String(encryptedText);
                    return Encoding.UTF8.GetString(data);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Base64 decode failed: {ex2.Message}");
                    
                    // If all fails, return empty string
                    return string.Empty;
                }
            }
        }

        public void ClearSettings()
        {
            lock (_lockObject)
            {
                try
                {
                    if (File.Exists(_settingsPath))
                    {
                        File.Delete(_settingsPath);
                    }
                    
                    // Reset cached settings
                    _cachedSettings = new AppSettings();
                    
                    System.Diagnostics.Debug.WriteLine("Settings cleared successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to clear settings: {ex.Message}");
                }
            }
        }

        public bool SettingsExist()
        {
            try
            {
                return File.Exists(_settingsPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking settings existence: {ex.Message}");
                return false;
            }
        }
    }
}
