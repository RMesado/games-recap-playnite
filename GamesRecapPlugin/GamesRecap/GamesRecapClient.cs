using Playnite.SDK;

namespace GamesRecap
{
    public class GamesRecapClient : LibraryClient
    {
        public override bool IsInstalled => true;

        public override void Open()
        {
            // Fase 3: abrirá BrowserView. Por ahora no hace nada.
        }
    }
}
