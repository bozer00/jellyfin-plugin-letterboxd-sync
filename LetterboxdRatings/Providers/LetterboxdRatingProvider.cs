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
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace LetterboxdRatings.Providers
{
    public class LetterboxdRatingProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly ILogger<LetterboxdRatingProvider> _logger;
        private readonly HttpClient _httpClient;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;

        public LetterboxdRatingProvider(ILogger<LetterboxdRatingProvider> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public string Name => "Letterboxd Ratings";

        public int Order => 3; // Execute after standard providers

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            // We do not support searching directly on Letterboxd
            return Task.FromResult<IEnumerable<RemoteSearchResult>>(Array.Empty<RemoteSearchResult>());
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetAsync(url, cancellationToken);
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>
            {
                Item = new Movie(),
                HasMetadata = false
            };

            // Copy provider IDs to match correctly
            if (info.ProviderIds != null)
            {
                foreach (var pair in info.ProviderIds)
                {
                    result.Item.ProviderIds[pair.Key] = pair.Value;
                }
            }

            // Extract IDs
            string? tmdbId = null;
            string? imdbId = null;
            info.ProviderIds?.TryGetValue("Tmdb", out tmdbId);
            info.ProviderIds?.TryGetValue("Imdb", out imdbId);

            if (string.IsNullOrEmpty(tmdbId) && string.IsNullOrEmpty(imdbId))
            {
                _logger.LogDebug("No TMDb or IMDb ID available for Letterboxd ratings lookup of '{Name}'", info.Name);
                return result;
            }

            // Check cache
            var cacheKey = !string.IsNullOrEmpty(tmdbId) ? $"tmdb_{tmdbId}" : $"imdb_{imdbId}";
            var cachedRating = TryGetFromCache(cacheKey);
            if (cachedRating.HasValue)
            {
                _logger.LogDebug("Letterboxd rating cache hit for '{Name}': {Rating}", info.Name, cachedRating.Value);
                ApplyRating(result.Item, cachedRating.Value);
                result.HasMetadata = true;
                return result;
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
                string url = string.Empty;

                if (!string.IsNullOrEmpty(tmdbId))
                {
                    url = $"https://letterboxd.com/tmdb/{tmdbId}/";
                }
                else if (!string.IsNullOrEmpty(imdbId))
                {
                    url = $"https://letterboxd.com/imdb/{imdbId}/";
                }

                _logger.LogInformation("Fetching Letterboxd rating for '{Name}' from {Url}", info.Name, url);
                
                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Letterboxd page not found for '{Name}' at {Url}", info.Name, url);
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        var html = await response.Content.ReadAsStringAsync(cancellationToken);
                        rating = ParseRatingFromHtml(html);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching/parsing Letterboxd rating for '{Name}'", info.Name);
                }

                if (rating.HasValue)
                {
                    _logger.LogInformation("Successfully resolved Letterboxd rating for '{Name}': {Rating}", info.Name, rating.Value);
                    SaveToCache(cacheKey, tmdbId, imdbId, rating.Value);
                    ApplyRating(result.Item, rating.Value);
                    result.HasMetadata = true;
                }
                else
                {
                    // Cache negative result (e.g. rating = 0 or -1) to avoid repeated failed lookups
                    SaveToCache(cacheKey, tmdbId, imdbId, -1f);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return result;
        }

        private float? ParseRatingFromHtml(string html)
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
                return parsedValue;
            }

            return null;
        }

        private void ApplyRating(Movie movie, float letterboxdRating)
        {
            if (letterboxdRating < 0) return; // Cached negative result, do not apply

            var config = Plugin.Instance?.Configuration;
            var mapping = config?.RatingMapping ?? "Community";

            // Convert 5-star rating to 10-point rating
            float convertedRating = letterboxdRating * 2f;

            if (string.Equals(mapping, "Community", StringComparison.OrdinalIgnoreCase))
            {
                movie.CommunityRating = convertedRating;
            }
            else if (string.Equals(mapping, "Critic", StringComparison.OrdinalIgnoreCase))
            {
                movie.CriticRating = convertedRating * 10f; // Scale to 100-point scale for critic rating (e.g. 85%)
            }
            else if (string.Equals(mapping, "Both", StringComparison.OrdinalIgnoreCase))
            {
                movie.CommunityRating = convertedRating;
                movie.CriticRating = convertedRating * 10f;
            }
        }

        private float? TryGetFromCache(string cacheKey)
        {
            var cacheFile = GetCacheFilePath();
            if (!File.Exists(cacheFile)) return null;

            try
            {
                var json = File.ReadAllText(cacheFile);
                var cache = JsonSerializer.Deserialize<Dictionary<string, LetterboxdRatingCacheItem>>(json);
                if (cache != null && cache.TryGetValue(cacheKey, out var item))
                {
                    // Cache is valid for 14 days
                    if ((DateTime.UtcNow - item.LastUpdated).TotalDays < 14)
                    {
                        return item.Rating;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read Letterboxd ratings cache.");
            }
            return null;
        }

        private void SaveToCache(string cacheKey, string? tmdbId, string? imdbId, float rating)
        {
            var cacheFile = GetCacheFilePath();
            try
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
                    LastUpdated = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var serialized = JsonSerializer.Serialize(cache, options);
                File.WriteAllText(cacheFile, serialized);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write Letterboxd ratings cache.");
            }
        }

        private string GetCacheFilePath()
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
    }
}
