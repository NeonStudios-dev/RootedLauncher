using RootedLauncher.services;
using System;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        var launcherService = new MinecraftLauncherService();

        while (true)
        {
            Console.WriteLine("\nMinecraft Launcher\n" +
                              "1. Authenticate\n" +
                              "2. List Accounts\n" +
                              "3. Launch Minecraft\n" +
                              "4. List Versions\n" +
                              "5. Status\n" +
                              "6. Kill Minecraft\n" +
                              "7. Exit");
            Console.Write("Select an option: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await Authenticate(launcherService);
                    break;
                case "2":
                    ListAccounts(launcherService);
                    break;
                case "3":
                    await LaunchMinecraft(launcherService);
                    break;
                case "4":
                    await ListVersions(launcherService);
                    break;
                case "5":
                    Status(launcherService);
                    break;
                case "6":
                    KillMinecraft(launcherService);
                    break;
                case "7":
                    return;
                default:
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }
    }

    private static async Task Authenticate(MinecraftLauncherService launcherService)
    {
        Console.WriteLine("Select authentication method (device_code, etc.): ");
        var method = Console.ReadLine();
        var session = await launcherService.AuthenticateAsync(method, -1, (msg) => {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        });
        Console.WriteLine($"Authenticated as {session.Username}");
    }

    private static void ListAccounts(MinecraftLauncherService launcherService)
    {
        var accounts = launcherService.GetAccounts();
        foreach (dynamic account in accounts)
        {
            Console.WriteLine($"- {account.identifier}");
        }
    }

    private static async Task LaunchMinecraft(MinecraftLauncherService launcherService)
    {
        Console.Write("Enter version (e.g., latest-release): ");
        var version = Console.ReadLine();
        Console.Write("Enter max RAM in MB (e.g., 4096): ");
        var maxRamStr = Console.ReadLine();
        if (int.TryParse(maxRamStr, out int maxRam))
        {
            await launcherService.LaunchMinecraftAsync(version, maxRam, 
                progress => {
                    Console.WriteLine($"Downloading {progress.CurrentFile}: {progress.Percentage}%");
                    return Task.CompletedTask;
                }, 
                log => {
                    Console.WriteLine(log);
                    return Task.CompletedTask;
                });
        }
        else
        {
            Console.WriteLine("Invalid RAM value.");
        }
    }

    private static async Task ListVersions(MinecraftLauncherService launcherService)
    {
        var versions = await launcherService.GetVersionsAsync();
        foreach (var version in versions)
        {
            Console.WriteLine($"- {version}");
        }
    }

    private static void Status(MinecraftLauncherService launcherService)
    {
        var status = launcherService.GetStatus();
        Console.WriteLine($"Status: {status}");
    }

    private static void KillMinecraft(MinecraftLauncherService launcherService)
    {
        launcherService.KillMinecraft();
        Console.WriteLine("Minecraft process killed.");
    }
}