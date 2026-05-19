//<summary>[DO NOT REMOVE]ActivityMonitor.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public static class ActivityMonitor
    {
        private static Dictionary<int, TimeSpan> _cpuHistory = new Dictionary<int, TimeSpan>();
        private static Dictionary<int, long> _ioHistory = new Dictionary<int, long>();
        private static long _lastTimestamp = Stopwatch.GetTimestamp();

        [StructLayout(LayoutKind.Sequential)]
        public struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

        /// <summary>
        /// NEW: Returns exactly how many real-world seconds passed since this was last called.
        /// </summary>
        public static double GetElapsedSeconds()
        {
            long now = Stopwatch.GetTimestamp();
            double elapsed = (double)(now - _lastTimestamp) / Stopwatch.Frequency;
            _lastTimestamp = now;
            return elapsed;
        }

        public static double GetTotalCpuUsage(List<int> familyPids, double elapsedMs, Dictionary<int, Process> cache)
        {
            if (familyPids == null || familyPids.Count == 0 || elapsedMs < 10) return 0.0;

            double totalActiveMs = 0;
            foreach (int pid in familyPids)
            {
                try
                {
                    if (cache.TryGetValue(pid, out Process p))
                    {
                        bool hasExited;
                        TimeSpan currentTicks;
                        try
                        {
                            hasExited = p.HasExited;
                            if (hasExited) continue;
                            currentTicks = p.TotalProcessorTime;
                        }
                        catch (ArgumentException) { continue; }
                        catch (InvalidOperationException) { continue; }

                        if (_cpuHistory.TryGetValue(pid, out TimeSpan previousTicks))
                        {
                            totalActiveMs += (currentTicks - previousTicks).TotalMilliseconds;
                        }
                        _cpuHistory[pid] = currentTicks;
                    }
                }
                catch { continue; }
            }

            double usage = (totalActiveMs / (Environment.ProcessorCount * elapsedMs)) * 100;

            // Clean up history if it gets bloated
            if (_cpuHistory.Count > 200)
            {
                var keysToRemove = _cpuHistory.Keys.Where(k => !cache.ContainsKey(k)).ToList();
                foreach (var k in keysToRemove) _cpuHistory.Remove(k);
            }

            return Math.Min(100.0, Math.Max(0.0, usage));
        }

        public static long GetTotalDiskBytes(List<int> familyPids, Dictionary<int, Process> cache)
        {
            if (familyPids == null || familyPids.Count == 0) return 0;

            long totalDelta = 0;
            foreach (int pid in familyPids)
            {
                try
                {
                    if (cache.TryGetValue(pid, out Process p))
                    {
                        bool hasExited;
                        IntPtr handle;
                        try
                        {
                            hasExited = p.HasExited;
                            if (hasExited) continue;
                            handle = p.Handle;
                        }
                        catch (ArgumentException) { continue; }
                        catch (InvalidOperationException) { continue; }

                        if (GetProcessIoCounters(handle, out IO_COUNTERS counters))
                        {
                            long currentBytes = (long)(counters.ReadTransferCount + counters.WriteTransferCount);
                            if (_ioHistory.TryGetValue(pid, out long previousBytes))
                            {
                                long delta = currentBytes - previousBytes;
                                if (delta > 0) totalDelta += delta;
                            }
                            _ioHistory[pid] = currentBytes;
                        }
                    }
                }
                catch { continue; }
            }

            // Clean up history if it gets bloated
            if (_ioHistory.Count > 200)
            {
                var keysToRemove = _ioHistory.Keys.Where(k => !cache.ContainsKey(k)).ToList();
                foreach (var k in keysToRemove) _ioHistory.Remove(k);
            }

            return totalDelta;
        }
    }
}