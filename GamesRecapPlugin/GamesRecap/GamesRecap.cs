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
                    Description = "Explorar Games Recap",
                    MenuSection = "@Games Recap",
                    Action = args2 =>
                    {
                        var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                        {
                            ShowMinimizeButton = true,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true
                        });
                        window.Title = "Games Recap — Explorar";
                        window.Width = 1100;
                        window.Height = 750;
                        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();

                        var view = new BrowserView();
                        var viewModel = new BrowserViewModel(ApiClient, Database);
                        view.DataContext = viewModel;
                        window.Content = view;
                        window.ShowDialog();
                    }
                },
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
                    Description = "Ver estado de caché local",
                    MenuSection = "@Games Recap",
                    Action = args2 =>
                    {
                        var cards = Database.GetCachedCardCount();
                        var games = Database.GetCachedGameCount();
                        var version = Database.GetInertiaVersion() ?? "(none)";
                        PlayniteApi.Dialogs.ShowMessage(
                            $"Cards en caché: {cards}\nJuegos en caché: {games}\nVersión Inertia: {version}",
                            "Games Recap - Caché");
                    }
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
                    $"Cards en caché local: {Database.GetCachedCardCount()}\n" +
                    $"Juegos en caché local: {Database.GetCachedGameCount()}",
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
            return new List<GameMetadata>();
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
