using Xunit;
using LetterboxdRatings.Providers;
using LetterboxdRatings;
using LetterboxdRatings.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities.Movies;
using System;

namespace Jellybox.Tests
{
    public class RatingsMappingTests : IDisposable
    {
        public RatingsMappingTests()
        {
            // Reset Plugin.Instance before each test
            ResetPluginInstance();
        }

        public void Dispose()
        {
            // Clean up static state after each test
            ResetPluginInstance();
        }

        private void ResetPluginInstance()
        {
            var prop = typeof(Plugin).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            prop?.SetValue(null, null);
        }

        private void SetPluginInstance(string ratingMapping, string overwritePolicy = "Overwrite")
        {
            var plugin = (Plugin)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
            var config = new PluginConfiguration { RatingMapping = ratingMapping, RatingOverwritePolicy = overwritePolicy };
            
            var baseType = typeof(BasePlugin<PluginConfiguration>);
            var configProp = baseType.GetProperty("Configuration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (configProp != null && configProp.CanWrite)
            {
                configProp.SetValue(plugin, config);
            }
            else
            {
                var field = baseType.GetField("<Configuration>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(plugin, config);
            }

            var prop = typeof(Plugin).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            prop?.SetValue(null, plugin);
        }

        [Fact]
        public void ApplyRating_WithCommunityMapping_AppliesScaledRatingToCommunityRating()
        {
            // Arrange
            SetPluginInstance("Community");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie();
            
            // Letterboxd rating = 3.5 out of 5 stars
            float letterboxdRating = 3.5f;

            // Act
            provider.ApplyRating(movie, letterboxdRating);

            // Assert
            // 3.5 * 2 = 7.0 for community rating
            Assert.Equal(7.0f, movie.CommunityRating);
            Assert.Null(movie.CriticRating); // CriticRating should remain unchanged (null)
        }

        [Fact]
        public void ApplyRating_WithCriticMapping_AppliesScaledRatingToCriticRatingOnly()
        {
            // Arrange
            SetPluginInstance("Critic");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie();
            
            // Letterboxd rating = 4.25 out of 5 stars
            float letterboxdRating = 4.25f;

            // Act
            provider.ApplyRating(movie, letterboxdRating);

            // Assert
            // 4.25 * 2 = 8.5 -> 8.5 * 10 = 85.0 for critic rating
            Assert.Equal(85.0f, movie.CriticRating);
            Assert.Null(movie.CommunityRating); // CommunityRating should remain unchanged (null)
        }

        [Fact]
        public void ApplyRating_WithBothMapping_AppliesScaledRatingToBothFields()
        {
            // Arrange
            SetPluginInstance("Both");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie();
            
            // Letterboxd rating = 3.8 out of 5 stars
            float letterboxdRating = 3.8f;

            // Act
            provider.ApplyRating(movie, letterboxdRating);

            // Assert
            // Community: 3.8 * 2 = 7.6
            // Critic: 7.6 * 10 = 76.0
            Assert.Equal(7.6f, movie.CommunityRating);
            Assert.Equal(76.0f, movie.CriticRating);
        }

        [Fact]
        public void ApplyRating_WithNegativeRating_DoesNotApplyRating()
        {
            // Arrange
            SetPluginInstance("Both");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie
            {
                CommunityRating = 5.0f,
                CriticRating = 50.0f
            };

            // Act
            provider.ApplyRating(movie, -1f); // Negative value (e.g. cached lookup failure)

            // Assert
            // Values should remain unchanged
            Assert.Equal(5.0f, movie.CommunityRating);
            Assert.Equal(50.0f, movie.CriticRating);
        }

        [Fact]
        public void ApplyRating_OnlyFillEmpty_PreservesExistingRating()
        {
            SetPluginInstance("Both", "OnlyFillEmpty");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie { CommunityRating = 6.0f };

            provider.ApplyRating(movie, 4.0f);

            Assert.Equal(6.0f, movie.CommunityRating);
            Assert.Equal(80.0f, movie.CriticRating);
        }

        [Fact]
        public void ApplyRating_Disabled_DoesNotModifyMovie()
        {
            SetPluginInstance("Both", "Disabled");
            var provider = new LetterboxdRatingProvider(null!);
            var movie = new Movie { CommunityRating = 6.0f, CriticRating = 60.0f };

            provider.ApplyRating(movie, 4.0f);

            Assert.Equal(6.0f, movie.CommunityRating);
            Assert.Equal(60.0f, movie.CriticRating);
        }
    }
}
