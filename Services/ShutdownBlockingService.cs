using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace BlockShutdown.Services
{
    public class ShutdownBlockingService : ApplicationContext
    {
        private NotifyIcon _notifyIcon;
        private bool _blockShutdown;
        private bool _askForConfirmation;
        private bool _runInLoop;
        private System.Windows.Forms.Timer _abortShutdownTimer;
        private CancellationTokenSource _cancellationTokenSource;
        
        // Aggressive blocking fields
        private IntPtr _keyboardHook;
        private bool _isAggressiveMode;
        private System.Windows.Forms.Timer _keepAliveTimer;
        private System.Windows.Forms.Timer _powerStateTimer;
        private bool _preventSleep;
        private bool _blockPowerKeys;

        // Windows API constants and structures
        private const int WM_QUERYENDSESSION = 0x11;
        private const int WM_ENDSESSION = 0x16;
        private const uint ENDSESSION_LOGOFF = 0x80000000;
        private const uint SHUTDOWN_NORETRY = 0x00000001;
        
        // Additional aggressive blocking constants
        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_APMQUERYSUSPEND = 0x0000;
        private const int PBT_APMQUERYSUSPENDFAILED = 0x0001;
        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        
        // Execution state constants
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        
        // Service control constants
        private const int SERVICE_CONTROL_STOP = 0x00000001;
        private const int SERVICE_CONTROL_SHUTDOWN = 0x00000005;

        // Windows API functions
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessShutdownParameters(uint dwLevel, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pwszReason);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetSystemPowerState(bool SystemAction, bool Force, bool DisableWakeEvents);

        // Delegates and structures
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private ConfigurationService.Configuration _config;

        public ShutdownBlockingService(ConfigurationService.Configuration config)
        {
            _config = config ?? new ConfigurationService.Configuration();
            _blockShutdown = _config.BlockShutdown;
            _askForConfirmation = _config.AskForConfirmation;
            _runInLoop = _config.RunInLoop;
            _isAggressiveMode = _config.AggressiveMode;
            _preventSleep = _config.PreventSleep;
            _blockPowerKeys = _config.BlockPowerKeys;
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeComponent();
            SetupShutdownHandling();
            
            // Apply configuration settings
            ApplyConfiguration();
        }

        private void ApplyConfiguration()
        {
            if (_config.RunInLoop)
            {
                StartAbortShutdownLoop();
            }

            if (_config.AggressiveMode)
            {
                StartAggressiveBlocking();
            }

            if (_config.PreventSleep)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            }

            if (_config.BlockPowerKeys)
            {
                InstallKeyboardHook();
            }
        }

        private void InitializeComponent()
        {
            // Create system tray icon
            // Try to load the custom icon, fall back to system icon if not found
            Icon trayIcon;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlockShutdown.ico");
                if (File.Exists(iconPath))
                {
                    trayIcon = new Icon(iconPath);
                }
                else
                {
                    // Try relative to the project directory during development
                    string projectIconPath = Path.Combine(Directory.GetCurrentDirectory(), "BlockShutdown.ico");
                    if (File.Exists(projectIconPath))
                    {
                        trayIcon = new Icon(projectIconPath);
                    }
                    else
                    {
                        trayIcon = SystemIcons.Application;
                    }
                }
            }
            catch
            {
                trayIcon = SystemIcons.Application;
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "Block Shutdown Control",
                Visible = true
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();
            
            var blockMenuItem = new ToolStripMenuItem("Block Shutdown", null, ToggleBlockShutdown)
            {
                Checked = _blockShutdown
            };
            
            var askMenuItem = new ToolStripMenuItem("Ask For Confirmation", null, ToggleAskForConfirmation)
            {
                Checked = _askForConfirmation
            };
            
            var loopMenuItem = new ToolStripMenuItem("Run Abort Loop", null, ToggleAbortLoop)
            {
                Checked = _runInLoop
            };
            
            var aggressiveMenuItem = new ToolStripMenuItem("Aggressive Mode", null, ToggleAggressiveMode)
            {
                Checked = _isAggressiveMode
            };
            
            var preventSleepMenuItem = new ToolStripMenuItem("Prevent Sleep", null, TogglePreventSleep)
            {
                Checked = _preventSleep
            };
            
            var blockPowerKeysMenuItem = new ToolStripMenuItem("Block Power Keys", null, ToggleBlockPowerKeys)
            {
                Checked = _blockPowerKeys
            };
            
            var separator = new ToolStripSeparator();
            var saveConfigMenuItem = new ToolStripMenuItem("Save Configuration", null, SaveConfiguration);
            var exitMenuItem = new ToolStripMenuItem("Exit", null, ExitApplication);

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                blockMenuItem,
                askMenuItem,
                loopMenuItem,
                aggressiveMenuItem,
                preventSleepMenuItem,
                blockPowerKeysMenuItem,
                separator,
                saveConfigMenuItem,
                exitMenuItem
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowStatus();
        }

        private void SetupShutdownHandling()
        {
            try
            {
                // Set process shutdown parameters (shutdown this process first)
                SetProcessShutdownParameters(0x4FF, 0);
                
                // Register for shutdown events
                Application.ApplicationExit += OnApplicationExit;
                
                // Set up message filtering for shutdown events
                Application.AddMessageFilter(new ShutdownMessageFilter(this));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up shutdown handling: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleBlockShutdown(object sender, EventArgs e)
        {
            _blockShutdown = !_blockShutdown;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _blockShutdown;
            }
        }

        private void ToggleAskForConfirmation(object sender, EventArgs e)
        {
            _askForConfirmation = !_askForConfirmation;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _askForConfirmation;
            }
        }

        private void ToggleAbortLoop(object sender, EventArgs e)
        {
            _runInLoop = !_runInLoop;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _runInLoop;
            }

            if (_runInLoop)
            {
                StartAbortShutdownLoop();
            }
            else
            {
                StopAbortShutdownLoop();
            }
        }

        private void StartAbortShutdownLoop()
        {
            if (_abortShutdownTimer != null)
            {
                _abortShutdownTimer.Stop();
                _abortShutdownTimer.Dispose();
            }

            _abortShutdownTimer = new System.Windows.Forms.Timer();
            _abortShutdownTimer.Interval = _config.AbortLoopInterval;
            _abortShutdownTimer.Tick += AbortShutdownTimer_Tick;
            _abortShutdownTimer.Start();
        }

        private void StopAbortShutdownLoop()
        {
            if (_abortShutdownTimer != null)
            {
                _abortShutdownTimer.Stop();
                _abortShutdownTimer.Dispose();
                _abortShutdownTimer = null;
            }
        }

        private void ToggleAggressiveMode(object sender, EventArgs e)
        {
            _isAggressiveMode = !_isAggressiveMode;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _isAggressiveMode;
            }

            if (_isAggressiveMode)
            {
                StartAggressiveBlocking();
            }
            else
            {
                StopAggressiveBlocking();
            }
        }

        private void TogglePreventSleep(object sender, EventArgs e)
        {
            _preventSleep = !_preventSleep;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _preventSleep;
            }

            if (_preventSleep)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            }
            else
            {
                SetThreadExecutionState(ES_CONTINUOUS);
            }
        }

        private void ToggleBlockPowerKeys(object sender, EventArgs e)
        {
            _blockPowerKeys = !_blockPowerKeys;
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                menuItem.Checked = _blockPowerKeys;
            }

            if (_blockPowerKeys)
            {
                InstallKeyboardHook();
            }
            else
            {
                UninstallKeyboardHook();
            }
        }

        private void StartAggressiveBlocking()
        {
            // Start keep-alive timer
            if (_keepAliveTimer == null)
            {
                _keepAliveTimer = new System.Windows.Forms.Timer();
                _keepAliveTimer.Interval = _config.KeepAliveInterval;
                _keepAliveTimer.Tick += KeepAliveTimer_Tick;
                _keepAliveTimer.Start();
            }

            // Start power state monitoring
            if (_powerStateTimer == null)
            {
                _powerStateTimer = new System.Windows.Forms.Timer();
                _powerStateTimer.Interval = _config.PowerStateInterval;
                _powerStateTimer.Tick += PowerStateTimer_Tick;
                _powerStateTimer.Start();
            }

            // Prevent sleep
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            // Install keyboard hook to block power keys
            InstallKeyboardHook();

            // Register hotkeys for emergency shutdown prevention
            RegisterEmergencyHotkeys();
        }

        private void StopAggressiveBlocking()
        {
            if (_keepAliveTimer != null)
            {
                _keepAliveTimer.Stop();
                _keepAliveTimer.Dispose();
                _keepAliveTimer = null;
            }

            if (_powerStateTimer != null)
            {
                _powerStateTimer.Stop();
                _powerStateTimer.Dispose();
                _powerStateTimer = null;
            }

            SetThreadExecutionState(ES_CONTINUOUS);
            UninstallKeyboardHook();
            UnregisterEmergencyHotkeys();
        }

        private void KeepAliveTimer_Tick(object sender, EventArgs e)
        {
            // Keep the system awake
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            
            // Abort any pending shutdowns
            AbortShutdownTimer_Tick(sender, e);
        }

        private void PowerStateTimer_Tick(object sender, EventArgs e)
        {
            // Monitor for power state changes and block them
            if (_blockShutdown)
            {
                // Additional aggressive shutdown prevention
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = "/change standby-timeout-ac 0",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                }
                catch { }
            }
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHook == IntPtr.Zero)
            {
                var moduleHandle = GetModuleHandle(null);
                _keyboardHook = SetWindowsHookEx(13, KeyboardHookProc, moduleHandle, 0); // WH_KEYBOARD_LL = 13
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _blockPowerKeys)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                
                // Block power-related keys
                switch (hookStruct.vkCode)
                {
                    case 0x5B: // Left Windows key
                    case 0x5C: // Right Windows key
                    case 0x73: // F4
                    case 0x74: // F5
                    case 0x75: // F6
                    case 0x76: // F7
                    case 0x77: // F8
                    case 0x78: // F9
                    case 0x79: // F10
                    case 0x7A: // F11
                    case 0x7B: // F12
                        // Check for Windows + L (lock), Windows + U (ease of access), etc.
                        if (GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0) // Windows key pressed
                        {
                            return (IntPtr)1; // Block the key
                        }
                        break;
                }
            }
            
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void RegisterEmergencyHotkeys()
        {
            // Register Ctrl+Alt+Shift+S as emergency shutdown prevention
            RegisterHotKey(IntPtr.Zero, 1, 0x0007, 0x53); // MOD_CONTROL | MOD_ALT | MOD_SHIFT, 'S'
        }

        private void UnregisterEmergencyHotkeys()
        {
            UnregisterHotKey(IntPtr.Zero, 1);
        }

        private void SaveConfiguration(object sender, EventArgs e)
        {
            try
            {
                // Update configuration with current values
                _config.BlockShutdown = _blockShutdown;
                _config.AskForConfirmation = _askForConfirmation;
                _config.RunInLoop = _runInLoop;
                _config.AggressiveMode = _isAggressiveMode;
                _config.PreventSleep = _preventSleep;
                _config.BlockPowerKeys = _blockPowerKeys;

                var configService = new ConfigurationService();
                configService.SaveConfiguration(_config);

                MessageBox.Show("Configuration saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AbortShutdownTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/a",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                // Log error silently
                Debug.WriteLine($"Error aborting shutdown: {ex.Message}");
            }
        }

        private void ShowStatus()
        {
            string status = $"Block Shutdown: {(_blockShutdown ? "Enabled" : "Disabled")}\n" +
                           $"Ask Confirmation: {(_askForConfirmation ? "Enabled" : "Disabled")}\n" +
                           $"Abort Loop: {(_runInLoop ? "Running" : "Stopped")}";
            
            MessageBox.Show(status, "Block Shutdown Status", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            if (_blockShutdown)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "shutdown",
                            Arguments = "/a",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
                catch { }
            }

            if (_askForConfirmation)
            {
                var result = MessageBox.Show("Something tried to stop BlockShutdown. Allow it?", 
                    "Confirm exit?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.No)
                {
                    return;
                }
            }

            ExitThread();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            if (_blockShutdown)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "shutdown",
                            Arguments = "/a",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
                catch { }
            }
        }

        public bool HandleShutdownEvent(uint wParam, uint lParam)
        {
            string eventType = "Unknown";
            if ((lParam & ENDSESSION_LOGOFF) != 0)
            {
                eventType = "Logoff";
            }
            else
            {
                eventType = "Shutdown";
            }

            ExecuteEventDirectory($"{eventType}Request");

            if (!_blockShutdown)
            {
                ExecuteEventDirectory(eventType);
                return true; // Allow shutdown
            }

            try
            {
                BlockShutdown($"BlockShutdown is blocking {eventType}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error blocking shutdown: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (_askForConfirmation)
            {
                var result = MessageBox.Show($"{eventType} in Progress.\nAllow it?", 
                    $"Confirm {eventType}", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        StopBlockingShutdown();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error stopping shutdown block: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    ExecuteEventDirectory(eventType);
                    return true; // Allow shutdown
                }
                else
                {
                    ExecuteEventDirectory($"{eventType}Block");
                    return false; // Block shutdown
                }
            }

            ExecuteEventDirectory($"{eventType}Block");
            return false; // Block shutdown
        }

        public bool HandlePowerEvent(uint wParam, uint lParam)
        {
            if (!_blockShutdown && !_isAggressiveMode)
                return false; // Let default handler process

            switch (wParam)
            {
                case PBT_APMQUERYSUSPEND:
                    // System is about to suspend
                    if (_blockShutdown || _isAggressiveMode)
                    {
                        // Block suspension
                        return true;
                    }
                    break;

                case PBT_APMSUSPEND:
                    // System is suspending
                    if (_blockShutdown || _isAggressiveMode)
                    {
                        // Try to wake up immediately
                        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                        return true;
                    }
                    break;

                case PBT_APMRESUMEAUTOMATIC:
                case PBT_APMRESUMESUSPEND:
                    // System is resuming
                    break;
            }

            return false;
        }

        public bool HandleSystemCommand(uint wParam, uint lParam)
        {
            if (!_blockShutdown && !_isAggressiveMode)
                return false; // Let default handler process

            // Check for shutdown/restart commands
            switch (wParam & 0xFFF0)
            {
                case 0x0010: // SC_CLOSE
                case 0x0011: // SC_QUERYENDSESSION
                case 0x0012: // SC_ENDSESSION
                case 0x0013: // SC_QUIT
                case 0x0014: // SC_LOGOFF
                case 0x0015: // SC_SHUTDOWN
                case 0x0016: // SC_REBOOT
                case 0x0017: // SC_TASKLIST
                case 0x0018: // SC_SCREENSAVE
                case 0x0019: // SC_HOTKEY
                case 0x001A: // SC_DEFAULT
                case 0x001B: // SC_MONITORPOWER
                case 0x001C: // SC_POWEROFF
                case 0x001D: // SC_POWERON
                case 0x001E: // SC_RESTORE
                case 0x001F: // SC_SEPARATOR
                case 0x0020: // SC_TASKLIST
                case 0x0021: // SC_SCREENSAVE
                case 0x0022: // SC_HOTKEY
                case 0x0023: // SC_DEFAULT
                case 0x0024: // SC_MONITORPOWER
                case 0x0025: // SC_POWEROFF
                case 0x0026: // SC_POWERON
                case 0x0027: // SC_RESTORE
                case 0x0028: // SC_SEPARATOR
                case 0x0029: // SC_TASKLIST
                case 0x002A: // SC_SCREENSAVE
                case 0x002B: // SC_HOTKEY
                case 0x002C: // SC_DEFAULT
                case 0x002D: // SC_MONITORPOWER
                case 0x002E: // SC_POWEROFF
                case 0x002F: // SC_POWERON
                case 0x0030: // SC_RESTORE
                case 0x0031: // SC_SEPARATOR
                case 0x0032: // SC_TASKLIST
                case 0x0033: // SC_SCREENSAVE
                case 0x0034: // SC_HOTKEY
                case 0x0035: // SC_DEFAULT
                case 0x0036: // SC_MONITORPOWER
                case 0x0037: // SC_POWEROFF
                case 0x0038: // SC_POWERON
                case 0x0039: // SC_RESTORE
                case 0x003A: // SC_SEPARATOR
                case 0x003B: // SC_TASKLIST
                case 0x003C: // SC_SCREENSAVE
                case 0x003D: // SC_HOTKEY
                case 0x003E: // SC_DEFAULT
                case 0x003F: // SC_MONITORPOWER
                case 0x0040: // SC_POWEROFF
                case 0x0041: // SC_POWERON
                case 0x0042: // SC_RESTORE
                case 0x0043: // SC_SEPARATOR
                case 0x0044: // SC_TASKLIST
                case 0x0045: // SC_SCREENSAVE
                case 0x0046: // SC_HOTKEY
                case 0x0047: // SC_DEFAULT
                case 0x0048: // SC_MONITORPOWER
                case 0x0049: // SC_POWEROFF
                case 0x004A: // SC_POWERON
                case 0x004B: // SC_RESTORE
                case 0x004C: // SC_SEPARATOR
                case 0x004D: // SC_TASKLIST
                case 0x004E: // SC_SCREENSAVE
                case 0x004F: // SC_HOTKEY
                case 0x0050: // SC_DEFAULT
                case 0x0051: // SC_MONITORPOWER
                case 0x0052: // SC_POWEROFF
                case 0x0053: // SC_POWERON
                case 0x0054: // SC_RESTORE
                case 0x0055: // SC_SEPARATOR
                case 0x0056: // SC_TASKLIST
                case 0x0057: // SC_SCREENSAVE
                case 0x0058: // SC_HOTKEY
                case 0x0059: // SC_DEFAULT
                case 0x005A: // SC_MONITORPOWER
                case 0x005B: // SC_POWEROFF
                case 0x005C: // SC_POWERON
                case 0x005D: // SC_RESTORE
                case 0x005E: // SC_SEPARATOR
                case 0x005F: // SC_TASKLIST
                case 0x0060: // SC_SCREENSAVE
                case 0x0061: // SC_HOTKEY
                case 0x0062: // SC_DEFAULT
                case 0x0063: // SC_MONITORPOWER
                case 0x0064: // SC_POWEROFF
                case 0x0065: // SC_POWERON
                case 0x0066: // SC_RESTORE
                case 0x0067: // SC_SEPARATOR
                case 0x0068: // SC_TASKLIST
                case 0x0069: // SC_SCREENSAVE
                case 0x006A: // SC_HOTKEY
                case 0x006B: // SC_DEFAULT
                case 0x006C: // SC_MONITORPOWER
                case 0x006D: // SC_POWEROFF
                case 0x006E: // SC_POWERON
                case 0x006F: // SC_RESTORE
                case 0x0070: // SC_SEPARATOR
                case 0x0071: // SC_TASKLIST
                case 0x0072: // SC_SCREENSAVE
                case 0x0073: // SC_HOTKEY
                case 0x0074: // SC_DEFAULT
                case 0x0075: // SC_MONITORPOWER
                case 0x0076: // SC_POWEROFF
                case 0x0077: // SC_POWERON
                case 0x0078: // SC_RESTORE
                case 0x0079: // SC_SEPARATOR
                case 0x007A: // SC_TASKLIST
                case 0x007B: // SC_SCREENSAVE
                case 0x007C: // SC_HOTKEY
                case 0x007D: // SC_DEFAULT
                case 0x007E: // SC_MONITORPOWER
                case 0x007F: // SC_POWEROFF
                case 0x0080: // SC_POWERON
                case 0x0081: // SC_RESTORE
                case 0x0082: // SC_SEPARATOR
                case 0x0083: // SC_TASKLIST
                case 0x0084: // SC_SCREENSAVE
                case 0x0085: // SC_HOTKEY
                case 0x0086: // SC_DEFAULT
                case 0x0087: // SC_MONITORPOWER
                case 0x0088: // SC_POWEROFF
                case 0x0089: // SC_POWERON
                case 0x008A: // SC_RESTORE
                case 0x008B: // SC_SEPARATOR
                case 0x008C: // SC_TASKLIST
                case 0x008D: // SC_SCREENSAVE
                case 0x008E: // SC_HOTKEY
                case 0x008F: // SC_DEFAULT
                case 0x0090: // SC_MONITORPOWER
                case 0x0091: // SC_POWEROFF
                case 0x0092: // SC_POWERON
                case 0x0093: // SC_RESTORE
                case 0x0094: // SC_SEPARATOR
                case 0x0095: // SC_TASKLIST
                case 0x0096: // SC_SCREENSAVE
                case 0x0097: // SC_HOTKEY
                case 0x0098: // SC_DEFAULT
                case 0x0099: // SC_MONITORPOWER
                case 0x009A: // SC_POWEROFF
                case 0x009B: // SC_POWERON
                case 0x009C: // SC_RESTORE
                case 0x009D: // SC_SEPARATOR
                case 0x009E: // SC_TASKLIST
                case 0x009F: // SC_SCREENSAVE
                case 0x00A0: // SC_HOTKEY
                case 0x00A1: // SC_DEFAULT
                case 0x00A2: // SC_MONITORPOWER
                case 0x00A3: // SC_POWEROFF
                case 0x00A4: // SC_POWERON
                case 0x00A5: // SC_RESTORE
                case 0x00A6: // SC_SEPARATOR
                case 0x00A7: // SC_TASKLIST
                case 0x00A8: // SC_SCREENSAVE
                case 0x00A9: // SC_HOTKEY
                case 0x00AA: // SC_DEFAULT
                case 0x00AB: // SC_MONITORPOWER
                case 0x00AC: // SC_POWEROFF
                case 0x00AD: // SC_POWERON
                case 0x00AE: // SC_RESTORE
                case 0x00AF: // SC_SEPARATOR
                case 0x00B0: // SC_TASKLIST
                case 0x00B1: // SC_SCREENSAVE
                case 0x00B2: // SC_HOTKEY
                case 0x00B3: // SC_DEFAULT
                case 0x00B4: // SC_MONITORPOWER
                case 0x00B5: // SC_POWEROFF
                case 0x00B6: // SC_POWERON
                case 0x00B7: // SC_RESTORE
                case 0x00B8: // SC_SEPARATOR
                case 0x00B9: // SC_TASKLIST
                case 0x00BA: // SC_SCREENSAVE
                case 0x00BB: // SC_HOTKEY
                case 0x00BC: // SC_DEFAULT
                case 0x00BD: // SC_MONITORPOWER
                case 0x00BE: // SC_POWEROFF
                case 0x00BF: // SC_POWERON
                case 0x00C0: // SC_RESTORE
                case 0x00C1: // SC_SEPARATOR
                case 0x00C2: // SC_TASKLIST
                case 0x00C3: // SC_SCREENSAVE
                case 0x00C4: // SC_HOTKEY
                case 0x00C5: // SC_DEFAULT
                case 0x00C6: // SC_MONITORPOWER
                case 0x00C7: // SC_POWEROFF
                case 0x00C8: // SC_POWERON
                case 0x00C9: // SC_RESTORE
                case 0x00CA: // SC_SEPARATOR
                case 0x00CB: // SC_TASKLIST
                case 0x00CC: // SC_SCREENSAVE
                case 0x00CD: // SC_HOTKEY
                case 0x00CE: // SC_DEFAULT
                case 0x00CF: // SC_MONITORPOWER
                case 0x00D0: // SC_POWEROFF
                case 0x00D1: // SC_POWERON
                case 0x00D2: // SC_RESTORE
                case 0x00D3: // SC_SEPARATOR
                case 0x00D4: // SC_TASKLIST
                case 0x00D5: // SC_SCREENSAVE
                case 0x00D6: // SC_HOTKEY
                case 0x00D7: // SC_DEFAULT
                case 0x00D8: // SC_MONITORPOWER
                case 0x00D9: // SC_POWEROFF
                case 0x00DA: // SC_POWERON
                case 0x00DB: // SC_RESTORE
                case 0x00DC: // SC_SEPARATOR
                case 0x00DD: // SC_TASKLIST
                case 0x00DE: // SC_SCREENSAVE
                case 0x00DF: // SC_HOTKEY
                case 0x00E0: // SC_DEFAULT
                case 0x00E1: // SC_MONITORPOWER
                case 0x00E2: // SC_POWEROFF
                case 0x00E3: // SC_POWERON
                case 0x00E4: // SC_RESTORE
                case 0x00E5: // SC_SEPARATOR
                case 0x00E6: // SC_TASKLIST
                case 0x00E7: // SC_SCREENSAVE
                case 0x00E8: // SC_HOTKEY
                case 0x00E9: // SC_DEFAULT
                case 0x00EA: // SC_MONITORPOWER
                case 0x00EB: // SC_POWEROFF
                case 0x00EC: // SC_POWERON
                case 0x00ED: // SC_RESTORE
                case 0x00EE: // SC_SEPARATOR
                case 0x00EF: // SC_TASKLIST
                case 0x00F0: // SC_SCREENSAVE
                case 0x00F1: // SC_HOTKEY
                case 0x00F2: // SC_DEFAULT
                case 0x00F3: // SC_MONITORPOWER
                case 0x00F4: // SC_POWEROFF
                case 0x00F5: // SC_POWERON
                case 0x00F6: // SC_RESTORE
                case 0x00F7: // SC_SEPARATOR
                case 0x00F8: // SC_TASKLIST
                case 0x00F9: // SC_SCREENSAVE
                case 0x00FA: // SC_HOTKEY
                case 0x00FB: // SC_DEFAULT
                case 0x00FC: // SC_MONITORPOWER
                case 0x00FD: // SC_POWEROFF
                case 0x00FE: // SC_POWERON
                case 0x00FF: // SC_RESTORE
                    // Block all system commands that could lead to shutdown
                    return true;
            }

            return false;
        }

        private void BlockShutdown(string reason)
        {
            var hwnd = GetConsoleWindow();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = Process.GetCurrentProcess().MainWindowHandle;
            }

            if (!ShutdownBlockReasonCreate(hwnd, reason))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private void StopBlockingShutdown()
        {
            var hwnd = GetConsoleWindow();
            if (hwnd == IntPtr.Zero)
            {
                hwnd = Process.GetCurrentProcess().MainWindowHandle;
            }

            ShutdownBlockReasonDestroy(hwnd);
        }

        private void ExecuteEventDirectory(string eventName)
        {
            if (!_config.EnableEventDirectories)
                return;

            try
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (!string.IsNullOrEmpty(_config.EventDirectoryBase))
                {
                    baseDir = Path.Combine(baseDir, _config.EventDirectoryBase);
                }
                
                string eventDir = Path.Combine(baseDir, eventName);
                
                if (!Directory.Exists(eventDir))
                {
                    Directory.CreateDirectory(eventDir);
                    if (_config.EnableLogging)
                    {
                        Debug.WriteLine($"Created event directory: {eventDir}");
                    }
                }
                else
                {
                    foreach (string file in Directory.GetFiles(eventDir))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = file,
                                UseShellExecute = true
                            });
                            if (_config.EnableLogging)
                            {
                                Debug.WriteLine($"Executed: {file}");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_config.EnableLogging)
                            {
                                Debug.WriteLine($"Error executing {file}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_config.EnableLogging)
                {
                    Debug.WriteLine($"Error in ExecuteEventDirectory: {ex.Message}");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                StopAbortShutdownLoop();
                StopAggressiveBlocking();
                UninstallKeyboardHook();
                UnregisterEmergencyHotkeys();
                _notifyIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
} 