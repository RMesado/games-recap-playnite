# Games Recap for Playnite

> Discover games from showcases and gaming conferences directly in Playnite.

---

**English** | [Español](#español)

---

## English

A [Playnite](https://playnite.link) plugin that integrates [gamesrecap.io](https://gamesrecap.io) as a game discovery source. Browse games announced at showcases (Summer Game Fest, Xbox, State of Play, Nintendo Direct, etc.), add them to your wishlist, and optionally promote them to your Playnite library.

### Features

- **Game browser** — Browse games from showcase history with cover art, tags, and metadata
- **Advanced filters** — Filter by platform, genre, tag, showcase series, release date, and more
- **Local wishlist** — Save games to a local SQLite wishlist (included in Playnite backups)
- **Release calendar** — Track upcoming releases with monthly/weekly/daily views
- **Release notifications** — Get notified when calendar games are about to launch
- **Playnite library integration** — Add games to your Playnite library with automatic metadata download
- **Priority metadata** — Uses your configured metadata sources in priority order (IGDB, SteamGridDB, etc.)
- **Full localization** — Spanish and English interfaces
- **Showcase filtering** — Filter by showcase series with multi-year chips
- **Card flip** — Interactive cards with front/back views showing additional info

### Requirements

- [Playnite](https://playnite.link) (Desktop mode)
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework/net481)

### Installation

#### From Playnite Addon Browser
1. Open Playnite → Addons → Browse
2. Search for "Games Recap"
3. Click Install

#### Manual Installation
1. Download the latest `.pext` from the [Releases](https://github.com/RMesado/games-recap-playnite/releases) page
2. Make sure Playnite is **closed** before installing or updating the plugin
3. Open the `.pext` file with Playnite

> ⚠️ **Important**: If Playnite is open during installation, Windows locks the plugin files and the installation will fail. Always close Playnite before installing or updating.

### Quick Start

1. Open Playnite and click the **Games Recap** sidebar icon (pink `#ff506e`)
2. Browse games from the main grid
3. Click ❤️ to add games to your wishlist
4. Flip cards to view trailers, add to calendar, or add to library
5. Use the sidebar filters to narrow down by platform, genre, showcase, or date
6. Switch to the **Calendar** view to track upcoming releases

### Settings

- **Default Wishlist Action**: Choose between saving locally only or also adding to Playnite library
- **Show Confirmation**: Toggle confirmation dialogs before adding games to the library
- **Calendar Refresh Interval**: Daily, Weekly, Monthly, or custom
- **Calendar Notifications**: Enable/disable notifications for upcoming releases

### Building from Source

See [docs/building.md](docs/building.md).

### Technical Documentation

- [Architecture](docs/architecture.md)
- [API Reference](docs/api.md)
- [Database Schema](docs/database.md)
- [Localization Guide](docs/localization.md)
- [Changelog](docs/changelog.md)

### License

[MIT](LICENSE)

---

## Español

Un plugin para [Playnite](https://playnite.link) que integra [gamesrecap.io](https://gamesrecap.io) como fuente de descubrimiento de juegos. Navega por juegos anunciados en showcases (Summer Game Fest, Xbox, State of Play, Nintendo Direct, etc.), añádelos a tu lista de deseos y promociónalos a tu biblioteca de Playnite.

### Características

- **Navegador de juegos** — Explora juegos de showcases con carátulas, etiquetas y metadatos
- **Filtros avanzados** — Filtra por plataforma, género, etiqueta, serie de showcase, fecha de lanzamiento y más
- **Lista de deseos local** — Guarda juegos en una wishlist SQLite local (incluida en backups de Playnite)
- **Calendario de lanzamientos** — Sigue próximos lanzamientos con vistas mensuales/semanales/diarias
- **Notificaciones de lanzamiento** — Recibe avisos cuando los juegos del calendario están por salir
- **Integración con biblioteca** — Añade juegos a tu biblioteca de Playnite con descarga automática de metadatos
- **Metadatos por prioridad** — Usa tus fuentes de metadatos configuradas en orden de prioridad (IGDB, SteamGridDB, etc.)
- **Localización completa** — Interfaz en español e inglés
- **Filtrado por showcase** — Filtra por serie de showcase con chips multi-año
- **Tarjetas interactivas** — Tarjetas con vistas frontal/trasera mostrando información adicional

### Requisitos

- [Playnite](https://playnite.link) (modo Escritorio)
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework/net481)

### Instalación

#### Desde el navegador de Addons de Playnite
1. Abre Playnite → Addons → Examinar
2. Busca "Games Recap"
3. Asegúrate de que Playnite esté **cerrado** antes de instalar o actualizar el plugin
4. Haz clic en Instalar

#### Instalación manual
1. Descarga el archivo `.pext` más reciente desde [Releases](https://github.com/RMesado/games-recap-playnite/releases)
2. Asegúrate de que Playnite esté **cerrado** antes de instalar o actualizar el plugin
3. Abre el archivo `.pext` con Playnite

> ⚠️ **Importante**: Si Playnite está abierto durante la instalación, Windows bloquea los archivos del plugin y la instalación falla. Siempre cierra Playnite antes de instalar o actualizar.

### Inicio rápido

1. Abre Playnite y haz clic en el icono de la barra lateral **Games Recap** (rosa `#ff506e`)
2. Navega por los juegos en la cuadrícula principal
3. Haz clic en ❤️ para añadir juegos a tu lista de deseos
4. Voltea las tarjetas para ver tráilers, añadir al calendario o añadir a la biblioteca
5. Usa los filtros laterales para acotar por plataforma, género, showcase o fecha
6. Cambia a la vista **Calendario** para seguir los próximos lanzamientos

### Configuración

- **Acción por defecto de Wishlist**: Elige entre guardar solo localmente o añadir también a la biblioteca de Playnite
- **Mostrar confirmación**: Activa/desactiva diálogos de confirmación antes de añadir juegos a la biblioteca
- **Intervalo de actualización del calendario**: Diario, Semanal, Mensual o personalizado
- **Notificaciones del calendario**: Activa/desactiva notificaciones de próximos lanzamientos

### Compilación desde el código fuente

Ver [docs/building.md](docs/building.md).

### Documentación técnica

- [Arquitectura](docs/architecture.md)
- [Referencia de API](docs/api.md)
- [Esquema de base de datos](docs/database.md)
- [Guía de localización](docs/localization.md)
- [Registro de cambios](docs/changelog.md)

### Licencia

[MIT](LICENSE)

---

## Development / Desarrollo

### Code conventions

- No Newtonsoft.Json — use `DataContractJsonSerializer`
- `[DataMember(Name = "snake_case")]` for JSON mapping
- C# 12.0 features allowed
- Logger via `Playnite.SDK.ILogger`

### Branch strategy

- `develop` — active development
- `main` — stable releases

### Screenshots / Capturas de pantalla

<img width="1920" height="1032" alt="image" src="https://github.com/user-attachments/assets/eab964e5-f26a-4f79-84c8-f6a48b5e0b2c" />
<img width="1918" height="1032" alt="image" src="https://github.com/user-attachments/assets/c3169155-c946-4ede-8f1b-ce6124c649a5" />



---

*Made with ❤️ by RMesado*
