using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

static class Utils
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AllocConsole();

    private static bool _consoleEnabled = false;

    internal static void CreateConsole()
    {
        AllocConsole();
        _consoleEnabled = true;
    }

    internal static void SetConsoleTitle(string title)
    {
        Console.Title = title;
    }

    internal static void SetConsoleEnabled(bool enabled)
    {
        _consoleEnabled = enabled;
    }

    internal static void Log(object message, params object[] args)
    {
        if (!_consoleEnabled) return;
        var msg = message?.ToString() ?? string.Empty;
        Console.WriteLine(args != null && args.Length > 0 ? string.Format(msg, args) : msg);
    }

    public static string GetOwnPath() {
        var possiblePaths = new List<string?> {
            Process.GetCurrentProcess().MainModule?.FileName,
            AppContext.BaseDirectory,
            Environment.GetCommandLineArgs().FirstOrDefault(),
            Assembly.GetEntryAssembly()?.Location,
            ".",
        };
        foreach (var path in possiblePaths.Where(p => !string.IsNullOrEmpty(p))) {
            if (System.IO.File.Exists(path!)) {
                return System.IO.Path.GetFullPath(path!);
            }
        }
        return string.Empty;
    }

    public static bool IsRunAsAdmin()
    {
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static void RelaunchAsAdmin(string[] args)
    {
        var exeName = GetOwnPath();
        var startInfo = new ProcessStartInfo(exeName)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = args != null ? string.Join(" ", args) : string.Empty
        };
        Process.Start(startInfo);
    }
}