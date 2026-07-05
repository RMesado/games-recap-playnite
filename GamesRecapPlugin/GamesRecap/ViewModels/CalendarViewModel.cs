using GamesRecap.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using Application = System.Windows.Application;

namespace GamesRecap.ViewModels
{
    public class CalendarViewModel : ObservableObject
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly LocalDatabase database;
        private readonly IPlayniteAPI playniteApi;

        public ObservableCollection<CalendarGameItem> LastMonthGames { get; } = new ObservableCollection<CalendarGameItem>();
        public ObservableCollection<CalendarGameItem> LastWeekGames { get; } = new ObservableCollection<CalendarGameItem>();
        public ObservableCollection<CalendarWeek> UpcomingWeeks { get; } = new ObservableCollection<CalendarWeek>();
        public ObservableCollection<string> DayHeaders { get; } = new ObservableCollection<string>();

        public bool HasLastMonthGames => LastMonthGames.Count > 0;
        public bool HasLastWeekGames => LastWeekGames.Count > 0;
        public bool HasUpcomingGames => UpcomingWeeks.Any(w => w.Days.Any(d => d.Games.Count > 0));

        public ICommand RemoveFromCalendarCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand GoBackToLibraryCommand { get; }

        public CalendarViewModel(LocalDatabase database, IPlayniteAPI playniteApi)
        {
            this.database = database;
            this.playniteApi = playniteApi;

            RemoveFromCalendarCommand = new RelayCommand<int>(gameId => RemoveFromCalendar(gameId));
            RefreshCommand = new RelayCommand(() => RefreshGames());
            GoBackToLibraryCommand = new RelayCommand(() => playniteApi.MainView.SwitchToLibraryView());
        }

        public void RefreshGames()
        {
            var allGames = database.GetAllCalendarGames();
            var today = DateTime.Today;
            var culture = CultureInfo.CurrentCulture;

            var lastWeekStart = today.AddDays(-7);
            var lastMonthStart = today.AddDays(-30);

            LastMonthGames.Clear();
            foreach (var g in allGames
                .Where(g => TryParseDate(g.ReleaseDate, out var d) && d >= lastMonthStart && d < lastWeekStart)
                .OrderByDescending(g => TryParseDate(g.ReleaseDate, out var d) ? d : DateTime.MinValue))
            {
                LastMonthGames.Add(new CalendarGameItem(g));
            }

            LastWeekGames.Clear();
            foreach (var g in allGames
                .Where(g => TryParseDate(g.ReleaseDate, out var d) && d >= lastWeekStart && d <= today)
                .OrderByDescending(g => TryParseDate(g.ReleaseDate, out var d) ? d : DateTime.MinValue))
            {
                LastWeekGames.Add(new CalendarGameItem(g));
            }

            DayHeaders.Clear();
            for (int i = 0; i < 6; i++)
            {
                var d = today.AddDays(-((int)today.DayOfWeek + 6) % 7 + i);
                if (today.DayOfWeek == DayOfWeek.Sunday && i == 0)
                    d = today.AddDays(-6);
                DayHeaders.Add(d.ToString("dddd", culture).ToUpper());
            }

            var endDate = today.AddDays(8 * 7);
            UpcomingWeeks.Clear();
            BuildWeeks(allGames, today, endDate);

            OnPropertyChanged(nameof(HasLastMonthGames));
            OnPropertyChanged(nameof(HasLastWeekGames));
            OnPropertyChanged(nameof(HasUpcomingGames));
        }

        private void BuildWeeks(System.Collections.Generic.List<CalendarGameEntry> allGames, DateTime start, DateTime end)
        {
            var monday = start.AddDays(-((int)start.DayOfWeek + 6) % 7);
            var culture = CultureInfo.CurrentCulture;
            var lang = culture.TwoLetterISOLanguageName;

            if (start.DayOfWeek == DayOfWeek.Sunday)
                monday = start.AddDays(-6);

            while (monday <= end)
            {
                var week = new CalendarWeek();
                for (int i = 0; i < 6; i++)
                {
                    var date = monday.AddDays(i);
                    var dayGames = allGames
                        .Where(g => TryParseDate(g.ReleaseDate, out var d) && d.Date == date.Date)
                        .Select(g => new CalendarGameItem(g))
                        .ToList();

                    week.Days.Add(new CalendarDay
                    {
                        Date = date,
                        DayName = date.ToString("dddd", culture).ToUpper(),
                        DayNameShort = date.ToString("ddd", culture).ToUpper(),
                        DateLabel = lang == "es"
                            ? date.ToString("d 'de' MMMM", culture).ToUpper()
                            : date.ToString("d MMMM", culture).ToUpper(),
                        Games = new ObservableCollection<CalendarGameItem>(dayGames)
                    });
                }

                UpcomingWeeks.Add(week);
                monday = monday.AddDays(7);
            }
        }

        private void RemoveFromCalendar(int gameId)
        {
            var allGames = LastMonthGames.Concat(LastWeekGames)
                .Concat(UpcomingWeeks.SelectMany(w => w.Days.SelectMany(d => d.Games)));
            var game = allGames.FirstOrDefault(g => g.GameId == gameId);
            var title = game?.Title ?? gameId.ToString();

            var result = playniteApi.Dialogs.ShowMessage(
                string.Format(Loc.Get("ConfirmRemoveCalendarMessage"), title),
                Loc.Get("ConfirmAddTitle"),
                MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;

            database.RemoveFromCalendar(gameId);
            RefreshGames();
            playniteApi.Dialogs.ShowMessage(
                string.Format(Loc.Get("CalendarRemoved"), title),
                Loc.Get("SuccessAddTitle"));
        }

        private static bool TryParseDate(string dateStr, out DateTime date)
        {
            return DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }

    public class CalendarWeek : ObservableObject
    {
        public ObservableCollection<CalendarDay> Days { get; } = new ObservableCollection<CalendarDay>();
        public double WeekHeight { get; set; }
    }

    public class CalendarDay : ObservableObject
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; }
        public string DayNameShort { get; set; }
        public string DateLabel { get; set; }
        public ObservableCollection<CalendarGameItem> Games { get; set; } = new ObservableCollection<CalendarGameItem>();
        public bool HasGames => Games.Count > 0;
    }

    public class CalendarGameItem
    {
        public int GameId { get; }
        public string Title { get; }
        public string CoverUrl { get; }
        public DateTime ReleaseDate { get; }

        public CalendarGameItem(CalendarGameEntry entry)
        {
            GameId = entry.GameId;
            Title = entry.Title;
            CoverUrl = entry.CoverUrl;
            ReleaseDate = DateTime.TryParseExact(entry.ReleaseDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : DateTime.MinValue;
        }
    }
}
