using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BlockShutdown.Services
{
    public class ShutdownMessageFilter : IMessageFilter
    {
        private readonly ShutdownBlockingService _service;

        // Windows API constants
        private const int WM_QUERYENDSESSION = 0x11;
        private const int WM_ENDSESSION = 0x16;

        public ShutdownMessageFilter(ShutdownBlockingService service)
        {
            _service = service;
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_QUERYENDSESSION:
                    // Handle shutdown/logoff query
                    bool allowShutdown = _service.HandleShutdownEvent((uint)m.WParam, (uint)m.LParam);
                    
                    // Set the result based on whether we want to allow or block the shutdown
                    if (!allowShutdown)
                    {
                        // Block the shutdown
                        m.Result = (IntPtr)0;
                        return true; // We handled the message
                    }
                    else
                    {
                        // Allow the shutdown
                        m.Result = (IntPtr)1;
                        return true; // We handled the message
                    }

                case WM_ENDSESSION:
                    // Handle actual shutdown/logoff
                    // This is called after WM_QUERYENDSESSION if the shutdown is allowed
                    return false; // Let the default handler process this

                case 0x0218: // WM_POWERBROADCAST
                    // Handle power management events
                    return _service.HandlePowerEvent((uint)m.WParam, (uint)m.LParam);

                case 0x0112: // WM_SYSCOMMAND
                    // Handle system commands like shutdown, restart, etc.
                    return _service.HandleSystemCommand((uint)m.WParam, (uint)m.LParam);
            }

            return false; // Let other messages pass through
        }
    }
} 