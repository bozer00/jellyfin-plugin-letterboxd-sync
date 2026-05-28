---
tags:
  - jellybox
  - letterboxd
  - plugin
  - changelog
---

# Changelog: Letterboxd Sync Plugin

All notable changes to the Letterboxd Sync plugin are documented here.

---

## [2.0.0.0] — 2026-05-28

### Added
- Scraper to resolve TMDb/IMDb IDs from Letterboxd film detail pages.
- Local JSON cache file (`LetterboxdCache.json`) stored in configuration directory to cache mappings and avoid rate limits.
- 3-tier cascade matching: TMDb ID → IMDb ID → Fuzzy title + year fallback.
- Match results panel showing total watchlist, matched count, and unmatched films (with links to Letterboxd).
- "Sync Now" button in the admin config page.
- Configurable sync interval setting (1h, 6h, 12h, 24h).
- Performance optimization: Loads target movies into memory once at task execution start rather than looping database queries.

### Changed
- Target framework updated to `.NET 10.0` for compatibility with Jellyfin 10.11.x APIs.

---

## [1.0.2.0] — 2026-05-26

### Changed
- Refactored HTML parser to match modern React-based Letterboxd markup (`LazyPoster` components with `data-item-slug` / `data-item-name` attributes).
- Added legacy fallback parser for older class-based `film-poster` markup.

---

## [1.0.1.0] — 2026-05-26

### Changed
- Bumped version to force Jellyfin update detection.

---

## [1.0.0.0] — 2026-05-26

### Added
- Initial release.
- Watchlist scraping via paginated HTTP requests to `letterboxd.com`.
- Fuzzy title + year matching against Jellyfin library.
- Two sync modes: Append Only and Full Sync.
- Glassmorphism-styled configuration page embedded in the Jellyfin admin dashboard.
- Configurable Jellyfin user target and playlist name.
- Automatic 12-hour scheduled background task.
