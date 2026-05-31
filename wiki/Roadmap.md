# Jellybox Project Roadmap 🚀

This roadmap outlines the milestones and backlog for the Jellybox integration suite.

---

## 🗺️ Milestones & Progress

### 1. Robust Metadata Matching (Completed)
* **ID Scraper**: Extract TMDb and IMDb IDs from Letterboxd film detail pages.
* **Cascading Matches**: Priority-based matching (TMDb ID → IMDb ID → Normalized title + year check).
* **Caching**: Local caching of slug-to-ID mappings to respect Letterboxd rate limits.
* **Alternative Titles**: Support matching against both Jellyfin display names and original titles (handling localized titles).

### 2. Letterboxd Ratings (Completed)
* **Metadata Scraper**: Extract average community ratings via meta tags and JSON-LD fallbacks.
* **In-place Custom Provider**: Implemented using `ICustomMetadataProvider` running after remote scrapers to ensure ratings persist on Jellyfin.
* **Flexible Mapping**: Map Letterboxd ratings directly to Community Rating, Critic Rating, or both.

### 3. Bidirectional Watched-State Sync (Planned)
* **Jellyfin → Letterboxd**: Automatically log movies or mark them as watched on Letterboxd when they are played in Jellyfin.
* **Letterboxd → Jellyfin**: Mark local movies as watched in Jellyfin when they are marked as played on Letterboxd.

### 4. Custom Lists & Schedules (Planned)
* **Arbitrary Lists**: Support syncing custom public lists and user favorites in addition to the watchlist.
* **List-Specific Intervals**: Let users configure independent sync intervals for different lists.
* **Enhanced Dashboard**: Expand the dashboard panel to show status/reports for all configured lists.
