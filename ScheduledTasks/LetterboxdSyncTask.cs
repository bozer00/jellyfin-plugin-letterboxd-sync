using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Model.Playlists;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace LetterboxdSync.ScheduledTasks
{
    public class LetterboxdSyncTask : IScheduledTask
    {
        private readonly ILogger<LetterboxdSyncTask> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly IUserManager _userManager;
        private readonly HttpClient _httpClient;

        public LetterboxdSyncTask(
            ILogger<LetterboxdSyncTask> logger,
            ILibraryManager libraryManager,
            IPlaylistManager playlistManager,
            IUserManager userManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _playlistManager = playlistManager;
            _userManager = userManager;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public string Name => "Sync Letterboxd Watchlist";

        public string Category => "Letterboxd Sync";

        public string Description => "Syncs a user's Letterboxd watchlist to a Jellyfin playlist.";

        public string Key => "LetterboxdSyncTask";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.IntervalTrigger,
                    IntervalTicks = TimeSpan.FromHours(12).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || string.IsNullOrWhiteSpace(config.LetterboxdUsername))
            {
                _logger.LogWarning("Letterboxd username is not configured. Skipping task.");
                progress.Report(100);
                return;
            }

            var username = config.LetterboxdUsername.Trim();
            var playlistName = string.IsNullOrWhiteSpace(config.PlaylistName) ? "Letterboxd Watchlist" : config.PlaylistName.Trim();

            _logger.LogInformation("Starting Letterboxd watchlist sync for user {Username} to playlist '{PlaylistName}'", username, playlistName);

            var films = await FetchWatchlistAsync(username, cancellationToken);
            if (films.Count == 0)
            {
                _logger.LogInformation("No films found in Letterboxd watchlist for {Username} (or watchlist is private/unavailable).", username);
                progress.Report(100);
                return;
            }

            _logger.LogInformation("Found {Count} films in Letterboxd watchlist.", films.Count);

            User? targetUser = null;
            if (!string.IsNullOrEmpty(config.JellyfinUserId) && Guid.TryParse(config.JellyfinUserId, out var configUserId))
            {
                targetUser = _userManager.GetUserById(configUserId);
            }

            if (targetUser == null)
            {
                targetUser = _userManager.Users.FirstOrDefault();
            }

            if (targetUser == null)
            {
                _logger.LogError("No Jellyfin user found. Cannot create/update playlist.");
                progress.Report(100);
                return;
            }

            _logger.LogInformation("Syncing to Jellyfin user: {Username}", targetUser.Username);

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
            var unmatchedFilms = new List<UnmatchedFilmInfo>();
            int processed = 0;

            foreach (var film in films)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!cache.TryGetValue(film.Slug, out var cacheItem))
                {
                    // Delay to respect rate limits
                    await Task.Delay(1000, cancellationToken);

                    var (tmdbId, imdbId) = await FetchMovieExternalIdsAsync(film.Slug, cancellationToken);

                    cacheItem = new LetterboxdCacheItem
                    {
                        Slug = film.Slug,
                        Title = film.Title,
                        Year = film.Year,
                        TmdbId = tmdbId,
                        ImdbId = imdbId,
                        CachedAt = DateTime.UtcNow
                    };
                    cache[film.Slug] = cacheItem;
                    cacheModified = true;
                }

                var matchedMovie = FindMovieInLibrary(film.Title, film.Year, cacheItem.TmdbId, cacheItem.ImdbId, allMovies);
                if (matchedMovie != null)
                {
                    matchedMovieIds.Add(matchedMovie.Id);
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
                progress.Report(10 + 40 * ((double)processed / films.Count));
            }

            if (cacheModified)
            {
                SaveCache(cache);
            }

            // Update configuration stats
            if (Plugin.Instance != null)
            {
                var configToUpdate = Plugin.Instance.Configuration;
                configToUpdate.LastSyncTotalCount = films.Count;
                configToUpdate.LastSyncMatchedCount = matchedMovieIds.Count;
                configToUpdate.LastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                configToUpdate.LastSyncUnmatchedFilmsJson = JsonSerializer.Serialize(unmatchedFilms);
                Plugin.Instance.SaveConfiguration();
            }

            _logger.LogInformation("Successfully matched {MatchedCount} out of {TotalCount} films in Jellyfin library.", matchedMovieIds.Count, films.Count);

            if (matchedMovieIds.Count == 0)
            {
                _logger.LogWarning("No films from the Letterboxd watchlist were matched in the Jellyfin library. Playlist will not be updated.");
                progress.Report(100);
                return;
            }

            var playlistQuery = new InternalItemsQuery(targetUser)
            {
                IncludeItemTypes = new[] { BaseItemKind.Playlist },
                Recursive = true
            };
            var playlists = _libraryManager.GetItemList(playlistQuery);
            var playlist = playlists.FirstOrDefault(p => string.Equals(p.Name, playlistName, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(config.SyncMode, "Sync", StringComparison.OrdinalIgnoreCase))
            {
                if (playlist != null)
                {
                    _logger.LogInformation("Deleting existing playlist '{PlaylistName}' for full sync.", playlistName);
                    _libraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = false });
                    playlist = null;
                }
            }

            if (playlist == null)
            {
                _logger.LogInformation("Creating new playlist '{PlaylistName}' with {Count} items.", playlistName, matchedMovieIds.Count);
                var createResult = _playlistManager.CreatePlaylist(new PlaylistCreationRequest
                {
                    Name = playlistName,
                    UserId = targetUser.Id,
                    ItemIdList = matchedMovieIds.ToArray()
                });
                _logger.LogInformation("Playlist created successfully. ID: {Id}", createResult.Id);
            }
            else
            {
                _logger.LogInformation("Updating existing playlist '{PlaylistName}' (Append mode).", playlistName);
                
                var currentItems = _libraryManager.GetItemList(new InternalItemsQuery(targetUser)
                {
                    ParentId = playlist.Id,
                    Recursive = true
                });
                var currentItemIds = currentItems.Select(i => i.Id).ToHashSet();

                var itemsToAdd = matchedMovieIds.Where(id => !currentItemIds.Contains(id)).ToArray();
                if (itemsToAdd.Length > 0)
                {
                    _logger.LogInformation("Adding {Count} new items to playlist.", itemsToAdd.Length);
                    await _playlistManager.AddItemToPlaylistAsync(playlist.Id, itemsToAdd, targetUser.Id);
                }
                else
                {
                    _logger.LogInformation("No new items to add to the playlist.");
                }
            }

            progress.Report(100);
            _logger.LogInformation("Letterboxd watchlist sync completed.");
        }

        private async Task<List<LetterboxdFilm>> FetchWatchlistAsync(string username, CancellationToken cancellationToken)
        {
            var films = new List<LetterboxdFilm>();
            int page = 1;
            bool hasMore = true;

            while (hasMore)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = $"https://letterboxd.com/{username}/watchlist/page/{page}/";
                _logger.LogInformation("Fetching page {Page} from {Url}", page, url);

                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        break;
                    }
                    response.EnsureSuccessStatusCode();

                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    var pageFilms = ParseWatchlistHtml(html);
                    if (pageFilms.Count == 0)
                    {
                        break;
                    }

                    films.AddRange(pageFilms);

                    hasMore = html.Contains("class=\"next\"");
                    page++;

                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching Letterboxd watchlist page {Page} for {Username}", page, username);
                    break;
                }
            }

            return films;
        }

        private List<LetterboxdFilm> ParseWatchlistHtml(string html)
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

        private BaseItem? FindMovieInLibrary(string title, int? year, string tmdbId, string imdbId, IReadOnlyList<BaseItem> allMovies)
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
            var matches = allMovies.Where(m => string.Equals(m.Name, title, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
            {
                var normalizedTitle = NormalizeTitle(title);
                matches = allMovies.Where(m => string.Equals(NormalizeTitle(m.Name ?? string.Empty), normalizedTitle, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (year.HasValue)
            {
                var yearMatch = matches.FirstOrDefault(m => m.ProductionYear == year.Value);
                if (yearMatch != null) return yearMatch;
            }

            return matches.FirstOrDefault();
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
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(cache.Values.ToList(), options);
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Letterboxd cache.");
            }
        }

        private string GetCacheFilePath()
        {
            var configPath = Plugin.Instance?.ConfigurationFilePath;
            if (string.IsNullOrEmpty(configPath))
            {
                return Path.Combine(Path.GetTempPath(), "LetterboxdCache.json");
            }
            return Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "LetterboxdCache.json");
        }

        private async Task<(string TmdbId, string ImdbId)> FetchMovieExternalIdsAsync(string slug, CancellationToken cancellationToken)
        {
            var url = $"https://letterboxd.com/film/{slug}/";
            _logger.LogInformation("Fetching film details from {Url}", url);
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (string.Empty, string.Empty);
                }
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching external IDs for film slug {Slug}", slug);
                return (string.Empty, string.Empty);
            }
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
    }

    public class UnmatchedFilmInfo
    {
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Slug { get; set; } = string.Empty;
    }
}
