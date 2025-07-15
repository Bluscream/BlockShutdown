using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace BlockShutdown.Services
{
    public class ConfigurationService<T> where T : class, new()
    {
        private readonly string _configFileName;
        private readonly string _appDataFolder;
        private readonly string _programFolder;
        private readonly List<ConfigEntry> _configEntries;

        public ConfigurationService(string configFileName, List<ConfigEntry> configEntries)
        {
            _configFileName = configFileName;
            _appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _programFolder = AppDomain.CurrentDomain.BaseDirectory;
            _configEntries = configEntries;
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

            public abstract void SetValue(T config, object value);
            public abstract object GetValue(T config);
            public abstract bool IsBoolean { get; }
        }

        public class BoolConfigEntry : ConfigEntry
        {
            public BoolConfigEntry(string name, string jsonPropertyName, string envVar, string arg, bool defaultValue = false)
                : base(name, jsonPropertyName, envVar, arg, defaultValue)
            {
            }

            public override void SetValue(T config, object value)
            {
                bool boolValue = ParseBool(value.ToString());
                var property = typeof(T).GetProperty(Name);
                if (property != null && property.PropertyType == typeof(bool))
                {
                    property.SetValue(config, boolValue);
                }
            }

            public override object GetValue(T config)
            {
                var property = typeof(T).GetProperty(Name);
                return property?.GetValue(config) ?? DefaultValue;
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

            public override void SetValue(T config, object value)
            {
                int intValue = ParseInt(value.ToString());
                var property = typeof(T).GetProperty(Name);
                if (property != null && property.PropertyType == typeof(int))
                {
                    property.SetValue(config, intValue);
                }
            }

            public override object GetValue(T config)
            {
                var property = typeof(T).GetProperty(Name);
                return property?.GetValue(config) ?? DefaultValue;
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

            public override void SetValue(T config, object value)
            {
                string stringValue = ParseString(value.ToString());
                var property = typeof(T).GetProperty(Name);
                if (property != null && property.PropertyType == typeof(string))
                {
                    property.SetValue(config, stringValue);
                }
            }

            public override object GetValue(T config)
            {
                var property = typeof(T).GetProperty(Name);
                return property?.GetValue(config) ?? DefaultValue;
            }

            public override bool IsBoolean => false;

            private static string ParseString(string value)
            {
                return value ?? "";
            }
        }



        public T LoadConfiguration(string[] commandLineArgs)
        {
            var config = new T();

            // Load from JSON files (lowest priority first)
            LoadFromJsonFile(config, Path.Combine(_programFolder, _configFileName));
            LoadFromJsonFile(config, Path.Combine(_appDataFolder, _configFileName));

            // Load from environment variables (higher priority)
            LoadFromEnvironmentVariables(config);

            // Load from command line arguments (highest priority)
            LoadFromCommandLineArgs(config, commandLineArgs);

            return config;
        }

        private void LoadFromJsonFile(T config, string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var jsonContent = File.ReadAllText(filePath);
                    var jsonConfig = JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jsonConfig != null)
                    {
                        // Apply JSON values using ConfigEntries
                        foreach (var entry in _configEntries)
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

        private object GetJsonPropertyValue(T jsonConfig, string propertyName)
        {
            var property = typeof(T).GetProperty(propertyName);
            return property?.GetValue(jsonConfig);
        }

        private void LoadFromEnvironmentVariables(T config)
        {
            try
            {
                foreach (var entry in _configEntries)
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

        private void LoadFromCommandLineArgs(T config, string[] args)
        {
            if (args == null) return;

            foreach (string arg in args)
            {
                string lowerArg = arg.ToLower();
                
                // Check for boolean flags
                foreach (var entry in _configEntries.Where(e => e.IsBoolean))
                {
                    if (lowerArg == $"/{entry.CommandLineArg}" || lowerArg == $"--{entry.CommandLineArg}")
                    {
                        entry.SetValue(config, true);
                        break;
                    }
                }

                // Check for key-value pairs
                foreach (var entry in _configEntries)
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

        private void ApplyValue(T config, ConfigEntry entry, object value)
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

        public void SaveConfiguration(T config, string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = Path.Combine(_appDataFolder, _configFileName);
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
            var defaultConfig = new T();
            SaveConfiguration(defaultConfig);
        }

        public List<ConfigEntry> GetConfigEntries()
        {
            return _configEntries.ToList();
        }
    }
} 