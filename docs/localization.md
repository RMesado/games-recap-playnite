# Localization Guide

## System

The plugin uses WPF `ResourceDictionary` for localization. Files are in `Localization/*.xaml` and loaded via `App.xaml` as `MergedDictionaries`. Order determines priority:

```
App.xaml:
  └─ Localization/es_ES.xaml  ← primary (used first)
  └─ Localization/en_US.xaml  ← fallback (keys not found in es_ES)
```

## File Format

Each key is a `sys:String` with a unique `x:Key`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="FilterPlatforms">Plataformas</sys:String>
    <sys:String x:Key="ConfirmAddMessage">¿Añadir "{0}" a la biblioteca de Playnite?</sys:String>
</ResourceDictionary>
```

## Naming Conventions

- **CamelCase** with no spaces
- Context prefix: `Filter*`, `Search*`, `Pagination*`, `Settings*`, `Error*`, etc.
- Keys with `{0}`, `{1}` placeholders are documented in the key name

## Usage in XAML

```xml
<TextBlock Text="{DynamicResource FilterPlatforms}" />
<Button ToolTip="{DynamicResource AddToWishlist}" />
```

## Usage in C#

```csharp
internal static class Loc
{
    public static string Get(string key)
    {
        var resource = Application.Current?.TryFindResource(key);
        return resource as string ?? key;
    }
}

// With string.Format for placeholders
string msg = string.Format(Loc.Get("ConfirmAddMessage"), cardVm.Title);

// Without placeholders
ErrorMessage = Loc.Get("ErrorConnection");
```

## Adding a New Locale

1. Copy `Localization/en_US.xaml` to `Localization/{code}.xaml` (e.g. `fr_FR.xaml`)
2. Translate the `sys:String` values
3. Add to `App.xaml` as first `ResourceDictionary`:
   ```xml
   <ResourceDictionary Source="Localization/fr_FR.xaml" />
   <ResourceDictionary Source="Localization/en_US.xaml" />
   ```
4. No `.csproj` changes needed — it already includes `Localization\*.xaml` with `CopyToOutputDirectory=PreserveNewest`

## Non-localized Strings

These are internal identifiers and should NOT be translated:

- `"Games Recap"` — Source name in Playnite database
- `"Wishlist"` — Tag name
- `"gr-"` — GameId prefix
- `"newest"` — Default sort value
