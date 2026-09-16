using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class Utf8ParserTests
{
    [Fact]
    public void DecimalEscapesDecodeUtf8BytesWithoutChangingNames()
    {
        var source = Sample.Source("hs-test");
        var lua = Sample.Lua(source, Sample.Item("hs-test") with { Name = "Täst😀" });
        Assert.Equal("Täst😀", Assert.Single(SavedVariablesReader.Read(lua, source).Observations).Name);
        lua = lua.Replace("Täst😀", """T\195\164st\240\159\152\128""", StringComparison.Ordinal);
        Assert.Equal("Täst😀", Assert.Single(SavedVariablesReader.Read(lua, source).Observations).Name);
    }
    [Fact]
    public void DepthAndFileSizeLimitsRejectExcessiveInputs()
    {
        var source = Sample.Source("hs-test");
        Assert.Throws<InvalidDataException>(() => SavedVariablesReader.Read("HourstoneDB={a=" + new string('{', 40) + new string('}', 40) + "}", source));
        Assert.Throws<InvalidDataException>(() => SavedVariablesReader.Read(new string(' ', SavedVariablesReader.MaximumFileBytes + 1), source));
    }
}
