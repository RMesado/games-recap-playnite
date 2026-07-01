using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GamesRecap.Services
{
    public class PlayniteLibrarySync
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public void AddToLibrary(int gameId, string title, int? igdbId, IPlayniteAPI api, LibraryPlugin plugin, LocalDatabase db)
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

                var playniteGame = api.Database.ImportGame(metadata, plugin);
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

            var igdbPlugin = metadataPlugins.FirstOrDefault(p =>
                p.Id == Guid.Parse("000001DB-DBD1-46C6-B5D0-B1BA559D10E4"));
            var sgdbPlugin = metadataPlugins.FirstOrDefault(p =>
                p.Id == Guid.Parse("f9a763e1-1ccb-4d7d-b955-d59e708f71c1"));

            logger.Info($"Metadata for '{game.Name}': IGDB={(igdbPlugin != null ? "yes" : "no")}, SteamGridDB={(sgdbPlugin != null ? "yes" : "no")}, igdbId={igdbId}");

            var hasUpdates = false;
            var fieldArgs = new GetMetadataFieldArgs();

            if (igdbPlugin != null)
            {
                hasUpdates |= ProcessPlugin(api, game, igdbPlugin, igdbId?.ToString(), fieldArgs);
            }
            else
            {
                logger.Warn("IGDB plugin not found — cannot download metadata");
            }

            if (sgdbPlugin != null)
            {
                var sgdbRequest = new Game
                {
                    Name = game.Name,
                    GameId = null,
                    PluginId = game.PluginId,
                    ReleaseDate = game.ReleaseDate
                };

                var sgdbOptions = new MetadataRequestOptions(sgdbRequest, true);
                using var sgdbProvider = sgdbPlugin.GetMetadataProvider(sgdbOptions);
                var sgdbFields = sgdbProvider?.AvailableFields;
                if (sgdbFields?.Count > 0)
                {
                    if (string.IsNullOrEmpty(game.CoverImage) && sgdbFields.Contains(MetadataField.CoverImage))
                    {
                        var cover = sgdbProvider.GetCoverImage(fieldArgs);
                        if (cover?.Path != null)
                        {
                            game.CoverImage = cover.Path;
                            hasUpdates = true;
                        }
                    }
                    if (string.IsNullOrEmpty(game.BackgroundImage) && sgdbFields.Contains(MetadataField.BackgroundImage))
                    {
                        var bg = sgdbProvider.GetBackgroundImage(fieldArgs);
                        if (bg?.Path != null)
                        {
                            game.BackgroundImage = bg.Path;
                            hasUpdates = true;
                        }
                    }
                    if (string.IsNullOrEmpty(game.Icon) && sgdbFields.Contains(MetadataField.Icon))
                    {
                        var icon = sgdbProvider.GetIcon(fieldArgs);
                        if (icon?.Path != null)
                        {
                            game.Icon = icon.Path;
                            hasUpdates = true;
                        }
                    }
                }
            }

            if (hasUpdates)
            {
                game.Modified = DateTime.Now;
                api.Database.Games.Update(game);
                logger.Info($"Metadata saved for '{game.Name}'");
            }

            var dbGame = api.Database.Games.Get(game.Id);
            logger.Info($"After metadata: game='{game.Name}', cover={!string.IsNullOrEmpty(dbGame?.CoverImage)}, desc={!string.IsNullOrEmpty(dbGame?.Description)}");
        }

        private bool ProcessPlugin(IPlayniteAPI api, Game game, MetadataPlugin plugin, string gameId, GetMetadataFieldArgs fieldArgs)
        {
            try
            {
                var requestGame = new Game
                {
                    Name = game.Name,
                    GameId = gameId,
                    PluginId = game.PluginId,
                    ReleaseDate = game.ReleaseDate
                };

                var options = new MetadataRequestOptions(requestGame, true);
                using var provider = plugin.GetMetadataProvider(options);
                if (provider == null)
                {
                    logger.Warn($"  {plugin.Name}: provider is null");
                    return false;
                }

                var availableFields = provider.AvailableFields;
                if (availableFields == null || availableFields.Count == 0)
                {
                    logger.Warn($"  {plugin.Name}: no match for '{game.Name}'");
                    return false;
                }

                var hasUpdates = false;

                foreach (var field in availableFields)
                {

                    switch (field)
                    {
                        case MetadataField.Name:
                            var name = provider.GetName(fieldArgs);
                            if (!string.IsNullOrEmpty(name) && name != game.Name)
                            {
                                game.Name = name;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.Genres:
                            var genres = provider.GetGenres(fieldArgs);
                            if (genres != null)
                            {
                                var genreIds = api.Database.Genres.Add(genres).Select(g => g.Id).ToList();
                                if (genreIds.Count > 0 && game.GenreIds == null || genreIds.Any(id => !game.GenreIds.Contains(id)))
                                {
                                    game.GenreIds = genreIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.ReleaseDate:
                            var releaseDate = provider.GetReleaseDate(fieldArgs);
                            if (releaseDate.HasValue)
                            {
                                game.ReleaseDate = releaseDate.Value;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.Developers:
                            var devs = provider.GetDevelopers(fieldArgs);
                            if (devs != null)
                            {
                                var devIds = api.Database.Companies.Add(devs).Select(c => c.Id).ToList();
                                if (devIds.Count > 0)
                                {
                                    game.DeveloperIds = devIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Publishers:
                            var pubs = provider.GetPublishers(fieldArgs);
                            if (pubs != null)
                            {
                                var pubIds = api.Database.Companies.Add(pubs).Select(c => c.Id).ToList();
                                if (pubIds.Count > 0)
                                {
                                    game.PublisherIds = pubIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Tags:
                            var tags = provider.GetTags(fieldArgs);
                            if (tags != null)
                            {
                                var tagIds = api.Database.Tags.Add(tags).Select(t => t.Id).ToList();
                                if (tagIds.Count > 0)
                                {
                                    game.TagIds = tagIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Description:
                            var desc = provider.GetDescription(fieldArgs);
                            if (!string.IsNullOrEmpty(desc))
                            {
                                game.Description = desc;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.CoverImage:
                            var cover = provider.GetCoverImage(fieldArgs);
                            if (cover != null && cover.Path != null)
                            {
                                game.CoverImage = cover.Path;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.BackgroundImage:
                            var bg = provider.GetBackgroundImage(fieldArgs);
                            if (bg != null && bg.Path != null)
                            {
                                game.BackgroundImage = bg.Path;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.Icon:
                            var icon = provider.GetIcon(fieldArgs);
                            if (icon != null && icon.Path != null)
                            {
                                game.Icon = icon.Path;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.Links:
                            var links = provider.GetLinks(fieldArgs)?.ToList();
                            if (links?.Count > 0)
                            {
                                game.Links = new ObservableCollection<Link>(links);
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.Features:
                            var features = provider.GetFeatures(fieldArgs);
                            if (features != null)
                            {
                                var featIds = api.Database.Features.Add(features).Select(f => f.Id).ToList();
                                if (featIds.Count > 0)
                                {
                                    game.FeatureIds = featIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Platform:
                            var platforms = provider.GetPlatforms(fieldArgs);
                            if (platforms != null)
                            {
                                var platIds = api.Database.Platforms.Add(platforms).Select(p => p.Id).ToList();
                                if (platIds.Count > 0)
                                {
                                    game.PlatformIds = platIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Series:
                            var series = provider.GetSeries(fieldArgs);
                            if (series != null)
                            {
                                var seriesIds = api.Database.Series.Add(series).Select(s => s.Id).ToList();
                                if (seriesIds.Count > 0)
                                {
                                    game.SeriesIds = seriesIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.AgeRating:
                            var ratings = provider.GetAgeRatings(fieldArgs);
                            if (ratings != null)
                            {
                                var ratingIds = api.Database.AgeRatings.Add(ratings).Select(r => r.Id).ToList();
                                if (ratingIds.Count > 0)
                                {
                                    game.AgeRatingIds = ratingIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.Region:
                            var regions = provider.GetRegions(fieldArgs);
                            if (regions != null)
                            {
                                var regionIds = api.Database.Regions.Add(regions).Select(r => r.Id).ToList();
                                if (regionIds.Count > 0)
                                {
                                    game.RegionIds = regionIds;
                                    hasUpdates = true;
                                }
                            }
                            break;
                        case MetadataField.CommunityScore:
                            var cs = provider.GetCommunityScore(fieldArgs);
                            if (cs.HasValue)
                            {
                                game.CommunityScore = cs.Value;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.CriticScore:
                            var cscore = provider.GetCriticScore(fieldArgs);
                            if (cscore.HasValue)
                            {
                                game.CriticScore = cscore.Value;
                                hasUpdates = true;
                            }
                            break;
                        case MetadataField.InstallSize:
                            var size = provider.GetInstallSize(fieldArgs);
                            if (size.HasValue)
                            {
                                game.InstallSize = size.Value;
                                hasUpdates = true;
                            }
                            break;
                    }
                }

                if (hasUpdates)
                    logger.Info($"  {plugin.Name}: downloaded {string.Join(", ", availableFields)}");

                return hasUpdates;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"  {plugin.Name}: failed for '{game?.Name}'");
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

            return metadata;
        }

        private static string SerializeList(List<string> items)
        {
            if (items == null || items.Count == 0) return null;
            var serializer = new DataContractJsonSerializer(typeof(List<string>));
            using var ms = new MemoryStream();
            serializer.WriteObject(ms, items);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static List<string> DeserializeList(string json)
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
    }
}
