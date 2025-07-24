using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;

public class EventDirectoryManager
{
    private const string eventsDirName = "Events";
    private List<DirectoryInfo> Roots { get; } = new() {
        #pragma warning disable CS8625
        new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)),
        new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
        #pragma warning restore CS8625
    };

    public EventDirectoryManager() { }	
    public EventDirectoryManager(List<DirectoryInfo> roots) { Roots = roots; }

    public void Initialize()
    {
        Log("Event Directories:");
        foreach (var root in Roots)
        {
            #pragma warning disable CS8625
            var eventDir = root.Combine(eventsDirName ?? string.Empty);
            #pragma warning restore CS8625
            Log($"\t{eventDir.FullName}");
        }
    }

    public void ExecuteEvent(string name, IDictionary<string, string> environmentVariables = null, IEnumerable<string> commandLineArgs = null)
    {
        if (string.IsNullOrEmpty(name)) return;
        Log($"Executing event {name ?? string.Empty}");
        foreach (var root in Roots)
        {
            #pragma warning disable CS8625
            var eventDir = root.Combine(eventsDirName ?? string.Empty, name ?? string.Empty);
            #pragma warning restore CS8625
            try {
                if (!eventDir.Exists) eventDir.Create();
                foreach (var file in eventDir.Exists ? eventDir.GetFiles("*.*", SearchOption.TopDirectoryOnly) : Array.Empty<FileInfo>())
            {
                ExecuteFile(file.FullName, environmentVariables, commandLineArgs);
            }
            } catch (Exception ex) {
                Log($"Error executing event {name ?? string.Empty}: {ex.Message}");
            }
        }
    }

    private void ExecuteFile(string filePath, IDictionary<string, string> environmentVariables = null, IEnumerable<string> commandLineArgs = null)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try
        {
            var isBatch = filePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
            var isShortcut = filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

            var startInfo = new ProcessStartInfo
            {
                FileName = isBatch ? "cmd.exe" : filePath,
                Arguments = isBatch ? $"/c \"{filePath}\"" : "",
                UseShellExecute = isShortcut ? true : false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (commandLineArgs != null) {
                startInfo.Arguments += string.Join(" ", commandLineArgs.Select(a => $"\"{a}\""));
            }

            if (environmentVariables != null)
            {
                if (!isShortcut)
                {
                    foreach (var kvp in environmentVariables)
                    {
                        Log($"Setting environment variable {kvp.Key} to {kvp.Value}");
                        startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                } else {
                    Log($"Warning: Environment variables are not supported for shortcuts (.lnk files)!");
                }
            }

            Log($"Executing {filePath} with arguments: {startInfo.Arguments}");

            var process = Process.Start(startInfo);
            if (process != null)
            {
                process.EnableRaisingEvents = false;
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to execute {filePath}: {ex.Message}");
        }
    }

    private void Log(object message, params object[] args)
    {
        var msg = message?.ToString() ?? string.Empty;
        #pragma warning disable CS8604
        var formattedMessage = args != null && args.Length > 0 ? string.Format(msg, args) : msg;
        #pragma warning restore CS8604
        Console.WriteLine(formattedMessage);
    }
}