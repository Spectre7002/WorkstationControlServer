using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WorkstationControlServer.ProcessManagement
{
    public class ProcessInfo
    {
        public int ProcessId;
        public string Name;
        public long RamBytes;
    }

    public class TaskManager
    {
        private class ProcessRamComparer : IComparer<Process>
        {
            public int Compare(Process x, Process y)
            {
                long ramX = 0;
                long ramY = 0;
                try { ramX = x.WorkingSet64; } catch { }
                try { ramY = y.WorkingSet64; } catch { }
                return ramY.CompareTo(ramX); // Сортування за спаданням
            }
        }

        public List<ProcessInfo> GetTopProcesses(int count)
        {
            List<ProcessInfo> topProcesses = new List<ProcessInfo>();
            try
            {
                Process[] allProcs = Process.GetProcesses();
                Array.Sort(allProcs, new ProcessRamComparer());

                int added = 0;
                for (int i = 0; i < allProcs.Length; i++)
                {
                    Process p = allProcs[i];
                    if (p.Id == 0 || p.ProcessName.ToLower() == "idle") continue;

                    ProcessInfo info = new ProcessInfo();
                    info.ProcessId = p.Id;
                    info.Name = p.ProcessName;
                    try { info.RamBytes = p.WorkingSet64; } catch { continue; }

                    topProcesses.Add(info);
                    added++;
                    if (added >= count) break;
                }
            }
            catch (Exception ex) { Console.WriteLine("Помилка TaskManager: " + ex.Message); }

            return topProcesses;
        }

        public void KillProcess(int pid)
        {
            try
            {
                Process p = Process.GetProcessById(pid);
                p.Kill();
            }
            catch (Exception ex) { Console.WriteLine("Помилка завершення процесу: " + ex.Message); }
        }
    }
}