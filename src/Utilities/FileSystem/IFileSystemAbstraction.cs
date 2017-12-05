using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Utilities.FileSystem
{
    public interface IFileSystemAbstraction
    {
        IEnumerable<string> EnumerateFiles(string path);
        IEnumerable<string> EnumerateFiles(string path, string searchPattern);
        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

        IEnumerable<string> EnumerateDirectories(string path);
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern);
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

        IEnumerable<string> EnumerateFileSystemEntries(string path);
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern);
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);

        bool Exists(string path);
    }
}
