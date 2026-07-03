using GamesRecap.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Web.Script.Serialization;

namespace GamesRecap.Services
{
    public class PlayniteLibrarySync
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly HttpClient validationHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static readonly Dictionary<string, MetadataField> JsonFieldMapping = new()
        {
            ["Name"] = MetadataField.Name,
            ["Genre"] = MetadataField.Genres,
            ["ReleaseDate"] = MetadataField.ReleaseDate,
            ["Developer"] = MetadataField.Developers,
            ["Publisher"] = MetadataField.Publishers,
            ["Tag"] = MetadataField.Tags,
            ["Feature"] = MetadataField.Features,
            ["Description"] = MetadataField.Description,
            ["CoverImage"] = MetadataField.CoverImage,
            ["BackgroundImage"] = MetadataField.BackgroundImage,
            ["Icon"] = MetadataField.Icon,
            ["Links"] = MetadataField.Links,
            ["CriticScore"] = MetadataField.CriticScore,
            ["CommunityScore"] = MetadataField.CommunityScore,
            ["AgeRating"] = MetadataField.AgeRating,
            ["Series"] = MetadataField.Series,
            ["Region"] = MetadataField.Region,
            ["Platform"] = MetadataField.Platform,
            ["InstallSize"] = MetadataField.InstallSize,
        };

        private static readonly string[] FieldProcessingOrder =
        {
            "Name", "Genre", "ReleaseDate", "Developer", "Publisher", "Tag", "Feature",
            "Description", "CoverImage", "BackgroundImage", "Icon", "Links",
            "CriticScore", "CommunityScore", "AgeRating", "Series", "Region",
            "Platform", "InstallSize"
        };

        public void AddToLibrary(int gameId, string title, int? igdbId, IPlayniteAPI api, LocalDatabase db)
        {
            var existingPlayniteId = db.GetPlayniteId(gameId);
            if (!string.IsNullOrEmpty(existingPlayniteId))
                return;

            try
            {
                var metadata = new GameMetadata
                {
                    GameId = $"gr-{gameId}",
                    Name = title,
                    Source = new MetadataNameProperty("Games Recap"),
                    Tags = new HashSet<MetadataProperty>
                    {
                        new MetadataNameProperty("Wishlist")
                    },
                    IsInstalled = false
                };

                var playniteGame = api.Database.ImportGame(metadata);
                var playniteId = playniteGame?.Id.ToString();

                if (string.IsNullOrEmpty(playniteId))
                    return;

                db.SetPlayniteId(gameId, playniteId);

                var tagsJson = SerializeList(new List<string> { "Wishlist" });
                db.UpsertPromotedGame(gameId, title, null,
                    null, null, tagsJson,
                    null, playniteId);

                var metadataPlugins = api.Addons.Plugins.OfType<MetadataPlugin>().ToList();
                logger.Info($"Found {metadataPlugins.Count} metadata plugins for '{title}'");

                api.Dialogs.ActivateGlobalProgress((progress) =>
                {
                    DownloadMetadataForGame(api, playniteGame.Id, igdbId, metadataPlugins);

                }, new GlobalProgressOptions($"Downloading metadata for '{title}'...")
                {
                    IsIndeterminate = true
                });

                logger.Info($"Added '{title}' (gr-{gameId}) to Playnite library with Id {playniteId}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to add '{title}' to Playnite library");
                throw;
            }
        }

        private void DownloadMetadataForGame(IPlayniteAPI api, Guid playniteGameId, int? igdbId, List<MetadataPlugin> metadataPlugins)
        {
            var game = api.Database.Games.Get(playniteGameId);
            if (game == null) return;

            var fieldArgs = new GetMetadataFieldArgs();
            var hasUpdates = false;

            Dictionary<string, MetadataFieldSetting> fieldPriorities = null;
            try
            {
                var configPath = Path.Combine(api.Paths.ConfigurationPath, "config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath, Encoding.UTF8);
                    var jss = new JavaScriptSerializer();
                    var config = jss.Deserialize<PlayniteConfigRoot>(json);
                    fieldPriorities = config?.MetadataSettings;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read MetadataSettings from config.json");
            }

            if (fieldPriorities == null || fieldPriorities.Count == 0)
            {
                logger.Warn("No metadata source priorities found in Playnite config");
                return;
            }

            var providerCache = new Dictionary<Guid, (OnDemandMetadataProvider provider, HashSet<MetadataField> fields)>();
            var disposables = new List<IDisposable>();

            try
            {
                foreach (var fieldName in FieldProcessingOrder)
                {
                    if (!fieldPriorities.TryGetValue(fieldName, out var fieldConfig)) continue;
                    if (!fieldConfig.Import) continue;
                    if (!JsonFieldMapping.TryGetValue(fieldName, out var field)) continue;

                    foreach (var sourceGuidStr in fieldConfig.Sources)
                    {
                        if (!Guid.TryParse(sourceGuidStr, out var guid)) continue;
                        if (guid == Guid.Empty) continue;

                        var plugin = metadataPlugins.FirstOrDefault(p => p.Id == guid);
                        if (plugin == null) continue;

                        if (!providerCache.TryGetValue(guid, out var cached))
                        {
                            var requestGame = new Game
                            {
                                Name = game.Name,
                                GameId = igdbId?.ToString(),
                                PluginId = game.PluginId,
                                ReleaseDate = game.ReleaseDate
                            };

                            var options = new MetadataRequestOptions(requestGame, true);
                            var provider = plugin.GetMetadataProvider(options);
                            if (provider == null) continue;

                            disposables.Add(provider);
                            var available = provider.AvailableFields != null
                                ? new HashSet<MetadataField>(provider.AvailableFields)
                                : new HashSet<MetadataField>();
                            cached = (provider, available);
                            providerCache[guid] = cached;
                            logger.Info($"  {plugin.Name}: {available.Count} field(s) available");
                        }

                        if (!cached.fields.Contains(field)) continue;

                        if (TryApplyField(cached.provider, field, fieldArgs, api, game))
                        {
                            hasUpdates = true;
                            break;
                        }
                    }
                }

                if (hasUpdates)
                {
                    game.Modified = DateTime.Now;
                    api.Database.Games.Update(game);
                    logger.Info($"Metadata saved for '{game.Name}'");
                }
            }
            finally
            {
                foreach (var d in disposables)
                    d.Dispose();
            }

            var dbGame = api.Database.Games.Get(game.Id);
            logger.Info($"After metadata: game='{game.Name}', cover={!string.IsNullOrEmpty(dbGame?.CoverImage)}, desc={!string.IsNullOrEmpty(dbGame?.Description)}");
        }

        private static bool TryApplyField(OnDemandMetadataProvider provider, MetadataField field, GetMetadataFieldArgs args, IPlayniteAPI api, Game game)
        {
            switch (field)
            {
                case MetadataField.Name:
                    var nameVal = provider.GetName(args);
                    if (string.IsNullOrEmpty(nameVal)) return false;
                    if (nameVal != game.Name) game.Name = nameVal;
                    return true;

                case MetadataField.Genres:
                    var genres = provider.GetGenres(args);
                    if (genres == null) return false;
                    var genreIds = api.Database.Genres.Add(genres).Select(g => g.Id).ToList();
                    if (genreIds.Count == 0) return false;
                    game.GenreIds = genreIds;
                    return true;

                case MetadataField.ReleaseDate:
                    var releaseDate = provider.GetReleaseDate(args);
                    if (!releaseDate.HasValue) return false;
                    game.ReleaseDate = releaseDate.Value;
                    return true;

                case MetadataField.Developers:
                    var devs = provider.GetDevelopers(args);
                    if (devs == null) return false;
                    var devIds = api.Database.Companies.Add(devs).Select(c => c.Id).ToList();
                    if (devIds.Count == 0) return false;
                    game.DeveloperIds = devIds;
                    return true;

                case MetadataField.Publishers:
                    var pubs = provider.GetPublishers(args);
                    if (pubs == null) return false;
                    var pubIds = api.Database.Companies.Add(pubs).Select(c => c.Id).ToList();
                    if (pubIds.Count == 0) return false;
                    game.PublisherIds = pubIds;
                    return true;

                case MetadataField.Tags:
                    var tags = provider.GetTags(args);
                    if (tags == null) return false;
                    var tagIds = api.Database.Tags.Add(tags).Select(t => t.Id).ToList();
                    if (tagIds.Count == 0) return false;
                    var wishlistTag = api.Database.Tags.FirstOrDefault(t => t.Name == "Wishlist");
                    if (wishlistTag != null && !tagIds.Contains(wishlistTag.Id))
                        tagIds.Insert(0, wishlistTag.Id);
                    game.TagIds = tagIds;
                    return true;

                case MetadataField.Description:
                    var descVal = provider.GetDescription(args);
                    if (string.IsNullOrEmpty(descVal)) return false;
                    game.Description = descVal;
                    return true;

                case MetadataField.CoverImage:
                    var cover = provider.GetCoverImage(args);
                    if (cover == null || cover.Path == null) return false;
                    if (!IsImageUrlValid(cover.Path))
                    {
                        logger.Warn($"Cover image not available: {cover.Path}");
                        return false;
                    }
                    game.CoverImage = cover.Path;
                    return true;

                case MetadataField.BackgroundImage:
                    var bg = provider.GetBackgroundImage(args);
                    if (bg == null || bg.Path == null) return false;
                    if (!IsImageUrlValid(bg.Path))
                    {
                        logger.Warn($"Background image not available: {bg.Path}");
                        return false;
                    }
                    game.BackgroundImage = bg.Path;
                    return true;

                case MetadataField.Icon:
                    var icon = provider.GetIcon(args);
                    if (icon == null || icon.Path == null) return false;
                    game.Icon = icon.Path;
                    return true;

                case MetadataField.Links:
                    var links = provider.GetLinks(args)?.ToList();
                    if (links == null || links.Count == 0) return false;
                    game.Links = new ObservableCollection<Link>(links);
                    return true;

                case MetadataField.Features:
                    var features = provider.GetFeatures(args);
                    if (features == null) return false;
                    var featIds = api.Database.Features.Add(features).Select(f => f.Id).ToList();
                    if (featIds.Count == 0) return false;
                    game.FeatureIds = featIds;
                    return true;

                case MetadataField.Platform:
                    var platforms = provider.GetPlatforms(args);
                    if (platforms == null) return false;
                    var platIds = api.Database.Platforms.Add(platforms).Select(p => p.Id).ToList();
                    if (platIds.Count == 0) return false;
                    game.PlatformIds = platIds;
                    return true;

                case MetadataField.Series:
                    var series = provider.GetSeries(args);
                    if (series == null) return false;
                    var seriesIds = api.Database.Series.Add(series).Select(s => s.Id).ToList();
                    if (seriesIds.Count == 0) return false;
                    game.SeriesIds = seriesIds;
                    return true;

                case MetadataField.AgeRating:
                    var ratings = provider.GetAgeRatings(args);
                    if (ratings == null) return false;
                    var ratingIds = api.Database.AgeRatings.Add(ratings).Select(r => r.Id).ToList();
                    if (ratingIds.Count == 0) return false;
                    game.AgeRatingIds = ratingIds;
                    return true;

                case MetadataField.Region:
                    var regions = provider.GetRegions(args);
                    if (regions == null) return false;
                    var regionIds = api.Database.Regions.Add(regions).Select(r => r.Id).ToList();
                    if (regionIds.Count == 0) return false;
                    game.RegionIds = regionIds;
                    return true;

                case MetadataField.CommunityScore:
                    var cs = provider.GetCommunityScore(args);
                    if (!cs.HasValue) return false;
                    game.CommunityScore = cs.Value;
                    return true;

                case MetadataField.CriticScore:
                    var cscore = provider.GetCriticScore(args);
                    if (!cscore.HasValue) return false;
                    game.CriticScore = cscore.Value;
                    return true;

                case MetadataField.InstallSize:
                    var size = provider.GetInstallSize(args);
                    if (!size.HasValue) return false;
                    game.InstallSize = size.Value;
                    return true;

                default:
                    return false;
            }
        }

        public GameMetadata MapToGameMetadata(PromotedGameEntry entry)
        {
            var platforms = DeserializeList(entry.PlatformsJson);
            var genres = DeserializeList(entry.GenresJson);
            var tags = DeserializeList(entry.TagsJson);

            var metadata = new GameMetadata
            {
                GameId = $"gr-{entry.GameId}",
                Name = entry.Title,
                Source = new MetadataNameProperty("Games Recap"),
                IsInstalled = false
            };

            if (platforms != null && platforms.Count > 0)
                metadata.Platforms = new HashSet<MetadataProperty>(
                    platforms.Select(p => new MetadataNameProperty(p)));

            if (genres != null && genres.Count > 0)
                metadata.Genres = new HashSet<MetadataProperty>(
                    genres.Select(g => new MetadataNameProperty(g)));

            if (tags != null && tags.Count > 0)
                metadata.Tags = new HashSet<MetadataProperty>(
                    tags.Select(t => new MetadataNameProperty(t)));

            if (!string.IsNullOrEmpty(entry.CoverUrl))
                metadata.CoverImage = new MetadataFile(entry.CoverUrl);

            if (!string.IsNullOrEmpty(entry.Description))
                metadata.Description = entry.Description;

            if (DateTime.TryParse(entry.ReleaseDate, out var releaseDate))
                metadata.ReleaseDate = new ReleaseDate(releaseDate);

            return metadata;
        }

        internal static string SerializeList(List<string> items)
        {
            if (items == null || items.Count == 0) return null;
            var serializer = new DataContractJsonSerializer(typeof(List<string>));
            using var ms = new MemoryStream();
            serializer.WriteObject(ms, items);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        internal static List<string> DeserializeList(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(List<string>));
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
                return (List<string>)serializer.ReadObject(ms);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to deserialize list from JSON: {json}");
                return null;
            }
        }

        private static bool IsImageUrlValid(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, path);
                var response = validationHttp.SendAsync(request).GetAwaiter().GetResult();
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
