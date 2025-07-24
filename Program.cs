using System;
using System.Windows.Forms;
using System.Security.Principal;
using System.Diagnostics;
using static Utils;

namespace BlockShutdown
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load configuration with priority: Command Line > Environment Variables > JSON files
            var configService = new ConfigurationService<Configuration>("BlockShutdown.json", Configuration.GetConfigEntries());
            var config = configService.LoadConfiguration(args);

            // Elevate if requested and not already running as admin
            if (config.Elevate && !Utils.IsRunAsAdmin())
            {
                try
                {
                    Utils.RelaunchAsAdmin(args);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User refused the elevation
                    return;
                }
                return;
            }

            // Create and start the shutdown blocking service with configuration
            var shutdownService = new ShutdownBlockingService(config);
            
            // Run the application (this will show the system tray icon)
            Application.Run(shutdownService);
        }
    }
} 