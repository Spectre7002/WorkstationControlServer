using System;
using System.Management;

namespace WorkstationControlServer.SystemActions
{
    public class BrightnessController
    {
        public int GetBrightness()
        {
            try
            {
                using (ManagementClass mclass = new ManagementClass("WmiMonitorBrightness"))
                {
                    mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                    ManagementObjectCollection instances = mclass.GetInstances();
                    foreach (ManagementObject instance in instances)
                    {
                        return Convert.ToInt32(instance.GetPropertyValue("CurrentBrightness"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка отримання яскравості (може не підтримуватися): " + ex.Message);
            }
            return 100; // Значення за замовчуванням у разі помилки
        }

        public void SetBrightness(int targetBrightness)
        {
            if (targetBrightness < 0) targetBrightness = 0;
            if (targetBrightness > 100) targetBrightness = 100;

            try
            {
                using (ManagementClass mclass = new ManagementClass("WmiMonitorBrightnessMethods"))
                {
                    mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                    ManagementObjectCollection instances = mclass.GetInstances();
                    foreach (ManagementObject instance in instances)
                    {
                        object[] args = new object[] { 1, targetBrightness };
                        instance.InvokeMethod("WmiSetBrightness", args);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка яскравості (монітор може не підтримувати WMI): " + ex.Message);
            }
        }
    }
}