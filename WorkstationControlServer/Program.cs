using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WorkstationControlServer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Автоматична перевірка та встановлення драйвера PawnIO
            EnsurePawnIOInstalled();

            Application.Run(new Form1());
        }

        private static void EnsurePawnIOInstalled()
        {
            string pawnIoPath = @"C:\Program Files\PawnIO\PawnIO.sys";
            string altPawnIoPath = @"C:\Windows\System32\drivers\pawnio.sys";

            // Якщо драйвер уже встановлено в систему — пропускаємо
            if (File.Exists(pawnIoPath) || File.Exists(altPawnIoPath))
            {
                return;
            }

            try
            {
                string targetInstallerPath = null;
                bool isTempFile = false;

                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = null;
                string[] resourceNames = assembly.GetManifestResourceNames();

                // 1. Спроба знайти вбудований ресурс всередині .exe
                for (int i = 0; i < resourceNames.Length; i++)
                {
                    if (resourceNames[i].EndsWith("PawnIO_Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                        resourceNames[i].EndsWith("PawnIOSetup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = resourceNames[i];
                        break;
                    }
                }

                if (resourceName != null)
                {
                    string tempInstaller = Path.Combine(Path.GetTempPath(), "PawnIOSetup.exe");
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            using (FileStream fileStream = new FileStream(tempInstaller, FileMode.Create))
                            {
                                stream.CopyTo(fileStream);
                            }
                            targetInstallerPath = tempInstaller;
                            isTempFile = true;
                        }
                    }
                }

                // 2. Якщо вшитого ресурсу немає — шукаємо файл у папці програми на диску
                if (string.IsNullOrEmpty(targetInstallerPath))
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] possiblePaths = new string[]
                    {
                        Path.Combine(baseDir, "Pawnio-pawniosetup", "PawnIOSetup.exe"),
                        Path.Combine(baseDir, "Pawnio-pawniosetup", "PawnIO_Setup.exe"),
                        Path.Combine(baseDir, "PawnIOSetup.exe"),
                        Path.Combine(baseDir, "PawnIO_Setup.exe")
                    };

                    for (int i = 0; i < possiblePaths.Length; i++)
                    {
                        if (File.Exists(possiblePaths[i]))
                        {
                            targetInstallerPath = possiblePaths[i];
                            break;
                        }
                    }
                }

                // 3. Запуск інсталятора в тиху
                if (!string.IsNullOrEmpty(targetInstallerPath) && File.Exists(targetInstallerPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = targetInstallerPath;
                    psi.Arguments = "-install -silent";
                    psi.UseShellExecute = true;

                    Process proc = Process.Start(psi);
                    if (proc != null)
                    {
                        proc.WaitForExit();
                    }

                    if (isTempFile && File.Exists(targetInstallerPath))
                    {
                        File.Delete(targetInstallerPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка автоматичного встановлення драйвера PawnIO: " + ex.Message);
            }
        }
    }
}