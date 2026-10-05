using System;
using System.Management;
using LibreHardwareMonitor.Hardware;
using WorkstationControlServer.Models;

namespace WorkstationControlServer.Sensors
{
    public class TelemetryService
    {
        private Computer _computer;
        private double _totalRamGB;
        private int _ramSpeedMts;

        public TelemetryService()
        {
            _computer = new Computer();
            _computer.IsCpuEnabled = true;
            _computer.IsGpuEnabled = true;
            _computer.IsMemoryEnabled = true;
            _computer.IsMotherboardEnabled = true;

            ReadStaticRamInfo();
        }

        private void ReadStaticRamInfo()
        {
            long totalBytes = 0;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["Capacity"] != null)
                        {
                            totalBytes += Convert.ToInt64(obj["Capacity"]);
                        }

                        if (_ramSpeedMts == 0)
                        {
                            if (obj["ConfiguredClockSpeed"] != null)
                            {
                                _ramSpeedMts = Convert.ToInt32(obj["ConfiguredClockSpeed"]);
                            }
                            else if (obj["Speed"] != null)
                            {
                                _ramSpeedMts = Convert.ToInt32(obj["Speed"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка WMI: " + ex.Message);
            }

            if (totalBytes > 0)
            {
                _totalRamGB = totalBytes / (1024.0 * 1024.0 * 1024.0);
            }
        }

        public void Start()
        {
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка ініціалізації датчиків: " + ex.Message);
            }
        }

        public void Stop()
        {
            try
            {
                _computer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Помилка закриття датчиків: " + ex.Message);
            }
        }

        public TelemetryData GetCurrentTelemetry()
        {
            TelemetryData data = new TelemetryData();
            data.RamTotalGB = _totalRamGB;
            data.RamSpeedMts = _ramSpeedMts;

            foreach (IHardware hardware in _computer.Hardware)
            {
                try
                {
                    hardware.Update();
                }
                catch
                {
                    continue;
                }

                ReadHardwareRecursive(hardware, data);
            }

            if (data.GpuTemperature == 0 && data.CpuTemperature > 0)
            {
                data.GpuTemperature = data.CpuTemperature;
            }

            return data;
        }

        private void ReadHardwareRecursive(IHardware hardware, TelemetryData data)
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.Value.HasValue)
                {
                    int val = (int)Math.Round(sensor.Value.Value);

                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        if (val > 0)
                        {
                            if (hardware.HardwareType == HardwareType.Cpu)
                            {
                                if (data.CpuTemperature == 0) data.CpuTemperature = val;
                            }
                            else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                     hardware.HardwareType == HardwareType.GpuAmd ||
                                     hardware.HardwareType == HardwareType.GpuIntel)
                            {
                                if (data.GpuTemperature == 0) data.GpuTemperature = val;
                            }
                        }
                    }
                    else if (sensor.SensorType == SensorType.Load)
                    {
                        if (hardware.HardwareType == HardwareType.Cpu && sensor.Name.Contains("Total"))
                        {
                            data.CpuLoad = val;
                        }
                        else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                 hardware.HardwareType == HardwareType.GpuAmd ||
                                 hardware.HardwareType == HardwareType.GpuIntel)
                        {
                            if (sensor.Name.Contains("Core") || sensor.Name.Contains("GPU") || sensor.Name.Contains("D3D"))
                            {
                                if (val > data.GpuLoad) data.GpuLoad = val;
                            }
                        }
                        else if (hardware.HardwareType == HardwareType.Memory && sensor.Name.Contains("Memory"))
                        {
                            data.RamUsagePercent = val;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Clock)
                    {
                        if (hardware.HardwareType == HardwareType.Cpu && sensor.Name.Contains("Core"))
                        {
                            if (val > data.CpuClock) data.CpuClock = val;
                        }
                        else if ((hardware.HardwareType == HardwareType.GpuNvidia ||
                                  hardware.HardwareType == HardwareType.GpuAmd ||
                                  hardware.HardwareType == HardwareType.GpuIntel) &&
                                 (sensor.Name.Contains("GPU Core") || sensor.Name.Contains("Core")))
                        {
                            if (data.GpuClock == 0) data.GpuClock = val;
                        }
                    }
                    else if (sensor.SensorType == SensorType.Data && hardware.HardwareType == HardwareType.Memory)
                    {
                        if (sensor.Name.Contains("Memory Used"))
                        {
                            data.RamUsedGB = Math.Round(sensor.Value.Value, 2);
                        }
                    }
                }
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                try
                {
                    subHardware.Update();
                }
                catch
                {
                    continue;
                }
                ReadHardwareRecursive(subHardware, data);
            }
        }
    }
}