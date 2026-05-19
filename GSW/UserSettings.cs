//<summary>[DO NOT REMOVE]UserSettings.cs</summary>


using System.Collections.Generic;

namespace GSWEngine
{
    public class UserSettings
    {
        // Initializing the list here is critical to avoid startup crashes
        public List<StoreTracker> Trackers { get; set; } = new List<StoreTracker>();

        public int ThemeIndex { get; set; } = 0;
        public bool TopMost { get; set; } = false;
        public int GracePeriod { get; set; } = 90;

        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;

        public int TweakerX { get; set; } = -1;
        public int TweakerY { get; set; } = -1;

        // NEW: The Intel Form Memory
        public int IntelX { get; set; } = -1;
        public int IntelY { get; set; } = -1;

        // NEW: The Log Form Memory
        public int LogX { get; set; } = -1;
        public int LogY { get; set; } = -1;

        // NEW: The Historical Ledger for Theme usage
        public Dictionary<string, int> ThemeUsage { get; set; } = new Dictionary<string, int>();

        public bool CloseUnnecessaryStoresOnLaunch { get; set; } = true;
    }
}