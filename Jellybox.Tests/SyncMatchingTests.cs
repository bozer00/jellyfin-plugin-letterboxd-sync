using Xunit;
using LetterboxdSync.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using System;
using System.Collections.Generic;

namespace Jellybox.Tests
{
    public class SyncMatchingTests
    {
        [Fact]
        public void FindMovieInLibrary_MatchesByTmdbIdFirst()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Wrong Title", ProviderIds = new Dictionary<string, string> { { "Tmdb", "584" } } };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProviderIds = new Dictionary<string, string> { { "Tmdb", "999" } } };
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("2 Fast 2 Furious", 2003, "584", "tt0322259", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id); // Matches by TMDb ID even if title is wrong
        }

        [Fact]
        public void FindMovieInLibrary_MatchesByImdbIdFallback()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Correct Title", ProviderIds = new Dictionary<string, string> { { "Imdb", "tt0322259" } } };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "Other Title", ProviderIds = new Dictionary<string, string> { { "Imdb", "tt9999999" } } };
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("Other Title", 2003, "", "tt0322259", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id); // Matches by IMDb ID when TMDb is not provided
        }

        [Fact]
        public void FindMovieInLibrary_MatchesFuzzyTitleAndYear()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProductionYear = 2003 };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProductionYear = 2020 }; // Wrong year
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("2 Fast 2 Furious", 2003, "", "", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id);
        }

        [Fact]
        public void FindMovieInLibrary_MatchesOriginalOrAlternativeTitle()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Spirited Away", OriginalTitle = "Sen to Chihiro no Kamikakushi", ProductionYear = 2001 };
            var movies = new List<BaseItem> { movie1 };

            // Act
            var match = taskInstance.FindMovieInLibrary("Sen to Chihiro no Kamikakushi", 2001, "", "", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id); // Matches original title
        }

        [Fact]
        public void FindMovieInLibrary_MatchesTitleFuzzyNormalization()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            
            // "F.a.s.t. & F-u-r-i-o-u-s" should normalize to "fastfurious" and match "Fast & Furious"
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Fast & Furious", ProductionYear = 2009 };
            var movies = new List<BaseItem> { movie1 };

            // Act
            var match = taskInstance.FindMovieInLibrary("F.a.s.t. & F-u-r-i-o-u-s", 2009, "", "", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id);
        }

        [Fact]
        public void FindMovieInLibrary_RejectsAmbiguousTitleAndYearFallback()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "The Thing", ProductionYear = 1982 };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "The Thing", ProductionYear = 1982 };
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("The Thing", 1982, "", "", movies);

            // Assert
            Assert.Null(match);
        }

        [Fact]
        public void FindMovieInLibrary_RejectsTitleOnlyFallbackWithoutYear()
        {
            // Arrange
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);
            var movie = new Movie { Id = Guid.NewGuid(), Name = "The Thing", ProductionYear = 1982 };
            var movies = new List<BaseItem> { movie };

            // Act
            var match = taskInstance.FindMovieInLibrary("The Thing", null, "", "", movies);

            // Assert
            Assert.Null(match);
        }
    }
}
