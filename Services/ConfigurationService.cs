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

        public abstract class ConfigEntry
        {
            public string Name { get; }
            public string JsonPropertyName { get; }
            public string EnvironmentVariable { get; }
            public string CommandLineArg { get; }
            public object DefaultValue { get; }

            protected ConfigEntry(string name, string jsonPropertyName, string envVar, string arg, object defaultValue)
            {
                Name = name;
                JsonPropertyName = jsonPropertyName;
                EnvironmentVariable = envVar;
                CommandLineArg = arg;
                DefaultValue = defaultValue;
            }

            public abstract void SetValue(Configuration config, object value);
            public abstract object GetValue(Configuration config);
            public abstract bool IsBoolean { get; }
        }

        public class BoolConfigEntry : ConfigEntry
        {
            public BoolConfigEntry(string name, string jsonPropertyName, string envVar, string arg, bool defaultValue = false)
                : base(name, jsonPropertyName, envVar, arg, defaultValue)
            {
            }

            public override void SetValue(Configuration config, object value)
            {
                bool boolValue = ParseBool(value.ToString());
                switch (Name)
                {
                    case "BlockShutdown": config.BlockShutdown = boolValue; break;
                    case "AskForConfirmation": config.AskForConfirmation = boolValue; break;
                    case "RunInLoop": config.RunInLoop = boolValue; break;
                    case "AggressiveMode": config.AggressiveMode = boolValue; break;
                    case "PreventSleep": config.PreventSleep = boolValue; break;
                    case "BlockPowerKeys": config.BlockPowerKeys = boolValue; break;
                    case "EnableEventDirectories": config.EnableEventDirectories = boolValue; break;
                    case "EnableLogging": config.EnableLogging = boolValue; break;
                }
            }

            public override object GetValue(Configuration config)
            {
                return Name switch
                {
                    "BlockShutdown" => config.BlockShutdown,
                    "AskForConfirmation" => config.AskForConfirmation,
                    "RunInLoop" => config.RunInLoop,
                    "AggressiveMode" => config.AggressiveMode,
                    "PreventSleep" => config.PreventSleep,
                    "BlockPowerKeys" => config.BlockPowerKeys,
                    "EnableEventDirectories" => config.EnableEventDirectories,
                    "EnableLogging" => config.EnableLogging,
                    _ => DefaultValue
                };
            }

            public override bool IsBoolean => true;

            private static bool ParseBool(string value)
            {
                return value?.ToLower() switch
                {
                    "true" or "1" or "yes" or "on" => true,
                    _ => false
                };
            }
        }

        public class IntConfigEntry : ConfigEntry
        {
            public IntConfigEntry(string name, string jsonPropertyName, string envVar, string arg, int defaultValue = 0)
                : base(name, jsonPropertyName, envVar, arg, defaultValue)
            {
            }

            public override void SetValue(Configuration config, object value)
            {
                int intValue = ParseInt(value.ToString());
                switch (Name)
                {
                    case "AbortLoopInterval": config.AbortLoopInterval = intValue; break;
                    case "KeepAliveInterval": config.KeepAliveInterval = intValue; break;
                    case "PowerStateInterval": config.PowerStateInterval = intValue; break;
                }
            }

            public override object GetValue(Configuration config)
            {
                return Name switch
                {
                    "AbortLoopInterval" => config.AbortLoopInterval,
                    "KeepAliveInterval" => config.KeepAliveInterval,
                    "PowerStateInterval" => config.PowerStateInterval,
                    _ => DefaultValue
                };
            }

            public override bool IsBoolean => false;

            private static int ParseInt(string value)
            {
                return int.TryParse(value, out int result) ? result : 0;
            }
        }

        public class StringConfigEntry : ConfigEntry
        {
            public StringConfigEntry(string name, string jsonPropertyName, string envVar, string arg, string defaultValue = "")
                : base(name, jsonPropertyName, envVar, arg, defaultValue)
            {
            }

            public override void SetValue(Configuration config, object value)
            {
                string stringValue = ParseString(value.ToString());
                switch (Name)
                {
                    case "EmergencyHotkey": config.EmergencyHotkey = stringValue; break;
                    case "EventDirectoryBase": config.EventDirectoryBase = stringValue; break;
                    case "LogLevel": config.LogLevel = stringValue; break;
                }
            }

            public override object GetValue(Configuration config)
            {
                return Name switch
                {
                    "EmergencyHotkey" => config.EmergencyHotkey,
                    "EventDirectoryBase" => config.EventDirectoryBase,
                    "LogLevel" => config.LogLevel,
                    _ => DefaultValue
                };
            }

            public override bool IsBoolean => false;

            private static string ParseString(string value)
            {
                return value ?? "";
            }
        }

        private static readonly List<ConfigEntry> ConfigEntries = new()
        {
            // Boolean configuration entries
            new BoolConfigEntry("BlockShutdown", "BlockShutdown", "BLOCKSHUTDOWN_BLOCK", "block", false),
            new BoolConfigEntry("AskForConfirmation", "AskForConfirmation", "BLOCKSHUTDOWN_ASK", "ask", false),
            new BoolConfigEntry("RunInLoop", "RunInLoop", "BLOCKSHUTDOWN_LOOP", "loop", false),
            new BoolConfigEntry("AggressiveMode", "AggressiveMode", "BLOCKSHUTDOWN_AGGRESSIVE", "aggressive", false),
            new BoolConfigEntry("PreventSleep", "PreventSleep", "BLOCKSHUTDOWN_PREVENT_SLEEP", "prevent-sleep", false),
            new BoolConfigEntry("BlockPowerKeys", "BlockPowerKeys", "BLOCKSHUTDOWN_BLOCK_POWER_KEYS", "block-power-keys", false),
            new BoolConfigEntry("EnableEventDirectories", "EnableEventDirectories", "BLOCKSHUTDOWN_ENABLE_EVENT_DIRECTORIES", "enable-events", true),
            new BoolConfigEntry("EnableLogging", "EnableLogging", "BLOCKSHUTDOWN_ENABLE_LOGGING", "enable-logging", false),
            
            // Integer configuration entries
            new IntConfigEntry("AbortLoopInterval", "AbortLoopInterval", "BLOCKSHUTDOWN_ABORT_INTERVAL", "abort-interval", 1000),
            new IntConfigEntry("KeepAliveInterval", "KeepAliveInterval", "BLOCKSHUTDOWN_KEEP_ALIVE_INTERVAL", "keep-alive-interval", 5000),
            new IntConfigEntry("PowerStateInterval", "PowerStateInterval", "BLOCKSHUTDOWN_POWER_STATE_INTERVAL", "power-state-interval", 1000),
            
            // String configuration entries
            new StringConfigEntry("EmergencyHotkey", "EmergencyHotkey", "BLOCKSHUTDOWN_EMERGENCY_HOTKEY", "emergency-hotkey", "Ctrl+Alt+Shift+S"),
            new StringConfigEntry("EventDirectoryBase", "EventDirectoryBase", "BLOCKSHUTDOWN_EVENT_DIRECTORY_BASE", "event-directory-base", "Programs"),
            new StringConfigEntry("LogLevel", "LogLevel", "BLOCKSHUTDOWN_LOG_LEVEL", "log-level", "Info")
            
            // Example: To add a new configuration entry, simply add it here:
            // new BoolConfigEntry("NewFeature", "NewFeature", "BLOCKSHUTDOWN_NEW_FEATURE", "new-feature", false),
            // new IntConfigEntry("NewTimeout", "NewTimeout", "BLOCKSHUTDOWN_NEW_TIMEOUT", "new-timeout", 5000),
            // new StringConfigEntry("NewPath", "NewPath", "BLOCKSHUTDOWN_NEW_PATH", "new-path", "default"),
        };

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
                        // Apply JSON values using ConfigEntries
                        foreach (var entry in ConfigEntries)
                        {
                            var jsonValue = GetJsonPropertyValue(jsonConfig, entry.JsonPropertyName);
                            if (jsonValue != null)
                            {
                                ApplyValue(config, entry, jsonValue);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load configuration from {filePath}: {ex.Message}");
            }
        }

        private object GetJsonPropertyValue(Configuration jsonConfig, string propertyName)
        {
            var property = typeof(Configuration).GetProperty(propertyName);
            return property?.GetValue(jsonConfig);
        }

        private void LoadFromEnvironmentVariables(Configuration config)
        {
            try
            {
                foreach (var entry in ConfigEntries)
                {
                    var envValue = Environment.GetEnvironmentVariable(entry.EnvironmentVariable);
                    if (!string.IsNullOrEmpty(envValue))
                    {
                        ApplyValue(config, entry, envValue);
                    }
                }
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
                
                // Check for boolean flags
                foreach (var entry in ConfigEntries.Where(e => e.IsBoolean))
                {
                    if (lowerArg == $"/{entry.CommandLineArg}" || lowerArg == $"--{entry.CommandLineArg}")
                    {
                        entry.SetValue(config, true);
                        break;
                    }
                }

                // Check for key-value pairs
                foreach (var entry in ConfigEntries)
                {
                    if (lowerArg.StartsWith($"/{entry.CommandLineArg}=") || lowerArg.StartsWith($"--{entry.CommandLineArg}="))
                    {
                        var value = ExtractValue(arg);
                        if (!string.IsNullOrEmpty(value))
                        {
                            ApplyValue(config, entry, value);
                        }
                        break;
                    }
                }
            }
        }

        private void ApplyValue(Configuration config, ConfigEntry entry, object value)
        {
            try
            {
                entry.SetValue(config, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not apply value {value} to {entry.Name}: {ex.Message}");
            }
        }

        private string ExtractValue(string arg)
        {
            var equalIndex = arg.IndexOf('=');
            return equalIndex >= 0 ? arg.Substring(equalIndex + 1) : string.Empty;
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
            var defaultConfig = new Configuration();
            SaveConfiguration(defaultConfig);
        }

        public List<ConfigEntry> GetConfigEntries()
        {
            return ConfigEntries.ToList();
        }
    }
} 