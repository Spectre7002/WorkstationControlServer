using System;
using System.Net.NetworkInformation;

namespace WorkstationControlServer.Sensors
{
    public class TrafficMonitor
    {
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastCheck;

        public TrafficMonitor()
        {
            _lastCheck = DateTime.Now;
            UpdateStats(out double temp1, out double temp2);
        }

        public void UpdateStats(out double downloadMBps, out double uploadMBps)
        {
            long currentReceived = 0;
            long currentSent = 0;

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                NetworkInterface ni = interfaces[i];
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    if (ni.Supports(NetworkInterfaceComponent.IPv4))
                    {
                        try
                        {
                            IPv4InterfaceStatistics stats = ni.GetIPv4Statistics();
                            currentReceived += stats.BytesReceived;
                            currentSent += stats.BytesSent;
                        }
                        catch
                        {
                            // Ігноруємо адаптери, які не дають статистику IPv4
                        }
                    }
                }
            }

            DateTime now = DateTime.Now;
            double seconds = (now - _lastCheck).TotalSeconds;
            if (seconds <= 0) seconds = 1;

            double bytesReceivedPerSec = (currentReceived - _lastBytesReceived) / seconds;
            double bytesSentPerSec = (currentSent - _lastBytesSent) / seconds;

            downloadMBps = bytesReceivedPerSec / 1024.0 / 1024.0;
            uploadMBps = bytesSentPerSec / 1024.0 / 1024.0;

            if (downloadMBps < 0) downloadMBps = 0;
            if (uploadMBps < 0) uploadMBps = 0;

            _lastBytesReceived = currentReceived;
            _lastBytesSent = currentSent;
            _lastCheck = now;
        }
    }
}