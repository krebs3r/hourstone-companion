using System;
namespace Hourstone.Companion.App;

/// <summary>Offline image resources for the supported WoW classes and client families.</summary>
public static class IconCatalog
{
    private const string Root = "pack://application:,,,/Assets/";
    public const string UnknownIconPath = Root + "Unknown.png";
    public const string HeartIconPath = Root + "Heart.png";

    public static string ClassIconPath(string? token)
    {
        string key = token?.Trim().ToUpperInvariant() ?? string.Empty;
        return key switch
        {
            "WARRIOR" or "PALADIN" or "HUNTER" or "ROGUE" or "PRIEST" or
            "DEATHKNIGHT" or "SHAMAN" or "MAGE" or "WARLOCK" or "MONK" or
            "DRUID" or "DEMONHUNTER" or "EVOKER" => Root + "Classes/" + key + ".png",
            _ => UnknownIconPath,
        };
    }

    public static string ClientIconPath(string? flavor) => flavor?.Trim().ToLowerInvariant() switch
    {
        "retail" => Root + "Clients/retail.png",
        "mists" => Root + "Clients/mists.png",
        "tbc" => Root + "Clients/tbc.png",
        "era" => Root + "Clients/era.png",
        _ => UnknownIconPath,
    };
}
