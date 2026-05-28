---
tags:
  - jellybox
  - technical
  - csharp
  - regex
  - jellyfin-api
  - architecture
---

# Technical Architecture: Jellybox C# Codebase 🏗️

The Jellybox plugin is built as a server-side C# plugin that runs inside the Jellyfin Server process. This note outlines its technical structure, dependencies, database queries, and parser logic.

---

## 📂 Component Layout

The code is divided into the following key components:

```
Jellybox/
├── Plugin.cs                          # Plugin entry point & web page register
├── Configuration/
│   ├── PluginConfiguration.cs        # Serialized XML configuration settings
│   └── configPage.html               # Admin UI panel template (HTML/JS)
└── ScheduledTasks/
    └── LetterboxdSyncTask.cs         # Core sync logic (fetching, parsing, database queries)
```

---

## 🔑 Core Class Blueprints

### 1. `Plugin.cs`
- Inherits from `BasePlugin<PluginConfiguration>` and implements `IHasWebPages`.
- **Purpose**: Defines the plugin identity (Guid: `f62e84d4-5390-482a-a96d-a60d0ee89311`) and injects the dashboard configuration page `configPage.html`.

### 2. `PluginConfiguration.cs`
Stores key-value state serialized as XML by Jellyfin:
- `LetterboxdUsername` (string): Username of the public Letterboxd account.
- `JellyfinUserId` (string): Guid of the Jellyfin user to sync library and playlist against.
- `PlaylistName` (string): Playlist title (defaults to `"Letterboxd Watchlist"`).
- `SyncMode` (string): `"Append"` or `"Sync"` (Full Sync).
- `SyncIntervalHours` (int): Hour interval for task trigger (1, 6, 12, 24).
- `LastSyncTotalCount` (int): Number of watchlist items in latest sync.
- `LastSyncMatchedCount` (int): Number of matched library items in latest sync.
- `LastSyncTime` (string): Timestamp of latest sync execution.
- `LastSyncUnmatchedFilmsJson` (string): JSON string of unmatched films list (slug, title, year).

### 3. `LetterboxdSyncTask.cs`
Implements `IScheduledTask` for background execution. 
- **Dependencies**:
  - `ILibraryManager`: Queries the local movie library database.
  - `IPlaylistManager`: Creates, updates, and deletes playlists.
  - `IUserManager`: Resolves the active user profiles.
- **Triggers**: Defaults to running automatically every 12 hours (`IntervalTrigger`).

---

## 🌐 Parser & Scraping Engine

Because Letterboxd does not provide an official API, the parsing logic in `LetterboxdSyncTask.cs` fetches pages sequentially (`https://letterboxd.com/{username}/watchlist/page/{page}/`) and runs Regex queries:

### React-based Markup Parser (Primary)
Letterboxd uses React components for poster grids:
- **Pattern**:
  ```regex
  <div[^>]*class="react-component"[^>]*data-component-class="LazyPoster"[^>]*>
  ```
- **Attributtes**: Extract `data-item-slug` (e.g. `parasite-2019`) and `data-item-name` (e.g. `Parasite (2019)`).
- **Year Parsing**: Year is parsed from parentheses `\((\d{4})\)$` and stripped to leave a clean title.

### Legacy HTML Parser (Fallback)
If the React parser returns 0 elements, it falls back to:
- **Pattern**:
  ```regex
  class="[^"]*film-poster[^"]*"[^>]*data-film-slug="([^"]+)"[^>]*>.*?alt="([^"]+)"
  ```
- **Year Parsing**: Extracted from the end of the slug (e.g. `-(\d{4})$`).

---

## 💾 Caching & External ID Scraping Engine

To resolve precise matches, Jellybox scrapes each film's detail page (`https://letterboxd.com/film/{slug}/`) to obtain TMDb and IMDb identifiers. To prevent rate limiting and excessive network overhead:

1. **Local Cache (`LetterboxdCache.json`)**:
   - Stored in Jellyfin's plugin configuration folder (resolved dynamically via `Path.GetDirectoryName(Plugin.Instance.ConfigurationFilePath)`).
   - Maps Letterboxd film slugs to `TmdbId`, `ImdbId`, and a timestamp.
   - Saves cached details at the end of the sync process if new slugs are resolved.
2. **Metadata Page Parser**:
   - For items missing from the local cache, the scraper fetches the page, introducing a **1000ms delay** between requests to respect Letterboxd resources.
   - Extracts the TMDb ID with the pattern `themoviedb\.org/movie/(\d+)` or `data-tmdb-id="(\d+)"`.
   - Extracts the IMDb ID with the pattern `imdb\.com/title/(tt\d+)`.

---

## 🔍 Matching & Playlist Logic

Jellybox fetches the target user's entire movie library into memory once at task startup, then implements a **3-tier cascade matching system**:

1. **TMDb ID Matching**: Checks if a library movie has a matching TMDb provider ID (`Tmdb`).
2. **IMDb ID Matching**: Checks if a library movie has a matching IMDb provider ID (`Imdb`).
3. **Fuzzy Title + Year Matching (Fallback)**:
   - Compares normalized title string (alphanumeric lowercase, ignores accents, spaces, and punctuation).
   - Validates using production year if available.

### Playlist Management:
- **Append Mode**: Appends new movie IDs to the existing playlist via `_playlistManager.AddItemToPlaylistAsync`.
- **Full Sync Mode**: Deletes the existing playlist and recreates it with all matched movie IDs to keep the playlist elements in the exact order of the Letterboxd watchlist.

For expansion planning, check [[Roadmap & Extensions]].
