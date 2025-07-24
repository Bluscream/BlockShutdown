using System;
using System.Collections.Generic;
using BlockShutdown.Services;

namespace BlockShutdown
{
    public class Configuration
    {
        public bool BlockShutdown { get; set; } = true;
        public bool AskForConfirmation { get; set; } = true;
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

        public static List<ConfigurationService<Configuration>.ConfigEntry> GetConfigEntries()
        {
            return new List<ConfigurationService<Configuration>.ConfigEntry>
            {
                // Boolean configuration entries
                new ConfigurationService<Configuration>.BoolConfigEntry("BlockShutdown", "BlockShutdown", "BLOCKSHUTDOWN_BLOCK", "block", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("AskForConfirmation", "AskForConfirmation", "BLOCKSHUTDOWN_ASK", "ask", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("RunInLoop", "RunInLoop", "BLOCKSHUTDOWN_LOOP", "loop", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("AggressiveMode", "AggressiveMode", "BLOCKSHUTDOWN_AGGRESSIVE", "aggressive", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("PreventSleep", "PreventSleep", "BLOCKSHUTDOWN_PREVENT_SLEEP", "prevent-sleep", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("BlockPowerKeys", "BlockPowerKeys", "BLOCKSHUTDOWN_BLOCK_POWER_KEYS", "block-power-keys", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("EnableEventDirectories", "EnableEventDirectories", "BLOCKSHUTDOWN_ENABLE_EVENT_DIRECTORIES", "enable-events", false),
                new ConfigurationService<Configuration>.BoolConfigEntry("EnableLogging", "EnableLogging", "BLOCKSHUTDOWN_ENABLE_LOGGING", "enable-logging", false),
                
                // Integer configuration entries
                new ConfigurationService<Configuration>.IntConfigEntry("AbortLoopInterval", "AbortLoopInterval", "BLOCKSHUTDOWN_ABORT_INTERVAL", "abort-interval", 1000),
                new ConfigurationService<Configuration>.IntConfigEntry("KeepAliveInterval", "KeepAliveInterval", "BLOCKSHUTDOWN_KEEP_ALIVE_INTERVAL", "keep-alive-interval", 5000),
                new ConfigurationService<Configuration>.IntConfigEntry("PowerStateInterval", "PowerStateInterval", "BLOCKSHUTDOWN_POWER_STATE_INTERVAL", "power-state-interval", 1000),
                
                // String configuration entries
                new ConfigurationService<Configuration>.StringConfigEntry("EmergencyHotkey", "EmergencyHotkey", "BLOCKSHUTDOWN_EMERGENCY_HOTKEY", "emergency-hotkey", "Ctrl+Alt+Shift+S"),
                new ConfigurationService<Configuration>.StringConfigEntry("EventDirectoryBase", "EventDirectoryBase", "BLOCKSHUTDOWN_EVENT_DIRECTORY_BASE", "event-directory-base", "Programs"),
                new ConfigurationService<Configuration>.StringConfigEntry("LogLevel", "LogLevel", "BLOCKSHUTDOWN_LOG_LEVEL", "log-level", "Info")
                
                // Example: To add a new configuration entry, simply add it here:
                // new ConfigurationService<Configuration>.BoolConfigEntry("NewFeature", "NewFeature", "BLOCKSHUTDOWN_NEW_FEATURE", "new-feature", false),
                // new ConfigurationService<Configuration>.IntConfigEntry("NewTimeout", "NewTimeout", "BLOCKSHUTDOWN_NEW_TIMEOUT", "new-timeout", 5000),
                // new ConfigurationService<Configuration>.StringConfigEntry("NewPath", "NewPath", "BLOCKSHUTDOWN_NEW_PATH", "new-path", "default"),
            };
        }
    }
}