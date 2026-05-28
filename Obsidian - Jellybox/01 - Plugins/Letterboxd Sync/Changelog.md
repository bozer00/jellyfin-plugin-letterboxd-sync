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
