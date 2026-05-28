---
tags:
  - jellybox
  - plugins
  - index
---

# 🔌 Plugins Index

This folder contains one sub-folder per Jellyfin plugin developed under the Jellybox project. Each plugin folder holds its own technical architecture, changelog, and implementation notes.

## Active Plugins

| Plugin | Status | Description |
|---|---|---|
| [[Technical Architecture\|Letterboxd Sync]] | 🟢 Active | Syncs a user's public Letterboxd watchlist to a Jellyfin playlist using TMDb/IMDb ID matching. |

## Future Plugins (Planned)

| Plugin | Status | Description |
|---|---|---|
| Trakt Sync | 🔴 Not started | Sync Trakt.tv watch history & lists to Jellyfin. |
| AniList Sync | 🔴 Not started | Sync AniList anime progress to Jellyfin. |

## Adding a New Plugin

When starting a new plugin, create a sub-folder here named after the plugin and add at minimum:
1. A `Technical Architecture.md` describing the plugin's class structure, APIs, and logic.
2. A `Changelog.md` to track version history.

Use the [[New Plugin Note]] template from `99 - Templates` to get started.
