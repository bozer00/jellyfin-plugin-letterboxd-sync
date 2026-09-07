using System.Net;
using System.Net.Http;
using System.Text;
using Jellybox.Shared;

namespace Jellybox.Tests;

public class MdbListClientTests
{
    [Fact]
    public async Task FetchesAllCursorPagesAndReturnsStableIds()
    {
        var handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;

            if (path == "/external/lists/user")
            {
                return Json("""
                    [{"id":42,"source":"letterboxd","url":"https://letterboxd.com/alice/watchlist/"}]
                    """);
            }

            if (path == "/external/lists/42/items" && !query.Contains("cursor="))
            {
                return Json("""
                    {"movies":[{"title":"First","release_year":2001,"imdb_id":"tt0000001","ids":{"tmdb":1,"imdb":"tt0000001"}}],"pagination":{"has_more":true,"next_cursor":"page-two"}}
                    """);
            }

            if (path == "/external/lists/42/items" && query.Contains("cursor=page-two"))
            {
                return Json("""
                    {"movies":[{"title":"Second","release_year":2002,"ids":{"tmdb":2,"imdb":"tt0000002"}}],"pagination":{"has_more":false,"next_cursor":null}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var client = new MdbListClient(httpClient);

        var result = await client.GetLetterboxdMoviesAsync("test-key", "alice", MdbListLetterboxdListKind.Watchlist, CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Null(result.Error);
        Assert.Collection(result.Movies,
            movie =>
            {
                Assert.Equal("First", movie.Title);
                Assert.Equal(2001, movie.Year);
                Assert.Equal("1", movie.TmdbId);
                Assert.Equal("tt0000001", movie.ImdbId);
            },
            movie =>
            {
                Assert.Equal("Second", movie.Title);
                Assert.Equal("2", movie.TmdbId);
                Assert.Equal("tt0000002", movie.ImdbId);
            });
        Assert.Contains(handler.Requests, request => request.AbsolutePath == "/external/lists/42/items" && request.Query.Contains("cursor=page-two"));
    }

    [Fact]
    public async Task RefusesAnIncompleteCursorResponse()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/external/lists/user" => Json("""
                [{"id":42,"source":"letterboxd","url":"https://letterboxd.com/alice/films/"}]
                """),
            "/external/lists/42/items" => Json("""
                {"movies":[],"pagination":{"has_more":true,"next_cursor":null}}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        using var httpClient = new HttpClient(handler);
        var client = new MdbListClient(httpClient);

        var result = await client.GetLetterboxdMoviesAsync("test-key", "alice", MdbListLetterboxdListKind.Watched, CancellationToken.None);

        Assert.False(result.Completed);
        Assert.Empty(result.Movies);
        Assert.Contains("without a next cursor", result.Error);
    }

    [Fact]
    public async Task RefusesAnItemResponseWithoutPaginationMetadata()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/external/lists/user" => Json("""
                [{"id":42,"source":"letterboxd","url":"https://letterboxd.com/alice/watchlist/"}]
                """),
            "/external/lists/42/items" => Json("""
                {"movies":[{"title":"A movie","release_year":2001,"ids":{"tmdb":1}}]}
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        using var httpClient = new HttpClient(handler);
        var client = new MdbListClient(httpClient);

        var result = await client.GetLetterboxdMoviesAsync("test-key", "alice", MdbListLetterboxdListKind.Watchlist, CancellationToken.None);

        Assert.False(result.Completed);
        Assert.Empty(result.Movies);
        Assert.Contains("pagination metadata", result.Error);
    }

    [Fact]
    public async Task RefusesToUseAListForAnotherLetterboxdProfile()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath == "/external/lists/user"
            ? Json("""
                [{"id":42,"source":"letterboxd","url":"https://letterboxd.com/someone-else/watchlist/"}]
                """)
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        using var httpClient = new HttpClient(handler);
        var client = new MdbListClient(httpClient);

        var result = await client.GetLetterboxdMoviesAsync("test-key", "alice", MdbListLetterboxdListKind.Watchlist, CancellationToken.None);

        Assert.False(result.Completed);
        Assert.Empty(result.Movies);
        Assert.Contains("No MDBList external list matches", result.Error);
        Assert.Single(handler.Requests);
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_handler(request));
        }
    }
}
