using GamesRecap.Models;
using GamesRecap.Services;
using GamesRecap.ViewModels;
using GamesRecap.Views;
using Playnite.SDK;
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
    public class GamesRecap : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GamesRecapSettingsViewModel settings;
        internal LocalDatabase Database { get; private set; }
        internal GamesRecapApiClient ApiClient { get; private set; }

        private BrowserView browserView;
        private BrowserViewModel browserViewModel;
        private CalendarView calendarView;
        private CalendarViewModel calendarViewModel;

        public override Guid Id { get; } = Guid.Parse("01af564c-edf6-49ba-b6e1-32a12fb28bec");

        public GamesRecap(IPlayniteAPI api) : base(api)
        {
            settings = new GamesRecapSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            var dbPath = Path.Combine(GetPluginUserDataPath(), "gamesrecap.db");
            Database = new LocalDatabase(dbPath);
            ApiClient = new GamesRecapApiClient(Database);

            logger.Info($"Games Recap initialized, DB at: {dbPath}");
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>();
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
                        browserViewModel = new BrowserViewModel(ApiClient, Database, PlayniteApi, settings.Settings);
                        browserView = new BrowserView();
                        browserView.DataContext = browserViewModel;
                    }
                    else
                    {
                        browserViewModel.RefreshLibraryGameIds();
                        browserViewModel.RefreshCalendarIds();
                    }

                    _ = browserViewModel.LoadCardsAsync(1);
                    return browserView;
                }
            };

            yield return new SidebarItem
            {
                Title = Loc.Get("ReleaseCalendar"),
                Type = SiderbarItemType.View,
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon-calendar.png"),
                Opened = () =>
                {
                    if (calendarView == null)
                    {
                        calendarViewModel = new CalendarViewModel(Database, PlayniteApi);
                        calendarView = new CalendarView();
                        calendarView.DataContext = calendarViewModel;
                    }

                    calendarViewModel.RefreshGames();
                    return calendarView;
                }
            };
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
