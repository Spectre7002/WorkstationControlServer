using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WorkstationControlServer.SystemActions
{
    public class SystemController
    {
        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_OFF = 2;

        public void LockPC()
        {
            try { LockWorkStation(); }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public void TurnOffMonitor(IntPtr formHandle)
        {
            try { SendMessage(formHandle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_OFF); }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public void SleepPC()
        {
            try { Application.SetSuspendState(PowerState.Suspend, false, false); }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public void ShutdownPC()
        {
            try { Process.Start("shutdown", "/s /t 0"); }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }
    }
}