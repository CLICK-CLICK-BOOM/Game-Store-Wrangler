using System;
using System.Threading.Tasks;
using Postgrest.Attributes;
using Postgrest.Models;
using Supabase;

namespace GSWEngine.Installer
{
    [Table("Install_stats")]
    public class InstallEvent : BaseModel
    {
        [Column("version")] public string Version { get; set; }
        [Column("country")] public string Country { get; set; }
        [Column("os_version")] public string OS { get; set; }
    }

    public static class SupabaseManager
    {
        private static Client _client;
        private const string Url = "https://oaoaqltnnplwdncedwcx.supabase.co";
        private const string Key = "sb_publishable_6vzI4WqMlCWa1s6EdvqVwA_gwwaCdgj";

        public static Client Client => _client;

        public static async Task Initialize()
        {
            if (_client != null) return;
            try
            {
                var options = new SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = true };
                _client = new Client(Url, Key, options);
                await _client.InitializeAsync();
            }
            catch { _client = null; } // Failsafe for offline/blocked environments
        }
    }
}