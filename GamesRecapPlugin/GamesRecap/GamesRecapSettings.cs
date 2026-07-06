using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GamesRecap
{
    public enum WishlistAction
    {
        SqliteOnly,
        AddToLibrary
    }

    public class GamesRecapSettings : ObservableObject
    {
        private WishlistAction defaultWishlistAction = WishlistAction.SqliteOnly;
        private bool showConfirmation = true;
        private int calendarRefreshIntervalDays = 30;
        private bool calendarNotifyMonthBefore = true;
        private bool calendarNotifyWeekBefore = true;
        private bool calendarNotifyDayBefore = true;
        private bool calendarNotifySameDay = true;

        public WishlistAction DefaultWishlistAction { get => defaultWishlistAction; set => SetValue(ref defaultWishlistAction, value); }
        public bool ShowConfirmation { get => showConfirmation; set => SetValue(ref showConfirmation, value); }
        public int CalendarRefreshIntervalDays { get => calendarRefreshIntervalDays; set => SetValue(ref calendarRefreshIntervalDays, Math.Max(1, Math.Min(365, value))); }
        public bool CalendarNotifyMonthBefore { get => calendarNotifyMonthBefore; set => SetValue(ref calendarNotifyMonthBefore, value); }
        public bool CalendarNotifyWeekBefore { get => calendarNotifyWeekBefore; set => SetValue(ref calendarNotifyWeekBefore, value); }
        public bool CalendarNotifyDayBefore { get => calendarNotifyDayBefore; set => SetValue(ref calendarNotifyDayBefore, value); }
        public bool CalendarNotifySameDay { get => calendarNotifySameDay; set => SetValue(ref calendarNotifySameDay, value); }
    }

    public class CalendarPresetItem
    {
        public string Id { get; set; }
        public string Display { get; set; }
        public int? Days { get; set; }
    }

    public class WishlistActionItem
    {
        public WishlistAction Value { get; set; }
        public string Display { get; set; }
    }

    internal static class Loc
    {
        public static string Get(string key)
        {
            var resource = Application.Current?.TryFindResource(key);
            return resource as string ?? key;
        }
    }

    public class GamesRecapSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GamesRecap plugin;
        private GamesRecapSettings editingClone;

        private GamesRecapSettings settings;
        private CalendarPresetItem selectedCalendarPreset;
        private string calendarCustomDaysText;
        public GamesRecapSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAddToLibraryAction));
                if (settings != null)
                    settings.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(GamesRecapSettings.DefaultWishlistAction))
                            OnPropertyChanged(nameof(IsAddToLibraryAction));
                        if (e.PropertyName == nameof(GamesRecapSettings.CalendarRefreshIntervalDays))
                            MatchCalendarPreset();
                    };
            }
        }

        public List<WishlistActionItem> WishlistActions { get; } = new()
        {
            new WishlistActionItem { Value = WishlistAction.SqliteOnly, Display = Loc.Get("SettingsActionSaveLocal") },
            new WishlistActionItem { Value = WishlistAction.AddToLibrary, Display = Loc.Get("SettingsActionAddLibrary") }
        };

        public bool IsAddToLibraryAction => Settings?.DefaultWishlistAction == WishlistAction.AddToLibrary;

        public List<CalendarPresetItem> CalendarRefreshPresets { get; } = new()
        {
            new CalendarPresetItem { Id = "daily",     Display = Loc.Get("SettingsCalendarPresetDaily"),     Days = 1 },
            new CalendarPresetItem { Id = "weekly",    Display = Loc.Get("SettingsCalendarPresetWeekly"),    Days = 7 },
            new CalendarPresetItem { Id = "monthly",   Display = Loc.Get("SettingsCalendarPresetMonthly"),   Days = 30 },
            new CalendarPresetItem { Id = "bimonthly", Display = Loc.Get("SettingsCalendarPresetBimonthly"), Days = 60 },
            new CalendarPresetItem { Id = "quarterly", Display = Loc.Get("SettingsCalendarPresetQuarterly"), Days = 90 },
            new CalendarPresetItem { Id = "custom",    Display = Loc.Get("SettingsCalendarPresetCustom"),    Days = null }
        };

        public CalendarPresetItem SelectedCalendarPreset
        {
            get => selectedCalendarPreset;
            set
            {
                if (selectedCalendarPreset == value) return;
                selectedCalendarPreset = value;
                OnPropertyChanged(nameof(SelectedCalendarPreset));
                OnPropertyChanged(nameof(IsCustomPreset));

                if (value?.Days.HasValue == true)
                {
                    Settings.CalendarRefreshIntervalDays = value.Days.Value;
                    CalendarCustomDaysText = value.Days.Value.ToString();
                }
            }
        }

        public bool IsCustomPreset => SelectedCalendarPreset?.Days.HasValue == false;

        public string CalendarCustomDaysText
        {
            get => calendarCustomDaysText;
            set
            {
                if (calendarCustomDaysText == value) return;
                calendarCustomDaysText = value;
                OnPropertyChanged(nameof(CalendarCustomDaysText));

                if (int.TryParse(value, out var days))
                    Settings.CalendarRefreshIntervalDays = Math.Max(1, Math.Min(365, days));
            }
        }

        public GamesRecapSettingsViewModel(GamesRecap plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<GamesRecapSettings>();

            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new GamesRecapSettings();
            }

            MatchCalendarPreset();
        }

        private void MatchCalendarPreset()
        {
            var days = Settings.CalendarRefreshIntervalDays;
            var preset = CalendarRefreshPresets.FirstOrDefault(p => p.Days == days);
            if (preset != null)
            {
                selectedCalendarPreset = preset;
            }
            else
            {
                selectedCalendarPreset = CalendarRefreshPresets.Last();
                calendarCustomDaysText = days.ToString();
            }
            OnPropertyChanged(nameof(SelectedCalendarPreset));
            OnPropertyChanged(nameof(IsCustomPreset));
            OnPropertyChanged(nameof(CalendarCustomDaysText));
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}