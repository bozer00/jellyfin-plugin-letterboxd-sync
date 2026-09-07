using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Entities;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace LetterboxdWatchedSync.ScheduledTasks
{
    public class LetterboxdWatchedSyncTask : IScheduledTask
    {
        private readonly ILogger<LetterboxdWatchedSyncTask> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly HttpClient _httpClient;
        private static readonly SemaphoreSlim _runLock = new SemaphoreSlim(1, 1);

        public LetterboxdWatchedSyncTask(
            ILogger<LetterboxdWatchedSyncTask> logger,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager,
            HttpClient? httpClient = null)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _httpClient = httpClient ?? CreateHttpClient();
        }

        public string Name => "Sync Letterboxd Watched History";

        public string Category => "Letterboxd Sync";

        public string Description => "Marks movies as played in Jellyfin based on a public Letterboxd watched history.";

        public string Key => "LetterboxdWatchedSyncTask";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var configuredInterval = Plugin.Instance?.Configuration.SyncIntervalHours ?? 24;
            var intervalHours = Plugin.NormalizeSyncIntervalHours(configuredInterval);

            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!await _runLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("A Letterboxd watched-history sync is already running. Skipping overlapping run.");
                progress.Report(100);
                return;
            }

            try
            {
                await ExecuteCoreAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _runLock.Release();
            }
        }

        private async Task ExecuteCoreAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.LetterboxdUsername))
            {
                _logger.LogWarning("Letterboxd username is not configured. Skipping task.");
                progress.Report(100);
                return;
            }

            var username = config.LetterboxdUsername.Trim();

            _logger.LogInformation("Starting Letterboxd watched history sync for user {Username}", username);

            User? targetUser = null;
            if (!string.IsNullOrEmpty(config.JellyfinUserId) && Guid.TryParse(config.JellyfinUserId, out var configUserId))
            {
                targetUser = _userManager.GetUserById(configUserId);
            }

            if (targetUser == null)
            {
                _logger.LogWarning("No configured Jellyfin user found. Skipping sync run entirely.");
                progress.Report(100);
                return;
            }

            _logger.LogInformation("Syncing to Jellyfin user: {Username}", targetUser.Username);

            var fetchResult = await FetchWatchedFilmsAsync(username, cancellationToken).ConfigureAwait(false);
            if (!fetchResult.Completed)
            {
                _logger.LogError("Letterboxd watched-history retrieval did not complete. Played states will not be changed.");
                progress.Report(100);
                return;
            }

            var films = fetchResult.Films;
            if (films.Count == 0)
            {
                _logger.LogInformation("No films found in Letterboxd watched history for {Username} (or list is private/unavailable).", username);
                progress.Report(100);
                return;
            }

            _logger.LogInformation("Found {Count} films in Letterboxd watched history.", films.Count);

            // Fetch all movies in the library once to optimize matching performance
            var allMoviesQuery = new InternalItemsQuery(targetUser)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                Recursive = true
            };
            var allMovies = _libraryManager.GetItemList(allMoviesQuery);

            var cache = LoadCache();
            bool cacheModified = false;

            var matchedMovieIds = new List<Guid>();
            var matchedMovies = new List<BaseItem>();
            var unmatchedFilms = new List<UnmatchedFilmInfo>();
            int processed = 0;
            int markedCount = 0;
            bool detailFetchIncomplete = false;

            foreach (var film in films)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!cache.TryGetValue(film.Slug, out var cacheItem) || !IsCacheItemUsable(cacheItem, DateTime.UtcNow))
                {
                    // Delay to respect rate limits
                    await Task.Delay(1000, cancellationToken);

                    var externalIds = await FetchMovieExternalIdsAsync(film.Slug, cancellationToken);
                    if (!externalIds.Completed)
                    {
                        _logger.LogWarning("Letterboxd detail retrieval for '{Title}' did not complete. Played states will not be changed.", film.Title);
                        detailFetchIncomplete = true;
                        break;
                    }

                    cacheItem = new LetterboxdCacheItem
                    {
                        Slug = film.Slug,
                        Title = film.Title,
                        Year = film.Year,
                        TmdbId = externalIds.TmdbId,
                        ImdbId = externalIds.ImdbId,
                        CachedAt = DateTime.UtcNow,
                        ExpiresAt = GetCacheExpiry(externalIds.TmdbId, externalIds.ImdbId, DateTime.UtcNow)
                    };
                    cache[film.Slug] = cacheItem;
                    cacheModified = true;
                }

                var matchedMovie = FindMovieInLibrary(film.Title, film.Year, cacheItem.TmdbId, cacheItem.ImdbId, allMovies);
                if (matchedMovie != null)
                {
                    matchedMovieIds.Add(matchedMovie.Id);
                    matchedMovies.Add(matchedMovie);
                    _logger.LogDebug("Matched: '{Title}' ({Year}) -> Jellyfin ID {Id}", film.Title, film.Year, matchedMovie.Id);
                }
                else
                {
                    unmatchedFilms.Add(new UnmatchedFilmInfo
                    {
                        Title = film.Title,
                        Year = film.Year,
                        Slug = film.Slug
                    });
                    _logger.LogWarning("Not matched: '{Title}' ({Year}) not found in Jellyfin.", film.Title, film.Year);
                }

                processed++;
                progress.Report(10 + 90 * ((double)processed / films.Count));
            }

            if (detailFetchIncomplete)
            {
                _logger.LogError("Letterboxd detail retrieval did not complete. Played states will not be changed.");
                progress.Report(100);
                return;
            }

            if (cacheModified)
            {
                SaveCache(cache);
            }

            foreach (var matchedMovie in matchedMovies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userData = _userDataManager.GetUserData(targetUser, matchedMovie);
                if (userData != null && !userData.Played)
                {
                    userData.Played = true;
                    userData.PlayCount = Math.Max(userData.PlayCount, 1);
                    userData.LastPlayedDate = DateTime.UtcNow;
                    _userDataManager.SaveUserData(targetUser, matchedMovie, userData, UserDataSaveReason.TogglePlayed, cancellationToken);
                    markedCount++;
                    _logger.LogInformation("Marked '{Title}' as Played for user {User}", matchedMovie.Name, targetUser.Username);
                }
            }

            // Cap unmatched films at 200 to prevent large XML config size
            var unmatchedToStore = unmatchedFilms.Take(200).ToList();

            // Update configuration stats
            if (Plugin.Instance != null)
            {
                var configToUpdate = Plugin.Instance.Configuration;
                configToUpdate.LastSyncTotalCount = films.Count;
                configToUpdate.LastSyncMatchedCount = matchedMovieIds.Count;
                configToUpdate.LastSyncMarkedCount = markedCount;
                configToUpdate.LastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                configToUpdate.LastSyncUnmatchedFilmsJson = JsonSerializer.Serialize(unmatchedToStore);
                Plugin.Instance.SaveConfiguration();
            }

            _logger.LogInformation("Successfully matched {MatchedCount} out of {TotalCount} films. Newly marked {MarkedCount} as played.", matchedMovieIds.Count, films.Count, markedCount);

            progress.Report(100);
            _logger.LogInformation("Letterboxd watched history sync completed.");
        }

        private async Task<LetterboxdFetchResult> FetchWatchedFilmsAsync(string username, CancellationToken cancellationToken)
        {
            var films = new List<LetterboxdFilm>();
            int page = 1;
            bool hasMore = true;

            while (hasMore)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = $"https://letterboxd.com/{username}/films/page/{page}/";
                _logger.LogInformation("Fetching page {Page} from {Url}", page, url);

                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return LetterboxdFetchResult.Complete(films);
                    }
                    response.EnsureSuccessStatusCode();

                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    var pageFilms = ParseWatchedFilmsHtml(html);
                    if (pageFilms.Count == 0)
                    {
                        _logger.LogWarning("Letterboxd watched-history page {Page} contained no films; treating retrieval as incomplete to protect played states.", page);
                        return LetterboxdFetchResult.Incomplete(films);
                    }

                    films.AddRange(pageFilms);

                    hasMore = HasNextPage(html);
                    if (!hasMore)
                    {
                        if (await ConfirmNoNextPageAsync(username, page + 1, cancellationToken).ConfigureAwait(false))
                        {
                            return LetterboxdFetchResult.Complete(films);
                        }

                        _logger.LogWarning("Could not positively confirm that watched-history page {Page} is the final page. Played states will not be changed.", page);
                        return LetterboxdFetchResult.Incomplete(films);
                    }

                    page++;

                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching Letterboxd watched films page {Page} for {Username}", page, username);
                    return LetterboxdFetchResult.Incomplete(films);
                }
            }

            return LetterboxdFetchResult.Complete(films);
        }

        private async Task<bool> ConfirmNoNextPageAsync(string username, int page, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync($"https://letterboxd.com/{username}/films/page/{page}/", cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return true;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return IsConfirmedEmptyTerminalPage(response.StatusCode, ParseWatchedFilmsHtml(html).Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not confirm whether Letterboxd watched-history page {Page} exists.", page);
                return false;
            }
        }

        internal static bool HasNextPage(string html)
        {
            return Regex.Matches(html, @"<a\b[^>]*>", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Any(match =>
                    Regex.IsMatch(match.Value, @"\bclass=""[^""]*\bnext\b[^""]*""", RegexOptions.IgnoreCase)
                    && Regex.IsMatch(match.Value, @"\bhref=""[^""]+""", RegexOptions.IgnoreCase));
        }

        internal static bool IsConfirmedEmptyTerminalPage(System.Net.HttpStatusCode statusCode, int filmCount)
        {
            return (int)statusCode is >= 200 and < 300 && filmCount == 0;
        }

        internal List<LetterboxdFilm> ParseWatchedFilmsHtml(string html)
        {
            var list = new List<LetterboxdFilm>();

            // Match each poster component block
            var blocks = Regex.Matches(html, @"<div[^>]*class=""react-component""[^>]*data-component-class=""LazyPoster""[^>]*>", RegexOptions.IgnoreCase);
            
            foreach (Match blockMatch in blocks)
            {
                var block = blockMatch.Value;
                var slugMatch = Regex.Match(block, @"data-item-slug=""([^""]+)""", RegexOptions.IgnoreCase);
                var nameMatch = Regex.Match(block, @"data-item-name=""([^""]+)""", RegexOptions.IgnoreCase);

                if (slugMatch.Success && nameMatch.Success)
                {
                    var slug = slugMatch.Groups[1].Value;
                    var fullName = System.Net.WebUtility.HtmlDecode(nameMatch.Groups[1].Value);
                    
                    var title = fullName;
                    int? year = null;

                    // Extract year from title if it ends in (YYYY)
                    var yearMatch = Regex.Match(fullName, @"\s*\((\d{4})\)$");
                    if (yearMatch.Success)
                    {
                        if (int.TryParse(yearMatch.Groups[1].Value, out var parsedYear))
                        {
                            year = parsedYear;
                        }
                        // Strip the year from the title for cleaner matching
                        title = fullName.Substring(0, yearMatch.Index).Trim();
                    }

                    list.Add(new LetterboxdFilm
                    {
                        Slug = slug,
                        Title = title,
                        Year = year
                    });
                }
            }

            // Fallback to legacy class-based matching if no react components found
            if (list.Count == 0)
            {
                var legacyMatches = Regex.Matches(html, @"class=""[^""]*film-poster[^""]*""[^>]*data-film-slug=""([^""]+)""[^>]*>.*?alt=""([^""]+)""", RegexOptions.Singleline);
                foreach (Match match in legacyMatches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var slug = match.Groups[1].Value;
                        var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);
                        int? year = null;
                        var yearMatch = Regex.Match(slug, @"-(\d{4})$");
                        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var parsedYear))
                        {
                            year = parsedYear;
                        }

                        list.Add(new LetterboxdFilm
                        {
                            Slug = slug,
                            Title = title,
                            Year = year
                        });
                    }
                }
            }

            return list;
        }

        internal BaseItem? FindMovieInLibrary(string title, int? year, string tmdbId, string imdbId, IReadOnlyList<BaseItem> allMovies)
        {
            // 1. Try TMDb ID
            if (!string.IsNullOrEmpty(tmdbId))
            {
                var match = allMovies.FirstOrDefault(m => 
                    m.ProviderIds != null &&
                    m.ProviderIds.TryGetValue("Tmdb", out var id) && 
                    string.Equals(id, tmdbId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 2. Try IMDb ID
            if (!string.IsNullOrEmpty(imdbId))
            {
                var match = allMovies.FirstOrDefault(m => 
                    m.ProviderIds != null &&
                    m.ProviderIds.TryGetValue("Imdb", out var id) && 
                    string.Equals(id, imdbId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            // 3. Fallback to Fuzzy/Title matching
            var matches = allMovies.Where(m => 
                string.Equals(m.Name, title, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.OriginalTitle, title, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            if (matches.Count == 0)
            {
                var normalizedTitle = NormalizeTitle(title);
                matches = allMovies.Where(m => 
                    string.Equals(NormalizeTitle(m.Name ?? string.Empty), normalizedTitle, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeTitle(m.OriginalTitle ?? string.Empty), normalizedTitle, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            if (!year.HasValue)
            {
                return null;
            }

            var yearMatches = matches.Where(m => m.ProductionYear == year.Value).ToList();
            return yearMatches.Count == 1 ? yearMatches[0] : null;
        }

        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return title;
            return Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]", "");
        }

        private Dictionary<string, LetterboxdCacheItem> LoadCache()
        {
            var cacheFile = GetCacheFilePath();
            if (File.Exists(cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(cacheFile);
                    var list = JsonSerializer.Deserialize<List<LetterboxdCacheItem>>(json);
                    if (list != null)
                    {
                        return list.ToDictionary(item => item.Slug, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load Letterboxd cache.");
                }
            }
            return new Dictionary<string, LetterboxdCacheItem>(StringComparer.OrdinalIgnoreCase);
        }

        private void SaveCache(Dictionary<string, LetterboxdCacheItem> cache)
        {
            var cacheFile = GetCacheFilePath();
            var temporaryCacheFile = cacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(cache.Values.ToList(), options);
                File.WriteAllText(temporaryCacheFile, json);
                File.Move(temporaryCacheFile, cacheFile, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Letterboxd cache.");
                try
                {
                    if (File.Exists(temporaryCacheFile))
                    {
                        File.Delete(temporaryCacheFile);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up temporary Letterboxd cache file.");
                }
            }
        }

        private string GetCacheFilePath()
        {
            var configPath = Plugin.Instance?.ConfigurationFilePath;
            if (string.IsNullOrEmpty(configPath))
            {
                return Path.Combine(Path.GetTempPath(), "LetterboxdWatchedCache.json");
            }
            return Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "LetterboxdWatchedCache.json");
        }

        internal async Task<ExternalIdFetchResult> FetchMovieExternalIdsAsync(string slug, CancellationToken cancellationToken)
        {
            var url = $"https://letterboxd.com/film/{slug}/";
            _logger.LogInformation("Fetching film details from {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return ExternalIdFetchResult.Complete(string.Empty, string.Empty);
                }
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var (tmdbId, imdbId) = ParseExternalIdsFromHtml(html);
                return ExternalIdFetchResult.Complete(tmdbId, imdbId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching external IDs for film slug {Slug}", slug);
                return ExternalIdFetchResult.Incomplete();
            }
        }

        internal static bool IsCacheItemUsable(LetterboxdCacheItem cacheItem, DateTime utcNow)
        {
            if (cacheItem.ExpiresAt > utcNow)
            {
                return true;
            }

            return cacheItem.ExpiresAt == default
                && (!string.IsNullOrWhiteSpace(cacheItem.TmdbId) || !string.IsNullOrWhiteSpace(cacheItem.ImdbId));
        }

        private static DateTime GetCacheExpiry(string tmdbId, string imdbId, DateTime utcNow)
        {
            return string.IsNullOrWhiteSpace(tmdbId) && string.IsNullOrWhiteSpace(imdbId)
                ? utcNow.AddHours(6)
                : utcNow.AddDays(30);
        }

        private static HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return httpClient;
        }

        internal static (string TmdbId, string ImdbId) ParseExternalIdsFromHtml(string html)
        {
            string tmdbId = string.Empty;
            string imdbId = string.Empty;

            var tmdbMatch = Regex.Match(html, @"themoviedb\.org/movie/(\d+)", RegexOptions.IgnoreCase);
            if (tmdbMatch.Success)
            {
                tmdbId = tmdbMatch.Groups[1].Value;
            }
            else
            {
                var tmdbMatch2 = Regex.Match(html, @"data-tmdb-id=""(\d+)""", RegexOptions.IgnoreCase);
                if (tmdbMatch2.Success)
                {
                    tmdbId = tmdbMatch2.Groups[1].Value;
                }
            }

            var imdbMatch = Regex.Match(html, @"imdb\.com/title/(tt\d+)", RegexOptions.IgnoreCase);
            if (imdbMatch.Success)
            {
                imdbId = imdbMatch.Groups[1].Value;
            }

            return (tmdbId, imdbId);
        }
    }

    public class LetterboxdFilm
    {
        public string Slug { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public int? Year { get; set; }
    }

    public class LetterboxdCacheItem
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string TmdbId { get; set; } = string.Empty;
        public string ImdbId { get; set; } = string.Empty;
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    internal sealed class ExternalIdFetchResult
    {
        private ExternalIdFetchResult(string tmdbId, string imdbId, bool completed)
        {
            TmdbId = tmdbId;
            ImdbId = imdbId;
            Completed = completed;
        }

        public string TmdbId { get; }
        public string ImdbId { get; }
        public bool Completed { get; }

        public static ExternalIdFetchResult Complete(string tmdbId, string imdbId) => new(tmdbId, imdbId, true);
        public static ExternalIdFetchResult Incomplete() => new(string.Empty, string.Empty, false);
    }

    public class UnmatchedFilmInfo
    {
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Slug { get; set; } = string.Empty;
    }

    internal sealed class LetterboxdFetchResult
    {
        private LetterboxdFetchResult(List<LetterboxdFilm> films, bool completed)
        {
            Films = films;
            Completed = completed;
        }

        public List<LetterboxdFilm> Films { get; }

        public bool Completed { get; }

        public static LetterboxdFetchResult Complete(List<LetterboxdFilm> films) => new LetterboxdFetchResult(films, true);

        public static LetterboxdFetchResult Incomplete(List<LetterboxdFilm> films) => new LetterboxdFetchResult(films, false);
    }
}
