using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellybox.Shared;

    public enum MdbListLetterboxdListKind
{
    Watchlist,
    Watched
}

    public sealed class MdbListMovie
{
    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string TmdbId { get; init; } = string.Empty;

    public string ImdbId { get; init; } = string.Empty;
}

    public sealed class MdbListFetchResult
{
    private MdbListFetchResult(IReadOnlyList<MdbListMovie> movies, bool completed, string? error)
    {
        Movies = movies;
        Completed = completed;
        Error = error;
    }

    public IReadOnlyList<MdbListMovie> Movies { get; }

    public bool Completed { get; }

    public string? Error { get; }

    public static MdbListFetchResult Complete(IReadOnlyList<MdbListMovie> movies) => new(movies, true, null);

    public static MdbListFetchResult Incomplete(string error) => new(Array.Empty<MdbListMovie>(), false, error);
}

    public sealed class MdbListClient
{
    private const string ApiBaseUrl = "https://api.mdblist.com";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public MdbListClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<MdbListFetchResult> GetLetterboxdMoviesAsync(
        string apiKey,
        string letterboxdUsername,
        MdbListLetterboxdListKind listKind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return MdbListFetchResult.Incomplete("MDBList API key is not configured.");
        }

        if (!IsValidLetterboxdUsername(letterboxdUsername))
        {
            return MdbListFetchResult.Incomplete("Letterboxd username is invalid.");
        }

        try
        {
            var externalLists = await GetExternalListsAsync(apiKey, cancellationToken).ConfigureAwait(false);
            var expectedUrl = BuildLetterboxdListUrl(letterboxdUsername, listKind);
            var matches = externalLists
                .Where(list => string.Equals(list.Source, "letterboxd", StringComparison.OrdinalIgnoreCase))
                .Where(list => UrlsMatch(list.Url, expectedUrl))
                .ToList();

            if (matches.Count != 1)
            {
                return MdbListFetchResult.Incomplete(matches.Count == 0
                    ? $"No MDBList external list matches {expectedUrl}."
                    : $"More than one MDBList external list matches {expectedUrl}.");
            }

            return await GetMoviesAsync(apiKey, matches[0].Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return MdbListFetchResult.Incomplete($"MDBList request failed: {exception.GetType().Name}.");
        }
    }

    internal static string BuildLetterboxdListUrl(string username, MdbListLetterboxdListKind listKind)
    {
        var path = listKind == MdbListLetterboxdListKind.Watchlist ? "watchlist" : "films";
        return $"https://letterboxd.com/{username.Trim()}/{path}/";
    }

    internal static bool UrlsMatch(string? actual, string expected)
    {
        if (!Uri.TryCreate(actual, UriKind.Absolute, out var actualUri) || !Uri.TryCreate(expected, UriKind.Absolute, out var expectedUri))
        {
            return false;
        }

        return string.Equals(actualUri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actualUri.Host, expectedUri.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(actualUri.AbsolutePath.TrimEnd('/'), expectedUri.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(actualUri.Query)
            && string.IsNullOrEmpty(actualUri.Fragment);
    }

    private async Task<List<MdbListExternalList>> GetExternalListsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/external/lists/user?apikey={Uri.EscapeDataString(apiKey)}");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<List<MdbListExternalList>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("MDBList external-list response was empty.");
    }

    private async Task<MdbListFetchResult> GetMoviesAsync(string apiKey, int externalListId, CancellationToken cancellationToken)
    {
        var movies = new List<MdbListMovie>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;

        do
        {
            var cursorQuery = string.IsNullOrEmpty(cursor) ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}";
            var url = $"{ApiBaseUrl}/external/lists/{externalListId}/items?mediatype=movie&limit=1000&apikey={Uri.EscapeDataString(apiKey)}{cursorQuery}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var page = await JsonSerializer.DeserializeAsync<MdbListItemsPage>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("MDBList item response was empty.");

            if (page.Pagination == null)
            {
                return MdbListFetchResult.Incomplete("MDBList item response did not include pagination metadata.");
            }

            foreach (var movie in page.Movies ?? [])
            {
                movies.Add(new MdbListMovie
                {
                    Title = movie.Title ?? string.Empty,
                    Year = movie.ReleaseYear,
                    TmdbId = movie.Ids?.Tmdb?.ToString() ?? string.Empty,
                    ImdbId = movie.Ids?.Imdb ?? movie.ImdbId ?? string.Empty
                });
            }

            cursor = page.Pagination.NextCursor;
            if (page.Pagination.HasMore && string.IsNullOrEmpty(cursor))
            {
                return MdbListFetchResult.Incomplete("MDBList indicated more pages without a next cursor.");
            }

            if (!string.IsNullOrEmpty(cursor) && !seenCursors.Add(cursor))
            {
                return MdbListFetchResult.Incomplete("MDBList returned a repeated pagination cursor.");
            }
        }
        while (!string.IsNullOrEmpty(cursor));

        return MdbListFetchResult.Complete(movies);
    }

    private static bool IsValidLetterboxdUsername(string username)
    {
        return !string.IsNullOrWhiteSpace(username)
            && username.Trim().Length <= 100
            && username.Trim().All(character => char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private sealed class MdbListExternalList
    {
        public int Id { get; init; }

        public string? Source { get; init; }

        public string? Url { get; init; }
    }

    private sealed class MdbListItemsPage
    {
        public List<MdbListItem>? Movies { get; init; }

        public MdbListPagination? Pagination { get; init; }
    }

    private sealed class MdbListItem
    {
        public string? Title { get; init; }

        [JsonPropertyName("imdb_id")]
        public string? ImdbId { get; init; }

        [JsonPropertyName("release_year")]
        public int? ReleaseYear { get; init; }

        public MdbListIds? Ids { get; init; }
    }

    private sealed class MdbListIds
    {
        public int? Tmdb { get; init; }

        public string? Imdb { get; init; }
    }

    private sealed class MdbListPagination
    {
        [JsonPropertyName("has_more")]
        public bool HasMore { get; init; }

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; init; }
    }
}
