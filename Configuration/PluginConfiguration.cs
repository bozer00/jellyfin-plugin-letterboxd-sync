using MediaBrowser.Model.Plugins;

namespace LetterboxdSync.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string LetterboxdUsername { get; set; } = string.Empty;

        public string JellyfinUserId { get; set; } = string.Empty;

        public string PlaylistName { get; set; } = "Letterboxd Watchlist";

        public string SyncMode { get; set; } = "Append";
    }
}
