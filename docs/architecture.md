# Project Architecture

## Overview

**Games Recap** is a **GenericPlugin** for [Playnite](https://playnite.link) that integrates [gamesrecap.io](https://gamesrecap.io) as a game discovery source. It provides a browser for games announced at showcases and gaming conferences, with a local wishlist and calendar, plus optional sync with the Playnite library.

## Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET Framework 4.8.1, C# 12.0, WPF |
| Serialization | `DataContractJsonSerializer` (System.Runtime.Serialization) |
| JSON Convention | `[DataMember(Name = "snake_case")]` |
| Database | System.Data.SQLite (System.Data.SQLite.Core.NetFramework 1.0.119.0) |
| SDK | PlayniteSDK 6.15.0 |
| Build | MSBuild 17.14 (Build Tools VS 2022) |
| Icon | SVG → PNG 128×128, color #ff506e |

## Project Structure

```
GamesRecapPlugin/GamesRecap/
├── GamesRecap.cs                   # Entry point, sidebar views (browser + calendar)
├── GamesRecap.csproj               # Target v4.8.1, LangVersion 12.0
├── GamesRecapSettings.cs           # Settings viewmodel + Loc helper
├── GamesRecapSettingsView.xaml/.cs # Settings UI
├── extension.yaml                  # Plugin manifest (Type: GenericPlugin)
├── icon.svg / icon.png             # #ff506e icon
├── icon-calendar.svg / icon-calendar.png  # Orange calendar icon
├── AGENTS.md                       # Agent context (for AI coding assistants)
├── Models/
│   ├── InertiaModels.cs            # 20 DTOs with DataContract/DataMember
│   └── MetadataFieldConfig.cs      # Metadata source configuration
├── Services/
│   ├── GamesRecapApiClient.cs      # HTTP client with Inertia headers
│   ├── LocalDatabase.cs            # SQLite: UserGameState + AppMeta + PromotedGames + CalendarGames
│   └── PlayniteLibrarySync.cs      # Playnite library sync (priority-based metadata)
├── ViewModels/
│   ├── BrowserViewModel.cs         # Browser logic: filters, grid, wishlist, calendar
│   └── CalendarViewModel.cs        # Calendar logic: 3 sections, weekly grid
├── Views/
│   ├── BrowserView.xaml/.cs        # Main view (cards, filters, pagination)
│   └── CalendarView.xaml/.cs       # Calendar view (last month, week, upcoming)
├── Resources/
│   ├── BadgeIcons.xaml             # SVG icons for badges
│   └── SharedResources.xaml        # Shared templates (standard tooltip)
├── Localization/
│   ├── es_ES.xaml                  # Spanish localization (primary)
│   └── en_US.xaml                  # English localization (fallback)
└── Data/schema.sql                 # Reference schema
```

## Plugin Type: GenericPlugin

Initially implemented as `LibraryPlugin`, it was migrated to `GenericPlugin` because:

- No auto-sync from external sources (like Steam, GOG)
- Games are added exclusively via manual `ImportGame(GameMetadata)`
- No `GetGames()`, `HasCustomizedGameImport` or `LibraryClient` needed
- The "Source" field is set via `metadata.Source = new MetadataNameProperty("Games Recap")`

**Main advantage**: Playnite does not call the plugin during startup import, eliminating metadata re-download on restart.

## User Flow

```
Plugin opened (main view: browser)
│
├── Header (back, refresh, wishlist filter, calendar filter)
├── Filters sidebar (text search, showcase, platform, genre, tag, date range, sort)
├── Progress bar (animated, #ff506e, during API load)
└── Card grid (24 per page, WrapPanel)
    ├── Cover/Screenshot (IGDB)
    ├── Tags with icons and colors (top-left)
    ├── "In Library" badge (green)
    ├── "In Calendar" badge (orange)
    ├── Origin showcase + date
    ├── Title + kind badge (DLC, Update...)
    ├── Release date (per-platform if release_windows)
    ├── FRONT: Wishlist star / Flip button
    └── BACK: Trailer button, Calendar button, Add to Library button
```

## Data Flow

```
User actions → BrowserViewModel
    ↓
LocalDatabase (SQLite)         GamesRecapApiClient (HTTP Inertia)
    ↓                                ↓
UserGameState, AppMeta,         gamesrecap.io API
PromotedGames, CalendarGames
    ↓
PlayniteLibrarySync → Playnite API (ImportGame, Games.Update)
```
