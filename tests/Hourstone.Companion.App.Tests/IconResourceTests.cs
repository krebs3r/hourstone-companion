using System.Collections;
using System.IO;
using System.Resources;
using System.Security.Cryptography;
using Hourstone.Companion.App;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class IconResourceTests
{
    [Fact]
    public void EverySupportedClassAndClientImageIsEmbeddedForOfflineUse()
    {
        string[] classes = ["WARRIOR", "PALADIN", "HUNTER", "ROGUE", "PRIEST", "DEATHKNIGHT", "SHAMAN", "MAGE", "WARLOCK", "MONK", "DRUID", "DEMONHUNTER", "EVOKER"];
        string[] clients = ["retail", "mists", "tbc", "era"];
        var expected = classes.Select(c => IconCatalog.ClassIconPath(c)).Concat(clients.Select(IconCatalog.ClientIconPath))
            .Append(IconCatalog.HeartIconPath).Append(IconCatalog.UnknownIconPath)
            .Select(uri => uri.Replace("pack://application:,,,/", "", StringComparison.Ordinal).ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var assembly = typeof(IconCatalog).Assembly;
        var resourceName = Assert.Single(assembly.GetManifestResourceNames(), name => name.EndsWith(".g.resources", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var resources = new ResourceReader(stream);
        var clientHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in resources)
        {
            var key = (string)entry.Key;
            if (!expected.Remove(key)) continue;
            var image = Assert.IsAssignableFrom<Stream>(entry.Value);
            using var bytes = new MemoryStream(); image.CopyTo(bytes);
            var png = bytes.ToArray();
            Assert.True(png.Length > 32, key);
            Assert.Equal("89504E470D0A1A0A", Convert.ToHexString(png.AsSpan(0, 8)));
            if (key.StartsWith("assets/clients/", StringComparison.Ordinal)) clientHashes.Add(Convert.ToHexString(SHA256.HashData(png)));
        }
        Assert.Empty(expected);
        Assert.Equal(4, clientHashes.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.invalid/image.png")]
    [InlineData("../../private")]
    public void UnknownTokensStayInsideEmbeddedResources(string? token)
    {
        Assert.Equal(IconCatalog.UnknownIconPath, IconCatalog.ClassIconPath(token));
        Assert.Equal(IconCatalog.UnknownIconPath, IconCatalog.ClientIconPath(token));
    }
}
