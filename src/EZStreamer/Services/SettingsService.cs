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

        public SettingsService()
        {
            _appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EZStreamer");
            
            _settingsPath = Path.Combine(_appDataFolder, "settings.json");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(_appDataFolder);
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new AppSettings();
                }

                var encryptedContent = File.ReadAllText(_settingsPath);
                var decryptedContent = DecryptString(encryptedContent);
                
                return JsonConvert.DeserializeObject<AppSettings>(decryptedContent) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                // If we can't load settings, create new ones
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var jsonContent = JsonConvert.SerializeObject(settings, Formatting.Indented);
                var encryptedContent = EncryptString(jsonContent);
                
                File.WriteAllText(_settingsPath, encryptedContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                throw new InvalidOperationException("Failed to save settings. Please check file permissions.", ex);
            }
        }

        public string GetAppDataFolder()
        {
            return _appDataFolder;
        }

        private string EncryptString(string plainText)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                // If encryption fails, return plain text (less secure but functional)
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            }
        }

        private string DecryptString(string encryptedText)
        {
            try
            {
                var data = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                try
                {
                    // Try to decode as plain base64 (fallback for non-encrypted data)
                    var data = Convert.FromBase64String(encryptedText);
                    return Encoding.UTF8.GetString(data);
                }
                catch
                {
                    // If all fails, return empty string
                    return string.Empty;
                }
            }
        }

        public void ClearSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Delete(_settingsPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear settings: {ex.Message}");
            }
        }

        public bool SettingsExist()
        {
            return File.Exists(_settingsPath);
        }
    }
}
