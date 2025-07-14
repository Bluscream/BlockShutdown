using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace BlockShutdown.Services
{
    public class ConfigurationService
    {
        private static readonly string ConfigFileName = "BlockShutdown.json";
        private static readonly string AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static readonly string ProgramFolder = AppDomain.CurrentDomain.BaseDirectory;

        public class Configuration
        {
            public bool BlockShutdown { get; set; } = false;
            public bool AskForConfirmation { get; set; } = false;
            public bool RunInLoop { get; set; } = false;
            public bool AggressiveMode { get; set; } = false;
            public bool PreventSleep { get; set; } = false;
            public bool BlockPowerKeys { get; set; } = false;
            public int AbortLoopInterval { get; set; } = 1000; // milliseconds
            public int KeepAliveInterval { get; set; } = 5000; // milliseconds
            public int PowerStateInterval { get; set; } = 1000; // milliseconds
            public string EmergencyHotkey { get; set; } = "Ctrl+Alt+Shift+S";
            public bool EnableEventDirectories { get; set; } = true;
            public string EventDirectoryBase { get; set; } = "Programs";
            public bool EnableLogging { get; set; } = false;
            public string LogLevel { get; set; } = "Info";
        }

        public Configuration LoadConfiguration(string[] commandLineArgs)
        {
            var config = new Configuration();

            // Load from JSON files (lowest priority first)
            LoadFromJsonFile(config, Path.Combine(ProgramFolder, ConfigFileName));
            LoadFromJsonFile(config, Path.Combine(AppDataFolder, ConfigFileName));

            // Load from environment variables (higher priority)
            LoadFromEnvironmentVariables(config);

            // Load from command line arguments (highest priority)
            LoadFromCommandLineArgs(config, commandLineArgs);

            return config;
        }

        private void LoadFromJsonFile(Configuration config, string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var jsonContent = File.ReadAllText(filePath);
                    var jsonConfig = JsonSerializer.Deserialize<Configuration>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jsonConfig != null)
                    {
                        // Only set values that are not default (to allow partial configs)
                        if (jsonConfig.BlockShutdown) config.BlockShutdown = true;
                        if (jsonConfig.AskForConfirmation) config.AskForConfirmation = true;
                        if (jsonConfig.RunInLoop) config.RunInLoop = true;
                        if (jsonConfig.AggressiveMode) config.AggressiveMode = true;
                        if (jsonConfig.PreventSleep) config.PreventSleep = true;
                        if (jsonConfig.BlockPowerKeys) config.BlockPowerKeys = true;
                        if (jsonConfig.AbortLoopInterval > 0) config.AbortLoopInterval = jsonConfig.AbortLoopInterval;
                        if (jsonConfig.KeepAliveInterval > 0) config.KeepAliveInterval = jsonConfig.KeepAliveInterval;
                        if (jsonConfig.PowerStateInterval > 0) config.PowerStateInterval = jsonConfig.PowerStateInterval;
                        if (!string.IsNullOrEmpty(jsonConfig.EmergencyHotkey)) config.EmergencyHotkey = jsonConfig.EmergencyHotkey;
                        if (jsonConfig.EnableEventDirectories) config.EnableEventDirectories = true;
                        if (!string.IsNullOrEmpty(jsonConfig.EventDirectoryBase)) config.EventDirectoryBase = jsonConfig.EventDirectoryBase;
                        if (jsonConfig.EnableLogging) config.EnableLogging = true;
                        if (!string.IsNullOrEmpty(jsonConfig.LogLevel)) config.LogLevel = jsonConfig.LogLevel;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail - use defaults
                Console.WriteLine($"Warning: Could not load configuration from {filePath}: {ex.Message}");
            }
        }

        private void LoadFromEnvironmentVariables(Configuration config)
        {
            try
            {
                // Boolean settings
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_BLOCK")) config.BlockShutdown = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_ASK")) config.AskForConfirmation = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_LOOP")) config.RunInLoop = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_AGGRESSIVE")) config.AggressiveMode = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_PREVENT_SLEEP")) config.PreventSleep = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_BLOCK_POWER_KEYS")) config.BlockPowerKeys = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_ENABLE_EVENT_DIRECTORIES")) config.EnableEventDirectories = true;
                if (GetEnvironmentVariableBool("BLOCKSHUTDOWN_ENABLE_LOGGING")) config.EnableLogging = true;

                // Integer settings
                var abortInterval = GetEnvironmentVariableInt("BLOCKSHUTDOWN_ABORT_INTERVAL");
                if (abortInterval > 0) config.AbortLoopInterval = abortInterval;

                var keepAliveInterval = GetEnvironmentVariableInt("BLOCKSHUTDOWN_KEEP_ALIVE_INTERVAL");
                if (keepAliveInterval > 0) config.KeepAliveInterval = keepAliveInterval;

                var powerStateInterval = GetEnvironmentVariableInt("BLOCKSHUTDOWN_POWER_STATE_INTERVAL");
                if (powerStateInterval > 0) config.PowerStateInterval = powerStateInterval;

                // String settings
                var emergencyHotkey = Environment.GetEnvironmentVariable("BLOCKSHUTDOWN_EMERGENCY_HOTKEY");
                if (!string.IsNullOrEmpty(emergencyHotkey)) config.EmergencyHotkey = emergencyHotkey;

                var eventDirectoryBase = Environment.GetEnvironmentVariable("BLOCKSHUTDOWN_EVENT_DIRECTORY_BASE");
                if (!string.IsNullOrEmpty(eventDirectoryBase)) config.EventDirectoryBase = eventDirectoryBase;

                var logLevel = Environment.GetEnvironmentVariable("BLOCKSHUTDOWN_LOG_LEVEL");
                if (!string.IsNullOrEmpty(logLevel)) config.LogLevel = logLevel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load environment variables: {ex.Message}");
            }
        }

        private void LoadFromCommandLineArgs(Configuration config, string[] args)
        {
            if (args == null) return;

            foreach (string arg in args)
            {
                string lowerArg = arg.ToLower();
                
                // Boolean flags
                if (lowerArg == "/block" || lowerArg == "--block") config.BlockShutdown = true;
                if (lowerArg == "/ask" || lowerArg == "--ask") config.AskForConfirmation = true;
                if (lowerArg == "/loop" || lowerArg == "--loop") config.RunInLoop = true;
                if (lowerArg == "/aggressive" || lowerArg == "--aggressive") config.AggressiveMode = true;
                if (lowerArg == "/prevent-sleep" || lowerArg == "--prevent-sleep") config.PreventSleep = true;
                if (lowerArg == "/block-power-keys" || lowerArg == "--block-power-keys") config.BlockPowerKeys = true;
                if (lowerArg == "/enable-events" || lowerArg == "--enable-events") config.EnableEventDirectories = true;
                if (lowerArg == "/enable-logging" || lowerArg == "--enable-logging") config.EnableLogging = true;

                // Key-value pairs
                if (lowerArg.StartsWith("/abort-interval=") || lowerArg.StartsWith("--abort-interval="))
                {
                    var value = ExtractValue(arg);
                    if (int.TryParse(value, out int interval) && interval > 0)
                        config.AbortLoopInterval = interval;
                }

                if (lowerArg.StartsWith("/keep-alive-interval=") || lowerArg.StartsWith("--keep-alive-interval="))
                {
                    var value = ExtractValue(arg);
                    if (int.TryParse(value, out int interval) && interval > 0)
                        config.KeepAliveInterval = interval;
                }

                if (lowerArg.StartsWith("/power-state-interval=") || lowerArg.StartsWith("--power-state-interval="))
                {
                    var value = ExtractValue(arg);
                    if (int.TryParse(value, out int interval) && interval > 0)
                        config.PowerStateInterval = interval;
                }

                if (lowerArg.StartsWith("/emergency-hotkey=") || lowerArg.StartsWith("--emergency-hotkey="))
                {
                    var value = ExtractValue(arg);
                    if (!string.IsNullOrEmpty(value))
                        config.EmergencyHotkey = value;
                }

                if (lowerArg.StartsWith("/event-directory-base=") || lowerArg.StartsWith("--event-directory-base="))
                {
                    var value = ExtractValue(arg);
                    if (!string.IsNullOrEmpty(value))
                        config.EventDirectoryBase = value;
                }

                if (lowerArg.StartsWith("/log-level=") || lowerArg.StartsWith("--log-level="))
                {
                    var value = ExtractValue(arg);
                    if (!string.IsNullOrEmpty(value))
                        config.LogLevel = value;
                }
            }
        }

        private string ExtractValue(string arg)
        {
            var equalIndex = arg.IndexOf('=');
            return equalIndex >= 0 ? arg.Substring(equalIndex + 1) : string.Empty;
        }

        private bool GetEnvironmentVariableBool(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) return false;
            
            return value.ToLower() switch
            {
                "true" or "1" or "yes" or "on" => true,
                _ => false
            };
        }

        private int GetEnvironmentVariableInt(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) return 0;
            
            return int.TryParse(value, out int result) ? result : 0;
        }

        public void SaveConfiguration(Configuration config, string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = Path.Combine(AppDataFolder, ConfigFileName);
                }

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var jsonContent = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration to {filePath}: {ex.Message}");
            }
        }

        public void CreateDefaultConfiguration()
        {
            var defaultConfig = new Configuration
            {
                BlockShutdown = false,
                AskForConfirmation = false,
                RunInLoop = false,
                AggressiveMode = false,
                PreventSleep = false,
                BlockPowerKeys = false,
                AbortLoopInterval = 1000,
                KeepAliveInterval = 5000,
                PowerStateInterval = 1000,
                EmergencyHotkey = "Ctrl+Alt+Shift+S",
                EnableEventDirectories = true,
                EventDirectoryBase = "Programs",
                EnableLogging = false,
                LogLevel = "Info"
            };

            SaveConfiguration(defaultConfig);
        }
    }
} 