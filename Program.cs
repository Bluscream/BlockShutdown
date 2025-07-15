using System;
using System.Windows.Forms;
using BlockShutdown.Services;

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

            // Create and start the shutdown blocking service with configuration
            var shutdownService = new ShutdownBlockingService(config);
            
            // Run the application (this will show the system tray icon)
            Application.Run(shutdownService);
        }
    }
} 