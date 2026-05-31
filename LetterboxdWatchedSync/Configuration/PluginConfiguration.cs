using MediaBrowser.Model.Plugins;

namespace LetterboxdWatchedSync.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string LetterboxdUsername { get; set; } = string.Empty;

        public string JellyfinUserId { get; set; } = string.Empty;

        public int SyncIntervalHours { get; set; } = 24;

        public int LastSyncTotalCount { get; set; }

        public int LastSyncMatchedCount { get; set; }

        public int LastSyncMarkedCount { get; set; }

        public string LastSyncTime { get; set; } = string.Empty;

        public string LastSyncUnmatchedFilmsJson { get; set; } = "[]";
    }
}
