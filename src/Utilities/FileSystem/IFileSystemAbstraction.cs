// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Utilities.FileSystem
{
    /// <summary>
    /// Abstracts away some file system operations
    /// </summary>
    public interface IFileSystemAbstraction
    {
        /// <summary>
        /// Returns an enumerable collection of file names in a specified path.
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path);

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path, string searchPattern);

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Returns an enumerable collection of directory names in a specified path.
        /// </summary>
        IEnumerable<string> EnumerateDirectories(string path);

        /// <summary>
        /// Returns an enumerable collection of directory names that match a search pattern in a specified path.
        /// </summary>
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern);

        /// <summary>
        /// Returns an enumerable collection of directory names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Returns an enumerable collection of file names and directory names in a specified path. 
        /// </summary>
        IEnumerable<string> EnumerateFileSystemEntries(string path);

        /// <summary>
        /// Returns an enumerable collection of file names and directory names that match a search pattern in a specified path.
        /// </summary>
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern);

        /// <summary>
        /// Returns an enumerable collection of file names and directory names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Determines whether the given path refers to an existing directory on disk.
        /// </summary>
        bool DirectoryExists(string path);
    }
}
