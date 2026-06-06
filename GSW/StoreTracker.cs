//<summary>[DO NOT REMOVE]StoreTracker.cs</summary>

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GSWEngine
{
    public class StoreTracker
    {
        public int DisplayIndex { get; set; } = 0;
        // --- DATA & STATE ---
        [JsonIgnore]
        public int GameActiveGraceTicks { get; set; } = 0;
        [JsonIgnore]
        public int ActiveGamePid { get; set; } = 0;
        [JsonIgnore]
        public List<int> LauncherPids { get; set; } = new List<int>();
        [JsonIgnore]
        public string PendingStatus { get; set; }
        [JsonIgnore]
        public bool IsGallowsCompatible { get; set; } = false;
        public DateTime LastLaunchTime { get; set; } = DateTime.MinValue; // The Guard
        [JsonIgnore]
        public int PendingTicks { get; set; } = 0;
        public string DisplayName { get; set; } = "";
        public string ProcessName { get; set; } = "";

        private string _currentStatus = "OFFLINE";
        [JsonIgnore]
        public string CurrentStatus
        {
            get => _currentStatus;
            set
            {
                _currentStatus = value;
                if (_currentStatus == "OFFLINE")
                {
                    CpuUsage = 0;
                    DeltaDiskBytes = 0;
                    TimeLeft = 0; // Reset the "Death Row" timer
                }
            }
        }
        public int KillCount { get; set; }
        public int LifetimeKills { get; set; }
        public long TotalMemoryReclaimed { get; set; }
        [JsonIgnore]
        public string FormattedMemoryReclaimed
        {
            get
            {
                if (TotalMemoryReclaimed > 1073741824) return $"{TotalMemoryReclaimed / 1073741824.0:F2} GB";
                return $"{TotalMemoryReclaimed / 1048576.0:F0} MB";
            }
        }
        [JsonIgnore]
        public int SessionKills { get; set; }
        public int LaunchCount { get; set; }
        public Dictionary<string, int> DetectedGames { get; set; } = new Dictionary<string, int>();
        public int MaxTime { get; set; }
        public int TimeLeft { get; set; }
        public bool ManualOverride { get; set; }
        [JsonIgnore]
        public double CpuUsage { get; set; }
        [JsonIgnore]
        public int ProbationTicks { get; set; } = 0;
        [JsonIgnore]
        public int BusyLabelTimer { get; set; } = 0;
        public bool IsGhost { get; set; } = false;
        [JsonIgnore]
        public long LastDiskBytes { get; set; } = 0;
        [JsonIgnore]
        public long DeltaDiskBytes { get; set; } = 0;
        [JsonIgnore]
        public bool IsPrimaryAppRunning { get; set; } = false;
        [JsonIgnore]
        public long AccumulatedDiskBytes { get; set; } = 0;
        [JsonIgnore]
        public int AccumulationTicks { get; set; } = 0;

        // --- THE DIPLOMATIC TREATY FLAG ---
        // This is what the StateEngine was screaming for.
        [JsonIgnore]
        public bool IsVassal =>
            DisplayName == "Battle.net" ||
            DisplayName == "Ubisoft" ||
            DisplayName == "EA Desktop";

        // --- DISPLAY LOGIC ---
        [JsonIgnore]
        public string CpuDisplay
        {
            get
            {
                if (CurrentStatus == "OFFLINE") return "-";
                return $"{CpuUsage:N1}%";
            }
        }

        [JsonIgnore]
        public string DiskDisplay
        {
            get
            {
                if (CurrentStatus == "OFFLINE") return "-";

                double val = DeltaDiskBytes;
                string unit = "B";

                if (val >= 1048576) { val /= 1048576.0; unit = "MB"; }
                else if (val >= 1024) { val /= 1024.0; unit = "KB"; }

                string fmt = "F0";
                if (val < 10) fmt = "F2";
                else if (val < 100) fmt = "F1";

                return $"{val.ToString(fmt)} {unit}";
            }
        }

        [JsonIgnore]
        public string GraceDisplay
        {
            get
            {
                // 1. If it's not minimized, or if 'Keep Open' is on, hide the timer.
                if (CurrentStatus != "MIN" && CurrentStatus != "MIN BUSY" || ManualOverride)
                    return "";

                // 2. Show the timer immediately (No delay)
                if (TimeLeft >= 0)
                {
                    TimeSpan t = TimeSpan.FromSeconds(TimeLeft);
                    return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
                }

                return "";
            }
        }

        // --- CONSTRUCTORS ---
        public StoreTracker() { }

        public StoreTracker(string d, string p, int max)
        {
            DisplayName = d;
            ProcessName = p;
            MaxTime = max;
            TimeLeft = max;
            CurrentStatus = "OFFLINE";
        }
    }
}