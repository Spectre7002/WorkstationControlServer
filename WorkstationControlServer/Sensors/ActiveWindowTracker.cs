using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkstationControlServer.Sensors
{
    public class ActiveWindowTracker
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }
            return "Невідоме вікно";
        }

        public string DetermineProfile(string windowTitle)
        {
            string lowerTitle = windowTitle.ToLower();

            if (lowerTitle.Contains("counter-strike") ||
                lowerTitle.Contains("cs2") ||
                lowerTitle.Contains("valorant") ||
                lowerTitle.Contains("rocket league") ||
                lowerTitle.Contains("snowrunner") ||
                lowerTitle.Contains("doom") ||
                lowerTitle.Contains("call of duty"))
            {
                return "Ігровий (Пінг, GPU, FPS)";
            }

            if (lowerTitle.Contains("visual studio") ||
                lowerTitle.Contains("code") ||
                lowerTitle.Contains("devenv"))
            {
                return "Робочий (Медіа, ОЗП, CPU)";
            }

            return "Стандартний (Головне меню)";
        }
    }
}