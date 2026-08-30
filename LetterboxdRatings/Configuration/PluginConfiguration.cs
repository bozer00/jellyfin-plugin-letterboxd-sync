using MediaBrowser.Model.Plugins;

namespace LetterboxdRatings.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string RatingMapping { get; set; } = "Community";

        // Preserve the historical behavior unless an administrator opts into a safer policy.
        public string RatingOverwritePolicy { get; set; } = "Overwrite";

        public bool ClearCacheRequested { get; set; }
    }
}
