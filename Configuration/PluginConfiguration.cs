using MediaBrowser.Model.Plugins;

namespace LetterboxdSync.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string LetterboxdUsername { get; set; } = string.Empty;

        public string JellyfinUserId { get; set; } = string.Empty;

        public string PlaylistName { get; set; } = "Letterboxd Watchlist";

        public string SyncMode { get; set; } = "Append";

        public int SyncIntervalHours { get; set; } = 12;

        public int LastSyncTotalCount { get; set; } = 0;

        public int LastSyncMatchedCount { get; set; } = 0;

        public string LastSyncUnmatchedFilmsJson { get; set; } = string.Empty;

        public string LastSyncTime { get; set; } = string.Empty;
    }
}
