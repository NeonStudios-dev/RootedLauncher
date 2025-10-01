using RootedLauncher.services;
using RootedLauncher.Services;
using CmlLib.Core.Auth;
using CmlLib.Core;
using CmlLib.Core.ProcessBuilder;
using System.Runtime.InteropServices;
using System.IO;

// ============================================================================
// Helper Methods
// ============================================================================
//test
static string CensorAccessToken(string token)
{
    return string.IsNullOrEmpty(token) ? token : "[CENSORED]";
}

static string GetRootedMcDirectory()
{
    string baseDir;
    
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".RootedMc"
        );
        Console.WriteLine("[INFO] Detected Windows platform");
    }
    else
    {
        baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".RootedMc"
        );
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Console.WriteLine("[INFO] Detected Linux platform");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Console.WriteLine("[INFO] Detected macOS platform");
    }
    
    // Ensure the directory exists
    Directory.CreateDirectory(baseDir);
    
    return baseDir;
}

static string GetGameDirectory(string version)
{
    string rootedMcDir = GetRootedMcDirectory();
    string gameDir = Path.Combine(rootedMcDir, "GameDir", "profile", version);
    Directory.CreateDirectory(gameDir);
    return gameDir;
}

// ============================================================================
// Main Application Start
// ============================================================================

Console.WriteLine("Starting Minecraft Launcher...\n");

// ============================================================================
// Initialize Account Manager
// ============================================================================

string rootedMcDir = GetRootedMcDirectory();
var accountManager = new AccountManager(rootedMcDir);

// ============================================================================
// Authentication (with account loading support)
// ============================================================================

MSession session;
var authService = new AuthService();

// Check if we have a stored account we can try to use
var defaultAccount = accountManager.GetDefaultAccount();

if (defaultAccount != null)
{
    Console.WriteLine($"[INFO] Found stored account: {defaultAccount.Username}");
    Console.WriteLine("[INFO] Attempting to use stored credentials...\n");
    
    try
    {
        // Try to create a session from stored credentials
        // Note: AccessToken may be expired, so you might need to implement token refresh logic
        session = new MSession(
            defaultAccount.Username,
            defaultAccount.AccessToken,
            defaultAccount.UUID
        );
        
        Console.WriteLine($"[INFO] Using stored account: {defaultAccount.Username}");
        accountManager.UpdateLastUsed(defaultAccount.UUID);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Stored credentials failed: {ex.Message}");
        Console.WriteLine("[INFO] Proceeding with fresh authentication...\n");
        session = await authService.AuthenticateAsync();
        
        // Save the new authentication
        accountManager.SaveAccount(
            session.Username,
            session.UUID,
            session.AccessToken,
            null, // RefreshToken if you have one
            setAsDefault: true
        );
    }
}
else
{
    Console.WriteLine("[INFO] No stored accounts found. Starting fresh authentication...\n");
    session = await authService.AuthenticateAsync();
    
    // Save the authenticated account
    Console.WriteLine("\n[INFO] Saving account credentials...");
    accountManager.SaveAccount(
        session.Username,
        session.UUID,
        session.AccessToken,
        null, // RefreshToken if you have one
        setAsDefault: true
    );
}

Console.WriteLine($"\nAuthentication successful!");
Console.WriteLine($"Welcome, {session.Username}");
Console.WriteLine($"UUID: {session.UUID}");

// ============================================================================
// Setup Game Directory
// ============================================================================

string gameVersion = "1.21";
string gameDir = GetGameDirectory(gameVersion);

Console.WriteLine($"[INFO] Game directory: {gameDir}\n");

// ============================================================================
// Initialize Launcher
// ============================================================================

Console.WriteLine("[INFO] Initializing Minecraft launcher...");
var path = new MinecraftPath(gameDir);
var launcher = new MinecraftLauncher(path);

// ============================================================================
// Custom Progress Bar Setup
// ============================================================================

var progressBar = new ProgressBarManager();
bool downloadStarted = false;

// File download progress handler
launcher.FileProgressChanged += (sender, args) =>
{
    if (!downloadStarted && args.TotalTasks > 0)
    {
        downloadStarted = true;
        progressBar.Initialize();
    }
    
    if (downloadStarted)
    {
        string fileName = args.Name ?? "Unknown file";
        progressBar.UpdateMainProgress(args.ProgressedTasks, args.TotalTasks, $"Downloading: {fileName}");
    }
};

// Byte download progress handler
launcher.ByteProgressChanged += (sender, args) =>
{
    if (downloadStarted && args.TotalBytes > 0)
    {
        progressBar.UpdateByteProgress(args.ProgressedBytes, args.TotalBytes);
    }
};

// ============================================================================
// Launch Configuration
// ============================================================================

var launchOption = new MLaunchOption
{
    Session = session,
    MaximumRamMb = 4096,
    ScreenWidth = 1280,
    ScreenHeight = 720
};

Console.WriteLine("[INFO] Launch options configured:");
Console.WriteLine($"  - Max RAM: {launchOption.MaximumRamMb} MB");
Console.WriteLine($"  - Screen: {launchOption.ScreenWidth}x{launchOption.ScreenHeight}\n");

// ============================================================================
// Launch Minecraft
// ============================================================================

Console.WriteLine($"[INFO] Preparing to launch Minecraft {gameVersion}...");

try
{
    Console.WriteLine($"[INFO] Installing and verifying game files...\n");
    var process = await launcher.InstallAndBuildProcessAsync(gameVersion, launchOption);
    
    // Complete and cleanup progress bars
    progressBar.Complete();
    await Task.Delay(100); // Brief pause for cleanup
    
    // Clear console after downloads complete
    Console.Clear();
    
    // Display clean launch information
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("       ROOTED MINECRAFT LAUNCHER");
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine($"\nPlayer: {session.Username}");
    Console.WriteLine($"Version: Minecraft {gameVersion}");
    Console.WriteLine($"\n[SUCCESS] Download complete! Starting game...\n");
    Console.WriteLine("═══════════════════════════════════════════════════\n");
    
    // Setup Minecraft output capture
    process.OutputDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            Console.WriteLine($"[MC] {e.Data}");
    };
    
    process.ErrorDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            Console.WriteLine($"[MC-ERROR] {e.Data}");
    };
    
    // Start Minecraft
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    
    Console.WriteLine("[INFO] Minecraft process started. Monitoring output...\n");
    
    await process.WaitForExitAsync();
    
    Console.WriteLine($"\n[INFO] Minecraft exited with code: {process.ExitCode}");
}
catch (Exception ex)
{
    progressBar.Complete();
    Console.WriteLine($"\n[ERROR] Failed to launch Minecraft: {ex.Message}");
    Console.WriteLine($"[ERROR] Stack trace:\n{ex.StackTrace}");
}

Console.WriteLine("\nPress any key to exit.");
Console.ReadKey();

// ============================================================================
// Custom Progress Bar Implementation (must be after top-level statements)
// ============================================================================

class ProgressBarManager
{
    private int _startLine;
    private bool _isActive;
    private readonly object _lock = new object();
    private string _mainStatus = "";
    private string _subStatus = "";
    private int _mainCurrent = 0;
    private int _mainTotal = 0;
    private long _byteCurrent = 0;
    private long _byteTotal = 0;
    private DateTime _lastUpdate = DateTime.MinValue;
    private bool _hasRendered = false;
    private const int UpdateIntervalMs = 50; // Update every 50ms for better responsiveness
    
    public void Initialize()
    {
        lock (_lock)
        {
            _startLine = Console.CursorTop;
            _isActive = true;
            _hasRendered = false;
            Console.CursorVisible = false;
            
            // Reserve space for progress bars
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            
            // Immediately render initial state
            _lastUpdate = DateTime.Now;
            Render();
        }
    }
    
    public void UpdateMainProgress(int current, int total, string status)
    {
        lock (_lock)
        {
            if (!_isActive) return;
            
            _mainCurrent = current;
            _mainTotal = total;
            _mainStatus = status;
            RenderIfNeeded();
        }
    }
    
    public void UpdateByteProgress(long current, long total)
    {
        lock (_lock)
        {
            if (!_isActive) return;
            
            _byteCurrent = current;
            _byteTotal = total;
            RenderIfNeeded();
        }
    }
    
    private void RenderIfNeeded()
    {
        // Always render the first time, then throttle subsequent updates
        if (!_hasRendered)
        {
            _hasRendered = true;
            _lastUpdate = DateTime.Now;
            Render();
            return;
        }
        
        // Only render if enough time has passed since last update
        var now = DateTime.Now;
        if ((now - _lastUpdate).TotalMilliseconds >= UpdateIntervalMs)
        {
            _lastUpdate = now;
            Render();
        }
    }
    
    private void Render()
    {
        if (!_isActive) return;
        
        try
        {
            // Save current position
            int currentLine = Console.CursorTop;
            
            // Draw main progress bar
            Console.SetCursorPosition(0, _startLine);
            DrawProgressBar(_mainCurrent, _mainTotal, _mainStatus, ConsoleColor.Green);
            
            // Draw byte progress bar
            Console.SetCursorPosition(0, _startLine + 1);
            if (_byteTotal > 0)
            {
                string byteStatus = $"{FormatBytes(_byteCurrent)} / {FormatBytes(_byteTotal)}";
                DrawProgressBar((int)(_byteCurrent * 100 / _byteTotal), 100, byteStatus, ConsoleColor.Cyan);
            }
            else
            {
                ClearLine();
            }
            
            // Extra line for spacing
            Console.SetCursorPosition(0, _startLine + 2);
            ClearLine();
            
            // Restore position
            Console.SetCursorPosition(0, _startLine + 3);
        }
        catch
        {
            // Ignore console errors during rendering
        }
    }
    
    private void DrawProgressBar(int current, int total, string status, ConsoleColor color)
    {
        if (total == 0)
        {
            // Show indeterminate state when total is unknown
            Console.Write("[");
            Console.ForegroundColor = color;
            Console.Write(new string('░', 50));
            Console.ResetColor();
            Console.Write("] Initializing...");
            return;
        }
        
        int percentage = (int)((double)current / total * 100);
        int barWidth = 50;
        int filledWidth = (int)((double)current / total * barWidth);
        
        // Clear the line first
        ClearLine();
        
        // Draw the progress bar
        Console.Write("[");
        
        Console.ForegroundColor = color;
        Console.Write(new string('█', filledWidth));
        Console.ResetColor();
        
        Console.Write(new string('░', barWidth - filledWidth));
        Console.Write($"] {percentage,3}% ");
        
        // Truncate status if too long
        int maxStatusLength = Console.WindowWidth - barWidth - 12;
        if (status.Length > maxStatusLength)
            status = status.Substring(0, maxStatusLength - 3) + "...";
        
        Console.Write(status);
    }
    
    private void ClearLine()
    {
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
    }
    
    public void Complete()
    {
        lock (_lock)
        {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Force final render to show 100%
            Render();
            
            // Small delay to ensure final state is visible
            Thread.Sleep(200);
            
            // Clear progress bar lines
            Console.SetCursorPosition(0, _startLine);
            ClearLine();
            Console.SetCursorPosition(0, _startLine + 1);
            ClearLine();
            Console.SetCursorPosition(0, _startLine + 2);
            ClearLine();
            
            Console.SetCursorPosition(0, _startLine);
            Console.CursorVisible = true;
        }
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}