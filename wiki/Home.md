# Welcome to Jellybox 🌟

**Jellybox** is an integration suite designed to connect self-hosted media servers (**Jellyfin**) with movie-tracking networks (**Letterboxd**). It helps automate syncing your watchlist and letterboxd ratings directly to your media library.

The project is developed as a multi-plugin codebase containing two independent plugins that can be built and installed together or separately.

---

## 🔌 Core Plugins

### 1. Letterboxd Watchlist Sync
This plugin automates syncing your public Letterboxd watchlist into a Jellyfin playlist.
* **Sync Modes**: 
  * *Append Mode*: Adds new watchlist additions without deleting items.
  * *Full Sync*: Rebuilds the playlist in-place to perfectly mirror the active Letterboxd watchlist.
* **Matching Engine**: Features a cascading matching strategy:
  1. TMDb ID Match (most accurate)
  2. IMDb ID Match (fallback)
  3. Fuzzy Title + Year Match (fallback)
* **Status UI**: Includes an embedded dashboard panel showing total watchlist items, match counts, and a list of unmatched films.

### 2. Letterboxd Ratings
This plugin fetches average community ratings from Letterboxd film detail pages and applies them directly to your Jellyfin library.
* **Mapping**: Configurable to map to Jellyfin's *Community Rating*, *Critic Rating*, or both.
* **Integration**: Registered as a custom metadata provider (`ICustomMetadataProvider`) to ensure it executes after remote providers (TMDb/OMDb), preventing ratings from being overwritten.
* **Caching & Limits**: Uses a local JSON cache valid for 14 days and thread-safe request delays to avoid rate-limiting.

---

## 📂 Repository Layout

```
jellyfin-plugin-letterboxd-sync/
├── LetterboxdSync/        # Source code for the watchlist sync plugin
├── LetterboxdRatings/     # Source code for the ratings metadata provider
├── manifest.json          # Combined plugin catalog manifest
└── build.sh               # Automated build & packaging script
```
