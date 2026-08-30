using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace LetterboxdRatings.Providers
{
    /// <summary>
    /// Custom metadata provider that fetches Letterboxd average ratings and applies them
    /// directly to movie items. Uses ICustomMetadataProvider to run AFTER all remote
    /// metadata providers (TMDb, OMDb, etc.) have finished; the administrator controls
    /// whether Letterboxd overwrites, fills, or leaves existing mapped ratings untouched.
    /// </summary>
    public class LetterboxdRatingProvider : ICustomMetadataProvider<Movie>, IHasOrder
    {
        private readonly ILogger<LetterboxdRatingProvider> _logger;
        private readonly HttpClient _httpClient;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly object _cacheLock = new object();
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int ValidRatingCacheDays = 14;
        private const int ConfirmedMissingCacheHours = 24;
        private const int TransientFailureCacheMinutes = 5;

        public LetterboxdRatingProvider(ILogger<LetterboxdRatingProvider> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public string Name => "Letterboxd Ratings";

        public int Order => 10; // Execute after all standard providers

        public async Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            // Extract IDs directly from the movie item
            string? tmdbId = null;
            string? imdbId = null;
            item.ProviderIds?.TryGetValue("Tmdb", out tmdbId);
            item.ProviderIds?.TryGetValue("Imdb", out imdbId);

            if (string.IsNullOrEmpty(tmdbId) && string.IsNullOrEmpty(imdbId))
            {
                _logger.LogDebug("No TMDb or IMDb ID available for Letterboxd ratings lookup of '{Name}'", item.Name);
                return ItemUpdateType.None;
            }

            // Check cache
            var cacheKey = !string.IsNullOrEmpty(tmdbId) ? $"tmdb_{tmdbId}" : $"imdb_{imdbId}";
            var cachedResult = TryGetFromCache(cacheKey);
            if (cachedResult != null)
            {
                if (cachedResult.Outcome != LetterboxdRatingCacheOutcome.Rating)
                {
                    // A short-lived missing or transient result protects Letterboxd without
                    // turning an outage into a two-week negative cache entry.
                    return ItemUpdateType.None;
                }

                _logger.LogDebug("Letterboxd rating cache hit for '{Name}': {Rating}", item.Name, cachedResult.Rating);
                ApplyRating(item, cachedResult.Rating);
                return ItemUpdateType.MetadataDownload;
            }

            // Fetch from Letterboxd with thread-safe rate-limiting
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                // Ensure at least 1.5 seconds between requests to avoid rate limits
                var elapsedSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                var delay = TimeSpan.FromMilliseconds(1500) - elapsedSinceLastRequest;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                _lastRequestTime = DateTime.UtcNow;

                float? rating = null;
                var cacheOutcome = LetterboxdRatingCacheOutcome.TransientFailure;
                string url = string.Empty;

                if (!string.IsNullOrEmpty(tmdbId))
                {
                    url = $"https://letterboxd.com/tmdb/{tmdbId}/";
                }
                else if (!string.IsNullOrEmpty(imdbId))
                {
                    url = $"https://letterboxd.com/imdb/{imdbId}/";
                }

                _logger.LogInformation("Fetching Letterboxd rating for '{Name}' from {Url}", item.Name, url);

                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Letterboxd page not found for '{Name}' at {Url}", item.Name, url);
                        cacheOutcome = LetterboxdRatingCacheOutcome.ConfirmedMissing;
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        var html = await response.Content.ReadAsStringAsync(cancellationToken);
                        rating = ParseRatingFromHtml(html);
                        if (rating.HasValue)
                        {
                            cacheOutcome = LetterboxdRatingCacheOutcome.Rating;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching/parsing Letterboxd rating for '{Name}'", item.Name);
                }

                if (rating.HasValue)
                {
                    _logger.LogInformation("Successfully resolved Letterboxd rating for '{Name}': {Rating}", item.Name, rating.Value);
                    SaveToCache(cacheKey, tmdbId, imdbId, rating.Value, LetterboxdRatingCacheOutcome.Rating);
                    ApplyRating(item, rating.Value);
                    return ItemUpdateType.MetadataDownload;
                }
                else
                {
                    SaveToCache(cacheKey, tmdbId, imdbId, 0f, cacheOutcome);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return ItemUpdateType.None;
        }

        internal float? ParseRatingFromHtml(string html)
        {
            // 1. Try twitter:data2 meta tag
            var match = Regex.Match(html, @"<meta[^>]*?name=""twitter:data2""[^>]*?content=""([0-9.]+)\s+out\s+of\s+5""[^>]*?>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(html, @"<meta[^>]*?content=""([0-9.]+)\s+out\s+of\s+5""[^>]*?name=""twitter:data2""[^>]*?>", RegexOptions.IgnoreCase);
            }

            // 2. Try JSON-LD aggregateRating fallback
            if (!match.Success)
            {
                match = Regex.Match(html, @"""ratingValue""\s*:\s*([0-9.]+)", RegexOptions.IgnoreCase);
            }

            if (match.Success && float.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var parsedValue))
            {
                return parsedValue is >= 0f and <= 5f ? parsedValue : null;
            }

            return null;
        }

        internal void ApplyRating(Movie movie, float letterboxdRating)
        {
            if (letterboxdRating is < 0 or > 5) return;

            var config = Plugin.Instance?.Configuration;
            var mapping = config?.RatingMapping ?? "Community";
            var overwritePolicy = config?.RatingOverwritePolicy ?? "Overwrite";
            if (string.Equals(overwritePolicy, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var onlyFillEmpty = string.Equals(overwritePolicy, "OnlyFillEmpty", StringComparison.OrdinalIgnoreCase);

            // Convert 5-star rating to 10-point rating
            float convertedRating = letterboxdRating * 2f;

            if (string.Equals(mapping, "Community", StringComparison.OrdinalIgnoreCase))
            {
                if (!onlyFillEmpty || !movie.CommunityRating.HasValue) movie.CommunityRating = convertedRating;
            }
            else if (string.Equals(mapping, "Critic", StringComparison.OrdinalIgnoreCase))
            {
                if (!onlyFillEmpty || !movie.CriticRating.HasValue) movie.CriticRating = convertedRating * 10f;
            }
            else if (string.Equals(mapping, "Both", StringComparison.OrdinalIgnoreCase))
            {
                if (!onlyFillEmpty || !movie.CommunityRating.HasValue) movie.CommunityRating = convertedRating;
                if (!onlyFillEmpty || !movie.CriticRating.HasValue) movie.CriticRating = convertedRating * 10f;
            }
        }

        private LetterboxdRatingCacheItem? TryGetFromCache(string cacheKey)
        {
            var cacheFile = GetCacheFilePath();
            if (!File.Exists(cacheFile)) return null;

            try
            {
                lock (_cacheLock)
                {
                    var json = File.ReadAllText(cacheFile);
                    var cache = JsonSerializer.Deserialize<Dictionary<string, LetterboxdRatingCacheItem>>(json);
                    if (cache != null && cache.TryGetValue(cacheKey, out var item) && IsCacheEntryCurrent(item))
                    {
                        return item;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read Letterboxd ratings cache.");
            }
            return null;
        }

        private static bool IsCacheEntryCurrent(LetterboxdRatingCacheItem item)
        {
            var age = DateTime.UtcNow - item.LastUpdated;
            return item.Outcome switch
            {
                // Historical cache files encoded failures as a negative rating. Do not
                // keep those legacy transient failures alive for the positive-rating TTL.
                LetterboxdRatingCacheOutcome.Rating => item.Rating >= 0 && age < TimeSpan.FromDays(ValidRatingCacheDays),
                LetterboxdRatingCacheOutcome.ConfirmedMissing => age < TimeSpan.FromHours(ConfirmedMissingCacheHours),
                LetterboxdRatingCacheOutcome.TransientFailure => age < TimeSpan.FromMinutes(TransientFailureCacheMinutes),
                _ => false
            };
        }

        private void SaveToCache(string cacheKey, string? tmdbId, string? imdbId, float rating, LetterboxdRatingCacheOutcome outcome)
        {
            var cacheFile = GetCacheFilePath();
            var temporaryCacheFile = cacheFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                lock (_cacheLock)
                {
                    Dictionary<string, LetterboxdRatingCacheItem> cache;
                    if (File.Exists(cacheFile))
                    {
                        var json = File.ReadAllText(cacheFile);
                        cache = JsonSerializer.Deserialize<Dictionary<string, LetterboxdRatingCacheItem>>(json)
                                ?? new Dictionary<string, LetterboxdRatingCacheItem>(StringComparer.OrdinalIgnoreCase);
                    }
                    else
                    {
                        cache = new Dictionary<string, LetterboxdRatingCacheItem>(StringComparer.OrdinalIgnoreCase);
                    }

                    cache[cacheKey] = new LetterboxdRatingCacheItem
                    {
                        TmdbId = tmdbId ?? string.Empty,
                        ImdbId = imdbId ?? string.Empty,
                        Rating = rating,
                        Outcome = outcome,
                        LastUpdated = DateTime.UtcNow
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var serialized = JsonSerializer.Serialize(cache, options);
                    File.WriteAllText(temporaryCacheFile, serialized);
                    File.Move(temporaryCacheFile, cacheFile, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write Letterboxd ratings cache.");
                if (File.Exists(temporaryCacheFile)) File.Delete(temporaryCacheFile);
            }
        }

        internal static void ClearCache()
        {
            var cacheFile = GetCacheFilePath();
            lock (_cacheLock)
            {
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
            }
        }

        private static string GetCacheFilePath()
        {
            var configPath = Plugin.Instance?.ConfigurationFilePath;
            if (string.IsNullOrEmpty(configPath))
            {
                return Path.Combine(Path.GetTempPath(), "LetterboxdRatingsCache.json");
            }
            return Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "LetterboxdRatingsCache.json");
        }
    }

    public class LetterboxdRatingCacheItem
    {
        public string TmdbId { get; set; } = string.Empty;
        public string ImdbId { get; set; } = string.Empty;
        public float Rating { get; set; }
        public DateTime LastUpdated { get; set; }
        public LetterboxdRatingCacheOutcome Outcome { get; set; }
    }

    public enum LetterboxdRatingCacheOutcome
    {
        Rating,
        ConfirmedMissing,
        TransientFailure
    }
}
