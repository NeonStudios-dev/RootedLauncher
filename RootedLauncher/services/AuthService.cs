using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
namespace RootedLauncher.services
{
    public class AuthService
    {
        private readonly JELoginHandler _loginHandler;
        private readonly ILogger _logger;
        private readonly bool _isWindows;

        public AuthService()
        {
            // Setup logger
            var loggerFactory = LoggerFactory.Create(config =>
            {
                config.ClearProviders();
                config.AddSimpleConsole();
                config.SetMinimumLevel(LogLevel.Information);
            });
            _logger = loggerFactory.CreateLogger("RootedLauncher");

            // Initialize login handler
            _loginHandler = JELoginHandlerBuilder.BuildDefault();

            // Detect OS
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public async Task<MSession> AuthenticateAsync()
        {
            while (true)
            {
                try
                {
                    /*
                    // List existing accounts
                    Console.WriteLine("\n=== Microsoft Account Login ===");
                    Console.WriteLine($"Platform: {GetPlatformName()}");
                    Console.WriteLine("\nSelect an account or create new:");
                    Console.WriteLine("[0] New Account (Interactive Login)");
                    */
                    var accounts = _loginHandler.AccountManager.GetAccounts().ToList();
                    for (int i = 0; i < accounts.Count; i++)
                    {
                        var account = accounts[i];
                        if (account is JEGameAccount jeAccount)
                        {
                            Console.WriteLine($"[{i + 1}] {jeAccount.Profile?.Username ?? "Unknown"} (UUID: {jeAccount.Profile?.UUID})");
                            Console.WriteLine($"     Last used: {jeAccount.LastAccess}");
                        }
                        else
                        {
                            Console.WriteLine($"[{i + 1}] {account.Identifier}");
                        }
                    }

                    // Show device code option for non-Windows or SSH scenarios
                    if (!_isWindows)
                    {
                        Console.WriteLine("\n[D] Device Code Authentication (for SSH/remote sessions)");
                    }

                    Console.Write("\nSelect option: ");
                    var input = Console.ReadLine()?.Trim() ?? "";

                    // Handle device code option
                    if (input.Equals("D", StringComparison.OrdinalIgnoreCase) && !_isWindows)
                    {
                        var newAccount = _loginHandler.AccountManager.NewAccount();
                        var session = await AuthenticateWithDeviceCode(newAccount);
                        return session;
                    }

                    if (!int.TryParse(input, out int selection))
                    {
                        Console.WriteLine("Invalid input. Please enter a number.");
                        continue;
                    }

                    // Get or create account
                    IXboxGameAccount selectedAccount;
                    if (selection == 0)
                    {
                        // New account
                        selectedAccount = _loginHandler.AccountManager.NewAccount();
                        Console.WriteLine("\nStarting authentication...");

                        if (_isWindows)
                        {
                            Console.WriteLine("A browser window will open. Please sign in with your Microsoft account.");
                            var session = await AuthenticateInteractiveOAuth(selectedAccount);
                            return session;
                        }
                        else
                        {
                            // Use Device Code for Linux/macOS as it's more reliable
                            Console.WriteLine("Using Device Code authentication for Linux/macOS...");
                            var session = await AuthenticateWithDeviceCode(selectedAccount);
                            return session;
                        }
                    }
                    else if (selection > 0 && selection <= accounts.Count)
                    {
                        // Existing account
                        selectedAccount = accounts[selection - 1];
                        Console.WriteLine($"\nAuthenticating with existing account...");

                        // Try silent authentication first
                        try
                        {
                            var session = await AuthenticateSilent(selectedAccount);
                            return session;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Silent authentication failed: {ex.Message}");
                            Console.WriteLine("Session expired. Re-authenticating...");

                            // Fall back to interactive
                            if (_isWindows)
                            {
                                var session = await AuthenticateInteractiveOAuth(selectedAccount);
                                return session;
                            }
                            else
                            {
                                var session = await AuthenticateInteractiveMsal(selectedAccount);
                                return session;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Authentication error: {ex.Message}");
                    Console.WriteLine($"Authentication failed: {ex.Message}");
                    Console.WriteLine("Press Enter to try again or Ctrl+C to exit...");
                    Console.ReadLine();
                }
            }
        }

        private async Task<MSession> AuthenticateInteractiveOAuth(IXboxGameAccount account)
        {
            // Windows-only OAuth with WebView2
            var authenticator = _loginHandler.CreateAuthenticator(account, default);

            // Add Microsoft OAuth with Interactive mode (Windows only)
            authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Interactive());

            // Add Xbox authentication
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());

            // Add JE (Java Edition) authenticator
            authenticator.AddJEAuthenticator();

            // Execute authentication
            var session = await authenticator.ExecuteForLauncherAsync();

            Console.WriteLine($"\n✓ Successfully authenticated as: {session.Username}");
            Console.WriteLine($"  UUID: {session.UUID}");

            return session;
        }

        private async Task<MSession> AuthenticateInteractiveMsal(IXboxGameAccount account)
        {
            // Cross-platform MSAL authentication
            var app = await MsalClientHelper.BuildApplicationWithCache("499c8d36-be2a-4231-9ebd-ef291b7bb64c");
            var authenticator = _loginHandler.CreateAuthenticatorWithNewAccount();

            // Add MSAL OAuth with Interactive mode (cross-platform)
            authenticator.AddMsalOAuth(app, msal => msal.Interactive());

            // Add Xbox authentication
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());

            // Add JE (Java Edition) authenticator
            authenticator.AddForceJEAuthenticator();

            // Execute authentication
            var session = await authenticator.ExecuteForLauncherAsync();

            Console.WriteLine($"\n✓ Successfully authenticated as: {session.Username}");
            Console.WriteLine($"  UUID: {session.UUID}");

            return session;
        }

        private async Task<MSession> AuthenticateWithDeviceCode(IXboxGameAccount account)
        {
            // Device Code authentication - works everywhere, especially good for SSH
           // Console.WriteLine("\n=== Device Code Authentication ===");
            var app = await MsalClientHelper.BuildApplicationWithCache("499c8d36-be2a-4231-9ebd-ef291b7bb64c");
            var authenticator = _loginHandler.CreateAuthenticatorWithNewAccount();

            // Add MSAL OAuth with DeviceCode mode
            authenticator.AddMsalOAuth(app, msal => msal.DeviceCode(code =>
            {
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine(code.Message);
                Console.WriteLine("=".PadRight(60, '=') + "\n");
                return Task.CompletedTask;
            }));

            // Add Xbox authentication
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());

            // Add JE (Java Edition) authenticator
            authenticator.AddJEAuthenticator();

            // Execute authentication
            var session = await authenticator.ExecuteForLauncherAsync();

            Console.WriteLine($"\n✓ Successfully authenticated as: {session.Username}");
            Console.WriteLine($"  UUID: {session.UUID}");

            return session;
        }

        private async Task<MSession> AuthenticateSilent(IXboxGameAccount account)
        {
            if (_isWindows)
            {
                // Windows OAuth silent authentication
                var authenticator = _loginHandler.CreateAuthenticator(account, default);
                authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Silent());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddJEAuthenticator();

                var session = await authenticator.ExecuteForLauncherAsync();
                Console.WriteLine($"\n✓ Authenticated as: {session.Username}");
                return session;
            }
            else
            {
                // Linux/macOS MSAL silent authentication
                var app = await MsalClientHelper.BuildApplicationWithCache("499c8d36-be2a-4231-9ebd-ef291b7bb64c");
                var authenticator = _loginHandler.CreateAuthenticator(account, default);
                authenticator.AddMsalOAuth(app, msal => msal.Silent());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddJEAuthenticator();

                var session = await authenticator.ExecuteForLauncherAsync();
                Console.WriteLine($"\n✓ Authenticated as: {session.Username}");
                return session;
            }
        }

        private string GetPlatformName()
        {
            if (OperatingSystem.IsWindows())
                return "Windows (OAuth)";
            else if (OperatingSystem.IsLinux())
                return "Linux (MSAL)";
            else if (OperatingSystem.IsMacOS())
                return "macOS (MSAL)";
            else
                return "Unknown";
        }

        public async Task RemoveAccountAsync()
        {
          //  Console.WriteLine("\n=== Remove Account ===");
            var accounts = _loginHandler.AccountManager.GetAccounts().ToList();

            if (accounts.Count == 0)
            {
                Console.WriteLine("No accounts to remove.");
                return;
            }

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                if (account is JEGameAccount jeAccount)
                {
                    Console.WriteLine($"[{i + 1}] {jeAccount.Profile?.Username ?? "Unknown"}");
                }
                else
                {
                    Console.WriteLine($"[{i + 1}] {account.Identifier}");
                }
            }

            Console.Write("\nSelect account number to remove (0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out int selection) &&
                selection > 0 && selection <= accounts.Count)
            {
                await _loginHandler.Signout(accounts[selection - 1]);
                Console.WriteLine("Account removed successfully.");
            }
        }
    }
}