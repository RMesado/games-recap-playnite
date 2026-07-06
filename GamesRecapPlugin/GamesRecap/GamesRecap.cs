using GamesRecap.Models;
using GamesRecap.Services;
using GamesRecap.ViewModels;
using GamesRecap.Views;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
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
        private Timer calendarRefreshTimer;

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

            LoadLocalizationResources();
            StartCalendarRefreshTimer();

            logger.Info($"Games Recap initialized, DB at: {dbPath}");
        }

        private void LoadLocalizationResources()
        {
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var app = System.Windows.Application.Current;
                if (app == null || dir == null) return;

                foreach (var file in new[] { "es_ES.xaml", "en_US.xaml" })
                {
                    var path = Path.Combine(dir, "Localization", file);
                    if (File.Exists(path))
                    {
                        var rd = new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) };
                        app.Resources.MergedDictionaries.Add(rd);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load localization resources");
            }
        }

        private void StartCalendarRefreshTimer()
        {
            calendarRefreshTimer = new Timer(86400000);
            calendarRefreshTimer.Elapsed += async (s, e) => await CalendarRefreshElapsed();
            calendarRefreshTimer.Start();
        }

        private async Task CalendarRefreshElapsed()
        {
            try
            {
                var intervalDays = settings.Settings.CalendarRefreshIntervalDays;
                var lastRefresh = Database.GetCalendarLastRefresh();

                if (lastRefresh.HasValue && (DateTime.UtcNow - lastRefresh.Value).TotalHours < intervalDays * 24)
                    return;

                var calSettings = settings.Settings;
                var calendarGames = Database.GetAllCalendarGames();
                if (calendarGames.Count == 0)
                {
                    Database.SetCalendarLastRefresh(DateTime.UtcNow);
                    return;
                }

                var ids = calendarGames.Select(g => g.GameId).ToList();
                var cards = await ApiClient.FetchCardsByIdsAsync(ids);
                if (cards == null || cards.Count == 0) return;

                var changedCount = 0;
                foreach (var card in cards)
                {
                    var game = card.Game;
                    if (game?.ReleaseDate == null) continue;

                    var dbEntry = calendarGames.FirstOrDefault(g => g.GameId == card.GameId);
                    if (dbEntry == null) continue;

                    if (dbEntry.ReleaseDate != game.ReleaseDate ||
                        dbEntry.Title != game.Title ||
                        dbEntry.CoverUrl != game.CoverImageUrl)
                    {
                        Database.UpdateCalendarGameDate(card.GameId, game.ReleaseDate, game.Title, game.CoverImageUrl);
                        changedCount++;
                    }
                }

                Database.SetCalendarLastRefresh(DateTime.UtcNow);

                if (changedCount > 0)
                {
                    var text = $"Games Recap: {changedCount} calendared game(s) had updated dates";
                    logger.Info(text);
                    _ = System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        PlayniteApi.Notifications.Remove("gamesrecap-calendar-refresh");
                        PlayniteApi.Notifications.Add("gamesrecap-calendar-refresh", text, NotificationType.Info);
                    }));
                }

                Database.ClearPastCalendarNotifications();
                var today = DateTime.Today;
                foreach (var game in calendarGames)
                {
                    if (!TryParseDate(game.ReleaseDate, out var release)) continue;
                    var daysLeft = (release - today).Days;

                    TryNotify(game, "month_before", daysLeft <= 30 && daysLeft > 7, calSettings.CalendarNotifyMonthBefore, "CalendarNotifyMonthText");
                    TryNotify(game, "week_before", daysLeft <= 7 && daysLeft > 1, calSettings.CalendarNotifyWeekBefore, "CalendarNotifyWeekText");
                    TryNotify(game, "day_before", daysLeft == 1, calSettings.CalendarNotifyDayBefore, "CalendarNotifyDayText");
                    TryNotify(game, "same_day", daysLeft == 0, calSettings.CalendarNotifySameDay, "CalendarNotifyTodayText");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Calendar refresh failed");
            }
        }

        private void TryNotify(CalendarGameEntry game, string type, bool condition, bool settingEnabled, string textKey)
        {
            if (!settingEnabled) return;
            if (Database.WasCalendarNotified(game.GameId, type)) return;
            if (!condition) return;

            Database.MarkCalendarNotified(game.GameId, type);
            logger.Info($"Calendar notify: {type} for game {game.GameId}");

            _ = System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                var text = string.Format(Loc.Get(textKey), game.Title);
                PlayniteApi.Notifications.Remove("gamesrecap-notify-" + type + "-" + game.GameId);
                PlayniteApi.Notifications.Add("gamesrecap-notify-" + type + "-" + game.GameId, text, NotificationType.Info);
            }));
        }

        private static bool TryParseDate(string dateStr, out DateTime date)
        {
            return DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
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
