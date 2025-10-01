using RootedLauncher.services;
using CmlLib.Core.Auth;
using CmlLib.Core;

// Create an instance of the authentication service
var authService = new AuthService();

// Start the authentication process and wait for the result
// This will display an interactive menu in the console
Console.WriteLine("Starting Minecraft Launcher...");
MSession session = await authService.AuthenticateAsync();

// The 'session' object now holds the authenticated user's info
Console.WriteLine($"Authentication successful!");
Console.WriteLine($"Welcome, {session.Username}");
Console.WriteLine($"UUID: {session.UUID}");

// You can now use this session object to launch the game with CmlLib
// For example:
// var launcher = new CMLauncher(new MinecraftPath());
// var launchOptions = new MLaunchOption
// {
//     Session = session,
//     VersionType = "release",
//     GameVersion = "1.20.1" 
// };
// await launcher.LaunchAsync(launchOptions);

Console.WriteLine("Press any key to exit.");
Console.ReadKey();