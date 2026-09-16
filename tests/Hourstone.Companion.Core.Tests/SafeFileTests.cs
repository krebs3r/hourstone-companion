using Hourstone.Companion.Core;
using Xunit;

namespace Hourstone.Companion.Core.Tests;

public sealed class SafeFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Hourstone.SafeFile.Tests-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void AtomicReplacementIsCompleteAndDoesNotTouchSiblingFiles()
    {
        Directory.CreateDirectory(root); var sibling = Path.Combine(root, "Hourstone.lua"); File.WriteAllText(sibling, "original saved variables");
        SafeFiles.AtomicWriteOwned(root, "Data.lua", "first"); SafeFiles.AtomicWriteOwned(root, "Data.lua", new string('x', 1024 * 1024));
        Assert.Equal(new string('x', 1024 * 1024), SafeFiles.StableRead(Path.Combine(root, "Data.lua")));
        Assert.Equal("original saved variables", File.ReadAllText(sibling)); Assert.Empty(Directory.EnumerateFiles(root, "*.tmp"));
    }
    [Fact]
    public void InvalidOutputNameAndOversizedReadAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => SafeFiles.AtomicWriteOwned(root, "../not-owned.lua", "no"));
        Directory.CreateDirectory(root); var source = Path.Combine(root, "large.lua"); File.WriteAllText(source, new string('x', 200));
        Assert.Throws<InvalidDataException>(() => SafeFiles.StableRead(source, 100));
    }
    [Fact]
    public async Task ReadRecoversAfterWriterReleasesSharingLock()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "locked.lua"); File.WriteAllText(path, "stable");
        using var open = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        var read = Task.Run(() => SafeFiles.StableRead(path)); await Task.Delay(25); open.Dispose();
        Assert.Equal("stable", await read);
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.SafeFile.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
