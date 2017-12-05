using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Utilities.FileSystem
{
    public class WindowsFileSystem : IFileSystemAbstraction
    {
        public IEnumerable<string> EnumerateFiles(string path)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            throw new System.NotImplementedException();
        }

        public bool Exists(string path)
        {
            throw new System.NotImplementedException();
        }

        private static WindowsNative.EnumerateDirectoryResult CustomEnumerateDirectoryEntries(
            string directoryPath,
            bool enumerateDirectory,
            string pattern,
            bool recursive,
            DirectoryEntriesAccumulator accumulators)
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

                    accumulators.Current.Succeeded = false;
                    return new WindowsNative.EnumerateDirectoryResult(directoryPath, findHandleOpenStatus, hr);
                }

                var accumulator = accumulators.Current;

                while (true)
                {
                    bool isDirectory = (findResult.DwFileAttributes & FileAttributes.Directory) != 0;

                    // There will be entries for the current and parent directories. Ignore those.
                    if (!isDirectory || (findResult.CFileName != "." && findResult.CFileName != ".."))
                    {
                        if (WindowsNative.PathMatchSpecW(findResult.CFileName, pattern))
                        {
                            if (!(enumerateDirectory ^ isDirectory))
                            {
                                accumulator.AddFile(findResult.CFileName);
                            }
                        }

                        accumulator.AddTrackFile(findResult.CFileName, findResult.DwFileAttributes);

                        if (recursive && isDirectory)
                        {
                            accumulators.AddNew(accumulator, findResult.CFileName);
                            var recurs = CustomEnumerateDirectoryEntries(
                                Path.Combine(directoryPath, findResult.CFileName),
                                enumerateDirectory,
                                pattern,
                                true,
                                accumulators);

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
