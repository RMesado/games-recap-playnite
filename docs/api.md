# API Reference: gamesrecap.io

## Overview

GamesRecap uses **Inertia.js**, not a conventional REST API. The server returns HTML or JSON depending on request headers.

## Required Headers

```http
GET https://gamesrecap.io/
X-Inertia: true
X-Inertia-Version: <hash>
Accept: application/json
```

- Without `X-Inertia: true` → server returns HTML
- Without `X-Inertia-Version` → server may force a full reload
- The version value must be cached in SQLite (`AppMeta` table) and updated with each successful response
- On **409 Conflict** (outdated version), the new version comes from the `version` field of the JSON response, not HTTP headers

## Single Endpoint

All functionality is accessed from a single endpoint:

```
GET https://gamesrecap.io/
```

Query parameters handle filtering, pagination and search.

## Response Structure

```json
{
  "component": "home",
  "props": {
    "features": { "userAccounts": false },
    "cards": { ...pagination + data },
    "filters": { ...active filters },
    "options": { ...available options },
    "upcomingShowcases": [ ... ],
    "hiddenMatchingCount": 0
  },
  "version": "91c5bce49007757d62740bf9f1aacac6"
}
```

## Card (content unit)

Each entry in `props.cards.data` represents a game presented in a specific showcase:

```json
{
  "id": 6268,
  "showcase_id": 300,
  "game_id": 4360,
  "sort_at": "2026-06-07T19:00:00Z",
  "is_draft": false,
  "game": { ... },
  "showcase": { ... },
  "media": [ ... ],
  "tags": [ ... ]
}
```

### Game object

| Field | Type | Notes |
|-------|------|-------|
| id | int | Game ID on gamesrecap.io |
| title | string | Game title |
| slug | string | URL slug |
| release_date | string | `yyyy-MM-dd` or null |
| cover_image_url | string | IGDB cover URL |
| screenshot_url | string | IGDB screenshot URL |
| igdb_id | int | IGDB identifier |
| kind | string | `game`, `dlc`, `update`, `expansion` |
| publisher | object | Publisher info |
| developers | array | Developer list |
| platforms | array | Platform list with pivot release dates |
| genres | array | Genre list |
| tags | array | Tag list (with icon, color, scope) |
| release_windows | array | Release dates per platform |

## Showcase

Showcases appear in **two contexts** with different fields:

1. **`options.showcases`** (filters): `id`, `name`, `event_name`, `start_at`, `series_key`, `series_label` — no `slug`
2. **`card.showcase`** (card association): `id`, `name`, `slug`, `series_key`, `start_at`, `end_at`, `url`, `event_name`, `event_id`

The `series_key` field is the historical grouping key: all "Xbox Games Showcase" across years share `series_key: "xbox"`. It allows filtering by showcase franchise regardless of year.

## Query Parameters

All filters are query parameters on the root endpoint `/`.

| Parameter | Type | Example | Description |
|-----------|------|---------|-------------|
| `q` | string | `q=control` | Text search |
| `platforms` | int[] | `platforms=1,2` | Include platforms (AND) |
| `exclude_platforms` | int[] | `exclude_platforms=6,7` | Exclude platforms |
| `genres` | int[] | `genres=1,23` | Include genres |
| `exclude_genres` | int[] | | Exclude genres |
| `tags` | int[] | `tags=1,5` | Include tags |
| `exclude_tags` | int[] | | Exclude tags |
| `showcases` | int[] | `showcases=300,280` | Filter by showcase(s) |
| `sort` | string | `sort=newest` | Sorting |
| `page` | int | `page=2` | Page number |
| `seen_ids` | int[] | | IDs marked as seen |
| `seen_mode` | string | | `only` or `exclude` |
| `release_from` | date | `release_from=2026-01-01` | Release date from |
| `release_to` | date | | Release date to |
| `hidden_ids` | int[] | | IDs marked as hidden |

### Special HTTP Headers

| Header | Type | Description |
|--------|------|-------------|
| `X-Wishlisted-Ids` | string | Wishlist IDs from local SQLite, comma-separated |
| `X-Wishlisted-Mode` | string | `only` or `exclude` |

**Note:** `wishlisted_ids` and `wishlisted_mode` are sent as HTTP headers, not query parameters.

## Sort Values

| Value | Description |
|-------|-------------|
| `newest` | Most recent first |
| `oldest` | Oldest first |
| `title_asc` | Title A→Z |
| `title_desc` | Title Z→A |
| `media_desc` | Most media first |
| `media_asc` | Least media first |
| `random` | Random |

## Complete Request Example

```http
GET https://gamesrecap.io/?q=control&platforms=1,2&genres=1,23&showcases=300,280&sort=newest&page=1
X-Inertia: true
X-Inertia-Version: 91c5bce49007757d62740bf9f1aacac6
X-Wishlisted-Ids: 4360,4874
X-Wishlisted-Mode: only
Accept: application/json
```
