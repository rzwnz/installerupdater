using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using InstallerService.Configuration;

namespace InstallerService.Tests.Registry;

public class InMemoryRegistryManagerTests
{
    private readonly InMemoryRegistryManager _sut;

    public InMemoryRegistryManagerTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<InMemoryRegistryManager>();
        _sut = new InMemoryRegistryManager(logger);
    }

    [Fact]
    public void WriteString_ThenReadString_ReturnsValue()
    {
        _sut.WriteString(@"SOFTWARE\Test", "Name", "Value1");
        var result = _sut.ReadString(@"SOFTWARE\Test", "Name");
        Assert.Equal("Value1", result);
    }

    [Fact]
    public void ReadString_NonExistentKey_ReturnsNull()
    {
        var result = _sut.ReadString(@"SOFTWARE\DoesNotExist", "Name");
        Assert.Null(result);
    }

    [Fact]
    public void ReadString_NonExistentValue_ReturnsNull()
    {
        _sut.WriteString(@"SOFTWARE\Test", "Existing", "yes");
        var result = _sut.ReadString(@"SOFTWARE\Test", "Missing");
        Assert.Null(result);
    }

    [Fact]
    public void WriteDWord_ThenReadDWord_ReturnsValue()
    {
        _sut.WriteDWord(@"SOFTWARE\Test", "Count", 42);
        var result = _sut.ReadDWord(@"SOFTWARE\Test", "Count");
        Assert.Equal(42, result);
    }

    [Fact]
    public void ReadDWord_NonExistentKey_ReturnsNull()
    {
        var result = _sut.ReadDWord(@"SOFTWARE\NoKey", "Count");
        Assert.Null(result);
    }

    [Fact]
    public void ReadDWord_WrongType_ReturnsNull()
    {
        _sut.WriteString(@"SOFTWARE\Test", "Val", "NotAnInt");
        var result = _sut.ReadDWord(@"SOFTWARE\Test", "Val");
        Assert.Null(result);
    }

    [Fact]
    public void DeleteValue_ExistingValue_ReturnsTrue()
    {
        _sut.WriteString(@"SOFTWARE\Test", "ToDelete", "val");
        var deleted = _sut.DeleteValue(@"SOFTWARE\Test", "ToDelete");
        Assert.True(deleted);
        Assert.Null(_sut.ReadString(@"SOFTWARE\Test", "ToDelete"));
    }

    [Fact]
    public void DeleteValue_NonExistentValue_ReturnsFalse()
    {
        var deleted = _sut.DeleteValue(@"SOFTWARE\Test", "NoSuchValue");
        Assert.False(deleted);
    }

    [Fact]
    public void DeleteValue_NonExistentKey_ReturnsFalse()
    {
        var deleted = _sut.DeleteValue(@"SOFTWARE\NoKey", "Val");
        Assert.False(deleted);
    }

    [Fact]
    public void KeyExists_AfterWrite_ReturnsTrue()
    {
        _sut.WriteString(@"SOFTWARE\Exists", "X", "Y");
        Assert.True(_sut.KeyExists(@"SOFTWARE\Exists"));
    }

    [Fact]
    public void KeyExists_NoKey_ReturnsFalse()
    {
        Assert.False(_sut.KeyExists(@"SOFTWARE\NoSuchKey"));
    }

    [Fact]
    public void GetValueNames_ReturnsAllNames()
    {
        _sut.WriteString(@"SOFTWARE\Multi", "A", "1");
        _sut.WriteString(@"SOFTWARE\Multi", "B", "2");
        _sut.WriteDWord(@"SOFTWARE\Multi", "C", 3);

        var names = _sut.GetValueNames(@"SOFTWARE\Multi");
        Assert.Equal(3, names.Count);
        Assert.Contains("A", names);
        Assert.Contains("B", names);
        Assert.Contains("C", names);
    }

    [Fact]
    public void GetValueNames_NonExistentKey_ReturnsEmpty()
    {
        var names = _sut.GetValueNames(@"SOFTWARE\Nothing");
        Assert.Empty(names);
    }

    [Fact]
    public void WriteString_Overwrite_ReturnsLatest()
    {
        _sut.WriteString(@"SOFTWARE\Test", "Key", "First");
        _sut.WriteString(@"SOFTWARE\Test", "Key", "Second");
        Assert.Equal("Second", _sut.ReadString(@"SOFTWARE\Test", "Key"));
    }

    [Fact]
    public void KeyPaths_AreCaseInsensitive()
    {
        _sut.WriteString(@"SOFTWARE\TestCase", "Val", "hello");
        var result = _sut.ReadString(@"software\testcase", "Val");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ValueNames_AreCaseInsensitive()
    {
        _sut.WriteString(@"SOFTWARE\Test", "MyValue", "data");
        var result = _sut.ReadString(@"SOFTWARE\Test", "myvalue");
        Assert.Equal("data", result);
    }
}
