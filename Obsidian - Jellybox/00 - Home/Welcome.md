---
tags:
  - jellybox
  - vault
  - index
---

# Welcome to the Jellybox Vault 🗂️

Welcome to your developer documentation vault for **Jellybox**. This vault is designed to organize, document, and plan the architecture and features of the Jellybox plugin suite.

## 📂 Vault Structure

```
Obsidian - Jellybox/
├── 00 - Home/                        # Project-level overview & this welcome page
│   ├── Welcome.md                    ← You are here
│   └── Project Overview.md
├── 01 - Plugins/                     # One sub-folder per Jellyfin plugin
│   ├── Plugins Index.md
│   └── Letterboxd Sync/
│       ├── Technical Architecture.md
│       └── Changelog.md
├── 02 - Design/                      # Design decisions & ADRs
│   ├── Design Index.md
│   └── Design Decisions.md
├── 03 - Roadmap/                     # Feature backlog & milestones
│   ├── Roadmap Index.md
│   └── Roadmap & Extensions.md
└── 99 - Templates/                   # Reusable note templates
    └── New Plugin Note.md
```

## 🧭 Quick Navigation

| Folder | Purpose | Start Here |
|---|---|---|
| **00 - Home** | Project identity & high-level goals | [[Project Overview]] |
| **01 - Plugins** | Per-plugin technical docs & changelogs | [[Plugins Index]] |
| **02 - Design** | Architectural decisions & specs | [[Design Decisions]] |
| **03 - Roadmap** | Feature backlog & sprint planning | [[Roadmap & Extensions]] |
| **99 - Templates** | Reusable note scaffolds | [[New Plugin Note]] |

## 💡 Conventions

- **Tags**: Every note has YAML frontmatter tags for the [TagFolder](obsidian://show-plugin?id=obsidian-tagfolder) plugin. Use `jellybox` as a universal tag, then add context-specific tags.
- **Wikilinks**: Use `[[Note Name]]` to cross-reference between notes. Obsidian resolves them regardless of folder depth.
- **Task Tracking**: Use markdown checklists (`- [ ]`) for features and milestones.
- **Git-friendly**: `.obsidian/` is gitignored. Only markdown content is tracked.
