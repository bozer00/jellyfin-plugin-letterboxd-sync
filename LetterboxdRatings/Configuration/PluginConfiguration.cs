using MediaBrowser.Model.Plugins;

namespace LetterboxdRatings.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string RatingMapping { get; set; } = "Community";
    }
}
