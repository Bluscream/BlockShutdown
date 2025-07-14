# BlockShutdown

A C# .NET application that replicates the functionality of AutoHotkey scripts to block system shutdown and logoff events. This application runs in the background and provides a system tray interface for controlling shutdown blocking behavior.

## Features

- **Shutdown Blocking**: Prevents system shutdown and logoff events
- **User Confirmation**: Option to ask for confirmation before allowing shutdown
- **Abort Loop**: Continuously runs `shutdown /a` to abort any pending shutdowns
- **Aggressive Mode**: Advanced blocking with multiple techniques
- **Sleep Prevention**: Keeps system awake and prevents sleep/hibernation
- **Power Key Blocking**: Blocks keyboard shortcuts that could trigger shutdown
- **System Tray Interface**: Easy-to-use context menu for controlling the application
- **Event Directory Execution**: Executes scripts in specific directories when shutdown events occur
- **Command Line Arguments**: Support for various startup options

## Configuration

The application supports multiple configuration sources with the following priority (highest to lowest):

1. **Command Line Arguments** - Highest priority
2. **Environment Variables** - Second priority
3. **BlockShutdown.json in %APPDATA%** - Third priority
4. **BlockShutdown.json in program folder** - Lowest priority

### Command Line Arguments

#### Boolean Flags
- `/block` or `--block` - Enable shutdown blocking
- `/ask` or `--ask` - Enable confirmation prompts
- `/loop` or `--loop` - Start the abort loop
- `/aggressive` or `--aggressive` - Enable aggressive mode
- `/prevent-sleep` or `--prevent-sleep` - Prevent system sleep
- `/block-power-keys` or `--block-power-keys` - Block power-related keyboard shortcuts
- `/enable-events` or `--enable-events` - Enable event directory execution
- `/enable-logging` or `--enable-logging` - Enable logging

#### Key-Value Arguments
- `/abort-interval=1000` or `--abort-interval=1000` - Set abort loop interval (ms)
- `/keep-alive-interval=5000` or `--keep-alive-interval=5000` - Set keep-alive interval (ms)
- `/power-state-interval=1000` or `--power-state-interval=1000` - Set power state monitoring interval (ms)
- `/emergency-hotkey="Ctrl+Alt+Shift+S"` or `--emergency-hotkey="Ctrl+Alt+Shift+S"` - Set emergency hotkey
- `/event-directory-base="Programs"` or `--event-directory-base="Programs"` - Set event directory base
- `/log-level="Info"` or `--log-level="Info"` - Set logging level

## Usage

### Environment Variables

All settings can be configured via environment variables with the `BLOCKSHUTDOWN_` prefix:

```bash
# Boolean settings
set BLOCKSHUTDOWN_BLOCK=true
set BLOCKSHUTDOWN_ASK=true
set BLOCKSHUTDOWN_LOOP=true
set BLOCKSHUTDOWN_AGGRESSIVE=true
set BLOCKSHUTDOWN_PREVENT_SLEEP=true
set BLOCKSHUTDOWN_BLOCK_POWER_KEYS=true
set BLOCKSHUTDOWN_ENABLE_EVENT_DIRECTORIES=true
set BLOCKSHUTDOWN_ENABLE_LOGGING=true

# Integer settings
set BLOCKSHUTDOWN_ABORT_INTERVAL=1000
set BLOCKSHUTDOWN_KEEP_ALIVE_INTERVAL=5000
set BLOCKSHUTDOWN_POWER_STATE_INTERVAL=1000

# String settings
set BLOCKSHUTDOWN_EMERGENCY_HOTKEY=Ctrl+Alt+Shift+S
set BLOCKSHUTDOWN_EVENT_DIRECTORY_BASE=Programs
set BLOCKSHUTDOWN_LOG_LEVEL=Info
```

### Configuration File

Create `BlockShutdown.json` in either the program folder or `%APPDATA%`:

```json
{
  "BlockShutdown": false,
  "AskForConfirmation": false,
  "RunInLoop": false,
  "AggressiveMode": false,
  "PreventSleep": false,
  "BlockPowerKeys": false,
  "AbortLoopInterval": 1000,
  "KeepAliveInterval": 5000,
  "PowerStateInterval": 1000,
  "EmergencyHotkey": "Ctrl+Alt+Shift+S",
  "EnableEventDirectories": true,
  "EventDirectoryBase": "Programs",
  "EnableLogging": false,
  "LogLevel": "Info"
}
```

### Basic Usage
```bash
# Run with default settings (no blocking)
BlockShutdown.exe

# Run with shutdown blocking enabled
BlockShutdown.exe /block

# Run with confirmation prompts enabled
BlockShutdown.exe /ask

# Run with abort loop enabled
BlockShutdown.exe /loop

# Run with aggressive mode and custom intervals
BlockShutdown.exe /aggressive /abort-interval=500 /keep-alive-interval=3000

# Combine multiple options
BlockShutdown.exe /block /ask /loop /aggressive
```

### System Tray Interface

The application runs in the system tray with the following menu options:

- **Block Shutdown** - Toggle shutdown blocking on/off
- **Ask For Confirmation** - Toggle confirmation prompts on/off
- **Run Abort Loop** - Toggle the abort loop on/off
- **Aggressive Mode** - Enable advanced blocking techniques
- **Prevent Sleep** - Keep system awake and prevent sleep/hibernation
- **Block Power Keys** - Block keyboard shortcuts that could trigger shutdown
- **Save Configuration** - Save current settings to configuration file
- **Exit** - Close the application

Double-click the tray icon to view the current status.

## Event Directories

The application creates and monitors directories in the Programs folder for executing scripts during shutdown events:

- `ShutdownRequest` - Executed when a shutdown is requested
- `Shutdown` - Executed when shutdown is allowed
- `ShutdownBlock` - Executed when shutdown is blocked
- `LogoffRequest` - Executed when a logoff is requested
- `Logoff` - Executed when logoff is allowed
- `LogoffBlock` - Executed when logoff is blocked

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Administrative privileges (required for shutdown blocking)

## Building

1. Ensure you have .NET 8.0 SDK installed
2. Clone or download the source code
3. Open a command prompt in the project directory
4. Run: `dotnet build`
5. The executable will be created in `bin/Debug/net8.0-windows/` or `bin/Release/net8.0-windows/`

## Installation

1. Build the application or download a pre-built release
2. Run the executable as Administrator
3. The application will start and appear in the system tray

## Technical Details

### Windows API Integration

The application uses several Windows API functions:

- `SetProcessShutdownParameters` - Sets shutdown priority
- `ShutdownBlockReasonCreate` - Creates a shutdown block reason
- `ShutdownBlockReasonDestroy` - Removes a shutdown block reason
- `SetThreadExecutionState` - Prevents system sleep and keeps display on
- `SetWindowsHookEx` - Installs keyboard hooks to block power keys
- `RegisterHotKey` - Registers emergency hotkeys for shutdown prevention
- Message filtering for `WM_QUERYENDSESSION`, `WM_POWERBROADCAST`, and `WM_SYSCOMMAND` events

### Architecture

- **Program.cs** - Main entry point and command line argument parsing
- **ShutdownBlockingService.cs** - Core service handling shutdown events and system tray
- **ShutdownMessageFilter.cs** - Windows message filtering for shutdown events

## Security Notes

- The application requires administrative privileges to block system shutdowns
- The application runs in the background and may prevent normal system shutdown
- Use with caution in production environments

## License

This project is open source and available under the MIT License.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests. 