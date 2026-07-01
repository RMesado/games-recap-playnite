using System.Collections.Generic;

namespace GamesRecap.Models
{
    public class PlayniteConfigRoot
    {
        public Dictionary<string, MetadataFieldSetting> MetadataSettings { get; set; }
    }

    public class MetadataFieldSetting
    {
        public bool Import { get; set; }
        public List<string> Sources { get; set; }
    }
}
