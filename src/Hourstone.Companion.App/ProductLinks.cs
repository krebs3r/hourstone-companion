using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

public enum ProductLink { AddonCurseForge, AddonGitHub, CompanionGitHub }
public sealed record BrowserLaunchResult(bool Succeeded, string? TechnicalError = null);

/// <summary>Fixed product destinations opened by Windows in the user's default browser.</summary>
public static class ProductLinks
{
    public static string Address(ProductLink link) => link switch
    {
        ProductLink.AddonCurseForge => "https://www.curseforge.com/wow/addons/hourstone-azeroth-hours",
        ProductLink.AddonGitHub => "https://github.com/krebs3r/hourstone-azeroth-hours",
        ProductLink.CompanionGitHub => "https://github.com/krebs3r/hourstone-companion",
        _ => throw new ArgumentOutOfRangeException(nameof(link))
    };

    public static string? AddonActionKey(LocalSourceReadiness readiness) => readiness switch
    {
        LocalSourceReadiness.AddonMissing => "InstallAddonOnCurseForge",
        LocalSourceReadiness.AddonOutdated => "UpdateAddonOnCurseForge",
        _ => null
    };

    public static BrowserLaunchResult Open(ProductLink link, Action<ProcessStartInfo>? launch = null)
        => OpenAddress(Address(link), launch);
    public static BrowserLaunchResult OpenAddress(string address, Action<ProcessStartInfo>? launch = null)
    {
        var start = new ProcessStartInfo(address) { UseShellExecute = true, Verb = "open" };
        try
        {
            if (launch is not null) launch(start);
            else Process.Start(start)?.Dispose();
            return new(true);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or NotSupportedException or SecurityException)
        { return new(false, ex.Message); }
    }
}
