using Playnite.SDK;
using System;
using System.IO;
using System.Reflection;

namespace GamesRecap
{
    public class GamesRecapClient : LibraryClient
    {
        public override bool IsInstalled => true;
        public override string Icon => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png");

        public override void Open()
        {
        }
    }
}
