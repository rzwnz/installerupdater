using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using InstallerService.Services;

namespace InstallerService.Tests.Services;

public class FileSystemServiceTests : IDisposable
{
    private readonly FileSystemService _sut;
    private readonly string _testDir;

    public FileSystemServiceTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<FileSystemService>();
        _sut = new FileSystemService(logger);
        _testDir = Path.Combine(Path.GetTempPath(), $"installer_fs_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void EnsureDirectory_CreatesDirectory()
    {
        var dir = Path.Combine(_testDir, "subdir");
        Assert.False(Directory.Exists(dir));

        _sut.EnsureDirectory(dir);

        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void EnsureDirectory_ExistingDirectory_DoesNotThrow()
    {
        Directory.CreateDirectory(_testDir);
        _sut.EnsureDirectory(_testDir); // Should not throw
        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public void CopyFile_CreatesDestination()
    {
        Directory.CreateDirectory(_testDir);
        var src = Path.Combine(_testDir, "source.txt");
        var dest = Path.Combine(_testDir, "copy", "dest.txt");
        File.WriteAllText(src, "hello");

        _sut.CopyFile(src, dest);

        Assert.True(File.Exists(dest));
        Assert.Equal("hello", File.ReadAllText(dest));
    }

    [Fact]
    public void CopyFile_Overwrite_ReplacesContent()
    {
        Directory.CreateDirectory(_testDir);
        var src = Path.Combine(_testDir, "source.txt");
        var dest = Path.Combine(_testDir, "dest.txt");
        File.WriteAllText(src, "new content");
        File.WriteAllText(dest, "old content");

        _sut.CopyFile(src, dest, overwrite: true);

        Assert.Equal("new content", File.ReadAllText(dest));
    }

    [Fact]
    public void DeleteFile_ExistingFile_ReturnsTrue()
    {
        Directory.CreateDirectory(_testDir);
        var file = Path.Combine(_testDir, "todelete.txt");
        File.WriteAllText(file, "delete me");

        var result = _sut.DeleteFile(file);

        Assert.True(result);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void DeleteFile_NonExistent_ReturnsFalse()
    {
        var result = _sut.DeleteFile(Path.Combine(_testDir, "nope.txt"));
        Assert.False(result);
    }

    [Fact]
    public void GetDirectorySize_CalculatesCorrectly()
    {
        Directory.CreateDirectory(_testDir);
        File.WriteAllBytes(Path.Combine(_testDir, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_testDir, "b.bin"), new byte[200]);

        var size = _sut.GetDirectorySize(_testDir);

        Assert.Equal(300, size);
    }

    [Fact]
    public void GetDirectorySize_NonExistent_ReturnsZero()
    {
        var size = _sut.GetDirectorySize("/nonexistent/path");
        Assert.Equal(0, size);
    }

    [Fact]
    public void CleanOldFiles_RemovesOldFiles()
    {
        Directory.CreateDirectory(_testDir);
        var oldFile = Path.Combine(_testDir, "old.log");
        var newFile = Path.Combine(_testDir, "new.log");
        File.WriteAllText(oldFile, "old");
        File.WriteAllText(newFile, "new");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-10));

        var count = _sut.CleanOldFiles(_testDir, TimeSpan.FromDays(5), "*.log");

        Assert.Equal(1, count);
        Assert.False(File.Exists(oldFile));
        Assert.True(File.Exists(newFile));
    }

    [Fact]
    public void CleanOldFiles_NonExistentDir_ReturnsZero()
    {
        var count = _sut.CleanOldFiles("/no/such/dir", TimeSpan.FromDays(1));
        Assert.Equal(0, count);
    }

    [Fact]
    public void FileExists_ReturnsCorrectly()
    {
        Directory.CreateDirectory(_testDir);
        var file = Path.Combine(_testDir, "test.txt");
        Assert.False(_sut.FileExists(file));
        File.WriteAllText(file, "x");
        Assert.True(_sut.FileExists(file));
    }

    [Fact]
    public void DirectoryExists_ReturnsCorrectly()
    {
        Assert.False(_sut.DirectoryExists(_testDir));
        Directory.CreateDirectory(_testDir);
        Assert.True(_sut.DirectoryExists(_testDir));
    }

    [Fact]
    public void GetDirectorySize_IncludesSubdirectories()
    {
        Directory.CreateDirectory(_testDir);
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(_testDir, "root.bin"), new byte[50]);
        File.WriteAllBytes(Path.Combine(subDir, "child.bin"), new byte[150]);

        var size = _sut.GetDirectorySize(_testDir);

        Assert.Equal(200, size);
    }
}
