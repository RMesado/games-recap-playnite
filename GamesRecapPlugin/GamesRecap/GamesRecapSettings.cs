using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public WishlistAction DefaultWishlistAction { get => defaultWishlistAction; set => SetValue(ref defaultWishlistAction, value); }
        public bool ShowConfirmation { get => showConfirmation; set => SetValue(ref showConfirmation, value); }
    }

    public class WishlistActionItem
    {
        public WishlistAction Value { get; set; }
        public string Display { get; set; }
    }

    public class GamesRecapSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GamesRecap plugin;
        private GamesRecapSettings editingClone;

        private GamesRecapSettings settings;
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
                    };
            }
        }

        public List<WishlistActionItem> WishlistActions { get; } = new()
        {
            new WishlistActionItem { Value = WishlistAction.SqliteOnly, Display = "Save to local database only" },
            new WishlistActionItem { Value = WishlistAction.AddToLibrary, Display = "Add to Playnite library" }
        };

        public bool IsAddToLibraryAction => Settings?.DefaultWishlistAction == WishlistAction.AddToLibrary;

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