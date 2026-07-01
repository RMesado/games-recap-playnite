using GamesRecap.Models;
using GamesRecap.Services;
using GamesRecap.ViewModels;
using GamesRecap.Views;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace GamesRecap
{
    public class GamesRecap : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GamesRecapSettingsViewModel settings;
        internal LocalDatabase Database { get; private set; }
        internal GamesRecapApiClient ApiClient { get; private set; }

        private BrowserView browserView;
        private BrowserViewModel browserViewModel;

        public override Guid Id { get; } = Guid.Parse("01af564c-edf6-49ba-b6e1-32a12fb28bec");

        public override string Name => "Games Recap";

        public override LibraryClient Client { get; } = new GamesRecapClient();

        public GamesRecap(IPlayniteAPI api) : base(api)
        {
            settings = new GamesRecapSettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
                HasCustomizedGameImport = true
            };

            var dbPath = Path.Combine(GetPluginUserDataPath(), "gamesrecap.db");
            Database = new LocalDatabase(dbPath);
            ApiClient = new GamesRecapApiClient(Database);

            logger.Info($"Games Recap initialized, DB at: {dbPath}");
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = "Test API: fetch desde gamesrecap.io",
                    MenuSection = "@Games Recap",
                    Action = args2 =>
                    {
                        TestApiFetch();
                    }
                },
                new MainMenuItem
                {
                    Description = "Ver estado de wishlist",
                    MenuSection = "@Games Recap",
                    Action = args2 =>
                    {
                        var wishlisted = Database.GetWishlistedIds().Count;
                        var seen = Database.GetSeenIds().Count;
                        var hidden = Database.GetHiddenIds().Count;
                        var version = Database.GetInertiaVersion() ?? "(none)";
                        PlayniteApi.Dialogs.ShowMessage(
                            $"Wishlist: {wishlisted} juegos\nVistos: {seen}\nOcultos: {hidden}\nVersión Inertia: {version}",
                            "Games Recap - Estado");
                    }
                }
            };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = "Games Recap",
                Type = SiderbarItemType.View,
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png"),
                Opened = () =>
                {
                    CleanupOrphanedPromotedGames();

                    if (browserView == null)
                    {
                        browserViewModel = new BrowserViewModel(ApiClient, Database, PlayniteApi, this, settings.Settings);
                        browserView = new BrowserView();
                        browserView.DataContext = browserViewModel;
                    }
                    else
                    {
                        browserViewModel.RefreshLibraryGameIds();
                    }

                    _ = browserViewModel.LoadCardsAsync(1);
                    return browserView;
                }
            };
        }

        private async void TestApiFetch()
        {
            try
            {
                logger.Info("TestApiFetch: starting...");
                var filters = new ActiveFilters
                {
                    Q = "Control",
                    Platforms = new List<int> { 1 },
                    Showcases = new List<int> { 300 },
                    Sort = "newest"
                };

                var props = await ApiClient.FetchCardsAsync(filters);

                var count = props?.Pages?.Data?.Count ?? 0;
                var total = props?.Pages?.Total ?? 0;

                PlayniteApi.Dialogs.ShowMessage(
                    $"Respuesta recibida correctamente.\n\n" +
                    $"Cards en página: {count}\nTotal: {total}\n" +
                    $"Versión Inertia: {Database.GetInertiaVersion()}\n" +
                    $"Juegos en wishlist: {Database.GetWishlistedIds().Count}",
                    "Games Recap - API Test");

                logger.Info($"TestApiFetch: OK, got {count} cards, total {total}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "TestApiFetch failed");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"Error al llamar a la API:\n{ex.Message}",
                    "Games Recap - Error");
            }
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var sync = new PlayniteLibrarySync();
            var entries = Database.GetAllPromotedGames();
            if (entries.Count == 0) return Enumerable.Empty<GameMetadata>();

            var result = new List<GameMetadata>();
            var toRemove = new List<int>();

            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.PlayniteId, out var guid))
                {
                    var exists = PlayniteApi.Database.Games.Any(g => g.Id == guid);
                    if (!exists)
                    {
                        toRemove.Add(entry.GameId);
                        continue;
                    }
                }
                result.Add(sync.MapToGameMetadata(entry));
            }

            if (toRemove.Count > 0)
            {
                foreach (var gameId in toRemove)
                {
                    Database.SetPlayniteId(gameId, null);
                    Database.RemovePromotedGame(gameId);
                }
                logger.Info($"Cleaned up {toRemove.Count} promoted games no longer in Playnite library");
            }

            return result;
        }

        internal void CleanupOrphanedPromotedGames()
        {
            var entries = Database.GetAllPromotedGames();
            var toRemove = new List<int>();

            foreach (var entry in entries)
            {
                if (Guid.TryParse(entry.PlayniteId, out var guid))
                {
                    var exists = PlayniteApi.Database.Games.Any(g => g.Id == guid);
                    if (!exists)
                    {
                        toRemove.Add(entry.GameId);
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var gameId in toRemove)
                {
                    Database.SetPlayniteId(gameId, null);
                    Database.RemovePromotedGame(gameId);
                }
                logger.Info($"CleanupOrphanedPromotedGames: removed {toRemove.Count} orphaned games");
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GamesRecapSettingsView();
        }
    }
}
