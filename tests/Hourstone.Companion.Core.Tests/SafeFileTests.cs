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
    public void ReadRecoversAfterWriterReleasesSharingLock()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "locked.lua"); File.WriteAllText(path, "stable");
        using (var open = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            var failure = Assert.Throws<IOException>(() => SafeFiles.StableRead(path));
            Assert.IsAssignableFrom<IOException>(failure.InnerException);
            Assert.Contains(failure.InnerException!.Message, failure.Message);
        }
        // The next scan must recover once the writer releases its lock.
        Assert.Equal("stable", SafeFiles.StableRead(path));
    }
    [Theory]
    [InlineData(0x00001000)] // Offline
    [InlineData(0x00040000)] // RecallOnOpen
    [InlineData(0x00400000)] // RecallOnDataAccess
    [InlineData(0x00401420)] // Offline cloud placeholder with ordinary attributes
    public void NonresidentFlagsProduceAnActionableTypedFailure(int attributes)
    {
        var path = Path.Combine(root, "peer.json");
        var failure = Assert.Throws<CloudFileNotLocalException>(() => SafeFiles.EnsureLocallyAvailable(path, (FileAttributes)attributes));
        Assert.Equal(path, failure.FilePath); Assert.Contains("always available", failure.Message); Assert.Null(failure.InnerException);
    }
    [Theory]
    [InlineData(0x00000080)] // Normal
    [InlineData(0x00080420)] // Pinned cloud reparse point
    [InlineData(0x00100420)] // Locally available, but not pinned
    public void ResidentCloudAttributesDoNotRequireHydration(int attributes) =>
        SafeFiles.EnsureLocallyAvailable(Path.Combine(root, "peer.json"), (FileAttributes)attributes);
    [Fact]
    public void OfflineFileIsNotReadOrWrappedAndRecoversWhenAvailable()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "peer.json"); File.WriteAllText(path, "resident content");
        var original = File.GetAttributes(path);
        File.SetAttributes(path, original | FileAttributes.Offline);
        try
        {
            var failure = Assert.Throws<CloudFileNotLocalException>(() => SafeFiles.StableRead(path));
            Assert.Equal(path, failure.FilePath); Assert.Null(failure.InnerException);
        }
        finally { File.SetAttributes(path, original); }
        Assert.Equal("resident content", SafeFiles.StableRead(path));
    }
    [Fact]
    public void MissingFileIsAReadFailureWithItsOriginalCause()
    {
        var failure = Assert.Throws<IOException>(() => SafeFiles.StableRead(Path.Combine(root, "missing.json")));
        Assert.IsType<FileNotFoundException>(failure.InnerException);
        Assert.Contains(failure.InnerException!.Message, failure.Message);
    }
    public void Dispose()
    {
        var full = Path.GetFullPath(root);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("Hourstone.SafeFile.Tests-", StringComparison.Ordinal) && Directory.Exists(full)) Directory.Delete(full, true);
    }
}
