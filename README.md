# Jellybox — Jellyfin × Letterboxd Integration Suite

<p align="center">
  <img src="thumb.png" alt="Jellybox Logo" width="150" height="150" style="border-radius: 20%;" />
</p>

[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.11.x-blue.svg)](https://jellyfin.org)
[![Target Framework](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com)
[![Build Tool](https://img.shields.io/badge/SDK-9.0-green.svg)](https://dotnet.microsoft.com)

A collection of server-side plugins for **Jellyfin** (10.11.x+) that integrates **Letterboxd** features directly into your media server. Jellybox is unofficial and is not affiliated with, endorsed by, or supported by Letterboxd.

---

## Plugins in this Repository

This repository contains three independent plugins:

1.  [Letterboxd Watchlist Sync](#1-letterboxd-watchlist-sync) — Automatically synchronize a user's public Letterboxd watchlist to a Jellyfin playlist.
2.  [Letterboxd Ratings](#2-letterboxd-ratings) — Fetch average community ratings from Letterboxd and apply them to movie items in your library.
3.  [Letterboxd Watched Sync](#3-letterboxd-watched-sync) — Automatically mark movies as watched in Jellyfin based on a public Letterboxd watched history.

---

## 1. Letterboxd Watchlist Sync

A scheduled background task plugin that synchronizes a user's watchlist using TMDb/IMDb ID resolution.

### Features
-   🔄 **Automatic Scheduled Sync:** Syncs watchlist automatically at a configurable interval (1h, 6h, 12h, or 24h).
-   🎯 **Smart Metadata Matching:** Three-tier matching engine (TMDb ID -> IMDb ID -> Fuzzy Title + Year fallback).
-   💾 **Local ID Cache:** Resolved TMDb/IMDb mappings are cached locally (`LetterboxdCache.json`) to avoid redundant requests.
-   ⚙️ **Two Sync Modes:**
    -   *Append Only:* Only appends new items; no items are ever removed.
    -   *Full Sync:* Re-creates the playlist to match the Letterboxd watchlist exactly (removes watched/deleted items and maintains correct order).
-   🎨 **Modern Config UI:** A premium, glassmorphism-styled dashboard page featuring a "Sync Now" button and match summary statistics.

---

## 2. Letterboxd Ratings

A metadata provider plugin that fetches average community ratings from Letterboxd and integrates them into your Jellyfin movie metadata.

### Features
-   ⭐ **Aggregate Ratings:** Scrapes the global average rating for movies (e.g., `4.13 out of 5`) directly from Letterboxd.
-   ⚙️ **Flexible Mapping:** Configure where the ratings are saved in Jellyfin's metadata:
    -   `Community`: Maps the score on a 10-point scale (e.g., `4.13` -> `8.26` Community Rating).
    -   `Critic`: Maps the score on a 100-point scale (e.g., `4.13` -> `83%` Critic Rating).
    -   `Both`: Applies the rating to both fields.
-   🔒 **Local Cache:** Valid scraped ratings are cached locally (`LetterboxdRatingsCache.json`) for 14 days; confirmed misses and transient failures use short retry windows. The configuration page can clear the cache immediately.
-   🚦 **Rate Limiting & request serialization:** Uses a thread-safe semaphore and a mandatory 1.5-second delay between outgoing requests to prevent IP bans.

---

## 3. Letterboxd Watched Sync

A scheduled background task plugin that parses a user's public Letterboxd watched history and marks matched movies as played in Jellyfin.

### Features
-   🔄 **Automatic Scheduled Sync:** Syncs played status automatically at a configurable interval (1h, 6h, 12h, or 24h).
-   🎯 **Smart Metadata Matching:** Three-tier matching engine (TMDb ID -> IMDb ID -> Fuzzy Title + Year fallback).
-   💾 **Local ID Cache:** Resolved TMDb/IMDb mappings are cached locally (`LetterboxdWatchedCache.json`) to avoid redundant requests.
-   ⚠️ **Safety First:** If no Jellyfin user is configured, the task aborts and logs a warning rather than modifying playstates for a default profile.
-   🎨 **Modern Config UI:** A premium, glassmorphism-styled dashboard page featuring a "Sync Now" button, match summary statistics, and an unmatched films grid (capped at 200 items).

---

## Installation

You can install either plugin via the **Jellyfin Plugin Catalog** or **manually**.

### Option A: Install via Plugin Catalog
1.  Go to your Jellyfin server **Dashboard** -> **Plugins**.
2.  Select the **Repositories** tab and click **Add**.
3.  Enter a name (e.g., `Jellybox`) and paste the raw repository `manifest.json` URL:
    ```
    https://raw.githubusercontent.com/bozer00/jellyfin-plugin-letterboxd-sync/main/manifest.json
    ```
4.  Click **Save**.
5.  Switch to the **Catalog** tab. Find the Jellybox plugin you want—**Letterboxd Watchlist Sync**, **Letterboxd Ratings**, or **Letterboxd Watched Sync**—then click **Install**.
6.  Restart your Jellyfin server.

### Option B: Manual Installation
1.  Download the matching plugin ZIP from a [GitHub Release](https://github.com/bozer00/jellyfin-plugin-letterboxd-sync/releases), then extract its DLL:
    -   `Jellyfin.Plugin.LetterboxdSync.dll` for the Watchlist Sync.
    -   `Jellyfin.Plugin.LetterboxdRatings.dll` for Ratings.
    -   `Jellyfin.Plugin.LetterboxdWatchedSync.dll` for Watched Sync.
2.  Navigate to your Jellyfin server's `plugins` directory.
3.  Create subdirectories `LetterboxdSync`, `LetterboxdRatings`, and `LetterboxdWatchedSync` respectively and place the matching DLL inside.
4.  Restart your Jellyfin server.

---

## Configuration

### Watchlist Sync Configuration
1.  Navigate to **Dashboard** -> **Plugins** -> **Installed** and click **Letterboxd Sync**.
2.  Configure your Letterboxd Username, Sync to User, Playlist Name, Sync Mode, and Sync Interval.
3.  Click **Save Settings**, or optionally click **Sync Now** to run an immediate sync.

### Ratings Configuration
1.  Navigate to **Dashboard** -> **Plugins** -> **Installed** and click **Letterboxd Ratings**.
2.  Configure the **Letterboxd Rating Mapping** option (`Community`, `Critic`, or `Both`) and its overwrite policy. The default overwrites mapped ratings to preserve prior behavior; choose **Only fill empty** to preserve existing metadata or **Disabled** to stop writes.
3.  Click **Save Settings**.
4.  Go to **Dashboard** -> **Libraries**, click on your Movies library options, and in the **Metadata downloaders** section, make sure **Letterboxd Ratings** is checked. Place it in your preferred order priority.
5.  Run a metadata refresh on your library to populate the ratings.

### Watched Sync Configuration
1.  Navigate to **Dashboard** -> **Plugins** -> **Installed** and click **Letterboxd Watched Sync**.
2.  Configure your Letterboxd Username, Jellyfin User, and Sync Interval.
3.  Click **Save Settings**, or optionally click **Sync Now** to run an immediate sync.

---

## Building Locally

The projects target **.NET 9.0**. Install the stable SDK selected by [`global.json`](global.json); the same SDK is used in CI.

To build all plugins and package them into ZIP archives, run the automated build script:
```bash
./build.sh
```

Alternatively, to compile individual projects:
```bash
dotnet build -c Release LetterboxdSync/LetterboxdSync.csproj
dotnet build -c Release LetterboxdRatings/LetterboxdRatings.csproj
dotnet build -c Release LetterboxdWatchedSync/LetterboxdWatchedSync.csproj
```

Run the full verification suite before opening a change:

```bash
dotnet restore Jellybox.sln
dotnet build Jellybox.sln -c Release --no-restore
dotnet test Jellybox.Tests/Jellybox.Tests.csproj -c Release --no-build
```

---

## Compatibility

Jellybox currently targets Jellyfin **10.11.x** and .NET 9.0. It is tested by CI as a plugin build and unit-test suite; it is not a compatibility guarantee for Jellyfin pre-releases, Jellyfin 12, or every host operating system. Please include your Jellyfin version and deployment type when reporting a problem.

---

## Privacy and network behavior

- Watchlist and watched sync use the public Letterboxd username configured in Jellyfin to request public Letterboxd pages. Ratings requests use a movie's TMDb or IMDb provider ID.
- The plugins do not ask for a Letterboxd password or API token.
- Sync caches and last-sync summaries are stored in Jellyfin's plugin configuration area. The caches can contain public film identifiers, titles, and matching results.
- Letterboxd pages are scraped rather than consumed through an official integration. Page changes, unavailable profiles, and rate limits can interrupt a sync or ratings lookup.
- Scraping public data may still be subject to Letterboxd's terms and your local policies. Use conservative sync intervals and review the behavior before enabling it on a shared server.

---

## Releases and support

Every supported release is built, tested, and published as immutable GitHub Release ZIPs. The release workflow produces a catalog manifest whose checksum is calculated from those exact archives; see [the release guide](docs/RELEASING.md) for the maintainer process.

For bug reports and feature ideas, use the issue forms. Remove private usernames, server URLs, filesystem paths, and credentials from screenshots or logs. Report security issues privately as described in [SECURITY.md](SECURITY.md). Contribution guidelines are in [CONTRIBUTING.md](CONTRIBUTING.md).
