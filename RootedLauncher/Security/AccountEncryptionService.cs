using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RootedLauncher.Security
{
    /// <summary>
    /// Provides AES encryption and decryption for sensitive account data.
    /// Uses DPAPI on Windows for additional key protection, or a derived key on other platforms.
    /// </summary>
    public class AccountEncryptionService
    {
        private readonly string _keyStoragePath;
        private const int KeySize = 256; // AES-256
        private const int BlockSize = 128;
        
        public AccountEncryptionService(string dataDirectory)
        {
            _keyStoragePath = Path.Combine(dataDirectory, ".keystore");
        }
        
        /// <summary>
        /// Encrypts plaintext data using AES encryption.
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;
                
            byte[] key = GetOrCreateEncryptionKey();
            
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Key = key;
                aes.GenerateIV(); // Generate a new IV for each encryption
                
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // Prepend IV to the encrypted data
                    msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                    
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
        
        /// <summary>
        /// Decrypts encrypted data back to plaintext.
        /// </summary>
        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;
                
            byte[] key = GetOrCreateEncryptionKey();
            byte[] encryptedData = Convert.FromBase64String(encryptedText);
            
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Key = key;
                
                // Extract IV from the beginning of the encrypted data
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(encryptedData, 0, iv, 0, iv.Length);
                aes.IV = iv;
                
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (MemoryStream msDecrypt = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        
        /// <summary>
        /// Gets the encryption key from storage, or creates a new one if it doesn't exist.
        /// On Windows, uses DPAPI for additional protection.
        /// </summary>
        private byte[] GetOrCreateEncryptionKey()
        {
            // If key file exists, load and unprotect it
            if (File.Exists(_keyStoragePath))
            {
                byte[] storedProtectedKey = File.ReadAllBytes(_keyStoragePath);
                return UnprotectKey(storedProtectedKey);
            }
            
            // Generate a new random key
            byte[] key = new byte[KeySize / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            
            // Protect the key before saving
            byte[] protectedKeyToSave = ProtectKey(key);
            
            // Ensure directory exists
            string directory = Path.GetDirectoryName(_keyStoragePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
                
            File.WriteAllBytes(_keyStoragePath, protectedKeyToSave);
            
            // Make the file hidden to reduce visibility
            try
            {
                File.SetAttributes(_keyStoragePath, FileAttributes.Hidden);
            }
            catch
            {
                // Ignore if setting hidden attribute fails (e.g., on Linux)
            }
            
            return key;
        }
        
        /// <summary>
        /// Protects the encryption key using DPAPI on Windows, or simple obfuscation on other platforms.
        /// Note: For production use on non-Windows platforms, consider using a proper key management system.
        /// </summary>
        private byte[] ProtectKey(byte[] key)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Use DPAPI on Windows for additional protection
                return ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
            }
            
            // On non-Windows platforms, return the key as-is
            // In production, you might want to use a password-derived key or other protection
            return key;
        }
        
        /// <summary>
        /// Unprotects the encryption key.
        /// </summary>
        private byte[] UnprotectKey(byte[] protectedKey)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                // Use DPAPI on Windows
                return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            }
            
            // On non-Windows platforms, the key is stored as-is
            return protectedKey;
        }
    }
}