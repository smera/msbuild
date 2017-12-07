// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Utilities.FileSystem
{
    /// <summary>
    /// Factory for <see cref="IFileSystemAbstraction"/>
    /// </summary>
    public static class FileSystemFactory
    {
        /// <nodoc/>
        public static IFileSystemAbstraction GetFileSystem()
        {
            // The windows-specific file system is only available on WindowsXp or higher
            if (IsWinXpOrHigher())
            {
                return WindowsFileSystem.Singleton();
            }

            // Otherwise we fall back into the standard managed file system API
            return ManagedFileSystem.Singleton();
        }

        private static bool IsWinXpOrHigher()
        {
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT &&
                   ((os.Version.Major > 5) || ((os.Version.Major == 5) && (os.Version.Minor >= 1)));
        }
    }
}
