# Jellybox — Jellyfin × Letterboxd Sync Plugin

<p align="center">
  <img src="thumb.png" alt="Jellybox Logo" width="150" height="150" style="border-radius: 20%;" />
</p>

[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.11.x-blue.svg)](https://jellyfin.org)
[![Target Framework](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Build Tool](https://img.shields.io/badge/SDK-10.0-green.svg)](https://dotnet.microsoft.com)

A server-side plugin for **Jellyfin** (10.11.x+) that automatically synchronizes a user's public **Letterboxd watchlist** to a Jellyfin playlist.

The sync task runs in the background at configurable intervals (or can be triggered manually from the dashboard), matching movies in your Letterboxd watchlist to your Jellyfin library using **TMDb/IMDb ID resolution** with a fuzzy title-matching fallback.

---

## Features

- 🔄 **Automatic Scheduled Sync:** Syncs your watchlist automatically at a configurable interval (1h, 6h, 12h, or 24h — default: 12h).
- 🎯 **Smart Metadata Matching:** Three-tier matching engine for maximum accuracy:
  1. **TMDb ID** — Scraped from the Letterboxd film detail page and matched against Jellyfin's metadata providers.
  2. **IMDb ID** — Fallback if TMDb ID is unavailable.
  3. **Fuzzy Title + Year** — Last resort: normalizes titles (removes special characters/casing) and validates production year.
- 💾 **Local ID Cache:** Resolved TMDb/IMDb mappings are cached locally (`LetterboxdCache.json`) to avoid redundant lookups and respect rate limits.
- 👥 **User Selection:** Choose which Jellyfin user the playlist should belong to.
- ⚙️ **Two Sync Modes:**
  - **Append Only:** Only adds new movies from your Letterboxd watchlist to your playlist; no items are ever removed.
  - **Full Sync:** Recreates the playlist to match your Letterboxd watchlist exactly (removes watched/deleted items and maintains the correct order).
- 🎨 **Modern Config UI:** A premium, glassmorphism-styled dashboard page integrated directly into your Jellyfin administration panel, featuring:
  - **Sync Now** button for on-demand synchronization.
  - **Matched / Unmatched summary** panel with expandable details.
  - **Configurable sync interval** dropdown.

---

## Installation

You can install this plugin either via the **Jellyfin Plugin Catalog** (recommended) or **manually**.

### Option A: Install via Plugin Catalog (Repository)
1. Go to your Jellyfin server **Dashboard** -> **Plugins**.
2. Select the **Repositories** tab and click **Add**.
3. Enter a name (e.g., `Jellybox`) and paste the raw `manifest.json` URL:
   ```
   https://raw.githubusercontent.com/bozer00/jellyfin-plugin-letterboxd-sync/main/manifest.json
   ```
4. Click **Save**.
5. Switch to the **Catalog** tab. Find **Letterboxd Watchlist Sync** under the *Playlists* category, click on it, and click **Install**.
6. Restart your Jellyfin server.

### Option B: Manual Installation
1. Download the compiled `Jellyfin.Plugin.LetterboxdSync.dll` file from the [latest release](https://github.com/bozer00/jellyfin-plugin-letterboxd-sync/releases).
2. Navigate to your Jellyfin server's `plugins` directory:
   - **Linux:** `/var/lib/jellyfin/plugins/`
   - **Windows:** `C:\ProgramData\Jellyfin\Server\plugins\`
   - **Docker:** your mapped `/config/plugins/` directory
3. Create a folder named `LetterboxdSync` and paste the `.dll` inside it.
4. Restart your Jellyfin server.

---

## Configuration

1. In the Jellyfin Web UI, navigate to **Dashboard** -> **Plugins** -> **Installed**.
2. Click on **Letterboxd Sync** to open its settings page.
3. Configure the following fields:
   - **Letterboxd Username:** Enter your public Letterboxd username.
   - **Sync to User:** Select which Jellyfin user's library and playlist to sync against.
   - **Playlist Name:** Enter the target playlist name (default: *Letterboxd Watchlist*).
   - **Sync Mode:** Choose between *Append Only* or *Full Sync*.
   - **Sync Interval:** Choose between *1 hour*, *6 hours*, *12 hours* (default), or *24 hours*.
4. Click **Save Settings**.
5. Optionally, click **Sync Now** to trigger an immediate synchronization.

---

## How It Works

1. **Letterboxd Scraping:** The plugin sends standard HTTP requests page-by-page to `https://letterboxd.com/USERNAME/watchlist/` (requires your watchlist to be set to **Public**).
2. **Parsing:** It parses the server-side rendered HTML using lightweight regex queries to extract:
   - Film URL slug (e.g., `parasite-2019`)
   - Film title (from the `data-item-name` attribute, e.g., `Parasite`)
   - Production year (extracted from the title if it ends in a 4-digit number in parentheses)
3. **Metadata ID Resolution:**
   - For each new film not yet in the local cache, the plugin fetches the Letterboxd film detail page (`/film/{slug}/`) to extract the **TMDb** and/or **IMDb** ID.
   - Resolved IDs are saved to `LetterboxdCache.json` to avoid redundant lookups on subsequent syncs.
4. **Smart Matching (3-tier cascade):**
   - **TMDb ID** → Queries Jellyfin's library for a movie with a matching TMDb provider ID.
   - **IMDb ID** → Falls back to IMDb provider ID matching if TMDb is unavailable.
   - **Fuzzy Title + Year** → As a last resort, normalizes titles (e.g., `Léon: The Professional` → `leontheprofessional`) and matches against production year.
5. **Playlist Management:** Depending on the sync mode:
   - **Append Only:** Identifies movies in the watchlist not yet in the playlist and appends them.
   - **Full Sync:** Deletes the existing playlist and recreates it with the matched items in Letterboxd order.

---

## Development & Building

The project targets **.NET 10.0** for the Jellyfin 10.11.x API.

### Prerequisites
- [.NET SDK 10.0+](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build Locally
```bash
dotnet build -c Release -o ./build
```

### Build with Docker
If you do not have the .NET SDK installed locally:
```bash
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build -c Release -o ./build
```

---

## Project Structure

```
Jellybox/
├── Plugin.cs                          # Plugin entry point & web page registration
├── LetterboxdSync.csproj              # Project file (.NET 10.0, Jellyfin SDK refs)
├── manifest.json                      # Jellyfin plugin repository manifest
├── Configuration/
│   ├── PluginConfiguration.cs         # Serialized settings (username, sync mode, etc.)
│   └── configPage.html                # Admin dashboard UI (glassmorphism-styled)
├── ScheduledTasks/
│   └── LetterboxdSyncTask.cs          # Core sync logic (scraping, matching, playlist ops)
├── Obsidian - Jellybox/               # Developer documentation vault (Obsidian markdown)
│   ├── Welcome.md
│   ├── Project Overview.md
│   ├── Technical Architecture.md
│   ├── Design Decisions.md
│   └── Roadmap & Extensions.md
└── build/                             # Compiled output (gitignored)
```

---

## Roadmap

- [x] Automatic watchlist sync (Append & Full Sync modes)
- [x] Fuzzy title + year matching
- [x] Glassmorphism admin config page
- [ ] TMDb/IMDb ID resolution with local caching
- [ ] Configurable sync interval (1h / 6h / 12h / 24h)
- [ ] "Sync Now" button in the config page
- [ ] Matched / Unmatched summary panel in UI
- [ ] Custom Letterboxd lists support
- [ ] Multi-user sync profiles
- [ ] Bidirectional watched-state synchronization

---

## License

This project is open-source. See the repository for license details.
