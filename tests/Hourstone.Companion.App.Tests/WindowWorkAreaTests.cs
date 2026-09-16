using System.Drawing;
using Hourstone.Companion.App;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class WindowWorkAreaTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 0, 0, 1920, 1032, 0, 0)]
    [InlineData(0, 0, 2560, 1440, 0, 0, 2560, 1392, 0, 0)]
    [InlineData(0, 0, 3840, 2160, 0, 0, 3840, 2064, 0, 0)]
    [InlineData(-2560, -200, 2560, 1440, -2560, -200, 2560, 1392, 0, 0)]
    [InlineData(1920, 0, 2560, 1440, 1920, 48, 2560, 1392, 0, 48)]
    [InlineData(-1920, 0, 1920, 1080, -1872, 0, 1872, 1080, 48, 0)]
    public void MaximizedContentFitsTheCurrentMonitorWorkArea(int mx, int my, int mw, int mh,
        int wx, int wy, int ww, int wh, int expectedX, int expectedY)
    {
        var result = WindowWorkArea.RelativeWorkArea(new(mx, my, mw, mh), new(wx, wy, ww, wh));
        Assert.Equal(new Rectangle(expectedX, expectedY, ww, wh), result);
        Assert.Equal(wx, result.Left + mx);
        Assert.Equal(wy + wh, result.Bottom + my);
    }
}