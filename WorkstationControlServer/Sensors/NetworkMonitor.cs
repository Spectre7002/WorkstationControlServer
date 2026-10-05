using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace WorkstationControlServer.Sensors
{
    public delegate void NetworkStatsUpdatedHandler(string appName, string ip, int ping, string country);

    public class NetworkMonitor : IDisposable
    {
        public event NetworkStatsUpdatedHandler NetworkStatsUpdated;

        private Thread _monitorThread;
        private bool _isRunning;
        private List<string> _watchedProcesses;
        private Dictionary<string, string> _countryCache;

        public NetworkMonitor()
        {
            _watchedProcesses = new List<string>();

            // База популярних ігор (включаючи тактичні шутери, гонки, RPG та хорори)
            _watchedProcesses.Add("cs2");
            _watchedProcesses.Add("valorant");
            _watchedProcesses.Add("trackmania");
            _watchedProcesses.Add("rocketleague");
            _watchedProcesses.Add("snowrunner");
            _watchedProcesses.Add("phasmophobia");
            _watchedProcesses.Add("doom");
            _watchedProcesses.Add("wutheringwaves");
            _watchedProcesses.Add("cod");
            _watchedProcesses.Add("modernwarfare");
            _watchedProcesses.Add("dota2");
            _watchedProcesses.Add("leagueoflegends");
            _watchedProcesses.Add("apex");
            _watchedProcesses.Add("overwatch");
            _watchedProcesses.Add("rainbowsix");
            _watchedProcesses.Add("gta5");
            _watchedProcesses.Add("fortnite");
            _watchedProcesses.Add("pubg");
            _watchedProcesses.Add("rust");
            _watchedProcesses.Add("minecraft");
            _watchedProcesses.Add("roblox");
            _watchedProcesses.Add("genshinimpact");
            _watchedProcesses.Add("warframe");
            _watchedProcesses.Add("destiny2");
            _watchedProcesses.Add("helldivers2");
            _watchedProcesses.Add("worldoftanks");

            // База популярних програм та месенджерів
            _watchedProcesses.Add("discord");
            _watchedProcesses.Add("telegram");
            _watchedProcesses.Add("chrome");
            _watchedProcesses.Add("firefox");
            _watchedProcesses.Add("edge");
            _watchedProcesses.Add("opera");
            _watchedProcesses.Add("brave");
            _watchedProcesses.Add("skype");
            _watchedProcesses.Add("zoom");
            _watchedProcesses.Add("teams");
            _watchedProcesses.Add("spotify");
            _watchedProcesses.Add("steam");
            _watchedProcesses.Add("epicgames");
            _watchedProcesses.Add("battlenet");

            _countryCache = new Dictionary<string, string>();
        }

        public void Start()
        {
            _isRunning = true;
            _monitorThread = new Thread(new ThreadStart(MonitorLoop));
            _monitorThread.IsBackground = true;
            _monitorThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void MonitorLoop()
        {
            while (_isRunning)
            {
                try
                {
                    CheckConnections();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Помилка мережевого монітора: " + ex.Message);
                }

                Thread.Sleep(4000);
            }
        }

        private void CheckConnections()
        {
            Process p = new Process();
            p.StartInfo.FileName = "netstat.exe";
            p.StartInfo.Arguments = "-n -o";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool foundConnection = false;

            foreach (string line in lines)
            {
                string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    string protocol = parts[0];
                    string foreignAddress = parts[2];
                    string pidStr = "";

                    if (protocol == "TCP" && parts.Length >= 5)
                    {
                        string state = parts[3];
                        pidStr = parts[4];
                        if (state != "ESTABLISHED") continue;
                    }
                    else
                    {
                        continue;
                    }

                    if (foreignAddress.StartsWith("127.") || foreignAddress.StartsWith("192.168.") || foreignAddress.StartsWith("0.0.0.0") || foreignAddress.StartsWith("[") || foreignAddress.StartsWith("*"))
                    {
                        continue;
                    }

                    int pid;
                    if (int.TryParse(pidStr, out pid))
                    {
                        Process proc = null;
                        try
                        {
                            proc = Process.GetProcessById(pid);
                        }
                        catch { continue; }

                        if (proc != null)
                        {
                            string procName = proc.ProcessName.ToLower();
                            bool isWatched = false;

                            foreach (string wp in _watchedProcesses)
                            {
                                if (procName.Contains(wp))
                                {
                                    isWatched = true;
                                    break;
                                }
                            }

                            if (isWatched)
                            {
                                int portIndex = foreignAddress.LastIndexOf(':');
                                string ip = foreignAddress;
                                if (portIndex > 0)
                                {
                                    ip = foreignAddress.Substring(0, portIndex);
                                }

                                int ping = GetPing(ip);
                                string country = GetCountry(ip);

                                if (NetworkStatsUpdated != null)
                                {
                                    NetworkStatsUpdated(proc.ProcessName, ip, ping, country);
                                }
                                foundConnection = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (!foundConnection && NetworkStatsUpdated != null)
            {
                NetworkStatsUpdated("Немає активних програм", "-", 0, "-");
            }
        }

        private int GetPing(string ipAddress)
        {
            try
            {
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(ipAddress, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    return (int)reply.RoundtripTime;
                }
            }
            catch { }
            return -1;
        }

        private string GetCountry(string ipAddress)
        {
            if (_countryCache.ContainsKey(ipAddress))
            {
                return _countryCache[ipAddress];
            }

            try
            {
                using (WebClient client = new WebClient())
                {
                    string country = client.DownloadString("http://ip-api.com/line/" + ipAddress + "?fields=country").Trim();
                    if (!string.IsNullOrEmpty(country) && country != "fail")
                    {
                        _countryCache.Add(ipAddress, country);
                        return country;
                    }
                }
            }
            catch { }

            return "Unknown";
        }

        public void Dispose()
        {
            Stop();
        }
    }
}