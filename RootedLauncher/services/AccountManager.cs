using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RootedLauncher.Security;

namespace RootedLauncher.Services
{
    
    /// <summary>
    /// Represents a stored Minecraft account with encrypted credentials.
    /// </summary>
    public class StoredAccount
    {
        public string Username { get; set; }
        public string UUID { get; set; }
        public string AccessToken { get; set; } // This will be encrypted
        public string RefreshToken { get; set; } // This will be encrypted
        public DateTime LastUsed { get; set; }
        public bool IsDefault { get; set; }
    }
    
    /// <summary>
    /// Container for the accounts file structure.
    /// </summary>
    public class AccountsData
    {
        public List<StoredAccount> Accounts { get; set; } = new List<StoredAccount>();
        public string FormatVersion { get; set; } = "1.0";
    }
    
    /// <summary>
    /// Manages encrypted storage and retrieval of Minecraft accounts.
    /// </summary>
    public class AccountManager
    {
        private readonly string _accountsFilePath;
        private readonly AccountEncryptionService _encryptionService;
        private AccountsData _accountsData;
        
        public AccountManager(string rootedMcDirectory)
        {
            _accountsFilePath = Path.Combine(rootedMcDirectory, "accounts.json");
            _encryptionService = new AccountEncryptionService(rootedMcDirectory);
            LoadAccounts();
        }
        
        /// <summary>
        /// Loads accounts from the encrypted file, or creates a new file if it doesn't exist.
        /// </summary>
        private void LoadAccounts()
        {
            if (!File.Exists(_accountsFilePath))
            {
                Console.WriteLine("[INFO] No accounts file found, creating new one.");
                _accountsData = new AccountsData();
                return;
            }
            
            try
            {
                string encryptedContent = File.ReadAllText(_accountsFilePath);
                string decryptedContent = _encryptionService.Decrypt(encryptedContent);
                _accountsData = JsonSerializer.Deserialize<AccountsData>(decryptedContent) ?? new AccountsData();
                Console.WriteLine($"[INFO] Loaded {_accountsData.Accounts.Count} account(s) from storage.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to load accounts file: {ex.Message}");
                Console.WriteLine("[INFO] Creating new accounts file.");
                _accountsData = new AccountsData();
            }
        }
        
        /// <summary>
        /// Saves all accounts to the encrypted file.
        /// </summary>
        private void SaveAccounts()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(_accountsFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                
                // Serialize to JSON
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonContent = JsonSerializer.Serialize(_accountsData, options);
                
                // Encrypt and save
                string encryptedContent = _encryptionService.Encrypt(jsonContent);
                File.WriteAllText(_accountsFilePath, encryptedContent);
                
                Console.WriteLine("[INFO] Accounts saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save accounts: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Adds or updates an account in the storage.
        /// </summary>
        public void SaveAccount(string username, string uuid, string accessToken, string refreshToken = null, bool setAsDefault = false)
        {
            // Check if account already exists
            var existingAccount = _accountsData.Accounts.FirstOrDefault(a => 
                a.UUID.Equals(uuid, StringComparison.OrdinalIgnoreCase));
            
            if (existingAccount != null)
            {
                // Update existing account
                existingAccount.Username = username;
                existingAccount.AccessToken = accessToken;
                existingAccount.RefreshToken = refreshToken;
                existingAccount.LastUsed = DateTime.UtcNow;
                
                if (setAsDefault)
                {
                    // Clear other defaults
                    foreach (var acc in _accountsData.Accounts)
                        acc.IsDefault = false;
                    existingAccount.IsDefault = true;
                }
                
                Console.WriteLine($"[INFO] Updated account: {username}");
            }
            else
            {
                // Add new account
                var newAccount = new StoredAccount
                {
                    Username = username,
                    UUID = uuid,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    LastUsed = DateTime.UtcNow,
                    IsDefault = setAsDefault || _accountsData.Accounts.Count == 0 // First account is default
                };
                
                if (newAccount.IsDefault)
                {
                    // Clear other defaults
                    foreach (var acc in _accountsData.Accounts)
                        acc.IsDefault = false;
                }
                
                _accountsData.Accounts.Add(newAccount);
                Console.WriteLine($"[INFO] Added new account: {username}");
            }
            
            SaveAccounts();
        }
        
        /// <summary>
        /// Gets the default account, or null if no accounts exist.
        /// </summary>
        public StoredAccount GetDefaultAccount()
        {
            return _accountsData.Accounts.FirstOrDefault(a => a.IsDefault) 
                ?? _accountsData.Accounts.OrderByDescending(a => a.LastUsed).FirstOrDefault();
        }
        
        /// <summary>
        /// Gets all stored accounts.
        /// </summary>
        public List<StoredAccount> GetAllAccounts()
        {
            return new List<StoredAccount>(_accountsData.Accounts);
        }
        
        /// <summary>
        /// Gets an account by UUID.
        /// </summary>
        public StoredAccount GetAccount(string uuid)
        {
            return _accountsData.Accounts.FirstOrDefault(a => 
                a.UUID.Equals(uuid, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Removes an account from storage.
        /// </summary>
        public bool RemoveAccount(string uuid)
        {
            var account = GetAccount(uuid);
            if (account != null)
            {
                _accountsData.Accounts.Remove(account);
                Console.WriteLine($"[INFO] Removed account: {account.Username}");
                SaveAccounts();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Sets an account as the default.
        /// </summary>
       public void SetDefaultAccount(string uuid)
        {
            bool setDFA = false;
            var account = GetAccount(uuid);
            if (account != null && setDFA == true)
            {
                foreach (var acc in _accountsData.Accounts)
                    acc.IsDefault = false;
                
                account.IsDefault = true;
                SaveAccounts();
                Console.WriteLine($"[INFO] Set default account: {account.Username}");
            }
        }
        
        /// <summary>
        /// Updates the last used timestamp for an account.
        /// </summary>
        public void UpdateLastUsed(string uuid)
        {
            var account = GetAccount(uuid);
            if (account != null)
            {
                account.LastUsed = DateTime.UtcNow;
                SaveAccounts();
            }
        }
        
        /// <summary>
        /// Checks if any accounts are stored.
        /// </summary>
        public bool HasAccounts()
        {
            return _accountsData.Accounts.Any();
        }
    }
}