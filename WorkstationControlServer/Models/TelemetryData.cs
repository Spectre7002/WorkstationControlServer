namespace WorkstationControlServer.Models
{
    public class TelemetryData
    {
        public int CpuTemperature { get; set; }
        public int CpuLoad { get; set; }
        public int CpuClock { get; set; }

        public int GpuTemperature { get; set; }
        public int GpuLoad { get; set; }
        public int GpuClock { get; set; }

        public int RamUsagePercent { get; set; }
        public double RamUsedGB { get; set; }
        public double RamTotalGB { get; set; }
        public int RamSpeedMts { get; set; }
    }
}