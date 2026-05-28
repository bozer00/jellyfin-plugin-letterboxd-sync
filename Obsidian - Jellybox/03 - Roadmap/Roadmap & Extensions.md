---
tags:
  - jellybox
  - roadmap
  - features
  - backlog
  - tmdb
  - letterboxd
---

# Roadmap & Extensions: Jellybox Evolution 🚀

This document acts as a development backlog and feature planning note. It outlines milestones for turning this plugin into a full-featured media-tracking suite.

---

## 🗺️ Roadmap Backlog

### Milestone 1: Robust Metadata Matching (High Priority)
- [ ] **TMDb/IMDb ID Integration**:
  - Scraping the specific film detail page (or parsing a Letterboxd data export CSV) to extract TMDb/IMDb ID.
  - Querying Jellyfin's provider IDs directly for $100\%$ accurate matching, bypassing title spelling discrepancies.
- [ ] **Alternative Title Handling**:
  - Support matching localized/translated movie titles stored in Jellyfin (e.g. matching `Le Voyage de Chihiro` to `Spirited Away`).

### Milestone 2: Bidirectional Watched-State Sync (Medium Priority)
- [ ] **Jellyfin $\rightarrow$ Letterboxd Played Logging**:
  - Automatically log movies to Letterboxd when marked "played" / "watched" in Jellyfin.
  - This requires either:
    - Simulating user authentication (session cookies).
    - Integrating a custom webhook/RSS agent.
- [ ] **Letterboxd $\rightarrow$ Jellyfin Played Importing**:
  - Automatically mark items as "played" in Jellyfin if they are logged or marked watched on Letterboxd.

### Milestone 3: Arbitrary Lists Sync (Medium Priority)
- [ ] **User Lists Support**:
  - Sync not only the *Watchlist*, but custom public lists or user favorites/likes.
  - Create corresponding custom playlists in Jellyfin.
- [ ] **Configurable Schedules per List**:
  - Let users configure independent sync intervals for different lists.

### Milestone 4: Config Dashboard UI Enhancements (Low Priority)
- [ ] **Sync Progress Logs**:
  - Display matched vs unmatched items in the config page UI.
  - Provide direct links to search for unmatched items in Jellyfin or Letterboxd.
- [ ] **Manual Sync Trigger Button**:
  - Include an interactive "Sync Now" button inside the Plugin config UI (bypassing the need to go to Jellyfin's Scheduled Tasks menu).

---

## 📦 Adding Other Plugins

As part of the **Jellybox** ecosystem, we plan to incorporate other sync services:
- **Obsidian Sync Plugin**: Automatically push Jellyfin watch statistics or lists directly into Obsidian markdown files (e.g., creating a daily/weekly viewing journal note).
- **Trakt.tv Integration**: Include side-by-side support for Trakt lists and history.
- **Anilist/MyAnimeList Sync**: Track anime progress from Jellyfin back to specialized tracking platforms.

Let's discuss which milestone to execute first!
