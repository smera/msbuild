using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;

namespace Microsoft.Build.Utilities.FileSystem
{
    public class WindowsFileSystem : IFileSystemAbstraction
    {
        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.File);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.File, searchPattern);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.File, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.Directory);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.Directory, searchPattern);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.Directory, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.FileOrDirectory);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.FileOrDirectory, searchPattern);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.FileOrDirectory, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public bool Exists(string path)
        {
            // TODO: reconsider if it makes sense to move this one to native as well
            return Directory.Exists(path);
        }

        /// <summary>
        /// The type of file artifact to search for
        /// </summary>
        private enum FileArtifactType : byte
        {
            File,
            Directory,
            FileOrDirectory
        }

        private static IEnumerable<string> EnumerateFileOrDirectories(string directoryPath, FileArtifactType fileArtifactType, string searchPattern = null, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var enumeration = new List<string>();

            var result = CustomEnumerateDirectoryEntries(
                directoryPath,
                fileArtifactType,
                searchPattern,
                searchOption,
                enumeration);

            // If the result indicates that the enumeration succeeded or the directory does not exist, then the result is considered success.
            // In particular, if the globed directory does not exist, then we want to return the empty file, and track for the anti-dependency.
            if (
                !(result.Status == WindowsNative.EnumerateDirectoryStatus.Success ||
                  result.Status == WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound))
            {
                throw result.CreateExceptionForError();
            }

            return enumeration;
        }

        private static WindowsNative.EnumerateDirectoryResult CustomEnumerateDirectoryEntries(
            string directoryPath,
            FileArtifactType fileArtifactType,
            string pattern,
            SearchOption searchOption,
            ICollection<string> result)
        {
            var searchDirectoryPath = Path.Combine(directoryPath.TrimEnd('\\'), "*");

            using (SafeFindFileHandle findHandle = WindowsNative.FindFirstFileW(searchDirectoryPath, out WindowsNative.WIN32_FIND_DATA findResult))
            {
                if (findHandle.IsInvalid)
                {
                    int hr = Marshal.GetLastWin32Error();
                    Debug.Assert(hr != WindowsNative.ErrorFileNotFound);

                    WindowsNative.EnumerateDirectoryStatus findHandleOpenStatus;
                    switch (hr)
                    {
                        case WindowsNative.ErrorFileNotFound:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound;
                            break;
                        case WindowsNative.ErrorPathNotFound:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound;
                            break;
                        case WindowsNative.ErrorDirectory:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.CannotEnumerateFile;
                            break;
                        case WindowsNative.ErrorAccessDenied:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.AccessDenied;
                            break;
                        default:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.UnknownError;
                            break;
                    }

                    return new WindowsNative.EnumerateDirectoryResult(directoryPath, findHandleOpenStatus, hr);
                }

                while (true)
                {
                    bool isDirectory = (findResult.DwFileAttributes & FileAttributes.Directory) != 0;

                    // There will be entries for the current and parent directories. Ignore those.
                    if (!isDirectory || (findResult.CFileName != "." && findResult.CFileName != ".."))
                    {
                        if (pattern == null || WindowsNative.PathMatchSpecW(findResult.CFileName, pattern))
                        {
                            if (fileArtifactType == FileArtifactType.FileOrDirectory || !(fileArtifactType == FileArtifactType.Directory ^ isDirectory))
                            {
                                result.Add(findResult.CFileName);
                            }
                        }

                        if (searchOption == SearchOption.AllDirectories && isDirectory)
                        {
                            var recurs = CustomEnumerateDirectoryEntries(
                                Path.Combine(directoryPath, findResult.CFileName),
                                fileArtifactType,
                                pattern,
                                searchOption,
                                result);

                            if (!recurs.Succeeded)
                            {
                                return recurs;
                            }
                        }
                    }

                    if (!WindowsNative.FindNextFileW(findHandle, out findResult))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        if (hr == WindowsNative.ErrorNoMoreFiles)
                        {
                            // Graceful completion of enumeration.
                            return new WindowsNative.EnumerateDirectoryResult(
                                directoryPath,
                                WindowsNative.EnumerateDirectoryStatus.Success,
                                hr);
                        }

                        Debug.Assert(hr != WindowsNative.ErrorSuccess);
                        return new WindowsNative.EnumerateDirectoryResult(
                            directoryPath,
                            WindowsNative.EnumerateDirectoryStatus.UnknownError,
                            hr);
                    }
                }
            }
        }
    }
}
