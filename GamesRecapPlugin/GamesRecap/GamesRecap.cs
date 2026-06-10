using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace GamesRecap
{
    public class GamesRecap : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GamesRecapSettingsViewModel settings;

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
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Fase 0: stub vacío. Fase 4 leerá juegos promovidos desde SQLite local.
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
