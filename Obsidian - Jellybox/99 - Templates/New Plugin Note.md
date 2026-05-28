---
tags:
  - jellybox
  - template
  - plugin
---

# {Plugin Name} — Technical Architecture

> [!NOTE]
> This is a template. Duplicate this note into `01 - Plugins/{Plugin Name}/` and fill in the sections below.

---

## 📂 Component Layout

```
{PluginName}/
├── Plugin.cs
├── Configuration/
│   ├── PluginConfiguration.cs
│   └── configPage.html
└── ScheduledTasks/
    └── {TaskName}.cs
```

---

## 🔑 Core Classes

### `Plugin.cs`
- **Guid**: `{guid}`
- **Purpose**: {description}

### `PluginConfiguration.cs`
- **Settings**:
  - `{Setting1}` ({type}): {description}

### `{TaskName}.cs`
- **Implements**: `IScheduledTask`
- **Dependencies**: {list injected services}
- **Default Trigger**: {interval}

---

## 🔍 Key Logic

{Describe the core algorithm, matching logic, or sync workflow here.}

---

## 🔗 Related Notes

- [[Plugins Index]]
- [[Design Decisions]]
- [[Roadmap & Extensions]]
