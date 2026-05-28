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

## 🔍 Matching & Playlist Logic

1. **Title Normalization**:
   If exact name matching fails, titles are normalized to a alphanumeric lowercase string to ignore accents, spacing, and punctuation:
   ```csharp
   private string NormalizeTitle(string title)
   {
       return Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]", "");
   }
   ```
2. **Year Matching**:
   If a year is extracted, the code ensures the production year matches Jellyfin's metadata. If no year was found, it matches the first normalized name match.
3. **Database Changes**:
   - **Append Mode**: Queries the playlist items and appends new movie IDs using `_playlistManager.AddItemToPlaylistAsync`.
   - **Full Sync Mode**: Deletes the old playlist using `_libraryManager.DeleteItem` and creates a fresh one using `_playlistManager.CreatePlaylist` to preserve Letterboxd sorting and handle removals.

For expansion planning, check [[Roadmap & Extensions]].
