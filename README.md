# Jellybox — Jellyfin × Letterboxd Integration Suite

<p align="center">
  <img src="thumb.png" alt="Jellybox Logo" width="150" height="150" style="border-radius: 20%;" />
</p>

[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.11.x-blue.svg)](https://jellyfin.org)
[![Target Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Build Tool](https://img.shields.io/badge/SDK-10.0-green.svg)](https://dotnet.microsoft.com)

A collection of server-side plugins for **Jellyfin** (10.11.x+) that integrates **Letterboxd** features directly into your media server.

---

## Plugins in this Repository

This repository contains two independent plugins:

1.  [Letterboxd Watchlist Sync](#1-letterboxd-watchlist-sync) — Automatically synchronize a user's public Letterboxd watchlist to a Jellyfin playlist.
2.  [Letterboxd Ratings](#2-letterboxd-ratings) — Fetch average community ratings from Letterboxd and apply them to movie items in your library.

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
-   🔒 **Local Cache:** Scraped ratings are cached locally (`LetterboxdRatingsCache.json`) and kept for 14 days, preventing redundant network requests.
-   🚦 **Rate Limiting & request serialization:** Uses a thread-safe semaphore and a mandatory 1.5-second delay between outgoing requests to prevent IP bans.

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
5.  Switch to the **Catalog** tab. Find **Letterboxd Watchlist Sync** or **Letterboxd Ratings** in the catalog, click on them, and click **Install**.
6.  Restart your Jellyfin server.

### Option B: Manual Installation
1.  Download the compiled `.dll` file from the repository releases:
    -   `Jellyfin.Plugin.LetterboxdSync.dll` for the Watchlist Sync.
    -   `Jellyfin.Plugin.LetterboxdRatings.dll` for Ratings.
2.  Navigate to your Jellyfin server's `plugins` directory.
3.  Create subdirectories `LetterboxdSync` and `LetterboxdRatings` respectively and paste the corresponding `.dll` inside them.
4.  Restart your Jellyfin server.

---

## Configuration

### Watchlist Sync Configuration
1.  Navigate to **Dashboard** -> **Plugins** -> **Installed** and click **Letterboxd Sync**.
2.  Configure your Letterboxd Username, Sync to User, Playlist Name, Sync Mode, and Sync Interval.
3.  Click **Save Settings**, or optionally click **Sync Now** to run an immediate sync.

### Ratings Configuration
1.  Navigate to **Dashboard** -> **Plugins** -> **Installed** and click **Letterboxd Ratings**.
2.  Configure the **Letterboxd Rating Mapping** option (`Community`, `Critic`, or `Both`).
3.  Click **Save Settings**.
4.  Go to **Dashboard** -> **Libraries**, click on your Movies library options, and in the **Metadata downloaders** section, make sure **Letterboxd Ratings** is checked. Place it in your preferred order priority.
5.  Run a metadata refresh on your library to populate the ratings.

---

## Building Locally

The projects target **.NET 10.0** and require the .NET SDK.

To build the Watchlist Sync plugin:
```bash
cd LetterboxdSync
dotnet build -c Release -o ../build
```

To build the Ratings plugin:
```bash
cd LetterboxdRatings
dotnet build -c Release -o ../build
```
