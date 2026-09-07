using Xunit;
using LetterboxdSync.ScheduledTasks;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellybox.Tests
{
    public class SyncParserTests
    {
        [Fact]
        public void ParseWatchlistHtml_WithReactLazyPosters_ReturnsCorrectFilms()
        {
            // Arrange
            var html = @"
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""parasite-2019"" data-item-name=""Parasite (2019)""></div>
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""anatomy-of-a-fall-2023"" data-item-name=""Anatomy of a Fall (2023)""></div>
                <div class=""react-component"" data-component-class=""LazyPoster"" data-item-slug=""la-la-land-2016"" data-item-name=""La La Land (2016)""></div>
            ";
            
            // We need a dummy instance of LetterboxdSyncTask. But we can't create one easily because it has constructor dependencies.
            // Wait, does ParseWatchlistHtml require the constructor parameters to run?
            // Let's check: ParseWatchlistHtml in LetterboxdSyncTask.cs does NOT access any private fields of the class!
            // It only accesses Regex and WebUtility. So passing nulls to the constructor is perfectly fine!
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);

            // Act
            var results = taskInstance.ParseWatchlistHtml(html);

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
        public void ParseWatchlistHtml_WithLegacyFilmPosters_ReturnsCorrectFilms()
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
            var taskInstance = new LetterboxdSyncTask(null!, null!, null!, null!);

            // Act
            var results = taskInstance.ParseWatchlistHtml(html);

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
            var (tmdbId, imdbId) = LetterboxdSyncTask.ParseExternalIdsFromHtml(html);

            // Assert
            Assert.Equal("584", tmdbId);
            Assert.Equal("tt0322259", imdbId);
        }

        [Fact]
        public void ParseExternalIdsFromHtml_WithDataAttributeTmdb_ExtractsTmdbCorrectly()
        {
            // Arrange
            var html = @"
                <div data-tmdb-id=""505642""></div>
                <a href=""https://www.imdb.com/title/tt6710474/"">IMDb</a>
            ";

            // Act
            var (tmdbId, imdbId) = LetterboxdSyncTask.ParseExternalIdsFromHtml(html);

            // Assert
            Assert.Equal("505642", tmdbId);
            Assert.Equal("tt6710474", imdbId);
        }

        [Fact]
        public void HasNextPage_RequiresNextLinkWithHref()
        {
            Assert.True(LetterboxdSyncTask.HasNextPage("<a href=\"/user/watchlist/page/2/\" class=\"next\">Next</a>"));
            Assert.False(LetterboxdSyncTask.HasNextPage("<span class=\"next disabled\">Next</span>"));
        }

        [Theory]
        [InlineData(System.Net.HttpStatusCode.OK, 0, true)]
        [InlineData(System.Net.HttpStatusCode.OK, 1, false)]
        [InlineData(System.Net.HttpStatusCode.NotFound, 0, false)]
        [InlineData(System.Net.HttpStatusCode.InternalServerError, 0, false)]
        public void IsConfirmedEmptyTerminalPage_AcceptsOnlySuccessfulEmptyResponses(System.Net.HttpStatusCode statusCode, int filmCount, bool expected)
        {
            Assert.Equal(expected, LetterboxdSyncTask.IsConfirmedEmptyTerminalPage(statusCode, filmCount));
        }

        [Fact]
        public async Task FetchMovieExternalIdsAsync_PropagatesRequestedCancellation()
        {
            using var httpClient = new HttpClient(new CancellationHandler());
            var task = new LetterboxdSyncTask(NullLogger<LetterboxdSyncTask>.Instance, null!, null!, null!, httpClient);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => task.FetchMovieExternalIdsAsync("a-film", cancellation.Token));
        }

        [Fact]
        public async Task FetchMovieExternalIdsAsync_ClassifiesServerFailureAsIncomplete()
        {
            using var httpClient = new HttpClient(new StatusCodeHandler(HttpStatusCode.ServiceUnavailable));
            var task = new LetterboxdSyncTask(NullLogger<LetterboxdSyncTask>.Instance, null!, null!, null!, httpClient);

            var result = await task.FetchMovieExternalIdsAsync("a-film", CancellationToken.None);

            Assert.False(result.Completed);
        }

        [Fact]
        public void IsCacheItemUsable_RetriesLegacyEmptyAndExpiredNegativeEntries()
        {
            var now = DateTime.UtcNow;

            Assert.False(LetterboxdSyncTask.IsCacheItemUsable(new LetterboxdCacheItem(), now));
            Assert.False(LetterboxdSyncTask.IsCacheItemUsable(new LetterboxdCacheItem { ExpiresAt = now.AddMinutes(-1) }, now));
            Assert.True(LetterboxdSyncTask.IsCacheItemUsable(new LetterboxdCacheItem { ExpiresAt = now.AddHours(1) }, now));
            Assert.True(LetterboxdSyncTask.IsCacheItemUsable(new LetterboxdCacheItem { TmdbId = "1" }, now));
        }

        private sealed class CancellationHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }
        }

        private sealed class StatusCodeHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;

            public StatusCodeHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(_statusCode));
            }
        }
    }
}
