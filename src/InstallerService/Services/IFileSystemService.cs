namespace InstallerService.Services;

/// <summary>
/// Manages file system operations: directory creation, ACL permissions, cleanup.
/// </summary>
public interface IFileSystemService
{
    /// <summary>Ensure a directory exists with proper permissions.</summary>
    void EnsureDirectory(string path);

    /// <summary>Set full-control permissions on a directory for a Windows user/group.</summary>
    void SetDirectoryPermissions(string path, string identity, bool fullControl);

    /// <summary>Copy a file with optional overwrite.</summary>
    void CopyFile(string source, string destination, bool overwrite = true);

    /// <summary>Safely delete a file if it exists.</summary>
    bool DeleteFile(string path);

    /// <summary>Get the size of a directory in bytes.</summary>
    long GetDirectorySize(string path);

    /// <summary>Clean files older than a threshold from a directory.</summary>
    int CleanOldFiles(string directory, TimeSpan maxAge, string pattern = "*");

    /// <summary>Check whether a file exists.</summary>
    bool FileExists(string path);

    /// <summary>Check whether a directory exists.</summary>
    bool DirectoryExists(string path);
}
