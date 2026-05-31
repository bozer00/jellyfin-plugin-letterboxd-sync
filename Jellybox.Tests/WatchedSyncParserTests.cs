using Xunit;
using LetterboxdWatchedSync.ScheduledTasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellybox.Tests
{
    public class WatchedSyncParserTests
    {
        [Fact]
        public void ParseWatchedFilmsHtml_WithReactLazyPosters_ReturnsCorrectFilms()
        {
            // Arrange
            var html = @"
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""parasite-2019"" data-item-name=""Parasite (2019)""></div>
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""anatomy-of-a-fall-2023"" data-item-name=""Anatomy of a Fall (2023)""></div>
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""la-la-land-2016"" data-item-name=""La La Land (2016)""></div>
            ";
            
            var taskInstance = new LetterboxdWatchedSyncTask(null!, null!, null!, null!);

            // Act
            var results = taskInstance.ParseWatchedFilmsHtml(html);

            // Assert
            Assert.Equal(3, results.Count);
            
            Assert.Equal("parasite-2019", results[0].Slug);
            Assert.Equal("Parasite", results[0].Title);
            Assert.Equal(2019, results[0].Year);

            Assert.Equal("anatomy-of-a-fall-2023", results[1].Slug);
            Assert.Equal("Anatomy of a Fall", results[1].Title);
            Assert.Equal(2023, results[1].Year);

            Assert.Equal("la-la-land-2016", results[2].Slug);
            Assert.Equal("La La Land", results[2].Title);
            Assert.Equal(2016, results[2].Year);
        }

        [Fact]
        public void ParseWatchedFilmsHtml_WithLegacyFilmPosters_ReturnsCorrectFilms()
        {
            // Arrange
            var html = @"
                <ul class=""poster-list"">
                    <li class=""poster-container"">
                        <div class=""really-long-class film-poster"" data-film-slug=""parasite-2019"">
                            <img alt=""Parasite"" />
                        </div>
                    </li>
                    <li class=""poster-container"">
                        <div class=""film-poster"" data-film-slug=""pulp-fiction-1994"">
                            <img alt=""Pulp Fiction"" />
                        </div>
                    </li>
                </ul>
            ";
            var taskInstance = new LetterboxdWatchedSyncTask(null!, null!, null!, null!);

            // Act
            var results = taskInstance.ParseWatchedFilmsHtml(html);

            // Assert
            Assert.Equal(2, results.Count);
            
            Assert.Equal("parasite-2019", results[0].Slug);
            Assert.Equal("Parasite", results[0].Title);
            Assert.Equal(2019, results[0].Year);

            Assert.Equal("pulp-fiction-1994", results[1].Slug);
            Assert.Equal("Pulp Fiction", results[1].Title);
            Assert.Equal(1994, results[1].Year);
        }

        [Fact]
        public void ParseExternalIdsFromHtml_ExtractsTmdbAndImdbIdsCorrectly()
        {
            // Arrange
            var html = @"
                <html>
                    <body>
                        <a href=""https://www.themoviedb.org/movie/584"">TMDb Link</a>
                        <a href=""https://www.imdb.com/title/tt0322259/"">IMDb Link</a>
                    </body>
                </html>
            ";

            // Act
            var (tmdbId, imdbId) = LetterboxdWatchedSyncTask.ParseExternalIdsFromHtml(html);

            // Assert
            Assert.Equal("584", tmdbId);
            Assert.Equal("tt0322259", imdbId);
        }

        [Fact]
        public void FindMovieInLibrary_MatchesByTmdbIdFirst()
        {
            // Arrange
            var taskInstance = new LetterboxdWatchedSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Wrong Title", ProviderIds = new Dictionary<string, string> { { "Tmdb", "584" } } };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProviderIds = new Dictionary<string, string> { { "Tmdb", "999" } } };
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("2 Fast 2 Furious", 2003, "584", "tt0322259", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id);
        }

        [Fact]
        public void FindMovieInLibrary_MatchesByImdbIdFallback()
        {
            // Arrange
            var taskInstance = new LetterboxdWatchedSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "Correct Title", ProviderIds = new Dictionary<string, string> { { "Imdb", "tt0322259" } } };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "Other Title", ProviderIds = new Dictionary<string, string> { { "Imdb", "tt9999999" } } };
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("Other Title", 2003, "", "tt0322259", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id);
        }

        [Fact]
        public void FindMovieInLibrary_MatchesFuzzyTitleAndYear()
        {
            // Arrange
            var taskInstance = new LetterboxdWatchedSyncTask(null!, null!, null!, null!);
            
            var movie1 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProductionYear = 2003 };
            var movie2 = new Movie { Id = Guid.NewGuid(), Name = "2 Fast 2 Furious", ProductionYear = 2020 };
            
            var movies = new List<BaseItem> { movie1, movie2 };

            // Act
            var match = taskInstance.FindMovieInLibrary("2 Fast 2 Furious", 2003, "", "", movies);

            // Assert
            Assert.NotNull(match);
            Assert.Equal(movie1.Id, match.Id);
        }
    }
}
