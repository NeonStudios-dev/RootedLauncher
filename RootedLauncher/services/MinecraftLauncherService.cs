using System.Diagnostics;
using System.Runtime.InteropServices;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.ProcessBuilder;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;

using Microsoft.Extensions.Logging;

namespace RootedLauncher.services
{
    public class DownloadProgress
    {
        public string CurrentFile { get; set; } = "";
        public long ProgressedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double Percentage => TotalBytes > 0 ? (double)ProgressedBytes / TotalBytes * 100 : 0;
    }

    public class MinecraftLauncherService
    {
        private readonly ILogger<MinecraftLauncherService> _logger;
        private readonly JELoginHandler _loginHandler;
        private readonly bool _isWindows;
        private MSession? _currentSession;
        private Process? _minecraftProcess;
        private readonly string _rootDir;
        private readonly string _gameBase;

        public MinecraftLauncherService()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            _logger = loggerFactory.CreateLogger<MinecraftLauncherService>();
            _loginHandler = JELoginHandlerBuilder.BuildDefault();
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _rootDir = Path.Combine(homeDir, ".RootedMc");
            _gameBase = Path.Combine(_rootDir, "GameDir", "profile");
            Directory.CreateDirectory(_rootDir);
            Directory.CreateDirectory(_gameBase);
        }

        public List<object> GetAccounts()
        {
            var accounts = _loginHandler.AccountManager.GetAccounts().ToList();
            var result = new List<object>();

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                result.Add(new
                {
                    index = i,
                    identifier = account.Identifier ?? "Unknown"
                });
            }

            return result;
        }

        public async Task<MSession> AuthenticateAsync(string? method, int accountIndex, Func<string, Task> promptCallback)
        {
            var accounts = _loginHandler.AccountManager.GetAccounts().ToList();

            IXboxGameAccount? account = null;

            // Try to use existing account
            if (accountIndex >= 0 && accountIndex < accounts.Count)
            {
                account = accounts[accountIndex];
                try
                {
                    _currentSession = await AuthenticateSilent(account);
                    await promptCallback($"Authenticated as {_currentSession.Username}");
                    return _currentSession;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Silent auth failed: {ex.Message}");
                    await promptCallback("Session expired, starting device code authentication...");
                }
            }

            // Create new account or re-auth existing
            if (account == null)
            {
                account = _loginHandler.AccountManager.NewAccount();
            }

            // Always use device code for WebSocket (user-friendly)
            _currentSession = await AuthenticateWithDeviceCode(account, promptCallback);
            await promptCallback($"Successfully authenticated as {_currentSession.Username}");
            
            return _currentSession;
        }

        private async Task<MSession> AuthenticateWithDeviceCode(IXboxGameAccount account, Func<string, Task> promptCallback)
        {
            var app = await MsalClientHelper.BuildApplicationWithCache("499c8d36-be2a-4231-9ebd-ef291b7bb64c");
            var authenticator = _loginHandler.CreateAuthenticatorWithNewAccount();

            authenticator.AddMsalOAuth(app, msal => msal.DeviceCode(async code =>
            {
                await promptCallback(code.Message);
            }));

            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
            authenticator.AddJEAuthenticator();
            
            return await authenticator.ExecuteForLauncherAsync();
        }

        private async Task<MSession> AuthenticateSilent(IXboxGameAccount account)
        {
            if (_isWindows)
            {
                var authenticator = _loginHandler.CreateAuthenticator(account, default);
                authenticator.AddMicrosoftOAuthForJE(oauth => oauth.Silent());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddJEAuthenticator();
                return await authenticator.ExecuteForLauncherAsync();
            }
            else
            {
                var app = await MsalClientHelper.BuildApplicationWithCache("499c8d36-be2a-4231-9ebd-ef291b7bb64c");
                var authenticator = _loginHandler.CreateAuthenticator(account, default);
                authenticator.AddMsalOAuth(app, msal => msal.Silent());
                authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
                authenticator.AddJEAuthenticator();
                return await authenticator.ExecuteForLauncherAsync();
            }
        }

        public async Task<List<object>> GetVersionsAsync()
        {
            try
            {
                var path = new MinecraftPath(Path.Combine(_gameBase, "default"));
                var launcher = new MinecraftLauncher(path);
                var versions = await launcher.GetAllVersionsAsync();

                return versions.Select(v => new
                {
                    name = v.Name,
                    type = v.Type.ToString()
                }).Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get versions: {ex.Message}");
                return new List<object>();
            }
        }

        public async Task LaunchMinecraftAsync(
            string version,
            int maxRam,
            Func<DownloadProgress, Task> progressCallback,
            Func<string, Task> logCallback)
        {
            if (_currentSession == null)
                throw new InvalidOperationException("Not authenticated. Please login first.");

            try
            {
                await logCallback($"Preparing to launch Minecraft {version}...");

                string versionDir = Path.Combine(_gameBase, version);
                Directory.CreateDirectory(versionDir);
                var path = new MinecraftPath(versionDir);
                var launcher = new MinecraftLauncher(path);

                var progress = new DownloadProgress();

                launcher.FileProgressChanged += (sender, args) =>
                {
                    progress.CurrentFile = args.Name;
                };

                launcher.ByteProgressChanged += async (sender, args) =>
                {
                    progress.ProgressedBytes = args.ProgressedBytes;
                    progress.TotalBytes = args.TotalBytes;
                    if (progress.TotalBytes > 0)
                    {
                        await progressCallback(progress);
                    }
                };

                await logCallback("Installing game files...");
                await launcher.InstallAsync(version);
                await logCallback("Installation complete!");

                var options = new MLaunchOption
                {
                    Session = _currentSession,
                    MaximumRamMb = maxRam
                };

                await logCallback("Building launch configuration...");
                var process = await launcher.BuildProcessAsync(version, options);

                string argsFile = Path.Combine(_rootDir, "args.txt");
                if (File.Exists(argsFile))
                {
                    string extraArgs = File.ReadAllText(argsFile);
                    process.StartInfo.Arguments += " " + extraArgs;
                    await logCallback($"Added custom arguments from args.txt");
                }

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;

                process.OutputDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await logCallback(e.Data);
                };

                process.ErrorDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await logCallback($"[ERROR] {e.Data}");
                };

                await logCallback("Starting Minecraft process...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _minecraftProcess = process;
                await logCallback($"Minecraft launched! PID: {process.Id}");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await process.WaitForExitAsync();
                        await logCallback($"Minecraft exited with code {process.ExitCode}");
                    }
                    catch (Exception ex)
                    {
                        await logCallback($"Process monitoring error: {ex.Message}");
                    }
                    finally
                    {
                        _minecraftProcess = null;
                    }
                });
            }
            catch (Exception ex)
            {
                await logCallback($"Launch failed: {ex.Message}");
                _logger.LogError($"Launch error: {ex}");
                throw;
            }
        }

        public object GetStatus()
        {
            return new
            {
                authenticated = _currentSession != null,
                username = _currentSession?.Username,
                minecraft_running = _minecraftProcess != null && !_minecraftProcess.HasExited,
                process_id = _minecraftProcess?.Id
            };
        }

        public void KillMinecraft()
        {
            try
            {
                if (_minecraftProcess != null && !_minecraftProcess.HasExited)
                {
                    _minecraftProcess.Kill(true);
                    _logger.LogInformation("Minecraft process killed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to kill Minecraft: {ex.Message}");
            }
            finally
            {
                _minecraftProcess = null;
            }
        }
    }
}