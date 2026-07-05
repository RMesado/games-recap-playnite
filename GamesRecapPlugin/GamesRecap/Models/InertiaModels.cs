using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GamesRecap.Models
{
    [DataContract]
    public class InertiaResponse
    {
        [DataMember(Name = "component")]
        public string Component { get; set; }

        [DataMember(Name = "props")]
        public HomeProps Props { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    public class HomeProps
    {
        [DataMember(Name = "features")]
        public Features Features { get; set; }

        [DataMember(Name = "cards")]
        public PaginatedData Pages { get; set; }

        [DataMember(Name = "filters")]
        public ActiveFilters Filters { get; set; }

        [DataMember(Name = "options")]
        public FilterOptions Options { get; set; }

        [DataMember(Name = "upcomingShowcases")]
        public List<UpcomingShowcase> UpcomingShowcases { get; set; } = new List<UpcomingShowcase>();

        [DataMember(Name = "hiddenMatchingCount")]
        public int HiddenMatchingCount { get; set; }

        [DataMember(Name = "topSpotlights")]
        public List<TopSpotlight> TopSpotlights { get; set; } = new List<TopSpotlight>();
    }

    [DataContract]
    public class Features
    {
        [DataMember(Name = "userAccounts")]
        public bool UserAccounts { get; set; }
    }

    [DataContract]
    public class PaginatedData
    {
        [DataMember(Name = "current_page")]
        public int CurrentPage { get; set; }

        [DataMember(Name = "data")]
        public List<Card> Data { get; set; } = new List<Card>();

        [DataMember(Name = "from")]
        public int? From { get; set; }

        [DataMember(Name = "last_page")]
        public int LastPage { get; set; }

        [DataMember(Name = "per_page")]
        public int PerPage { get; set; }

        [DataMember(Name = "total")]
        public int Total { get; set; }

        [DataMember(Name = "next_page_url")]
        public string NextPageUrl { get; set; }

        [DataMember(Name = "prev_page_url")]
        public string PrevPageUrl { get; set; }
    }

    [DataContract]
    public class Card
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "showcase_id")]
        public int ShowcaseId { get; set; }

        [DataMember(Name = "game_id")]
        public int GameId { get; set; }

        [DataMember(Name = "sort_at")]
        public string SortAt { get; set; }

        [DataMember(Name = "is_draft")]
        public bool IsDraft { get; set; }

        [DataMember(Name = "game")]
        public GrGame Game { get; set; }

        [DataMember(Name = "showcase")]
        public Showcase Showcase { get; set; }

        [DataMember(Name = "media")]
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();

        [DataMember(Name = "tags")]
        public List<GrTag> Tags { get; set; } = new List<GrTag>();
    }

    [DataContract]
    public class GrGame
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "release_date")]
        public string ReleaseDate { get; set; }

        [DataMember(Name = "cover_image_url")]
        public string CoverImageUrl { get; set; }

        [DataMember(Name = "screenshot_url")]
        public string ScreenshotUrl { get; set; }

        [DataMember(Name = "igdb_id")]
        public int? IgdbId { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "is_draft")]
        public bool IsDraft { get; set; }

        [DataMember(Name = "publisher")]
        public Company Publisher { get; set; }

        [DataMember(Name = "developers")]
        public List<Company> Developers { get; set; } = new List<Company>();

        [DataMember(Name = "platforms")]
        public List<GrPlatform> Platforms { get; set; } = new List<GrPlatform>();

        [DataMember(Name = "genres")]
        public List<GrGenre> Genres { get; set; } = new List<GrGenre>();

        [DataMember(Name = "tags")]
        public List<GrTag> Tags { get; set; } = new List<GrTag>();

        [DataMember(Name = "release_windows")]
        public List<ReleaseWindow> ReleaseWindows { get; set; } = new List<ReleaseWindow>();
    }

    [DataContract]
    public class GrPlatform
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "pivot")]
        public PivotData Pivot { get; set; }
    }

    [DataContract]
    public class GrGenre
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }
    }

    [DataContract]
    public class GrTag
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "icon")]
        public string Icon { get; set; }

        [DataMember(Name = "color")]
        public string Color { get; set; }

        [DataMember(Name = "auto_apply_rule")]
        public string AutoApplyRule { get; set; }

        [DataMember(Name = "scope")]
        public string Scope { get; set; }
    }

    [DataContract]
    public class PivotData
    {
        [DataMember(Name = "game_id")]
        public int? GameId { get; set; }

        [DataMember(Name = "platform_id")]
        public int? PlatformId { get; set; }

        [DataMember(Name = "release_date")]
        public string ReleaseDate { get; set; }
    }

    [DataContract]
    public class ReleaseWindow
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "game_id")]
        public int GameId { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; }

        [DataMember(Name = "date")]
        public string Date { get; set; }

        [DataMember(Name = "label")]
        public string Label { get; set; }

        [DataMember(Name = "label_start")]
        public string LabelStart { get; set; }

        [DataMember(Name = "label_end")]
        public string LabelEnd { get; set; }

        [DataMember(Name = "platform_ids")]
        public List<int> PlatformIds { get; set; }

        [DataMember(Name = "display_order")]
        public int DisplayOrder { get; set; }
    }

    [DataContract]
    public class Company
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }
    }

    [DataContract]
    public class Showcase
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "series_key")]
        public string SeriesKey { get; set; }

        [DataMember(Name = "start_at")]
        public string StartAt { get; set; }

        [DataMember(Name = "end_at")]
        public string EndAt { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "event_name")]
        public string EventName { get; set; }

        [DataMember(Name = "event_id")]
        public int? EventId { get; set; }
    }

    [DataContract]
    public class UpcomingShowcase
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }

        [DataMember(Name = "start_at")]
        public string StartAt { get; set; }

        [DataMember(Name = "end_at")]
        public string EndAt { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "event_name")]
        public string EventName { get; set; }
    }

    [DataContract]
    public class MediaItem
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "is_unavailable")]
        public bool IsUnavailable { get; set; }
    }

    [DataContract]
    public class TopSpotlight
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "icon_url")]
        public string IconUrl { get; set; }

        [DataMember(Name = "text")]
        public string Text { get; set; }

        [DataMember(Name = "link_url")]
        public string LinkUrl { get; set; }
    }

    [DataContract]
    public class ActiveFilters
    {
        [DataMember(Name = "q")]
        public string Q { get; set; }

        [DataMember(Name = "platforms")]
        public List<int> Platforms { get; set; } = new List<int>();

        [DataMember(Name = "exclude_platforms")]
        public List<int> ExcludePlatforms { get; set; } = new List<int>();

        [DataMember(Name = "genres")]
        public List<int> Genres { get; set; } = new List<int>();

        [DataMember(Name = "exclude_genres")]
        public List<int> ExcludeGenres { get; set; } = new List<int>();

        [DataMember(Name = "tags")]
        public List<int> Tags { get; set; } = new List<int>();

        [DataMember(Name = "exclude_tags")]
        public List<int> ExcludeTags { get; set; } = new List<int>();

        [DataMember(Name = "showcases")]
        public List<int> Showcases { get; set; } = new List<int>();

        [DataMember(Name = "hidden_ids")]
        public List<int> HiddenIds { get; set; } = new List<int>();

        [DataMember(Name = "seen_ids")]
        public List<int> SeenIds { get; set; } = new List<int>();

        [DataMember(Name = "seen_mode")]
        public string SeenMode { get; set; }

        [DataMember(Name = "wishlisted_ids")]
        public List<int> WishlistedIds { get; set; } = new List<int>();

        [DataMember(Name = "wishlisted_mode")]
        public string WishlistedMode { get; set; }

        [DataMember(Name = "page")]
        public int? Page { get; set; }

        [DataMember(Name = "release_from")]
        public string ReleaseFrom { get; set; }

        [DataMember(Name = "release_to")]
        public string ReleaseTo { get; set; }

        [DataMember(Name = "sort")]
        public string Sort { get; set; }

        [DataMember(Name = "view")]
        public string View { get; set; }
    }

    [DataContract]
    public class FilterOptions
    {
        [DataMember(Name = "platforms")]
        public List<OptionItem> Platforms { get; set; } = new List<OptionItem>();

        [DataMember(Name = "genres")]
        public List<OptionItem> Genres { get; set; } = new List<OptionItem>();

        [DataMember(Name = "tags")]
        public List<OptionItem> Tags { get; set; } = new List<OptionItem>();

        [DataMember(Name = "showcases")]
        public List<Showcase> Showcases { get; set; } = new List<Showcase>();

        [DataMember(Name = "sorts")]
        public List<SortOption> Sorts { get; set; } = new List<SortOption>();
    }

    [DataContract]
    public class OptionItem
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "slug")]
        public string Slug { get; set; }
    }

    public class ReleaseWindowDisplay
    {
        public string Date { get; set; }
        public string Platforms { get; set; }
    }

    [DataContract]
    public class SortOption
    {
        [DataMember(Name = "value")]
        public string Value { get; set; }

        [DataMember(Name = "label")]
        public string Label { get; set; }
    }
}
