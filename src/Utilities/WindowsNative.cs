using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Win32.SafeHandles;

#pragma warning disable 1591 // disabling warning about missing API documentation; TODO: Remove this line and write documentation!
#pragma warning disable CA1823 // Unused field
#pragma warning disable SA1203 // Constant fields must appear before non-constant fields
#pragma warning disable SA1139 // Use literal suffix notation instead of casting

namespace Microsoft.Build.Utilities.FileSystem
{
    /// <summary>
    /// Storage related native calls
    /// </summary>
    public static class WindowsNative
    {
        #region PInvoke and structs

        /// <summary>
        /// FSCTL_READ_FILE_USN_DATA
        /// </summary>
        private const uint FsctlReadFileUsnData = 0x900eb;

        /// <summary>
        /// FSCTL_WRITE_USN_CLOSE_RECORD
        /// </summary>
        private const uint FsctlWriteUsnCloseRecord = 0x900ef;

        /// <summary>
        /// FSCTL_QUERY_USN_JOURNAL
        /// </summary>
        private const uint FsctlQueryUsnJournal = 0x900f4;

        /// <summary>
        /// FSCTL_READ_USN_JOURNAL
        /// </summary>
        private const uint FsctlReadUsnJournal = 0x900bb;

        /// <summary>
        /// FSCTL_READ_UNPRIVILEGED_USN_JOURNAL
        /// </summary>
        private const uint FsctlReadUnprivilegedUsnJournal = 0x903ab;

        /// <summary>
        /// INVALID_FILE_ATTRIBUTES
        /// </summary>
        private const uint InvalidFileAttributes = 0xFFFFFFFF;

        /// <summary>
        /// ERROR_JOURNAL_NOT_ACTIVE
        /// </summary>
        private const uint ErrorJournalNotActive = 0x49B;

        /// <summary>
        ///  ERROR_JOURNAL_DELETE_IN_PROGRESS
        /// </summary>
        private const uint ErrorJournalDeleteInProgress = 0x49A;

        /// <summary>
        ///  ERROR_JOURNAL_ENTRY_DELETED
        /// </summary>
        private const uint ErrorJournalEntryDeleted = 0x49D;

        /// <summary>
        /// ERROR_NO_MORE_FILES
        /// </summary>
        public const uint ErrorNoMoreFiles = 0x12;

        /// <summary>
        /// ERROR_INVALID_PARAMETER
        /// </summary>
        private const uint ErrorInvalidParameter = 0x57;

        /// <summary>
        /// ERROR_INVALID_FUNCTION
        /// </summary>
        private const uint ErrorInvalidFunction = 0x1;

        /// <summary>
        /// ERROR_ONLY_IF_CONNECTED
        /// </summary>
        private const uint ErrorOnlyIfConnected = 0x4E3;

        /// <summary>
        /// ERROR_SUCCESS
        /// </summary>
        public const int ErrorSuccess = 0x0;

        /// <summary>
        /// ERROR_ACCESS_DENIED
        /// </summary>
        public const int ErrorAccessDenied = 0x5;

        /// <summary>
        /// ERROR_SHARING_VIOLATION
        /// </summary>
        private const int ErrorSharingViolation = 0x20;

        /// <summary>
        /// ERROR_TOO_MANY_LINKS
        /// </summary>
        private const int ErrorTooManyLinks = 0x476;

        /// <summary>
        /// ERROR_NOT_SAME_DEVICE
        /// </summary>
        private const int ErrorNotSameDevice = 0x11;

        /// <summary>
        /// ERROR_NOT_SUPPORTED
        /// </summary>
        private const int ErrorNotSupported = 0x32;

        /// <summary>
        /// ERROR_FILE_NOT_FOUND
        /// </summary>
        public const int ErrorFileNotFound = 0x2;

        /// <summary>
        /// ERROR_PATH_NOT_FOUND
        /// </summary>
        public const int ErrorPathNotFound = 0x3;

        /// <summary>
        /// ERROR_NOT_READY
        /// </summary>
        public const int ErrorNotReady = 0x15;

        /// <summary>
        /// FVE_E_LOCKED_VOLUME
        /// </summary>
        public const int FveLockedVolume = unchecked((int)0x80310000);

        /// <summary>
        /// ERROR_DIRECTORY
        /// </summary>
        public const int ErrorDirectory = 0x10b;

        /// <summary>
        /// ERROR_PARTIAL_COPY
        /// </summary>
        public const int ErrorPartialCopy = 0x12b;

        /// <summary>
        /// ERROR_IO_PENDING
        /// </summary>
        private const int ErrorIOPending = 0x3E5;

        /// <summary>
        /// ERROR_IO_INCOMPLETE
        /// </summary>
        private const int ErrorIOIncomplete = 0x3E4;

        /// <summary>
        /// ERROR_ABANDONED_WAIT_0
        /// </summary>
        private const int ErrorAbandonedWait0 = 0x2DF;

        /// <summary>
        /// ERROR_HANDLE_EOF
        /// </summary>
        private const int ErrorHandleEof = 0x26;

        /// <summary>
        /// Infinite timeout.
        /// </summary>
        private const int Infinite = -1;

        /// <summary>
        /// Maximum path length.
        /// </summary>
        private const int MaxPath = 260;

        /// <summary>
        /// Maximum path length for \\?\ style paths.
        /// </summary>
        private const int MaxLongPath = 32767;

        /// <summary>
        /// A value representing INVALID_HANDLE_VALUE.
        /// </summary>
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>
        /// OSVERSIONINFOEX
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/ms724833(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// This definition is taken with minor modifications from the BCL.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class OsVersionInfoEx
        {
            public static readonly int Size = Marshal.SizeOf<OsVersionInfoEx>();

            public OsVersionInfoEx()
            {
                // This must be set to Size before use, since it is validated by consumers such as VerifyVersionInfo.
                OSVersionInfoSize = Size;
            }

            public int OSVersionInfoSize;
            public int MajorVersion;
            public int MinorVersion;
            public int BuildNumber;
            public int PlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string CSDVersion;
            public ushort ServicePackMajor;
            public ushort ServicePackMinor;
            public short SuiteMask;
            public byte ProductType;
            public byte Reserved;
        }

        /// <summary>
        /// Request structure indicating this program's supported version range of Usn records.
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/hh802705(v=vs.85).aspx
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ReadFileUsnData
        {
            /// <summary>
            /// Size of this structure (there are no variable length fields).
            /// </summary>
            public static readonly int Size = Marshal.SizeOf<ReadFileUsnData>();

            /// <summary>
            /// Indicates that FSCTL_READ_FILE_USN_DATA should return either V2 or V3 records (those with NTFS or ReFS-sized file IDs respectively).
            /// </summary>
            /// <remarks>
            /// This request should work on Windows 8 / Server 2012 and above.
            /// </remarks>
            public static readonly ReadFileUsnData NtfsAndReFSCompatible = new ReadFileUsnData()
            {
                MinMajorVersion = 2,
                MaxMajorVersion = 3,
            };

            /// <summary>
            /// Indicates that FSCTL_READ_FILE_USN_DATA should return only V2 records (those with NTFS file IDs, even if using ReFS).
            /// </summary>
            /// <remarks>
            /// This request should work on Windows 8 / Server 2012 and above.
            /// </remarks>
            public static readonly ReadFileUsnData NtfsCompatible = new ReadFileUsnData()
            {
                MinMajorVersion = 2,
                MaxMajorVersion = 2,
            };

            public ushort MinMajorVersion;
            public ushort MaxMajorVersion;
        }

        /// <summary>
        /// Request structure indicating expected journal identifier, start USN, etc.
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/hh802706(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// We use the V1 rather than V0 structure even before 8.1 / Server 2012 R2 (it is just like
        /// the V0 version but with the version range fields at the end).
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct ReadUsnJournalData
        {
            /// <summary>
            /// Size of this structure (there are no variable length fields).
            /// </summary>
            public static readonly int Size = Marshal.SizeOf<ReadUsnJournalData>();

            public Usn StartUsn;
            public uint ReasonMask;
            public uint ReturnOnlyOnClose;
            public ulong Timeout;
            public ulong BytesToWaitFor;
            public ulong UsnJournalID;
            public ushort MinMajorVersion;
            public ushort MaxMajorVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileRenameInfo
        {
            public byte ReplaceIfExists;
            public IntPtr RootDirectory;

            /// <summary>
            /// Length of the string starting at <see cref="FileName"/> in *bytes* (not characters).
            /// </summary>
            public int FileNameLengthInBytes;

            /// <summary>
            /// First character of filename; this is a variable length array as determined by FileNameLength.
            /// </summary>
            public char FileName;
        }

        /// <summary>
        /// Metadata about a volume's change journal (<c>USN_JOURNAL_DATA_V0</c>).
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa365721(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// We always use a V0 structure rather than V1, since V1 just adds a supported USN version range (e.g. 2, 3).
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public sealed class QueryUsnJournalData
        {
            /// <summary>
            /// Size of this structure (there are no variable length fields).
            /// </summary>
            public static readonly int Size = Marshal.SizeOf<QueryUsnJournalData>();

            /// <summary>
            /// Journal identifier which must be used to read the current journal.
            /// </summary>
            /// <remarks>
            /// If this identifier changes, USNs for the old identifier are invalid.
            /// </remarks>
            public ulong UsnJournalId;

            /// <summary>
            /// First USN that can be read from the current journal.
            /// </summary>
            public Usn FirstUsn;

            /// <summary>
            /// Next USN that will be written to the current journal.
            /// </summary>
            public Usn NextUsn;

            /// <summary>
            /// Lowest USN which is valid for the current journal identifier.
            /// </summary>
            /// <remarks>
            /// <see cref="FirstUsn"/> might be higher since the beginning of the
            /// journal may have been truncated (without a new identifier).
            /// </remarks>
            public Usn LowestValidUsn;

            /// <summary>
            /// Max USN after which the journal will have to be fully re-created.
            /// </summary>
            public Usn MaxUsn;

            /// <summary>
            /// Max size after which part of the journal will be truncated.
            /// </summary>
            public ulong MaximumSize;

            /// <summary>
            /// Number of bytes by which the journal extends (and possibly truncates at the beginning).
            /// </summary>
            public ulong AllocationDelta;
        }

        /// <summary>
        /// 128-bit file ID, which durably and uniquely represents a file on an NTFS or ReFS volume.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct FileId : IEquatable<FileId>
        {
            /// <summary>
            /// Low bits
            /// </summary>
            public readonly ulong Low;

            /// <summary>
            /// High bits
            /// </summary>
            public readonly ulong High;

            /// <summary>
            /// Constructs a file ID from two longs, constituting the high and low bits (128 bits total).
            /// </summary>
            public FileId(ulong high, ulong low)
            {
                High = high;
                Low = low;
            }

            /// <inheritdoc />
            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "[FileID 0x{0:X16}{1:X16}]", High, Low);
            }

            /// <inheritdoc />
            public bool Equals(FileId other)
            {
                return other.High == High && other.Low == Low;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return StructUtilities.Equals(this, obj);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                // An ancient prophecy has foretold of a ReFS file ID that actually needed the high bits.
                return unchecked((int)((High ^ Low) ^ ((High ^ Low) >> 32)));
            }

            /// <nodoc />
            public static bool operator ==(FileId left, FileId right)
            {
                return left.Equals(right);
            }

            /// <nodoc />
            public static bool operator !=(FileId left, FileId right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Union tag for <see cref="FileIdDescriptor"/>.
        /// </summary>
        /// <remarks>
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa364227(v=vs.85).aspx
        /// </remarks>
        private enum FileIdDescriptorType
        {
            FileId = 0,

            // ObjectId = 1, - Not supported
            ExtendedFileId = 2,
        }

        /// <summary>
        /// Structure to specify a file ID to <see cref="WindowsNative.OpenFileById"/>.
        /// </summary>
        /// <remarks>
        /// On the native side, the ID field is a union of a 64-bit file ID, a 128-bit file ID,
        /// and an object ID (GUID). Since we only pass this in to <see cref="WindowsNative.OpenFileById"/>
        /// we simply specify the ID part to C# as a 128-bit file ID and ensure that the high bytes are
        /// empty when we are specifying a 64-bit ID.
        /// Note that since downlevel the union members are a GUID and a 64-bit file ID (extended file ID unsupported),
        /// the structure size is fortunately same in all cases (because the object ID GUID is 16 bytes / 128-bits).
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa364227(v=vs.85).aspx
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdDescriptor
        {
            private static readonly int s_size = Marshal.SizeOf<FileIdDescriptor>();

            public readonly int Size;
            public readonly FileIdDescriptorType Type;
            public readonly FileId ExtendedFileId;

            public FileIdDescriptor(FileId fileId)
            {
                if (IsExtendedFileIdSupported())
                {
                    Type = FileIdDescriptorType.ExtendedFileId;
                }
                else
                {
                    Debug.Assert(fileId.High == 0, "File ID should not have high bytes when extended IDs are not supported on the underlying OS");
                    Type = FileIdDescriptorType.FileId;
                }

                Size = s_size;
                ExtendedFileId = fileId;
            }
        }

        /// <summary>
        /// These flags indicate the changes represented by a particular Usn record.
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/hh802708(v=vs.85).aspx
        /// </summary>
        [Flags]
        public enum UsnChangeReasons : uint
        {
            /// <summary>
            /// A user has either changed one or more file or directory attributes (for example, the read-only,
            /// hidden, system, archive, or sparse attribute), or one or more time stamps.
            /// </summary>
            BasicInfoChange = 0x00008000,

            /// <summary>
            /// The file or directory is closed.
            /// </summary>
            Close = 0x80000000,

            /// <summary>
            /// The compression state of the file or directory is changed from or to compressed.
            /// </summary>
            CompressionChange = 0x00020000,

            /// <summary>
            /// The file or directory is extended (added to).
            /// </summary>
            DataExtend = 0x00000002,

            /// <summary>
            /// The data in the file or directory is overwritten.
            /// </summary>
            DataOverwrite = 0x00000001,

            /// <summary>
            /// The file or directory is truncated.
            /// </summary>
            DataTruncation = 0x00000004,

            /// <summary>
            /// The user made a change to the extended attributes of a file or directory.
            /// These NTFS file system attributes are not accessible to Windows-based applications.
            /// </summary>
            ExtendedAttributesChange = 0x00000400,

            /// <summary>
            /// The file or directory is encrypted or decrypted.
            /// </summary>
            EncryptionChange = 0x00040000,

            /// <summary>
            /// The file or directory is created for the first time.
            /// </summary>
            FileCreate = 0x00000100,

            /// <summary>
            /// The file or directory is deleted.
            /// </summary>
            FileDelete = 0x00000200,

            /// <summary>
            /// An NTFS file system hard link is added to or removed from the file or directory.
            /// An NTFS file system hard link, similar to a POSIX hard link, is one of several directory
            /// entries that see the same file or directory.
            /// </summary>
            HardLinkChange = 0x00010000,

            /// <summary>
            /// A user changes the FILE_ATTRIBUTE_NOT_CONTENT_INDEXED attribute.
            /// </summary>
            IndexableChange = 0x00004000,

            /// <summary>
            /// The one or more named data streams for a file are extended (added to).
            /// </summary>
            NamedDataExtend = 0x00000020,

            /// <summary>
            /// The data in one or more named data streams for a file is overwritten.
            /// </summary>
            NamedDataOverwrite = 0x00000010,

            /// <summary>
            /// The one or more named data streams for a file is truncated.
            /// </summary>
            NamedDataTruncation = 0x00000040,

            /// <summary>
            /// The object identifier of a file or directory is changed.
            /// </summary>
            ObjectIdChange = 0x00080000,

            /// <summary>
            /// A file or directory is renamed, and the file name in the USN_RECORD_V3 structure is the new name.
            /// </summary>
            RenameNewName = 0x00002000,

            /// <summary>
            /// The file or directory is renamed, and the file name in the USN_RECORD_V3 structure is the previous name.
            /// </summary>
            RenameOldName = 0x00001000,

            /// <summary>
            /// The reparse point that is contained in a file or directory is changed, or a reparse point is added to or
            /// deleted from a file or directory.
            /// </summary>
            ReparsePointChange = 0x00100000,

            /// <summary>
            /// A change is made in the access rights to a file or directory.
            /// </summary>
            SecurityChange = 0x00000800,

            /// <summary>
            /// A named stream is added to or removed from a file, or a named stream is renamed.
            /// </summary>
            StreamChange = 0x00200000,
        }

        /// <summary>
        /// Header data in common between USN_RECORD_V2 and USN_RECORD_V3. These fields are needed to determine how to interpret a returned record.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeUsnRecordHeader
        {
            /// <summary>
            /// Size of the record header in bytes.
            /// </summary>
            public static readonly int Size = Marshal.SizeOf<NativeUsnRecordHeader>();

            public int RecordLength;
            public ushort MajorVersion;
            public ushort MinorVersion;
        }

        /// <summary>
        /// USN_RECORD_V3
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/hh802708(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// The Size is explicitly set to the actual used size + the needing padding to 8-byte alignment
        /// (for Usn, Timestamp, etc.). Two of those padding bytes are actually the first character of the filename.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Size = 0x50)]
        private struct NativeUsnRecordV3
        {
            /// <summary>
            /// Size of a record with two filename characters (starting at WCHAR FileName[1]; not modeled in the C# struct),
            /// or one filename character and two bytes of then-needed padding (zero-length filenames are disallowed).
            /// This is the minimum size that should ever be returned.
            /// </summary>
            public static readonly int MinimumSize = Marshal.SizeOf<NativeUsnRecordV3>();

            /// <summary>
            /// Maximum size of a single V3 record, assuming the NTFS / ReFS 255 character file name length limit.
            /// </summary>
            /// <remarks>
            /// ( (MaximumComponentLength - 1) * sizeof(WCHAR) + sizeof(USN_RECORD_V3)
            /// See http://msdn.microsoft.com/en-us/library/windows/desktop/hh802708(v=vs.85).aspx
            /// Due to padding this is perhaps an overestimate.
            /// </remarks>
            public static readonly int MaximumSize = MinimumSize + (254 * 2);

            public NativeUsnRecordHeader Header;
            public FileId FileReferenceNumber;
            public FileId ParentFileReferenceNumber;
            public Usn Usn;
            public long TimeStamp;
            public uint Reason;
            public uint SourceInfo;
            public uint SecurityId;
            public uint FileAttributes;
            public ushort FileNameLength;
            public ushort FileNameOffset;

            // WCHAR FileName[1];
        }

        /// <summary>
        /// TODO: this is not documented by WDG yet.
        /// TODO: OpenSource
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct FileDispositionInfoEx
        {
            public FileDispositionFlags Flags;
        }

        /// <summary>
        /// TODO: this is not properly documented by WDG yet.
        /// TODO: OpenSource
        /// </summary>
        [Flags]
        private enum FileDispositionFlags : uint
        {
#pragma warning disable CA1008 // Enums should have zero value
            DoNotDelete = 0x00000000,
#pragma warning restore CA1008 // Enums should have zero value
            Delete = 0x00000001,

            /// <summary>
            /// NTFS default behavior on link removal is when the last handle is closed on that link, the link is physically gone.
            /// The link is marked for deletion when the FILE_FLAG_DELETE_ON_CLOSE is specified on open or FileDispositionInfo is called.
            /// Although, the link is marked as deleted until the last handle on that link is closed,
            /// it can not be re-purposed as it physically exists.
            /// This is also true for superseded rename case where the target cannot be deleted if other handles are opened on that link.
            /// This makes Windows distinct in nature than how Linux works handling the links where the link name is freed
            /// and can be re-purposed as soon as you deleted/rename the link by closing the handle that requested the delete/rename
            /// regardless of other handles are opened on that link.
            /// FileDispositionInfoEx and FileRenameInfoEx implement the POSIX style delete/rename behavior.
            /// For POSIX style superseded rename, the target needs to be opened with FILE_SHARE_DELETE access by other openers.
            /// </summary>
            PosixSemantics = 0x00000002,
            ForceImageSectionCheck = 0x00000004,
            OnClose = 0x00000008,
        }

        /// <summary>
        /// USN_RECORD_V2
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa365722(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// The Size is explicitly set to the actual used size + the needing padding to 8-byte alignment
        /// (for Usn, Timestamp, etc.). Two of those padding bytes are actually the first character of the filename.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Size = 0x40)]
        private struct NativeUsnRecordV2
        {
            /// <summary>
            /// Size of a record with two filename characters (starting at WCHAR FileName[1]; not modeled in the C# struct),
            /// or one filename character and two bytes of then-needed padding (zero-length filenames are disallowed).
            /// This is the minimum size that should ever be returned.
            /// </summary>
            public static readonly int MinimumSize = Marshal.SizeOf<NativeUsnRecordV2>();

            /// <summary>
            /// Maximum size of a single V2 record, assuming the NTFS / ReFS 255 character file name length limit.
            /// </summary>
            /// <remarks>
            /// ( (MaximumComponentLength - 1) * sizeof(WCHAR) + sizeof(USN_RECORD_V2)
            /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa365722(v=vs.85).aspx
            /// Due to padding this is perhaps an overestimate.
            /// </remarks>
            public static readonly int MaximumSize = MinimumSize + (254 * 2);

            public NativeUsnRecordHeader Header;
            public ulong FileReferenceNumber;
            public ulong ParentFileReferenceNumber;
            public Usn Usn;
            public long TimeStamp;
            public uint Reason;
            public uint SourceInfo;
            public uint SecurityId;
            public uint FileAttributes;
            public ushort FileNameLength;
            public ushort FileNameOffset;

            // WCHAR FileName[1];
        }

        /// <summary>
        /// FILE_INFO_BY_HANDLE_CLASS for GetFileInformationByHandleEx.
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa364953(v=vs.85).aspx
        /// </summary>
        private enum FileInfoByHandleClass : uint
        {
            FileBasicInfo = 0x0,
            FileStandardInfo = 0x1,
            FileNameInfo = 0x2,
            FileRenameInfo = 0x3,
            FileDispositionInfo = 0x4,
            FileAllocationInfo = 0x5,
            FileEndOfFileInfo = 0x6,
            FileStreamInfo = 0x7,
            FileCompressionInfo = 0x8,
            FileAttributeTagInfo = 0x9,
            FileIdBothDirectoryInfo = 0xa,
            FileIdBothDirectoryRestartInfo = 0xb,
            FileRemoteProtocolInfo = 0xd,
            FileFullDirectoryInfo = 0xe,
            FileFullDirectoryRestartInfo = 0xf,
            FileStorageInfo = 0x10,
            FileAlignmentInfo = 0x11,
            FileIdInfo = 0x12,
            FileIdExtdDirectoryInfo = 0x13,
            FileIdExtdDirectoryRestartInfo = 0x14,
            FileDispositionInfoEx = 0x15,
            FileRenameInfoEx = 0x16,
        }

        /// <summary>
        /// This corresponds to FILE_ID_INFO as returned by GetFileInformationByHandleEx (with <see cref="FileInfoByHandleClass.FileIdInfo"/>).
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/hh802691(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// Note that the FileId field supports a ReFS-sized ID. This is because the corresponding FileIdInfo class was added in 8.1 / Server 2012 R2.
        /// </remarks>
        [StructLayout(LayoutKind.Sequential)]
        public struct FileIdAndVolumeId : IEquatable<FileIdAndVolumeId>
        {
            internal static readonly int Size = Marshal.SizeOf<FileIdAndVolumeId>();

            /// <summary>
            /// Volume containing the file.
            /// </summary>
            public readonly ulong VolumeSerialNumber;

            /// <summary>
            /// Unique identifier of the referenced file (within the containing volume).
            /// </summary>
            public readonly FileId FileId;

            /// <obvious />
            public FileIdAndVolumeId(ulong volumeSerialNumber, FileId fileId)
            {
                VolumeSerialNumber = volumeSerialNumber;
                FileId = fileId;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return StructUtilities.Equals(this, obj);
            }

            /// <inheritdoc />
            public bool Equals(FileIdAndVolumeId other)
            {
                return FileId == other.FileId && VolumeSerialNumber == other.VolumeSerialNumber;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return FileId.GetHashCode() ^ VolumeSerialNumber.GetHashCode();
            }

            /// <nodoc />
            public static bool operator ==(FileIdAndVolumeId left, FileIdAndVolumeId right)
            {
                return left.Equals(right);
            }

            /// <nodoc />
            public static bool operator !=(FileIdAndVolumeId left, FileIdAndVolumeId right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Represents per-file USN data (that returned from a single-file query with <see cref="WindowsNative.ReadFileUsnByHandle"/>).
        /// </summary>
        /// <remarks>
        /// This is the managed projection of the useful fields of <see cref="NativeUsnRecordV3"/> when querying a single file.
        /// It does not correspond to any actual native structure.
        /// </remarks>
        public struct MiniUsnRecord : IEquatable<MiniUsnRecord>
        {
            /// <summary>
            /// ID of the file to which this record pertains
            /// </summary>
            public readonly FileId FileId;

            /// <summary>
            /// Change journal cursor at which this record sits.
            /// </summary>
            public readonly Usn Usn;

            internal MiniUsnRecord(FileId file, Usn usn)
            {
                FileId = file;
                Usn = usn;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return StructUtilities.Equals(this, obj);
            }

            /// <inheritdoc />
            public bool Equals(MiniUsnRecord other)
            {
                return FileId == other.FileId && Usn == other.Usn;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return HashCodeHelper.Combine(FileId.GetHashCode(), Usn.GetHashCode());
            }

            /// <nodoc />
            public static bool operator ==(MiniUsnRecord left, MiniUsnRecord right)
            {
                return left.Equals(right);
            }

            /// <nodoc />
            public static bool operator !=(MiniUsnRecord left, MiniUsnRecord right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Represents USN data from reading a journal.
        /// </summary>
        /// <remarks>
        /// This is the managed projection of the useful fields of <see cref="NativeUsnRecordV3"/>.
        /// It does not correspond to any actual native structure.
        /// Note that this record may be invalid. A record is invalid if it has Usn 0, which indicates
        /// that either the volume's change journal is disabled or that this particular file has not
        /// been modified since the change journal was enabled.
        /// </remarks>
        public struct UsnRecord : IEquatable<UsnRecord>
        {
            /// <summary>
            /// ID of the file to which this record pertains
            /// </summary>
            public readonly FileId FileId;

            /// <summary>
            /// ID of the containing directory of the file at the time of this change.
            /// </summary>
            public readonly FileId ContainerFileId;

            /// <summary>
            /// Change journal cursor at which this record sits.
            /// </summary>
            public readonly Usn Usn;

            /// <summary>
            /// Reason for the change.
            /// </summary>
            public readonly UsnChangeReasons Reason;

            internal UsnRecord(FileId file, FileId container, Usn usn, UsnChangeReasons reasons)
            {
                FileId = file;
                ContainerFileId = container;
                Usn = usn;
                Reason = reasons;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return StructUtilities.Equals(this, obj);
            }

            /// <inheritdoc />
            public bool Equals(UsnRecord other)
            {
                return FileId == other.FileId && Usn == other.Usn && Reason == other.Reason && ContainerFileId == other.ContainerFileId;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return HashCodeHelper.Combine(FileId.GetHashCode(), Usn.GetHashCode(), Reason.GetHashCode(), ContainerFileId.GetHashCode());
            }

            /// <nodoc />
            public static bool operator ==(UsnRecord left, UsnRecord right)
            {
                return left.Equals(right);
            }

            /// <nodoc />
            public static bool operator !=(UsnRecord left, UsnRecord right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Flags for <c>CreateFile</c> and <c>OpenFileById</c>
        /// </summary>
        [Flags]
        public enum FileFlagsAndAttributes : uint
        {
            /// <summary>
            /// No flags.
            /// </summary>
            None = 0,

            /// <summary>
            /// The file should be archived. Applications use this attribute to mark files for backup or removal.
            /// </summary>
            FileAttributeArchive = 0x20,

            /// <summary>
            /// The file or directory is encrypted. For a file, this means that all data in the file is encrypted. For a directory,
            /// this means that encryption is the default for newly created files and subdirectories. For more information, see File
            /// Encryption.
            /// This flag has no effect if FILE_ATTRIBUTE_SYSTEM is also specified.
            /// </summary>
            FileAttributeEncrypted = 0x4000,

            /// <summary>
            /// The file is hidden. Do not include it in an ordinary directory listing.
            /// </summary>
            FileAttributeHidden = 0x2,

            /// <summary>
            /// The file does not have other attributes set. This attribute is valid only if used alone.
            /// </summary>
            FileAttributeNormal = 0x80,

            /// <summary>
            /// The data of a file is not immediately available. This attribute indicates that file data is physically moved to offline
            /// storage. This attribute is used by Remote Storage, the hierarchical storage management software. Applications should
            /// not arbitrarily change this attribute.
            /// </summary>
            FileAttributeOffline = 0x1000,

            /// <summary>
            /// The file is read only. Applications can read the file, but cannot write to or delete it.
            /// </summary>
            FileAttributeReadOnly = 0x1,

            /// <summary>
            /// The file is part of or used exclusively by an operating system.
            /// </summary>
            FileAttributeSystem = 0x4,

            /// <summary>
            /// The file is being used for temporary storage.
            /// </summary>
            FileAttributeTemporary = 0x100,

            /// <summary>
            /// The file is being opened or created for a backup or restore operation. The system ensures that the calling process
            /// overrides file security checks when the process has SE_BACKUP_NAME and SE_RESTORE_NAME privileges. For more
            /// information, see Changing Privileges in a Token.
            /// You must set this flag to obtain a handle to a directory. A directory handle can be passed to some functions instead of
            /// a file handle.
            /// </summary>
            FileFlagBackupSemantics = 0x02000000,

            /// <summary>
            /// The file is to be deleted immediately after all of its handles are closed, which includes the specified handle and any
            /// other open or duplicated handles.
            /// If there are existing open handles to a file, the call fails unless they were all opened with the FILE_SHARE_DELETE
            /// share mode.
            /// Subsequent open requests for the file fail, unless the FILE_SHARE_DELETE share mode is specified.
            /// </summary>
            FileFlagDeleteOnClose = 0x04000000,

            /// <summary>
            /// The file or device is being opened with no system caching for data reads and writes. This flag does not affect hard
            /// disk caching or memory mapped files.
            /// </summary>
            FileFlagNoBuffering = 0x20000000,

            /// <summary>
            /// The file data is requested, but it should continue to be located in remote storage. It should not be transported back
            /// to local storage. This flag is for use by remote storage systems.
            /// </summary>
            FileFlagOpenNoRecall = 0x00100000,

            /// <summary>
            /// Normal reparse point processing will not occur; CreateFile will attempt to open the reparse point. When a file is
            /// opened, a file handle is returned, whether or not the filter that controls the reparse point is operational.
            /// This flag cannot be used with the CREATE_ALWAYS flag.
            /// If the file is not a reparse point, then this flag is ignored.
            /// </summary>
            FileFlagOpenReparsePoint = 0x00200000,

            /// <summary>
            /// The file or device is being opened or created for asynchronous I/O.
            /// When subsequent I/O operations are completed on this handle, the event specified in the OVERLAPPED structure will be
            /// set to the signaled state.
            /// If this flag is specified, the file can be used for simultaneous read and write operations.
            /// If this flag is not specified, then I/O operations are serialized, even if the calls to the read and write functions
            /// specify an OVERLAPPED structure.
            /// </summary>
            FileFlagOverlapped = 0x40000000,

            /// <summary>
            /// Access will occur according to POSIX rules. This includes allowing multiple files with names, differing only in case,
            /// for file systems that support that naming.
            /// Use care when using this option, because files created with this flag may not be accessible by applications that are
            /// written for MS-DOS or 16-bit Windows.
            /// </summary>
            FileFlagPosixSemantics = 0x0100000,

            /// <summary>
            /// Access is intended to be random. The system can use this as a hint to optimize file caching.
            /// This flag has no effect if the file system does not support cached I/O and FILE_FLAG_NO_BUFFERING.
            /// </summary>
            FileFlagRandomAccess = 0x10000000,

            /// <summary>
            /// The file or device is being opened with session awareness. If this flag is not specified, then per-session devices
            /// (such as a redirected USB device) cannot be opened by processes running in session 0.
            /// </summary>
            FileFlagSessionAware = 0x00800000,

            /// <summary>
            /// Access is intended to be sequential from beginning to end. The system can use this as a hint to optimize file caching.
            /// This flag should not be used if read-behind (that is, reverse scans) will be used.
            /// This flag has no effect if the file system does not support cached I/O and FILE_FLAG_NO_BUFFERING.
            /// For more information, see the Caching Behavior section of this topic.
            /// </summary>
            FileFlagSequentialScan = 0x08000000,

            /// <summary>
            /// Write operations will not go through any intermediate cache, they will go directly to disk.
            /// </summary>
            FileFlagWriteThrough = 0x80000000,

            /// <summary>
            /// When opening a named pipe, the pipe server can only impersonate this client at the 'anonymous' level (i.e., no privilege is made available).
            /// </summary>
            /// <remarks>
            /// This is actually <c>SECURITY_SQOS_PRESENT</c> which makes <c>CreateFile</c> respect SQQS flags; those flags are ignored unless this is specified.
            /// But <c>SECURITY_ANONYMOUS</c> is zero; so think of this as those two flags together (much easier to use correctly).
            /// </remarks>
            SecurityAnonymous = 0x00100000,
        }

        /// <summary>
        /// Normalized status indication (derived from a native error code and the creation disposition).
        /// </summary>
        /// <remarks>
        /// This is useful for two reasons: it is an enum for which we can know all cases are handled, and successful opens
        /// are always <see cref="OpenFileStatus.Success"/> (the distinction between opening / creating files is moved to
        /// <see cref="OpenFileResult.OpenedOrTruncatedExistingFile"/>)
        /// </remarks>
        public enum OpenFileStatus
        {
            /// <summary>
            /// The file was opened (a valid handle was obtained).
            /// </summary>
            /// <remarks>
            /// The <see cref="OpenFileResult.NativeErrorCode"/> may be something other than <c>ERROR_SUCCESS</c>,
            /// since some open modes indicate if a file existed already or was created new via a special error code.
            /// </remarks>
            Success,

            /// <summary>
            /// The file was not found, and no handle was obtained.
            /// </summary>
            FileNotFound,

            /// <summary>
            /// Some directory component in the path was not found, and no handle was obtained.
            /// </summary>
            PathNotFound,

            /// <summary>
            /// The file was opened already with an incompatible share mode, and no handle was obtained.
            /// </summary>
            SharingViolation,

            /// <summary>
            /// The file cannot be opened with the requested access level, and no handle was obtained.
            /// </summary>
            AccessDenied,

            /// <summary>
            /// The file already exists (and the open mode specifies failure for existent files); no handle was obtained.
            /// </summary>
            FileAlreadyExists,

            /// <summary>
            /// The device the file is on is not ready. Should be treated as a nonexistent file.
            /// </summary>
            ErrorNotReady,

            /// <summary>
            /// The volume the file is on is locked. Should be treated as a nonexistent file.
            /// </summary>
            FveLockedVolume,

            /// <summary>
            /// See <see cref="OpenFileResult.NativeErrorCode"/>
            /// </summary>
            UnknownError,
        }

        /// <summary>
        /// Whether the hresult status is one that should be treated as a nonexistent file
        /// </summary>
        /// <remarks>
        /// This must be in sync with the code in static bool IsPathNonexistent(DWORD error) function on the Detours side in FileAccessHelper.cpp.
        ///
        /// Also keep this in sync with <see cref="IsNonexistent(OpenFileStatus)"/> below
        /// NotReadyDevice is treated as non-existent probe.
        /// BitLocker locked volume is treated as non-existent probe.
        /// </remarks>
        public static bool IsHresultNonesixtent(int hr)
        {
            return hr == ErrorFileNotFound ||
                    hr == ErrorPathNotFound ||
                    hr == ErrorNotReady ||
                    hr == FveLockedVolume;
        }

        /// <summary>
        /// Whether the status is one that should be treated as a nonexistent file
        /// </summary>
        /// <remarks>
        /// Keep this in sync with <see cref="IsHresultNonesixtent(int)"/> above </remarks>
        public static bool IsNonexistent(this OpenFileStatus status)
        {
            return status == OpenFileStatus.FileNotFound ||
                status == OpenFileStatus.PathNotFound ||
                status == OpenFileStatus.ErrorNotReady ||
                status == OpenFileStatus.FveLockedVolume;
        }

        /// <summary>
        /// Represents the result of attempting to open a file (such as with <see cref="WindowsNative.OpenFileById"/>).
        /// </summary>
        public struct OpenFileResult : IEquatable<OpenFileResult>
        {
            /// <summary>
            /// Normalized status indication (derived from <see cref="NativeErrorCode"/> and the creation disposition).
            /// </summary>
            /// <remarks>
            /// This is useful for two reasons: it is an enum for which we can know all cases are handled, and successful opens
            /// are always <see cref="OpenFileStatus.Success"/> (the distinction between opening / creating files is moved to
            /// <see cref="OpenedOrTruncatedExistingFile"/>)
            /// </remarks>
            public readonly OpenFileStatus Status;

            /// <summary>
            /// Indicates if an existing file was opened (or truncated). For creation dispositions such as <see cref="FileMode.OpenOrCreate"/>,
            /// either value is possible on success. On failure, this is always <c>false</c> since no file was opened.
            /// </summary>
            public readonly bool OpenedOrTruncatedExistingFile;

            /// <summary>
            /// Native error code.
            /// </summary>
            /// <remarks>
            /// This is the same as returned by <c>GetLastError</c>, except when it is not guaranteed to be set; then it is normalized to
            /// <c>ERROR_SUCCESS</c>
            /// </remarks>
            public readonly int NativeErrorCode;

            private const int ErrorFileExists = 0x50;

            private const int ErrorAlreadyExists = 0xB7;

            private const int ErrorInvalidParameter = 0x57;

            /// <summary>
            /// Creates an <see cref="OpenFileResult"/> without any normalization from native error code.
            /// </summary>
            public OpenFileResult(OpenFileStatus status, int nativeErrorCode, bool openedOrTruncatedExistingFile)
            {
                Status = status;
                NativeErrorCode = nativeErrorCode;
                OpenedOrTruncatedExistingFile = openedOrTruncatedExistingFile;
            }

            /// <summary>
            /// Creates an <see cref="OpenFileResult"/> from observed return values from a native function.
            /// </summary>
            /// <remarks>
            /// <paramref name="openingById"/> is needed since <c>OpenFileById</c> has some quirky error codes.
            /// </remarks>
            public OpenFileResult(int nativeErrorCode, FileMode creationDisposition, bool handleIsValid, bool openingById)
            {
                // Here's a handy table of various FileModes, corresponding dwCreationDisposition, and their properties:
                // See http://msdn.microsoft.com/en-us/library/windows/desktop/aa363858(v=vs.85).aspx
                // Managed FileMode | Creation disp.    | Error always set? | Distinguish existence?    | Existing file on success?
                // ----------------------------------------------------------------------------------------------------------------
                // Append           | OPEN_ALWAYS       | 1                 | 1                         | 0
                // Create           | CREATE_ALWAYS     | 1                 | 1                         | 0
                // CreateNew        | CREATE_NEW        | 0                 | 0                         | 0
                // Open             | OPEN_EXISTING     | 0                 | 0                         | 1
                // OpenOrCreate     | OPEN_ALWAYS       | 1                 | 1                         | 0
                // Truncate         | TRUNCATE_EXISTING | 0                 | 0                         | 1
                //
                // Note that some modes always set a valid last-error, and those are the same modes
                // that distinguish existence on success (i.e., did we just create a new file or did we open one).
                // The others do not promise to set ERROR_SUCCESS and instead failure implies existence
                // (or absence) according to the 'Existing file on success?' column.
                bool modeDistinguishesExistence =
                    creationDisposition == FileMode.OpenOrCreate ||
                    creationDisposition == FileMode.Create ||
                    creationDisposition == FileMode.Append;

                if (handleIsValid && !modeDistinguishesExistence)
                {
                    nativeErrorCode = ErrorSuccess;
                }

                NativeErrorCode = nativeErrorCode;
                OpenedOrTruncatedExistingFile = false;

                switch (nativeErrorCode)
                {
                    case ErrorSuccess:
                        Contract.Assume(handleIsValid);
                        Status = OpenFileStatus.Success;
                        OpenedOrTruncatedExistingFile = creationDisposition == FileMode.Open || creationDisposition == FileMode.Truncate;
                        break;
                    case ErrorFileNotFound:
                        Contract.Assume(!handleIsValid);
                        Status = OpenFileStatus.FileNotFound;
                        break;
                    case ErrorPathNotFound:
                        Contract.Assume(!handleIsValid);
                        Status = OpenFileStatus.PathNotFound;
                        break;
                    case ErrorAccessDenied:
                        Contract.Assume(!handleIsValid);
                        Status = OpenFileStatus.AccessDenied;
                        break;
                    case ErrorSharingViolation:
                        Contract.Assume(!handleIsValid);
                        Status = OpenFileStatus.SharingViolation;
                        break;
                    case ErrorNotReady:
                        Status = OpenFileStatus.ErrorNotReady;
                        break;
                    case FveLockedVolume:
                        Status = OpenFileStatus.FveLockedVolume;
                        break;
                    case ErrorInvalidParameter:
                        Contract.Assume(!handleIsValid);

                        // Experimentally, it seems OpenFileById throws ERROR_INVALID_PARAMETER if the file ID doesn't exist.
                        // This is very unfortunate, since that is also used for e.g. invalid sizes for FILE_ID_DESCRIPTOR. Oh well.
                        Status = openingById ? OpenFileStatus.FileNotFound : OpenFileStatus.UnknownError;
                        break;
                    case ErrorFileExists:
                    case ErrorAlreadyExists:
                        if (!handleIsValid)
                        {
                            Contract.Assume(creationDisposition == FileMode.CreateNew);
                            Status = OpenFileStatus.FileAlreadyExists;
                            OpenedOrTruncatedExistingFile = false;
                        }
                        else
                        {
                            Contract.Assert(modeDistinguishesExistence);
                            Status = OpenFileStatus.Success;
                            OpenedOrTruncatedExistingFile = true;
                        }

                        break;
                    default:
                        Contract.Assume(!handleIsValid);
                        Status = OpenFileStatus.UnknownError;
                        break;
                }

                Contract.Assert(Succeeded || !OpenedOrTruncatedExistingFile);
                Contract.Assert(handleIsValid == Succeeded);
            }

            /// <summary>
            /// Indicates if a handle was opened.
            /// </summary>
            public bool Succeeded
            {
                get { return Status == OpenFileStatus.Success; }
            }

            /// <summary>
            /// Throws an exception if the native error code could not be canonicalized (a fairly exceptional circumstance).
            /// </summary>
            /// <remarks>
            /// This is a good <c>default:</c> case when switching on every possible <see cref="OpenFileStatus"/>
            /// </remarks>
            public NativeWin32Exception ThrowForUnknownError()
            {
                Contract.Requires(Status == OpenFileStatus.UnknownError);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Throws an exception if the native error code was canonicalized (known and common, but not handled by the caller).
            /// </summary>
            public NativeWin32Exception ThrowForKnownError()
            {
                Contract.Requires(Status != OpenFileStatus.UnknownError && Status != OpenFileStatus.Success);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Throws an exception for a failed open.
            /// </summary>
            public NativeWin32Exception ThrowForError()
            {
                Contract.Requires(Status != OpenFileStatus.Success);
                throw Status == OpenFileStatus.UnknownError ? ThrowForUnknownError() : ThrowForKnownError();
            }

            /// <summary>
            /// Creates (but does not throw) an exception for this result. The result must not be successful.
            /// </summary>
            public NativeWin32Exception CreateExceptionForError()
            {
                Contract.Requires(Status != OpenFileStatus.Success);
                if (Status == OpenFileStatus.UnknownError)
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        "Opening a file handle failed");
                }
                else
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Opening a file handle failed: {0:G}",
                            Status));
                }
            }

            /// <summary>
            /// Creates a <see cref="Failure"/> representing this result. The result must not be successful.
            /// </summary>
            public Failure CreateFailureForError()
            {
                Contract.Requires(Status != OpenFileStatus.Success);

                if (Status == OpenFileStatus.UnknownError)
                {
                    return new NativeWin32Failure(NativeErrorCode).Annotate("Opening a file handle failed");
                }
                else
                {
                    return new NativeWin32Failure(NativeErrorCode).Annotate(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Opening a file handle failed: {0:G}",
                            Status));
                }
            }

            /// <inheritdoc />
            public bool Equals(OpenFileResult other)
            {
                return other.NativeErrorCode == NativeErrorCode &&
                       other.OpenedOrTruncatedExistingFile == OpenedOrTruncatedExistingFile &&
                       other.Status == Status;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return StructUtilities.Equals(this, obj);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return NativeErrorCode + (OpenedOrTruncatedExistingFile ? 1 : 0) | ((short)Status << 16);
            }

            /// <nodoc />
            public static bool operator ==(OpenFileResult left, OpenFileResult right)
            {
                return left.Equals(right);
            }

            /// <nodoc />
            public static bool operator !=(OpenFileResult left, OpenFileResult right)
            {
                return !left.Equals(right);
            }
        }

        /// <summary>
        /// Desired access flags for <see cref="WindowsNative.CreateFileW"/>
        /// </summary>
        [Flags]
        public enum FileDesiredAccess : uint
        {
            /// <summary>
            /// No access requested.
            /// </summary>
            None = 0,

            /// <summary>
            /// Waitable handle (always required by CreateFile?)
            /// </summary>
            Synchronize = 0x00100000,

            /// <summary>
            /// Object can be deleted.
            /// </summary>
            Delete = 0x00010000,

            /// <summary>
            /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa364399(v=vs.85).aspx
            /// </summary>
            GenericRead = 0x80000000,

            /// <summary>
            /// See http://msdn.microsoft.com/en-us/library/windows/desktop/aa364399(v=vs.85).aspx
            /// </summary>
            GenericWrite = 0x40000000,

            /// <summary>
            /// Can read file or directory attributes.
            /// </summary>
            FileReadAttributes = 0x0080,
        }

        /// <summary>
        /// <c>FILE_BASIC_INFO</c>
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct FileBasicInfo
        {
            /// <summary>
            /// UTC FILETIME of the file's creation.
            /// </summary>
            public ulong CreationTime;

            /// <summary>
            /// UTC FILETIME of the last access to the file.
            /// </summary>
            public ulong LastAccessTime;

            /// <summary>
            /// UTC FILETIME of the last write to the file.
            /// </summary>
            public ulong LastWriteTime;

            /// <summary>
            /// UTC FILETIME of the last change to the file (e.g. attribute change or a write)
            /// </summary>
            public ulong ChangeTime;

            /// <summary>
            /// File attributes
            /// </summary>
            public FileAttributes Attributes;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            FileDesiredAccess dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileMode dwCreationDisposition,
            FileFlagsAndAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern SafeFileHandle ReOpenFile(
            SafeFileHandle hOriginalFile,
            FileDesiredAccess dwDesiredAccess,
            FileShare dwShareMode,
            FileFlagsAndAttributes dwFlagsAndAttributes);

        [Flags]
        private enum FlushFileBuffersFlags : uint
        {
            /// <summary>
            /// Corresponds to <c>FLUSH_FLAGS_FILE_DATA_ONLY</c>.
            /// If set, this operation will write the data for the given file from the
            /// Windows in-memory cache.  This will NOT commit any associated metadata
            /// changes.  This will NOT send a SYNC to the storage device to flush its
            /// cache.  Not supported on volume handles.  Only supported by the NTFS
            /// filesystem.
            /// </summary>
            FileDataOnly = 0x00000001,

            /// <summary>
            /// Corresponds to <c>FLUSH_FLAGS_NO_SYNC</c>.
            /// If set, this operation will commit both the data and metadata changes for
            /// the given file from the Windows in-memory cache.  This will NOT send a SYNC
            /// to the storage device to flush its cache.  Not supported on volume handles.
            /// Only supported by the NTFS filesystem.
            /// </summary>
            NoSync = 0x00000002,
        }

        /// <summary>
        /// Lower-level file-flush facility, like <c>FlushFileBuffers</c>. Allows cache-only flushes without sending an expensive 'sync' command to the underlying disk.
        /// See https://msdn.microsoft.com/en-us/library/windows/hardware/hh967720(v=vs.85).aspx
        /// </summary>
        [DllImport("ntdll.dll", SetLastError = false, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern unsafe NtStatus NtFlushBuffersFileEx(
            SafeFileHandle handle,
            FlushFileBuffersFlags mode,
            void* parameters,
            int parametersSize,
            IoStatusBlock* ioStatusBlock);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern SafeFileHandle OpenFileById(
            SafeFileHandle hFile, // Any handle on the relevant volume
            [In] FileIdDescriptor lpFileId,
            FileDesiredAccess dwDesiredAccess,
            FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            FileFlagsAndAttributes dwFlagsAndAttributes);

        /// <summary>
        /// Creates an I/O completion port or associates an existing port with a file handle.
        /// </summary>
        /// <remarks>
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa363862(v=vs.85).aspx
        /// We marshal the result as an IntPtr since, given an <paramref name="existingCompletionPort"/>,
        /// we get back the same handle value. Wrapping the same handle value again would result in double-frees on finalize.
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateIoCompletionPort(
            SafeFileHandle handle,
            SafeIOCompletionPortHandle existingCompletionPort,
            IntPtr completionKey,
            int numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern unsafe bool GetOverlappedResult(
            SafeFileHandle hFile,
            Overlapped* lpOverlapped,
            int* lpNumberOfBytesTransferred,
            [MarshalAs(UnmanagedType.Bool)] bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1415:DeclarePInvokesCorrectly", Justification = "Overlapped intentionally redefined.")]
        private static extern unsafe bool GetQueuedCompletionStatus(
            SafeIOCompletionPortHandle hCompletionPort,
            int* lpNumberOfBytes,
            IntPtr* lpCompletionKey,
            Overlapped** lpOverlapped,
            int dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1415:DeclarePInvokesCorrectly", Justification = "Overlapped intentionally redefined.")]
        private static extern unsafe bool PostQueuedCompletionStatus(
            SafeIOCompletionPortHandle hCompletionPort,
            int dwNumberOfBytesTransferred,
            IntPtr dwCompletionKey,
            Overlapped* lpOverlapped);

        [Flags]
        private enum FileCompletionMode
        {
            FileSkipCompletionPortOnSuccess = 0x1,
            FileSkipSetEventOnHandle = 0x2,
        }

        /// <summary>
        /// Sets the mode for dispatching IO completions on the given file handle.
        /// </summary>
        /// <remarks>
        /// Skipping completion port queueing on success (i.e., synchronous completion) avoids wasted thread handoffs but requires an aware caller
        /// (that does not assume <c>ERROR_IO_PENDING</c>).
        /// Skipping the signaling of the file object itself via <see cref="FileCompletionMode.FileSkipSetEventOnHandle"/> can avoid some
        /// wasted work and locking in the event there's not a specific event provided in the corresponding <c>OVERLAPPED</c> structure.
        /// See http://blogs.technet.com/b/winserverperformance/archive/2008/06/26/designing-applications-for-high-performance-part-iii.aspx
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileCompletionNotificationModes(SafeFileHandle handle, FileCompletionMode mode);

        /// <summary>
        /// <c>OVERLAPPED</c> sturcture for async IO completion.
        /// See https://msdn.microsoft.com/en-us/library/windows/desktop/ms684342(v=vs.85).aspx
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct Overlapped
        {
            /// <summary>
            /// Internal completion state. Access via <c>GetQueuedCompletionStatus</c> or <c>GetOverlappedResult</c>.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]
            public IntPtr InternalLow;

            /// <summary>
            /// Internal completion state. Access via <c>GetQueuedCompletionStatus</c> or <c>GetOverlappedResult</c>.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]
            public IntPtr InternalHigh;

            /// <summary>
            /// Low part of the start offset (part of the I/O request).
            /// </summary>
            public uint OffsetLow;

            /// <summary>
            /// High part of the start offset (part of the I/O request).
            /// </summary>
            public uint OffsetHigh;

            /// <summary>
            /// Event handle to signal on completion. Not needed when using I/O completion ports exclusively.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]
            public IntPtr EventHandle;

            /// <summary>
            /// Start offset (part of the I/O request).
            /// </summary>
            public long Offset
            {
                get
                {
                    return checked((long)Bits.GetLongFromInts(OffsetHigh, OffsetLow));
                }

                set
                {
                    OffsetLow = Bits.GetLowInt(checked((ulong)value));
                    OffsetHigh = Bits.GetHighInt(checked((ulong)value));
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1415:DeclarePInvokesCorrectly", Justification = "Overlapped intentionally redefined.")]
        private static extern unsafe bool ReadFile(
            SafeFileHandle hFile,
            byte* lpBuffer,
            int nNumberOfBytesToRead,
            int* lpNumberOfBytesRead,
            Overlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1415:DeclarePInvokesCorrectly", Justification = "Overlapped intentionally redefined.")]
        private static extern unsafe bool WriteFile(
            SafeFileHandle hFile,
            byte* lpBuffer,
            int nNumberOfBytesToWrite,
            int* lpNumberOfBytesWritten,
            Overlapped* lpOverlapped);

        [SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "OsVersionInfoEx.CSDVersion",
            Justification = "This appears impossible to satisfy.")]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VerifyVersionInfo(
            [In] OsVersionInfoEx versionInfo,
            uint typeMask,
            ulong conditionMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern ulong VerSetConditionMask(
            ulong existingMask,
            uint typeMask,
            byte conditionMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle deviceHandle,
            uint ioControlCode,
            IntPtr inputBuffer,
            int inputBufferSize,
            IntPtr outputBuffer,
            int outputBufferSize,
            out int bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle deviceHandle,
            uint ioControlCode,
            IntPtr inputBuffer,
            int inputBufferSize,
            [Out] QueryUsnJournalData outputBuffer,
            int outputBufferSize,
            out int bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint ioControlCode,
            ref STORAGE_PROPERTY_QUERY inputBuffer,
            int inputBufferSize,
            out DEVICE_SEEK_PENALTY_DESCRIPTOR outputBuffer,
            int outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle deviceHandle,
            uint fileInformationClass,
            IntPtr outputFileInformationBuffer,
            int outputBufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
              SafeFileHandle hFile,
              uint fileInformationClass,
              IntPtr lpFileInformation,
              int bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileSizeEx(
            SafeFileHandle handle,
            out long size);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformationByHandleW(
            SafeFileHandle fileHandle,
            [Out] StringBuilder volumeNameBuffer, // Buffer for volume name (if not null)
            int volumeNameBufferSize,
            IntPtr volumeSerial, // Optional pointer to a DWORD to be populated with the volume serial number
            IntPtr maximumComponentLength, // Optional pointer to a DWORD to be populated with the max component length.
            IntPtr fileSystemFlags, // Optional pointer to a DWORD to be populated with flags of supported features on the volume (e.g. hardlinks)
            [Out] StringBuilder fileSystemNameBuffer, // Buffer for volume FS, e.g. "NTFS" (if not null)
            int fileSystemNameBufferSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern SafeFindVolumeHandle FindFirstVolumeW(
            [Out] StringBuilder volumeNameBuffer,
            int volumeNameBufferLength);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextVolumeW(
            SafeFindVolumeHandle findVolumeHandle,
            [Out] StringBuilder volumeNameBuffer,
            int volumeNameBufferLength);

        /// <summary>
        /// Disposes a <see cref="SafeFindVolumeHandle"/>
        /// </summary>
        /// <remarks>
        /// Since this is used by <see cref="SafeFindVolumeHandle"/> itself, we expose
        /// the inner <see cref="IntPtr"/> (rather than trying to marshal the handle wrapper
        /// from within its own release method).
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindVolumeClose(IntPtr findVolumeHandle);

        /// <summary>
        /// Disposes a typical handle.
        /// </summary>
        /// <remarks>
        /// Since this is used by safe handle wrappers (e.g. <see cref="SafeIOCompletionPortHandle"/>), we expose
        /// the inner <see cref="IntPtr"/> (rather than trying to marshal the handle wrapper
        /// from within its own release method).
        /// </remarks>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr reservedSecurityAttributes);

        /// <summary>
        /// Symbolic link target.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
        [SuppressMessage("Microsoft.Naming", "CA1714:FlagsEnumsShouldHavePluralNames")]
        [Flags]
        public enum SymbolicLinkTarget : uint
        {
            /// <summary>
            /// The link target is a file.
            /// </summary>
            File = 0x0,

            /// <summary>
            /// The link target is a directory.
            /// </summary>
            Directory = 0x1,
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, SymbolicLinkTarget dwFlags);

        /// <summary>
        /// When this flag is set on the process or thread error mode, 'the system does not display the critical-error-handler message box'.
        /// In this context, we don't want a weird message box prompting to insert a CD / floppy when querying volume information.
        /// </summary>
        /// <remarks>
        /// Seriously?!
        /// Corresponds to SEM_FAILCRITICALERRORS
        /// </remarks>
        private const int SemFailCriticalErrors = 1;

        /// <os>Windows 7+</os>
        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern int GetThreadErrorMode();

        /// <os>Windows 7+</os>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadErrorMode(int newErrorMode, out int oldErrorMode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetFinalPathNameByHandleW(SafeFileHandle hFile, [Out] StringBuilder filePathBuffer, int filePathBufferSize, int flags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveDirectoryW(
            string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for custom enumeration.")]
        public static extern SafeFindFileHandle FindFirstFileW(
            string lpFileName,
            out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for custom enumeration.")]
        public static extern bool FindNextFileW(SafeHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr findFileHandle);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible", Justification = "Needed for creating symlinks.")]
        public static extern bool PathMatchSpecW([In] string pszFileParam, [In] string pszSpec);

        /// <summary>
        /// Values for the DwReserved0 member of the WIN32_FIND_DATA struct.
        /// </summary>
        public enum DwReserved0Flag : uint
        {
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_RESERVED_ZERO = 0x00000000, // Reserved reparse tag value.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_RESERVED_ONE = 0x00000001, // Reserved reparse tag value.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003, // Used for mount point support, specified in section 2.1.2.5.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_HSM = 0xC0000004, // Obsolete.Used by legacy Hierarchical Storage Manager Product.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_HSM2 = 0x80000006, // Obsolete.Used by legacy Hierarchical Storage Manager Product.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_DRIVER_EXTENDER = 0x80000005, // Home server drive extender.<3>
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_SIS = 0x80000007, // Used by single-instance storage (SIS) filter driver.Server-side interpretation only, not meaningful over the wire.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_DFS = 0x8000000A, // Used by the DFS filter.The DFS is described in the Distributed File System (DFS): Referral Protocol Specification[MS - DFSC]. Server-side interpretation only, not meaningful over the wire.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_DFSR = 0x80000012, // Used by the DFS filter.The DFS is described in [MS-DFSC]. Server-side interpretation only, not meaningful over the wire.
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_FILTER_MANAGER = 0x8000000B, // Used by filter manager test harness.<4>
            [SuppressMessage("Microsoft.Naming", "CA1700:DoNotNameEnumValuesReserved")]
            [SuppressMessage("Microsoft.Naming", "CA1707:RemoveUnderscoresFromMemberName")]
            IO_REPARSE_TAG_SYMLINK = 0xA000000C, // Used for symbolic link support. See section 2.1.2.4.
        }

        /// <summary>
        /// <c>WIN32_FIND_DATA</c>
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        [SuppressMessage("Microsoft.Naming", "CA1707:IdentifiersShouldNotContainUnderscores")]
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct WIN32_FIND_DATA
        {
            /// <summary>
            /// The file attributes of a file
            /// </summary>
            public FileAttributes DwFileAttributes;

            /// <summary>
            /// Specified when a file or directory was created
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtCreationTime;

            /// <summary>
            /// Specifies when the file was last read from, written to, or for executable files, run.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtLastAccessTime;

            /// <summary>
            /// For a file, the structure specifies when the file was last written to, truncated, or overwritten.
            /// For a directory, the structure specifies when the directory is created.
            /// </summary>
            public System.Runtime.InteropServices.ComTypes.FILETIME FtLastWriteTime;

            /// <summary>
            /// The high-order DWORD value of the file size, in bytes.
            /// </summary>
            public uint NFileSizeHigh;

            /// <summary>
            /// The low-order DWORD value of the file size, in bytes.
            /// </summary>
            public uint NFileSizeLow;

            /// <summary>
            /// If the dwFileAttributes member includes the FILE_ATTRIBUTE_REPARSE_POINT attribute, this member specifies the reparse point tag.
            /// </summary>
            public uint DwReserved0;

            /// <summary>
            /// Reserved for future use.
            /// </summary>
            public uint DwReserved1;

            /// <summary>
            /// The name of the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPath)]
            public string CFileName;

            /// <summary>
            /// An alternative name for the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string CAlternate;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api",
            Justification = "We explicitly need to call the native SetFileAttributes as the managed one does not support long paths.")]
        internal static extern bool SetFileAttributesW(
            string lpFileName,
            FileAttributes dwFileAttributes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U4)]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api",
            Justification = "We explicitly need to call the native GetFileAttributes as the managed one does not support long paths.")]
        internal static extern uint GetFileAttributesW(
            string lpFileName);

        /// <summary>
        /// Storage property query
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/ff800840(v=vs.85).aspx
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_PROPERTY_QUERY
        {
            public uint PropertyId;
            public uint QueryType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] AdditionalParameters;
        }

        private const uint StorageDeviceSeekPenaltyProperty = 7;
        private const uint PropertyStandardQuery = 0;

        /// <summary>
        /// Specifies whether a device has a seek penalty.
        /// https://msdn.microsoft.com/en-us/library/ff552549.aspx
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
        {
            public uint Version;
            public uint Size;
            [MarshalAs(UnmanagedType.U1)]
            public bool IncursSeekPenalty;
        }

        // Consts from sdk\inc\winioctl.h
        private const uint METHOD_BUFFERED = 0;
        private const uint FILE_ANY_ACCESS = 0;
        private const uint FILE_DEVICE_MASS_STORAGE = 0x0000002d;
        private const uint IOCTL_STORAGE_BASE = FILE_DEVICE_MASS_STORAGE;
        private static readonly uint IOCTL_STORAGE_QUERY_PROPERTY = CTL_CODE(IOCTL_STORAGE_BASE, 0x500, METHOD_BUFFERED, FILE_ANY_ACCESS);

        private static uint CTL_CODE(uint deviceType, uint function, uint method, uint access)
        {
            return (deviceType << 16) | (access << 14) | (function << 2) | method;
        }

        /// <summary>
        /// Reparse data buffer - from ntifs.h.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct REPARSE_DATA_BUFFER
        {
            public DwReserved0Flag ReparseTag;

            public ushort ReparseDataLength;

            public ushort Reserved;

            public ushort SubstituteNameOffset;

            public ushort SubstituteNameLength;

            public ushort PrintNameOffset;

            public ushort PrintNameLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3FF0)]
            public byte[] PathBuffer;
        }

        private const int FSCTL_SET_REPARSE_POINT = 0x000900A4;

        #endregion

        /// <summary>
        /// Indicates if the running OS is at least Windows 8.0 / Server 2012.
        /// </summary>
        private static readonly bool s_runningWindows8OrAbove = IsOSVersionGreaterOrEqual(6, 2);

        /// <summary>
        /// Indicates if the running OS is at least Windows 8.1 / Server 2012R2.
        /// </summary>
        private static readonly bool s_runningWindows8Point1OrAbove = IsOSVersionGreaterOrEqual(6, 3);

        /// <summary>
        /// Calls VerifyVersionInfo to determine if the running OS's version meets or exceeded the given major.minor version.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Environment.OSVersion"/>, this works for Windows 8.1 and above.
        /// See the deprecation warnings at http://msdn.microsoft.com/en-us/library/windows/desktop/ms724451(v=vs.85).aspx
        /// </remarks>
        public static bool IsOSVersionGreaterOrEqual(int major, int minor)
        {
            const uint ErrorOldWinVersion = 0x47e; // ERROR_OLD_WIN_VERSION
            const uint MajorVersion = 0x2; // VER_MAJOR_VERSION
            const uint MinorVersion = 0x1; // VER_MINOR_VERSION
            const byte CompareGreaterOrEqual = 0x3; // VER_GREATER_EQUAL

            ulong conditionMask = VerSetConditionMask(0, MajorVersion, CompareGreaterOrEqual);
            conditionMask = VerSetConditionMask(conditionMask, MinorVersion, CompareGreaterOrEqual);

            OsVersionInfoEx comparand = new OsVersionInfoEx { OSVersionInfoSize = OsVersionInfoEx.Size, MajorVersion = major, MinorVersion = minor };
            bool satisfied = VerifyVersionInfo(comparand, MajorVersion | MinorVersion, conditionMask);
            int hr = Marshal.GetLastWin32Error();

            if (!satisfied && hr != ErrorOldWinVersion)
            {
                throw ThrowForNativeFailure(hr, "VerifyVersionInfo");
            }

            return satisfied;
        }

        /// <summary>
        /// Disposable struct to push / pop a thread-local error mode (e.g. <see cref="WindowsNative.SemFailCriticalErrors"/>) within a 'using' block.
        /// This context must be created and disposed on the same thread.
        /// </summary>
        private struct ErrorModeContext : IDisposable
        {
            private readonly bool m_isValid;
            private readonly int m_oldErrorMode;
            private readonly int m_thisErrorMode;
            private readonly int m_threadId;

            /// <summary>
            /// Creates an error mode context that represent pushing <paramref name="thisErrorMode"/> on top of the current <paramref name="oldErrorMode"/>
            /// </summary>
            private ErrorModeContext(int oldErrorMode, int thisErrorMode)
            {
                m_isValid = true;
                m_oldErrorMode = oldErrorMode;
                m_thisErrorMode = thisErrorMode;
                m_threadId = Thread.CurrentThread.ManagedThreadId;
            }

            /// <summary>
            /// Pushes an error mode context which is the current mode with the given extra flags set.
            /// (i.e., we push <c><see cref="WindowsNative.GetThreadErrorMode"/> | <paramref name="additionalFlags"/></c>)
            /// </summary>
            public static ErrorModeContext PushWithAddedFlags(int additionalFlags)
            {
                int currentErrorMode = GetThreadErrorMode();
                int thisErrorMode = currentErrorMode | additionalFlags;

                int oldErrorModeViaSet;
                if (!SetThreadErrorMode(thisErrorMode, out oldErrorModeViaSet))
                {
                    int hr = Marshal.GetLastWin32Error();
                    throw ThrowForNativeFailure(hr, "SetThreadErrorMode");
                }

                Contract.Assume(currentErrorMode == oldErrorModeViaSet, "Thread error mode should only be change from calls on this thread");

                return new ErrorModeContext(oldErrorMode: currentErrorMode, thisErrorMode: thisErrorMode);
            }

            /// <summary>
            /// Sets <c>SEM_FAILCRITICALERRORS</c> in the thread's error mode (if it is not set already).
            /// The returned <see cref="ErrorModeContext"/> must be disposed to restore the prior error mode (and the disposal must occur on the same thread).
            /// </summary>
            /// <remarks>
            /// The intended effect is to avoid a blocking message box if a file path on a CD / floppy drive letter is poked without media inserted.
            /// This is neccessary before using volume management functions such as <see cref="ListVolumeGuidPathsAndSerials"/>
            /// See http://msdn.microsoft.com/en-us/library/windows/desktop/ms680621(v=vs.85).aspx
            /// </remarks>
            public static ErrorModeContext DisableMessageBoxForRemovableMedia()
            {
                return PushWithAddedFlags(SemFailCriticalErrors);
            }

            /// <summary>
            /// Pops this error mode context off of the thread's error mode stack.
            /// </summary>
            public void Dispose()
            {
                Contract.Assume(m_isValid);
                Contract.Assume(m_threadId == Thread.CurrentThread.ManagedThreadId, "An ErrorModeContext must be disposed on the same thread on which it was created");

                int errorModeBeforeRestore;
                if (!SetThreadErrorMode(m_oldErrorMode, out errorModeBeforeRestore))
                {
                    int hr = Marshal.GetLastWin32Error();
                    throw ThrowForNativeFailure(hr, "SetThreadErrorMode");
                }

                Contract.Assume(errorModeBeforeRestore == m_thisErrorMode, "The thread error mode changed within the ErrorModeContext, but was not restored before popping this context.");
            }
        }

        /// <summary>
        /// Flushes cached pages for a file back to the filesystem. Unlike <c>FlushFileBuffers</c>, this does NOT
        /// issue a *disk-wide* cache flush, and so does NOT guarantee that written data is durable on disk (but it does
        /// force pages dirtied by e.g. a writable memory-mapping to be visible to the filesystem).
        /// The given handle must be opened with write access.
        /// </summary>
        /// <remarks>
        /// This wraps <c>NtFlushBuffersFileEx</c> and returns <c>NtStatus</c> that indicates whether the flush was a success.
        /// </remarks>
        public static unsafe NtStatus FlushPageCacheToFilesystem(SafeFileHandle handle)
        {
            IoStatusBlock iosb = default(IoStatusBlock);
            NtStatus status = NtFlushBuffersFileEx(handle, FlushFileBuffersFlags.FileDataOnly, null, 0, &iosb);
            return status;
        }

        /// <summary>
        /// Calls FSCTL_READ_FILE_USN_DATA on the given file handle. This returns a scrubbed USN record that contains all fields other than
        /// TimeStamp, Reason, and SourceInfo. If the volume's journal is disabled or the file has not been touched since journal creation,
        /// the USN field of the record will be 0.
        /// </summary>
        /// <remarks>
        /// <paramref name="forceJournalVersion2"/> results in requesting a USN_RECORD_V2 result (even on 8.1+, which supports USN_RECORD_V3).
        /// This allows testing V2 marshaling when not running a downlevel OS.
        /// </remarks>
        public static unsafe MiniUsnRecord? ReadFileUsnByHandle(SafeFileHandle fileHandle, bool forceJournalVersion2 = false)
        {
            Contract.Requires(fileHandle != null);

            int bytesReturned;

            // We support V2 and V3 records. V3 records (with ReFS length FileIds) are larger, so we allocate a buffer on that assumption.
            int recordBufferLength = NativeUsnRecordV3.MaximumSize;
            byte* recordBuffer = stackalloc byte[recordBufferLength];

            ReadFileUsnData readOptions = forceJournalVersion2 ? ReadFileUsnData.NtfsCompatible : ReadFileUsnData.NtfsAndReFSCompatible;

            if (!DeviceIoControl(
                    fileHandle,
                    ioControlCode: FsctlReadFileUsnData,
                    inputBuffer: (IntPtr)(&readOptions),
                    inputBufferSize: ReadFileUsnData.Size,
                    outputBuffer: (IntPtr)recordBuffer,
                    outputBufferSize: recordBufferLength,
                    bytesReturned: out bytesReturned,
                    overlapped: IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorJournalDeleteInProgress || error == ErrorJournalNotActive || error == ErrorInvalidFunction || error == ErrorOnlyIfConnected)
                {
                    return null;
                }

                throw ThrowForNativeFailure(error, "DeviceIoControl(FSCTL_READ_FILE_USN_DATA)");
            }

            NativeUsnRecordHeader* recordHeader = (NativeUsnRecordHeader*)recordBuffer;

            Contract.Assume(
                bytesReturned >= NativeUsnRecordHeader.Size,
                "Not enough data returned for a valid USN record header");

            Contract.Assume(
                bytesReturned == recordHeader->RecordLength,
                "RecordLength field disagrees from number of bytes actually returned; but we were expecting exactly one record.");

            MiniUsnRecord resultRecord;
            if (recordHeader->MajorVersion == 3)
            {
                Contract.Assume(!forceJournalVersion2);

                Contract.Assume(
                    bytesReturned >= NativeUsnRecordV3.MinimumSize && bytesReturned <= NativeUsnRecordV3.MaximumSize,
                    "FSCTL_READ_FILE_USN_DATA returned an amount of data that does not correspond to a valid USN_RECORD_V3.");

                NativeUsnRecordV3* record = (NativeUsnRecordV3*)recordBuffer;

                Contract.Assume(
                    record->Reason == 0 && record->TimeStamp == 0 && record->SourceInfo == 0,
                    "FSCTL_READ_FILE_USN_DATA scrubs these fields. Marshalling issue?");

                resultRecord = new MiniUsnRecord(record->FileReferenceNumber, record->Usn);
            }
            else if (recordHeader->MajorVersion == 2)
            {
                Contract.Assume(
                    bytesReturned >= NativeUsnRecordV2.MinimumSize && bytesReturned <= NativeUsnRecordV2.MaximumSize,
                    "FSCTL_READ_FILE_USN_DATA returned an amount of data that does not correspond to a valid USN_RECORD_V2.");

                NativeUsnRecordV2* record = (NativeUsnRecordV2*)recordBuffer;

                Contract.Assume(
                    record->Reason == 0 && record->TimeStamp == 0 && record->SourceInfo == 0,
                    "FSCTL_READ_FILE_USN_DATA scrubs these fields. Marshalling issue?");

                resultRecord = new MiniUsnRecord(new FileId(0, record->FileReferenceNumber), record->Usn);
            }
            else
            {
                Contract.Assume(
                    false,
                    "An unrecognized record version was returned, even though version 2 or 3 was requested.");
                throw new InvalidOperationException("Unreachable");
            }

            Domino.Storage.Tracing.Logger.Log.StorageReadUsn(Events.StaticContext, resultRecord.FileId.High, resultRecord.FileId.Low, resultRecord.Usn.Value);

            return resultRecord;
        }

        /// <summary>
        /// Status indication of <c>FSCTL_READ_USN_JOURNAL</c>.
        /// </summary>
        public enum ReadUsnJournalStatus : byte
        {
            /// <summary>
            /// Reading the journal succeeded. Zero or more records have been retrieved.
            /// </summary>
            Success,

            /// <summary>
            /// The journal on the specified volume is not active.
            /// </summary>
            JournalNotActive,

            /// <summary>
            /// The journal on the specified volume is being deleted (a later read would return <see cref="JournalNotActive"/>).
            /// </summary>
            JournalDeleteInProgress,

            /// <summary>
            /// There is a valid journal, but the specified <see cref="ReadUsnJournalData.StartUsn"/> has been truncated out of it.
            /// Consider specifying a start USN of 0 to get the earliest available records.
            /// </summary>
            JournalEntryDeleted,

            /// <summary>
            /// Incorrect parameter error happens when the volume format is broken.
            /// </summary>
            InvalidParameter,

            /// <summary>
            /// The queried volume does not support writing a change journal.
            /// </summary>
            VolumeDoesNotSupportChangeJournals,
        }

        /// <summary>
        /// Result of reading a USN journal with <see cref="WindowsNative.TryReadUsnJournal"/>.
        /// </summary>
        public sealed class ReadUsnJournalResult
        {
            /// <summary>
            /// Status indication of the read attempt.
            /// </summary>
            public readonly ReadUsnJournalStatus Status;

            /// <summary>
            /// If the read <see cref="Succeeded"/>, specifies the next USN that will be recorded in the journal
            /// (a continuation cursor for futher reads).
            /// </summary>
            public readonly Usn NextUsn;

            /// <summary>
            /// If the read <see cref="Succeeded"/>, the list of records retrieved.
            /// </summary>
            public readonly IReadOnlyCollection<UsnRecord> Records;

            /// <nodoc />
            public ReadUsnJournalResult(ReadUsnJournalStatus status, Usn nextUsn, IReadOnlyCollection<UsnRecord> records)
            {
                Contract.Requires((status == ReadUsnJournalStatus.Success) == (records != null), "Records list should be present only on success");

                Status = status;
                NextUsn = nextUsn;
                Records = records;
            }

            /// <summary>
            /// Indicates if reading the journal succeeded.
            /// </summary>
            public bool Succeeded => Status == ReadUsnJournalStatus.Success;
        }

        /// <summary>
        /// Calls FSCTL_READ_USN_JOURNAL on the given volume handle.
        /// </summary>
        /// <remarks>
        /// <paramref name="forceJournalVersion2"/> results in requesting a USN_RECORD_V2 result (even on 8.1+, which supports USN_RECORD_V3).
        /// This allows testing V2 marshaling when not running a downlevel OS.
        /// <paramref name="buffer"/> is a caller-provided buffer (which does not need to be pinned). The contents of the buffer are undefined (the
        /// purpose of the buffer parameter is to allow pooling / re-using buffers across repeated journal reads).
        /// </remarks>
        public static unsafe ReadUsnJournalResult TryReadUsnJournal(SafeFileHandle volumeHandle, byte[] buffer, ulong journalId, Usn startUsn = default(Usn), bool forceJournalVersion2 = false, bool isJournalUnprivileged = false)
        {
            Contract.Requires(volumeHandle != null);
            Contract.Requires(buffer != null && buffer.Length > 0);
            Contract.Ensures(Contract.Result<ReadUsnJournalResult>() != null);

            var readOptions = new ReadUsnJournalData
            {
                MinMajorVersion = 2,
                MaxMajorVersion = forceJournalVersion2 ? (ushort)2 : (ushort)3,
                StartUsn = startUsn,
                Timeout = 0,
                BytesToWaitFor = 0,
                ReasonMask = uint.MaxValue, // TODO: Filter this!
                ReturnOnlyOnClose = 0,
                UsnJournalID = journalId,
            };

            int bytesReturned;
            bool ioctlSuccess;
            int error;

            fixed (byte* pRecordBuffer = buffer)
            {
                ioctlSuccess = DeviceIoControl(
                    volumeHandle,
                    ioControlCode: isJournalUnprivileged ? FsctlReadUnprivilegedUsnJournal : FsctlReadUsnJournal,
                    inputBuffer: (IntPtr)(&readOptions),
                    inputBufferSize: ReadUsnJournalData.Size,
                    outputBuffer: (IntPtr)pRecordBuffer,
                    outputBufferSize: buffer.Length,
                    bytesReturned: out bytesReturned,
                    overlapped: IntPtr.Zero);
                error = Marshal.GetLastWin32Error();
            }

            if (!ioctlSuccess)
            {
                ReadUsnJournalStatus errorStatus;
                switch ((uint)error)
                {
                    case ErrorJournalNotActive:
                        errorStatus = ReadUsnJournalStatus.JournalNotActive;
                        break;
                    case ErrorJournalDeleteInProgress:
                        errorStatus = ReadUsnJournalStatus.JournalDeleteInProgress;
                        break;
                    case ErrorJournalEntryDeleted:
                        errorStatus = ReadUsnJournalStatus.JournalEntryDeleted;
                        break;
                    case ErrorInvalidParameter:
                        errorStatus = ReadUsnJournalStatus.InvalidParameter;
                        break;
                    case ErrorInvalidFunction:
                        errorStatus = ReadUsnJournalStatus.VolumeDoesNotSupportChangeJournals;
                        break;
                    default:
                        throw ThrowForNativeFailure(error, "DeviceIoControl(FSCTL_READ_USN_JOURNAL)");
                }

                return new ReadUsnJournalResult(errorStatus, nextUsn: new Usn(0), records: null);
            }

            Contract.Assume(bytesReturned >= sizeof(ulong), "The output buffer should always contain the updated USN cursor (even if no records were returned)");

            var recordsToReturn = new List<UsnRecord>();
            ulong nextUsn;
            fixed (byte* recordBufferBase = buffer)
            {
                nextUsn = *(ulong*)recordBufferBase;
                byte* currentRecordBase = recordBufferBase + sizeof(ulong);
                Contract.Assume(currentRecordBase != null);

                // One past the end of the record part of the buffer
                byte* recordsEnd = recordBufferBase + bytesReturned;

                while (currentRecordBase < recordsEnd)
                {
                    Contract.Assume(
                        currentRecordBase + NativeUsnRecordHeader.Size <= recordsEnd,
                        "Not enough data returned for a valid USN record header");

                    NativeUsnRecordHeader* currentRecordHeader = (NativeUsnRecordHeader*)currentRecordBase;

                    Contract.Assume(
                        currentRecordBase + currentRecordHeader->RecordLength <= recordsEnd,
                        "RecordLength field advances beyond the buffer");

                    if (currentRecordHeader->MajorVersion == 3)
                    {
                        Contract.Assume(!forceJournalVersion2);

                        Contract.Assume(
                            currentRecordHeader->RecordLength >= NativeUsnRecordV3.MinimumSize && currentRecordHeader->RecordLength <= NativeUsnRecordV3.MaximumSize,
                            "Size in record header does not correspond to a valid USN_RECORD_V3.");

                        NativeUsnRecordV3* record = (NativeUsnRecordV3*)currentRecordBase;
                        recordsToReturn.Add(new UsnRecord(
                            record->FileReferenceNumber,
                            record->ParentFileReferenceNumber,
                            record->Usn,
                            (UsnChangeReasons)record->Reason));
                    }
                    else if (currentRecordHeader->MajorVersion == 2)
                    {
                        Contract.Assume(
                            currentRecordHeader->RecordLength >= NativeUsnRecordV2.MinimumSize && currentRecordHeader->RecordLength <= NativeUsnRecordV2.MaximumSize,
                            "Size in record header does not correspond to a valid USN_RECORD_V2.");

                        NativeUsnRecordV2* record = (NativeUsnRecordV2*)currentRecordBase;
                        recordsToReturn.Add(new UsnRecord(
                            new FileId(0, record->FileReferenceNumber),
                            new FileId(0, record->ParentFileReferenceNumber),
                            record->Usn,
                            (UsnChangeReasons)record->Reason));
                    }
                    else
                    {
                        Contract.Assume(
                            false,
                            "An unrecognized record version was returned, even though version 2 or 3 was requested.");
                        throw new InvalidOperationException("Unreachable");
                    }

                    currentRecordBase += currentRecordHeader->RecordLength;
                }
            }

            return new ReadUsnJournalResult(ReadUsnJournalStatus.Success, new Usn(nextUsn), recordsToReturn);
        }

        /// <summary>
        /// Status indication of <c>FSCTL_QUERY_USN_JOURNAL</c>.
        /// </summary>
        public enum QueryUsnJournalStatus
        {
            /// <summary>
            /// Querying the journal succeeded.
            /// </summary>
            Success,

            /// <summary>
            /// The journal on the specified volume is not active.
            /// </summary>
            JournalNotActive,

            /// <summary>
            /// The journal on the specified volume is being deleted (a later read would return <see cref="JournalNotActive"/>).
            /// </summary>
            JournalDeleteInProgress,

            /// <summary>
            /// The queried volume does not support writing a change journal.
            /// </summary>
            VolumeDoesNotSupportChangeJournals,

            /// <summary>
            /// Incorrect parameter error happens when the volume format is broken.
            /// </summary>
            InvalidParameter,

            /// <summary>
            /// Access denied error when querying the journal.
            /// </summary>
            AccessDenied,
        }

        /// <summary>
        /// Result of querying a USN journal with <see cref="WindowsNative.TryQueryUsnJournal"/>.
        /// </summary>
        public sealed class QueryUsnJournalResult
        {
            /// <summary>
            /// Status indication of the query attempt.
            /// </summary>
            public readonly QueryUsnJournalStatus Status;

            private readonly QueryUsnJournalData m_data;

            /// <nodoc />
            public QueryUsnJournalResult(QueryUsnJournalStatus status, QueryUsnJournalData data)
            {
                Contract.Requires((status == QueryUsnJournalStatus.Success) == (data != null), "Journal data should be present only on success");

                Status = status;
                m_data = data;
            }

            /// <summary>
            /// Indicates if querying the journal succeeded.
            /// </summary>
            public bool Succeeded => Status == QueryUsnJournalStatus.Success;

            /// <summary>
            /// Returns the queried data (fails if not <see cref="Succeeded"/>).
            /// </summary>
            public QueryUsnJournalData Data
            {
                get
                {
                    Contract.Requires(Succeeded);
                    return m_data;
                }
            }
        }

        /// <summary>
        /// Calls FSCTL_QUERY_USN_JOURNAL on the given volume handle.
        /// </summary>
        public static QueryUsnJournalResult TryQueryUsnJournal(SafeFileHandle volumeHandle)
        {
            Contract.Requires(volumeHandle != null);
            Contract.Ensures(Contract.Result<QueryUsnJournalResult>() != null);

            var data = new QueryUsnJournalData();

            int bytesReturned;
            bool ioctlSuccess = DeviceIoControl(
                volumeHandle,
                ioControlCode: FsctlQueryUsnJournal,
                inputBuffer: IntPtr.Zero,
                inputBufferSize: 0,
                outputBuffer: data,
                outputBufferSize: QueryUsnJournalData.Size,
                bytesReturned: out bytesReturned,
                overlapped: IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();

            if (!ioctlSuccess)
            {
                QueryUsnJournalStatus errorStatus;
                switch ((uint)error)
                {
                    case ErrorJournalNotActive:
                        errorStatus = QueryUsnJournalStatus.JournalNotActive;
                        break;
                    case ErrorJournalDeleteInProgress:
                        errorStatus = QueryUsnJournalStatus.JournalDeleteInProgress;
                        break;
                    case ErrorInvalidFunction:
                        errorStatus = QueryUsnJournalStatus.VolumeDoesNotSupportChangeJournals;
                        break;
                    case ErrorInvalidParameter:
                        errorStatus = QueryUsnJournalStatus.InvalidParameter;
                        break;
                    case ErrorAccessDenied:
                        errorStatus = QueryUsnJournalStatus.AccessDenied;
                        break;
                    default:
                        throw ThrowForNativeFailure(error, "DeviceIoControl(FSCTL_QUERY_USN_JOURNAL)");
                }

                return new QueryUsnJournalResult(errorStatus, data: null);
            }

            Contract.Assume(bytesReturned == QueryUsnJournalData.Size, "Output buffer size mismatched (not all fields populated?)");

            return new QueryUsnJournalResult(QueryUsnJournalStatus.Success, data);
        }

        /// <summary>
        /// Calls FSCTL_WRITE_USN_CLOSE_RECORD on the given file handle, and returns the new USN (not a full record). The new USN corresponds to
        /// a newly-written 'close' record, meaning that any not-yet-checkpointed (deferred) change reasons have been flushed.
        /// If writing the close record fails due to the volume's journal being disabled, null is returned.
        /// </summary>
        public static unsafe Usn? TryWriteUsnCloseRecordByHandle(SafeFileHandle fileHandle)
        {
            Contract.Requires(fileHandle != null);

            int bytesReturned;
            ulong writtenUsn;

            if (!DeviceIoControl(
                    fileHandle,
                    ioControlCode: FsctlWriteUsnCloseRecord,
                    inputBuffer: IntPtr.Zero,
                    inputBufferSize: 0,
                    outputBuffer: (IntPtr)(&writtenUsn),
                    outputBufferSize: sizeof(ulong),
                    bytesReturned: out bytesReturned,
                    overlapped: IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();

                if (error == ErrorJournalDeleteInProgress || error == ErrorJournalNotActive)
                {
                    return null;
                }

                throw ThrowForNativeFailure(error, "DeviceIoControl(FSCTL_WRITE_USN_CLOSE_RECORD)");
            }

            Contract.Assume(bytesReturned == sizeof(ulong));

            Domino.Storage.Tracing.Logger.Log.StorageCheckpointUsn(Events.StaticContext, writtenUsn);

            return new Usn(writtenUsn);
        }

        /// <summary>
        /// Indicates if <see cref="TryGetFileIdAndVolumeIdByHandle"/> is supported on this running OS.
        /// Note that even with OS support, particular file system drivers may not support it.
        /// </summary>
        [Pure]
        public static bool CanGetFileIdAndVolumeIdByHandle()
        {
            return s_runningWindows8Point1OrAbove;
        }

        /// <summary>
        /// Indicates if the extended (128-bit) file ID type is supported on this running OS.
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/aa364227(v=vs.85).aspx
        /// </summary>
        [Pure]
        private static bool IsExtendedFileIdSupported()
        {
            return s_runningWindows8OrAbove;
        }

        /// <summary>
        /// Calls GetFileInformationByHandleEx on the given file handle to retrieve its file ID and volume ID. Those two IDs together uniquely identify a file.
        /// See http://msdn.microsoft.com/en-us/library/windows/desktop/hh802691(v=vs.85).aspx
        /// </summary>
        /// <remarks>
        /// This should only be called if <see cref="CanGetFileIdAndVolumeIdByHandle"/> returns true. The needed API was added in Windows 8.1 / Server 2012R2.
        /// Note that even if the API is supported, the underlying volume for the given handle may not; only in that case, this function returns <c>null</c>.
        /// </remarks>
        public static unsafe FileIdAndVolumeId? TryGetFileIdAndVolumeIdByHandle(SafeFileHandle fileHandle)
        {
            Contract.Requires(fileHandle != null);
            Contract.Requires(CanGetFileIdAndVolumeIdByHandle());

            var info = default(FileIdAndVolumeId);
            if (!GetFileInformationByHandleEx(fileHandle, (uint)FileInfoByHandleClass.FileIdInfo, (IntPtr)(&info), FileIdAndVolumeId.Size))
            {
                int hr = Marshal.GetLastWin32Error();
                if (hr == ErrorInvalidParameter)
                {
                    return null;
                }

                ThrowForNativeFailure(hr, "GetFileInformationByHandleEx");
            }

            return info;
        }

        /// <summary>
        /// Calls GetFileInformationByHandleEx on the given file handle to retrieve its attributes. This requires 'READ ATTRIBUTES' access on the handle.
        /// </summary>
        public static unsafe FileAttributes GetFileAttributesByHandle(SafeFileHandle fileHandle)
        {
            Contract.Requires(fileHandle != null);

            var info = default(FileBasicInfo);
            if (!GetFileInformationByHandleEx(fileHandle, (uint)FileInfoByHandleClass.FileBasicInfo, (IntPtr)(&info), sizeof(FileBasicInfo)))
            {
                int hr = Marshal.GetLastWin32Error();
                ThrowForNativeFailure(hr, "GetFileInformationByHandleEx");
            }

            return info.Attributes;
        }

        /// <summary>
        /// Queries the current length (end-of-file position) of an open file.
        /// </summary>
        public static long GetFileLengthByHandle(SafeFileHandle fileHandle)
        {
            Contract.Requires(fileHandle != null);

            long size;
            if (!GetFileSizeEx(fileHandle, out size))
            {
                int hr = Marshal.GetLastWin32Error();
                ThrowForNativeFailure(hr, "GetFileSizeEx");
            }

            return size;
        }

        /// <summary>
        /// Returns a 32-bit volume serial number for the volume containing the given file.
        /// </summary>
        /// <remarks>
        /// This is the short serial number as seen in 'dir', whereas <see cref="TryGetFileIdAndVolumeIdByHandle" /> returns a
        /// longer (64-bit) serial (this short serial should be in its low bits).
        /// </remarks>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "short")]
        public static unsafe uint GetShortVolumeSerialNumberByHandle(SafeFileHandle fileHandle)
        {
            uint serial = 0;
            bool success = GetVolumeInformationByHandleW(
                fileHandle,
                volumeNameBuffer: null,
                volumeNameBufferSize: 0,
                volumeSerial: (IntPtr)(&serial),
                maximumComponentLength: IntPtr.Zero,
                fileSystemFlags: IntPtr.Zero,
                fileSystemNameBuffer: null,
                fileSystemNameBufferSize: 0);
            if (!success)
            {
                int hr = Marshal.GetLastWin32Error();
                throw ThrowForNativeFailure(hr, "GetVolumeInformationByHandleW");
            }

            return serial;
        }

        /// <summary>
        /// Returns a 64-bit volume serial number for the volume containing the given file when possible.
        /// If retrieving a 64-bit serial is not supported on this platform, this returns a synthesized one via
        /// sign-extending the 32-bit short serial (<see cref="GetShortVolumeSerialNumberByHandle"/>).
        /// </summary>
        /// <remarks>
        /// This picks between <see cref="TryGetFileIdAndVolumeIdByHandle" /> (if available) and <see cref="GetShortVolumeSerialNumberByHandle"/>.
        /// </remarks>
        public static ulong GetVolumeSerialNumberByHandle(SafeFileHandle fileHandle)
        {
            if (CanGetFileIdAndVolumeIdByHandle())
            {
                FileIdAndVolumeId? maybeInfo = TryGetFileIdAndVolumeIdByHandle(fileHandle);
                if (maybeInfo.HasValue)
                {
                    return maybeInfo.Value.VolumeSerialNumber;
                }
            }

            return (ulong)GetShortVolumeSerialNumberByHandle(fileHandle);
        }

        /// <summary>
        /// Attempts to set 'delete' disposition on the given handle, such that its directory entry is unlinked when all remaining handles are closed.
        /// </summary>
        public static unsafe bool TrySetDeletionDisposition(SafeFileHandle handle)
        {
            byte delete = 1;
            return SetFileInformationByHandle(handle, (uint)FileInfoByHandleClass.FileDispositionInfo, (IntPtr)(&delete), sizeof(byte));
        }

        /// <summary>
        /// Attempts to rename a file (via a handle) to its destination. The handle must have been opened with DELETE access.
        /// </summary>
        public static unsafe bool TryRename(SafeFileHandle handle, string destination, bool replaceExisting)
        {
            // FileRenameInfo as we've defined it contains one character which is enough for a terminating null byte. Then, we need room for the real characters.
            int fileNameLengthInBytesExcludingNull = destination.Length * sizeof(char);
            int structSizeIncludingDestination = sizeof(FileRenameInfo) + fileNameLengthInBytesExcludingNull;

            var buffer = new byte[structSizeIncludingDestination];

            fixed (byte* b = buffer)
            {
                var renameInfo = (FileRenameInfo*)b;
                renameInfo->ReplaceIfExists = replaceExisting ? (byte)1 : (byte)0;
                renameInfo->RootDirectory = IntPtr.Zero;
                renameInfo->FileNameLengthInBytes = fileNameLengthInBytesExcludingNull + sizeof(char);

                char* filenameBuffer = &renameInfo->FileName;
                for (int i = 0; i < destination.Length; i++)
                {
                    filenameBuffer[i] = destination[i];
                }

                filenameBuffer[destination.Length] = (char)0;
                Contract.Assume(buffer.Length > 2 && b[buffer.Length - 1] == 0 && b[buffer.Length - 2] == 0);

                return SetFileInformationByHandle(handle, (uint)FileInfoByHandleClass.FileRenameInfo, (IntPtr)renameInfo, structSizeIncludingDestination);
            }
        }

        /// <summary>
        /// Updates all timestamps for a file.
        /// </summary>
        public static unsafe void SetFileTimestamps(SafeFileHandle handle, DateTime creationTime, DateTime accessTime, DateTime lastWriteTime, DateTime lastChangeTime)
        {
            var newInfo = default(FileBasicInfo);
            newInfo.Attributes = (FileAttributes)0;
            newInfo.CreationTime = unchecked((ulong)creationTime.ToFileTimeUtc());
            newInfo.LastAccessTime = unchecked((ulong)accessTime.ToFileTimeUtc());
            newInfo.LastWriteTime = unchecked((ulong)lastWriteTime.ToFileTimeUtc());
            newInfo.ChangeTime = unchecked((ulong)lastChangeTime.ToFileTimeUtc());

            if (!SetFileInformationByHandle(handle, (uint)FileInfoByHandleClass.FileBasicInfo, (IntPtr)(&newInfo), sizeof(FileBasicInfo)))
            {
                ThrowForNativeFailure(Marshal.GetLastWin32Error(), "SetFileInformationByHandle");
            }
        }

        /// <summary>
        /// Attempts to delete file using posix semantics. Note: this function requires Win10 RS2 to run successfully.
        /// Otherwise the API call will just fail. Returns true if the deletion was successful.
        /// </summary>
        public static unsafe bool TryPosixDelete(string pathToDelete, out OpenFileResult openFileResult)
        {
            SafeFileHandle handle = CreateFileW(
                pathToDelete,
                FileDesiredAccess.Delete,
                FileShare.Delete | FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                FileFlagsAndAttributes.FileFlagBackupSemantics | FileFlagsAndAttributes.FileFlagOpenReparsePoint,
                IntPtr.Zero);

            using (handle)
            {
                int hr = Marshal.GetLastWin32Error();
                if (handle.IsInvalid)
                {
                    Tracing.Logger.Log.StorageTryOpenOrCreateFileFailure(Events.StaticContext, pathToDelete, (int)FileMode.Open, hr);
                    openFileResult = new OpenFileResult(hr, FileMode.Open, handleIsValid: false, openingById: false);
                    return false;
                }

                // handle will not be actually valid after this function terminates,
                // but it was at this time, and this is what we are reporting.
                openFileResult = new OpenFileResult(hr, FileMode.Open, handleIsValid: true, openingById: false);
                FileDispositionInfoEx fdi;
                fdi.Flags = FileDispositionFlags.Delete | FileDispositionFlags.PosixSemantics;

                // this is an optimistic call that might fail, so we are not calling Marshal.GetLastWin32Error() after it, just
                // relying on return value.
                bool deleted = SetFileInformationByHandle(
                    handle,
                    (uint)FileInfoByHandleClass.FileDispositionInfoEx,
                    (IntPtr)(&fdi),
                    sizeof(FileDispositionInfoEx));
                return deleted;
            }
        }

#pragma warning disable CA1724 // The type name FileSystem conflicts in whole or in part with the namespace name 'ContentStoreInterfaces.FileSystem'
        /// <summary>
        /// File system used on a volume.
        /// </summary>
        public enum FileSystem
#pragma warning restore CA1724 // The type name FileSystem conflicts in whole or in part with the namespace name 'ContentStoreInterfaces.FileSystem'
        {
            /// <summary>
            /// NTFS
            /// </summary>
            NTFS,

            /// <summary>
            /// ReFS (Windows 8.1+ / Server 2012R2+)
            /// </summary>
            ReFS,

            /// <summary>
            /// Anything other than ReFS or NTFS
            /// </summary>
            Unknown,
        }

        /// <summary>
        /// Returns a 64-bit volume serial number for the volume containing the given file when possible.
        /// If retrieving a 64-bit serial is not supported on this platform, this returns a synthesized one via
        /// sign-extending the 32-bit short serial (<see cref="GetShortVolumeSerialNumberByHandle"/>).
        /// </summary>
        public static FileSystem GetVolumeFileSystemByHandle(SafeFileHandle fileHandle)
        {
            var fileSystemNameBuffer = new StringBuilder(32);
            bool success = GetVolumeInformationByHandleW(
                fileHandle,
                volumeNameBuffer: null,
                volumeNameBufferSize: 0,
                volumeSerial: IntPtr.Zero,
                maximumComponentLength: IntPtr.Zero,
                fileSystemFlags: IntPtr.Zero,
                fileSystemNameBuffer: fileSystemNameBuffer,
                fileSystemNameBufferSize: fileSystemNameBuffer.Capacity);
            if (!success)
            {
                int hr = Marshal.GetLastWin32Error();
                throw ThrowForNativeFailure(hr, "GetVolumeInformationByHandleW");
            }

            string fileSystemName = fileSystemNameBuffer.ToString();
            switch (fileSystemName)
            {
                case "NTFS":
                    return FileSystem.NTFS;
                case "ReFS":
                    return FileSystem.ReFS;
                default:
                    return FileSystem.Unknown;
            }
        }

        /// <summary>
        /// Tries to open a directory handle.
        /// </summary>
        /// <remarks>
        /// The returned handle is suitable for operations such as <see cref="GetVolumeInformationByHandleW"/> but not wrapping in a <see cref="FileStream"/>.
        /// <see cref="FileDesiredAccess.Synchronize"/> is added implciitly.
        /// This function does not throw for any failure of CreateFileW.
        /// </remarks>
        public static OpenFileResult TryOpenDirectory(string directoryPath, FileDesiredAccess desiredAccess, FileShare shareMode, FileFlagsAndAttributes flagsAndAttributes, out SafeFileHandle handle)
        {
            Contract.Requires(!string.IsNullOrEmpty(directoryPath));
            Contract.Ensures(Contract.Result<OpenFileResult>().Succeeded == (Contract.ValueAtReturn(out handle) != null));
            Contract.Ensures(!Contract.Result<OpenFileResult>().Succeeded || !Contract.ValueAtReturn(out handle).IsInvalid);

            handle = CreateFileW(
                directoryPath,
                desiredAccess | FileDesiredAccess.Synchronize,
                shareMode,
                lpSecurityAttributes: IntPtr.Zero,
                dwCreationDisposition: FileMode.Open,
                dwFlagsAndAttributes: flagsAndAttributes | FileFlagsAndAttributes.FileFlagBackupSemantics,
                hTemplateFile: IntPtr.Zero);
            int hr = Marshal.GetLastWin32Error();

            if (handle.IsInvalid)
            {
                Domino.Storage.Tracing.Logger.Log.StorageTryOpenDirectoryFailure(Events.StaticContext, directoryPath, hr);
                handle = null;
                Contract.Assume(hr != 0);
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: false, openingById: false);
                Contract.Assume(!result.Succeeded); // CC: should be provable
                return result;
            }
            else
            {
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: true, openingById: false);
                Contract.Assume(result.Succeeded); // CC: should be provable
                return result;
            }
        }

        /// <summary>
        /// Tries to open a directory handle.
        /// </summary>
        /// <remarks>
        /// The returned handle is suitable for operations such as <see cref="GetVolumeInformationByHandleW"/> but not wrapping in a <see cref="FileStream"/>.
        /// <see cref="FileDesiredAccess.Synchronize"/> is added implciitly.
        /// This function does not throw for any failure of CreateFileW.
        /// </remarks>
        public static OpenFileResult TryOpenDirectory(string directoryPath, FileShare shareMode, out SafeFileHandle handle)
        {
            Contract.Requires(!string.IsNullOrEmpty(directoryPath));
            Contract.Ensures(Contract.Result<OpenFileResult>().Succeeded == (Contract.ValueAtReturn(out handle) != null));
            Contract.Ensures(!Contract.Result<OpenFileResult>().Succeeded || !Contract.ValueAtReturn(out handle).IsInvalid);

            return TryOpenDirectory(directoryPath, FileDesiredAccess.None, shareMode, FileFlagsAndAttributes.None, out handle);
        }

        /// <summary>
        /// Tries to open a file handle (for a new or existing file).
        /// </summary>
        /// <remarks>
        /// This is a thin wrapper for <c>CreateFileW</c>.
        /// Note that unlike other managed wrappers, this does not throw exceptions.
        /// Supports paths greater than MAX_PATH if the appropriate prefix is used
        /// </remarks>
        public static OpenFileResult TryCreateOrOpenFile(
            string path,
            FileDesiredAccess desiredAccess,
            FileShare shareMode,
            FileMode creationDisposition,
            FileFlagsAndAttributes flagsAndAttributes,
            out SafeFileHandle handle)
        {
            handle = CreateFileW(
                path,
                desiredAccess,
                shareMode,
                lpSecurityAttributes: IntPtr.Zero,
                dwCreationDisposition: creationDisposition,
                dwFlagsAndAttributes: flagsAndAttributes,
                hTemplateFile: IntPtr.Zero);
            int hr = Marshal.GetLastWin32Error();

            if (handle.IsInvalid)
            {
                Domino.Storage.Tracing.Logger.Log.StorageTryOpenOrCreateFileFailure(Events.StaticContext, path, (int)creationDisposition, hr);
                handle = null;
                Contract.Assume(hr != 0);
                var result = new OpenFileResult(hr, creationDisposition, handleIsValid: false, openingById: false);
                Contract.Assume(!result.Succeeded); // CC: should be provable
                return result;
            }
            else
            {
                var result = new OpenFileResult(hr, creationDisposition, handleIsValid: true, openingById: false);
                Contract.Assume(result.Succeeded); // CC: should be provable
                return result;
            }
        }

        /// <summary>
        /// Well-known expected failure cases for <see cref="WindowsNative.TryReOpenFile"/>
        /// </summary>
        public enum ReOpenFileStatus
        {
            /// <summary>
            /// The file was opened (a valid handle was obtained).
            /// </summary>
            Success,

            /// <summary>
            /// The file was opened already with an incompatible share mode, and no handle was obtained.
            /// </summary>
            SharingViolation,

            /// <summary>
            /// The file cannot be opened with the requested access level, and no handle was obtained.
            /// </summary>
            AccessDenied,
        }

        /// <summary>
        /// Tries to open a new file handle (with new access, share mode, and flags) via an existing handle to that file.
        /// </summary>
        /// <remarks>
        /// Wrapper for <c>ReOpenFile</c>.
        /// </remarks>
        public static ReOpenFileStatus TryReOpenFile(
            SafeFileHandle existing,
            FileDesiredAccess desiredAccess,
            FileShare shareMode,
            FileFlagsAndAttributes flagsAndAttributes,
            out SafeFileHandle reopenedHandle)
        {
            Contract.Requires(existing != null);
            Contract.Ensures((Contract.Result<ReOpenFileStatus>() == ReOpenFileStatus.Success) == (Contract.ValueAtReturn(out reopenedHandle) != null));
            Contract.Ensures((Contract.Result<ReOpenFileStatus>() != ReOpenFileStatus.Success) || !Contract.ValueAtReturn(out reopenedHandle).IsInvalid);

            SafeFileHandle newHandle = ReOpenFile(existing, desiredAccess, shareMode, flagsAndAttributes);
            int hr = Marshal.GetLastWin32Error();
            if (newHandle.IsInvalid)
            {
                reopenedHandle = null;
                Contract.Assume(hr != ErrorSuccess, "Invalid handle should imply an error.");
                switch (hr)
                {
                    case ErrorSharingViolation:
                        return ReOpenFileStatus.SharingViolation;
                    case ErrorAccessDenied:
                        return ReOpenFileStatus.AccessDenied;
                    default:
                        throw ThrowForNativeFailure(hr, "ReOpenFile");
                }
            }
            else
            {
                reopenedHandle = newHandle;
                return ReOpenFileStatus.Success;
            }
        }

        /// <summary>
        /// Creates a new IO completion port.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "Handles are either null/invalid or intentionally returned to caller")]
        public static SafeIOCompletionPortHandle CreateIOCompletionPort()
        {
            IntPtr rawHandle = CreateIoCompletionPort(
                handle: new SafeFileHandle(new IntPtr(-1), ownsHandle: false),
                existingCompletionPort: SafeIOCompletionPortHandle.CreateInvalid(),
                completionKey: IntPtr.Zero,
                numberOfConcurrentThreads: 0);
            int error = Marshal.GetLastWin32Error();

            var handle = new SafeIOCompletionPortHandle(rawHandle);

            if (handle.IsInvalid)
            {
                throw ThrowForNativeFailure(error, "CreateIoCompletionPort");
            }

            return handle;
        }

        /// <summary>
        /// Binds a file handle to the given IO completion port. The file must have been opened with <see cref="FileFlagsAndAttributes.FileFlagOverlapped"/>.
        /// Future completed IO operations for this handle will be queued to the specified port.
        /// </summary>
        /// <remarks>
        /// Along with binding to the port, this function also sets the handle's completion mode to <c>FILE_SKIP_COMPLETION_PORT_ON_SUCCESS</c>.
        /// This means that the caller should respect <c>ERROR_SUCCESS</c> (don't assume <c>ERROR_IO_PENDING</c>).
        /// </remarks>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods")]
        public static void BindFileHandleToIOCompletionPort(SafeFileHandle handle, SafeIOCompletionPortHandle port, IntPtr completionKey)
        {
            Contract.Requires(handle != null && !handle.IsInvalid);
            Contract.Requires(port != null && !port.IsInvalid);

            IntPtr returnedHandle = CreateIoCompletionPort(
                handle: handle,
                existingCompletionPort: port,
                completionKey: completionKey,
                numberOfConcurrentThreads: 0);

            if (returnedHandle == IntPtr.Zero || returnedHandle == INVALID_HANDLE_VALUE)
            {
                throw ThrowForNativeFailure(Marshal.GetLastWin32Error(), "CreateIoCompletionPort");
            }

            // Note that we do not wrap returnedHandle as a safe handle. This is because we would otherwise have two safe handles
            // wrapping the same underlying handle value, and could then double-free it.
            Contract.Assume(returnedHandle == port.DangerousGetHandle());

            // TODO 454491: We could also set FileSkipSetEventOnHandle here, such that the file's internal event is not cleared / signaled by the IO manager.
            //       However, this is a compatibility problem for existing usages of e.g. DeviceIoControl that do not specify an OVERLAPPED (which
            //       may wait on the file to be signaled). Ideally, we never depend on signaling a file handle used for async I/O, since we may
            //       to issue concurrent operations on the handle (and without the IO manager serializing requests as with sync handles, depending
            //       on signaling and waiting the file handle is simply unsafe).
            // We need unchecked here. The issue is that the SetFileCompletionNotificationModes native function returns BOOL, which is actually an int8.
            // When marshaling to Bool, if the highest bit is set we can get overflow error.
            bool success = unchecked(SetFileCompletionNotificationModes(
                handle,
                FileCompletionMode.FileSkipCompletionPortOnSuccess));

            if (!success)
            {
                throw ThrowForNativeFailure(Marshal.GetLastWin32Error(), "SetFileCompletionNotificationModes");
            }
        }

        /// <summary>
        /// Completion and success status of an async I/O operation.
        /// </summary>
        public enum FileAsyncIOStatus
        {
            /// <summary>
            /// The I/O operation is still in progress.
            /// </summary>
            Pending,

            /// <summary>
            /// The I/O operation has completed, and was successful.
            /// </summary>
            Succeeded,

            /// <summary>
            /// The I/O operation has completed, and failed with an error.
            /// </summary>
            Failed,
        }

        /// <summary>
        /// Result of a pending or completed I/O operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct FileAsyncIOResult
        {
            /// <summary>
            /// Success / completion status. The rest of the result is not valid when the status is <see cref="FileAsyncIOStatus.Pending"/>.
            /// </summary>
            public readonly FileAsyncIOStatus Status;
            private readonly int m_bytesTransferred;
            private readonly int m_error;

            internal FileAsyncIOResult(FileAsyncIOStatus status, int bytesTransferred, int error)
            {
                Contract.Requires(bytesTransferred >= 0);
                Contract.Requires((status == FileAsyncIOStatus.Succeeded) == (error == ErrorSuccess));
                Status = status;
                m_bytesTransferred = bytesTransferred;
                m_error = error;
            }

            /// <summary>
            /// Number of bytes transferred (from the requested start offset).
            /// Present only when the result status is not <see cref="FileAsyncIOStatus.Pending"/>.
            /// </summary>
            public int BytesTransferred
            {
                get
                {
                    Contract.Requires(Status != FileAsyncIOStatus.Pending);
                    return m_bytesTransferred;
                }
            }

            /// <summary>
            /// Native error code.
            /// Present only when the result status is not <see cref="FileAsyncIOStatus.Pending"/>.
            /// If the status is <see cref="FileAsyncIOStatus.Succeeded"/>, then this is <c>ERROR_SUCCESS</c>.
            /// </summary>
            public int Error
            {
                get
                {
                    Contract.Requires(Status != FileAsyncIOStatus.Pending);
                    return m_error;
                }
            }

            /// <summary>
            /// Indicates if the native error code specifies that the end of the file has been reached (specific to reading).
            /// Present only when the result status is not <see cref="FileAsyncIOStatus.Pending"/>.
            /// </summary>
            public bool ErrorIndicatesEndOfFile
            {
                get
                {
                    return Error == ErrorHandleEof;
                }
            }
        }

        /// <summary>
        /// Issues an async read via <c>ReadFile</c>. The eventual completion will possibly be sent to an I/O completion port, associated with <see cref="BindFileHandleToIOCompletionPort"/>.
        /// Note that <paramref name="pinnedBuffer"/> must be pinned on a callstack that lives until I/O completion or with a pinning <see cref="GCHandle"/>,
        /// similarly with the provided <paramref name="pinnedOverlapped" />; both are accessed by the kernel as the request is processed in the background.
        /// </summary>
        public static unsafe FileAsyncIOResult ReadFileOverlapped(SafeFileHandle handle, byte* pinnedBuffer, int bytesToRead, long fileOffset, Overlapped* pinnedOverlapped)
        {
            Contract.Requires(handle != null && !handle.IsInvalid);

            pinnedOverlapped->Offset = fileOffset;

            bool success = ReadFile(handle, pinnedBuffer, bytesToRead, lpNumberOfBytesRead: (int*)IntPtr.Zero, lpOverlapped: pinnedOverlapped);
            return CreateFileAsyncIOResult(handle, pinnedOverlapped, success);
        }

        /// <summary>
        /// Issues an async write via <c>WriteFile</c>. The eventual completion will possibly be sent to an I/O completion port, associated with <see cref="BindFileHandleToIOCompletionPort"/>.
        /// Note that <paramref name="pinnedBuffer"/> must be pinned on a callstack that lives until I/O completion or with a pinning <see cref="GCHandle"/>,
        /// similarly with the provided <paramref name="pinnedOverlapped" />; both are accessed by the kernel as the request is processed in the background.
        /// </summary>
        public static unsafe FileAsyncIOResult WriteFileOverlapped(SafeFileHandle handle, byte* pinnedBuffer, int bytesToWrite, long fileOffset, Overlapped* pinnedOverlapped)
        {
            Contract.Requires(handle != null && !handle.IsInvalid);

            pinnedOverlapped->Offset = fileOffset;

            bool success = WriteFile(handle, pinnedBuffer, bytesToWrite, lpNumberOfBytesWritten: (int*)IntPtr.Zero, lpOverlapped: pinnedOverlapped);
            return CreateFileAsyncIOResult(handle, pinnedOverlapped, success);
        }

        /// <summary>
        /// Common conversion from an overlapped <c>ReadFile</c> or <c>WriteFile</c> result to a <see cref="FileAsyncIOResult"/>.
        /// This must be called immediately after the IO operation such that <see cref="Marshal.GetLastWin32Error"/> is still valid.
        /// </summary>
        [SuppressMessage("Microsoft.Interoperability", "CA1404:CallGetLastErrorImmediatelyAfterPInvoke", Justification = "Intentionally wrapping GetLastWin32Error")]
        private static unsafe FileAsyncIOResult CreateFileAsyncIOResult(SafeFileHandle handle, Overlapped* pinnedOverlapped, bool success)
        {
            if (success)
            {
                // Success: IO completed synchronously and we will assume no completion packet is coming (due to FileCompletionMode.FileSkipCompletionPortOnSuccess).
                int bytesTransferred, error;
                GetCompletedOverlappedResult(handle, pinnedOverlapped, out error, out bytesTransferred);
                Contract.Assume(error == ErrorSuccess, "IO operation indicated success, but the completed OVERLAPPED did not contain ERROR_SUCCESS");
                return new FileAsyncIOResult(FileAsyncIOStatus.Succeeded, bytesTransferred: bytesTransferred, error: ErrorSuccess);
            }
            else
            {
                // Pending (a completion packet is expected) or synchronous failure.
                int error = Marshal.GetLastWin32Error();
                Contract.Assume(error != ErrorSuccess);

                bool completedSynchronously = error != ErrorIOPending;
                return new FileAsyncIOResult(
                    completedSynchronously ? FileAsyncIOStatus.Failed : FileAsyncIOStatus.Pending,
                    bytesTransferred: 0,
                    error: error);
            }
        }

        /// <summary>
        /// Unpacks a completed <c>OVERLAPPED</c> structure into the number of bytes transferred and error code for the completed operation.
        /// Fails if the given overlapped structure indicates that the IO operation has not yet completed.
        /// </summary>
        public static unsafe void GetCompletedOverlappedResult(SafeFileHandle handle, Overlapped* overlapped, out int error, out int bytesTransferred)
        {
            int bytesTransferredTemp = 0;
            if (!GetOverlappedResult(handle, overlapped, &bytesTransferredTemp, bWait: false))
            {
                bytesTransferred = 0;
                error = Marshal.GetLastWin32Error();
                if (error == ErrorIOIncomplete)
                {
                    throw ThrowForNativeFailure(error, "GetOverlappedResult");
                }
            }
            else
            {
                bytesTransferred = bytesTransferredTemp;
                error = ErrorSuccess;
            }
        }

        /// <summary>
        /// Status of dequeueing an I/O completion packet from a port. Indepenent from success / failure in the packet itself.
        /// </summary>
        public enum IOCompletionPortDequeueStatus
        {
            /// <summary>
            /// A packet was dequeued.
            /// </summary>
            Succeeded,

            /// <summary>
            /// The completion port has been closed, so further dequeues cannot proceed.
            /// </summary>
            CompletionPortClosed,
        }

        /// <summary>
        /// Result of dequeueing an I/O completion packet from a port.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public unsafe struct IOCompletionPortDequeueResult
        {
            /// <summary>
            /// Dequeue status (for the dequeue operation itself).
            /// </summary>
            public readonly IOCompletionPortDequeueStatus Status;
            private readonly FileAsyncIOResult m_completedIO;
            private readonly IntPtr m_completionKey;
            private readonly Overlapped* m_dequeuedOverlapped;

            internal IOCompletionPortDequeueResult(FileAsyncIOResult completedIO, Overlapped* dequeuedOverlapped, IntPtr completionKey)
            {
                Contract.Requires(completedIO.Status == FileAsyncIOStatus.Succeeded || completedIO.Status == FileAsyncIOStatus.Failed);
                Status = IOCompletionPortDequeueStatus.Succeeded;
                m_completedIO = completedIO;
                m_completionKey = completionKey;
                m_dequeuedOverlapped = dequeuedOverlapped;
            }

            internal IOCompletionPortDequeueResult(IOCompletionPortDequeueStatus status)
            {
                Contract.Requires(status != IOCompletionPortDequeueStatus.Succeeded);
                Status = status;
                m_completedIO = default(FileAsyncIOResult);
                m_completionKey = default(IntPtr);
                m_dequeuedOverlapped = null;
            }

            /// <summary>
            /// Result of the asynchronous I/O that completed. Available only if the status is <see cref="IOCompletionPortDequeueStatus.Succeeded"/>,
            /// meaning that a packet was actually dequeued.
            /// </summary>
            public FileAsyncIOResult CompletedIO
            {
                get
                {
                    Contract.Requires(Status == IOCompletionPortDequeueStatus.Succeeded);
                    Contract.Ensures(Contract.Result<FileAsyncIOResult>().Status != FileAsyncIOStatus.Pending);
                    return m_completedIO;
                }
            }

            /// <summary>
            /// Completion key (handle unique identifier) of the completed I/O. Available only if the status is <see cref="IOCompletionPortDequeueStatus.Succeeded"/>,
            /// meaning that a packet was actually dequeued.
            /// </summary>
            public IntPtr CompletionKey
            {
                get
                {
                    Contract.Requires(Status == IOCompletionPortDequeueStatus.Succeeded);
                    return m_completionKey;
                }
            }

            /// <summary>
            /// Pointer to the overlapped originally used to isse the completed I/O. Available only if the status is <see cref="IOCompletionPortDequeueStatus.Succeeded"/>,
            /// meaning that a packet was actually dequeued.
            /// </summary>
            public Overlapped* DequeuedOverlapped
            {
                get
                {
                    Contract.Requires(Status == IOCompletionPortDequeueStatus.Succeeded);
                    return m_dequeuedOverlapped;
                }
            }
        }

        /// <summary>
        /// Attempts to dequeue a completion packet from a completion port. The result indicates whether or not a packet
        /// was dequeued, and if so the packet's contents.
        /// </summary>
        [SuppressMessage("Microsoft.Interoperability", "CA1404:CallGetLastErrorImmediatelyAfterPInvoke", Justification = "Incorrect analysis")]
        public static unsafe IOCompletionPortDequeueResult GetQueuedCompletionStatus(SafeIOCompletionPortHandle completionPort)
        {
            // Possible indications:
            //   dequeuedOverlapped == null && !result: dequeue failed. Maybe ERROR_ABANDONED_WAIT_0 (port closed)?
            //   dequeuedOverlapped != null && !result: Dequeue succeeded. IO failed.
            //   dequeuedOverlapped != null && result: Dequeue succeeded. IO succeeded.
            //   dequeuedOverlapped == null && result: PostQueuedCompletionStatus with null OVERLAPPED
            // See https://msdn.microsoft.com/en-us/library/windows/desktop/aa364986%28v=vs.85%29.aspx
            Overlapped* dequeuedOverlapped = null;
            int bytesTransferred = 0;
            IntPtr completionKey = default(IntPtr);
            bool result = GetQueuedCompletionStatus(completionPort, &bytesTransferred, &completionKey, &dequeuedOverlapped, Infinite);

            if (result || dequeuedOverlapped != null)
            {
                // Latter three cases; dequeue succeeded.
                int error = ErrorSuccess;
                if (!result)
                {
                    error = Marshal.GetLastWin32Error();
                    Contract.Assume(error != ErrorSuccess);
                }

                return new IOCompletionPortDequeueResult(
                    new FileAsyncIOResult(
                        result ? FileAsyncIOStatus.Succeeded : FileAsyncIOStatus.Failed,
                        bytesTransferred: bytesTransferred,
                        error: error),
                    dequeuedOverlapped,
                    completionKey);
            }
            else
            {
                // Dequeue failed: dequeuedOverlapped == null && !result
                int error = Marshal.GetLastWin32Error();

                if (error == ErrorAbandonedWait0)
                {
                    return new IOCompletionPortDequeueResult(IOCompletionPortDequeueStatus.CompletionPortClosed);
                }
                else
                {
                    throw ThrowForNativeFailure(error, "GetQueuedCompletionStatus");
                }
            }
        }

        /// <summary>
        /// Queues a caller-defined completion packet to a completion port.
        /// </summary>
        public static unsafe void PostQueuedCompletionStatus(SafeIOCompletionPortHandle completionPort, IntPtr completionKey)
        {
            if (!PostQueuedCompletionStatus(completionPort, dwNumberOfBytesTransferred: 0, dwCompletionKey: completionKey, lpOverlapped: null))
            {
                throw ThrowForNativeFailure(Marshal.GetLastWin32Error(), "PostQueuedCompletionStatus");
            }
        }

        /// <summary>
        /// Status of a <see cref="WindowsNative.TryCreateHardLink"/> operation.
        /// </summary>
        public enum CreateHardLinkStatus
        {
            /// <summary>
            /// Succeeded.
            /// </summary>
            Success,

            /// <summary>
            /// Hardlinks may not span volumes, but the destination path is on a different volume.
            /// </summary>
            FailedSinceDestinationIsOnDifferentVolume,

            /// <summary>
            /// The source file cannot have more links. It is at the filesystem's link limit.
            /// </summary>
            FailedDueToPerFileLinkLimit,

            /// <summary>
            /// The filesystem containing the source and destination does not support hardlinks.
            /// </summary>
            FailedSinceNotSupportedByFilesystem,

            /// <summary>
            /// AccessDenied was returned
            /// </summary>
            FailedAccessDenied,

            /// <summary>
            /// Generic failure.
            /// </summary>
            Failed,
        }

        /// <summary>
        /// Tries to create a hardlink to the given file. The destination must not exist.
        /// </summary>
        public static CreateHardLinkStatus TryCreateHardLink(string link, string linkTarget)
        {
            bool result = CreateHardLinkW(link, linkTarget, IntPtr.Zero);
            if (result)
            {
                return CreateHardLinkStatus.Success;
            }

            switch (Marshal.GetLastWin32Error())
            {
                case ErrorNotSameDevice:
                    return CreateHardLinkStatus.FailedSinceDestinationIsOnDifferentVolume;
                case ErrorTooManyLinks:
                    return CreateHardLinkStatus.FailedDueToPerFileLinkLimit;
                case ErrorNotSupported:
                    return CreateHardLinkStatus.FailedSinceNotSupportedByFilesystem;
                case ErrorAccessDenied:
                    return CreateHardLinkStatus.FailedAccessDenied;
                default:
                    return CreateHardLinkStatus.Failed;
            }
        }

        /// <summary>
        /// Tries to create a hardlink to the given file via the SetInformationFile API. This is slightly
        /// different than calling CreateHardLinkW since it allows a link to be created on a linkTarget that
        /// is not writeable
        /// </summary>
        public static CreateHardLinkStatus TryCreateHardLinkViaSetInformationFile(string link, string linkTarget)
        {
            using (FileStream handle = FileUtilities.CreateFileStream(linkTarget, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.None))
            {
                FileLinkInformation fileLinkInformation = new FileLinkInformation(@"\??\" + link, true);
                IoStatusBlock block;
                var status = NtSetInformationFile(handle.SafeFileHandle, out block, fileLinkInformation, (uint)Marshal.SizeOf(fileLinkInformation), FileInformationClass.FileLinkInformation);
                var result = Marshal.GetLastWin32Error();
                if (status.IsSuccessful)
                {
                    return CreateHardLinkStatus.Success;
                }
                else
                {
                    switch (result)
                    {
                        case ErrorTooManyLinks:
                            return CreateHardLinkStatus.FailedDueToPerFileLinkLimit;
                        case ErrorNotSameDevice:
                            return CreateHardLinkStatus.FailedSinceDestinationIsOnDifferentVolume;
                        case ErrorAccessDenied:
                            return CreateHardLinkStatus.FailedAccessDenied;
                        case ErrorNotSupported:
                            return CreateHardLinkStatus.FailedSinceNotSupportedByFilesystem;
                        default:
                            return CreateHardLinkStatus.Failed;
                    }
                }
            }
        }

        /// <summary>
        /// Tries to create a symbolic link.
        /// </summary>
        public static bool TryCreateSymbolicLink(string symLinkFileName, string targetFileName, bool isTargetFile)
        {
            return CreateSymbolicLinkW(symLinkFileName, targetFileName, isTargetFile ? SymbolicLinkTarget.File : SymbolicLinkTarget.Directory);
        }

        /// <summary>
        /// Creates junction.
        /// A junction is essentially a softlink (a.k.a. symlink) between directories
        /// So, we would expect deleting that directory would make the junction point to missing data
        /// </summary>
        /// <param name="junctionPoint">Junction name.</param>
        /// <param name="targetDir">Target directory.</param>
        public static void CreateJunction(string junctionPoint, string targetDir)
        {
            const string NonInterpretedPathPrefix = @"\??\";

            if (!Directory.Exists(targetDir))
            {
                throw new IOException(I($"Target path '{targetDir}' does not exist or is not a directory."));
            }

            SafeFileHandle handle;
            var openReparsePoint = TryOpenReparsePoint(junctionPoint, FileDesiredAccess.GenericWrite, out handle);

            if (!openReparsePoint.Succeeded)
            {
                openReparsePoint.ThrowForError();
            }

            using (handle)
            {
                byte[] targetDirBytes = Encoding.Unicode.GetBytes(NonInterpretedPathPrefix + Path.GetFullPath(targetDir));

                REPARSE_DATA_BUFFER reparseDataBuffer = new REPARSE_DATA_BUFFER
                {
                    ReparseTag = DwReserved0Flag.IO_REPARSE_TAG_MOUNT_POINT,
                    ReparseDataLength = (ushort)(targetDirBytes.Length + 12),
                    SubstituteNameOffset = 0,
                    SubstituteNameLength = (ushort)targetDirBytes.Length,
                    PrintNameOffset = (ushort)(targetDirBytes.Length + 2),
                    PrintNameLength = 0,
                    PathBuffer = new byte[0x3ff0],
                };

                Array.Copy(targetDirBytes, reparseDataBuffer.PathBuffer, targetDirBytes.Length);

                int inBufferSize = Marshal.SizeOf(reparseDataBuffer);
                IntPtr inBuffer = Marshal.AllocHGlobal(inBufferSize);

                try
                {
                    Marshal.StructureToPtr(reparseDataBuffer, inBuffer, false);

                    int bytesReturned;
                    bool success = DeviceIoControl(
                        handle,
                        FSCTL_SET_REPARSE_POINT,
                        inBuffer,
                        targetDirBytes.Length + 20,
                        IntPtr.Zero,
                        0,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!success)
                    {
                        throw CreateWin32Exception(Marshal.GetLastWin32Error(), "DeviceIoControl");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuffer);
                }
            }
        }

        private static OpenFileResult TryOpenReparsePoint(string reparsePoint, FileDesiredAccess accessMode, out SafeFileHandle reparsePointHandle)
        {
            reparsePointHandle = CreateFileW(
                reparsePoint,
                accessMode,
                FileShare.Read | FileShare.Write | FileShare.Delete,
                IntPtr.Zero,
                FileMode.Open,
                FileFlagsAndAttributes.FileFlagBackupSemantics | FileFlagsAndAttributes.FileFlagOpenReparsePoint,
                IntPtr.Zero);

            int hr = Marshal.GetLastWin32Error();

            if (reparsePointHandle.IsInvalid)
            {
                reparsePointHandle = null;
                Contract.Assume(hr != 0);
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: false, openingById: false);
                Contract.Assume(!result.Succeeded);
                return result;
            }
            else
            {
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: true, openingById: false);
                Contract.Assume(result.Succeeded); // CC: should be provable
                return result;
            }
        }

        /// <summary>
        /// Enumerates volumes on the system and, for those accessible, returns a pair of (volume guid path, serial)
        /// </summary>
        /// <remarks>
        /// The volume guid path ends in a trailing slash, so it is openable as a directory (the volume root).
        /// The serial is the same as defined by <see cref="GetVolumeSerialNumberByHandle"/> for any file on the volume;
        /// note that the top 32 bits may be insignificant if long serials cannot be retireved on this platform (i.e.
        /// if <see cref="CanGetFileIdAndVolumeIdByHandle"/> is false).
        /// </remarks>
        public static List<Tuple<VolumeGuidPath, ulong>> ListVolumeGuidPathsAndSerials()
        {
            Contract.Ensures(Contract.Result<List<Tuple<VolumeGuidPath, ulong>>>().Count > 0);
            Contract.Ensures(Contract.ForAll(Contract.Result<List<Tuple<VolumeGuidPath, ulong>>>(), t => t.Item1.IsValid));

            var volumeList = new List<Tuple<VolumeGuidPath, ulong>>();

            // We don't want funky message boxes for poking removable media, e.g. a CD drive without a disk.
            // By observation, these drives *may* be returned when enumerating volumes. Run 'wmic volume get DeviceId,Name'
            // when an empty floppy / cd drive is visible in explorer.
            using (ErrorModeContext.DisableMessageBoxForRemovableMedia())
            {
                var volumeNameBuffer = new StringBuilder(capacity: MaxPath + 1);
                using (SafeFindVolumeHandle findVolumeHandle = FindFirstVolumeW(volumeNameBuffer, volumeNameBuffer.Capacity))
                {
                    {
                        int hr = Marshal.GetLastWin32Error();

                        // The docs say we'll see an invalid handle if it 'fails to find any volumes'. It's very hard to run this program without a volume, though.
                        // http://msdn.microsoft.com/en-us/library/windows/desktop/aa364425(v=vs.85).aspx
                        if (findVolumeHandle.IsInvalid)
                        {
                            throw ThrowForNativeFailure(hr, "FindNextVolumeW");
                        }
                    }

                    do
                    {
                        string volumeGuidPathString = volumeNameBuffer.ToString();
                        volumeNameBuffer.Clear();

                        Contract.Assume(!string.IsNullOrEmpty(volumeGuidPathString) && volumeGuidPathString[volumeGuidPathString.Length - 1] == '\\');
                        VolumeGuidPath volumeGuidPath;
                        bool volumeGuidPathParsed = VolumeGuidPath.TryCreate(volumeGuidPathString, out volumeGuidPath);
                        Contract.Assume(volumeGuidPathParsed, "FindFirstVolume / FindNextVolume promise to return volume GUID paths");

                        SafeFileHandle volumeRoot;
                        if (TryOpenDirectory(volumeGuidPathString, FileShare.Delete | FileShare.Read | FileShare.Write, out volumeRoot).Succeeded)
                        {
                            ulong serial;
                            using (volumeRoot)
                            {
                                serial = GetVolumeSerialNumberByHandle(volumeRoot);
                            }

                            Domino.Storage.Tracing.Logger.Log.StorageFoundVolume(Events.StaticContext, volumeGuidPathString, serial);
                            volumeList.Add(Tuple.Create(volumeGuidPath, serial));
                        }
                    }
                    while (FindNextVolumeW(findVolumeHandle, volumeNameBuffer, volumeNameBuffer.Capacity));

                    // FindNextVolumeW returned false; hopefully for the right reason.
                    {
                        int hr = Marshal.GetLastWin32Error();
                        if (hr != ErrorNoMoreFiles)
                        {
                            throw ThrowForNativeFailure(hr, "FindNextVolumeW");
                        }
                    }
                }
            }

            return volumeList;
        }

        /// <summary>
        /// Tries to open a file via its <see cref="FileId"/>. The <paramref name="existingHandleOnVolume"/> can be any handle on the same volume
        /// (file IDs are unique only per volume).
        /// </summary>
        /// <remarks>
        /// This function does not throw for any failure of <c>OpenFileById</c>.
        /// </remarks>
        public static OpenFileResult TryOpenFileById(
            SafeFileHandle existingHandleOnVolume,
            FileId fileId,
            FileDesiredAccess desiredAccess,
            FileShare shareMode,
            FileFlagsAndAttributes flagsAndAttributes,
            out SafeFileHandle handle)
        {
            Contract.Requires(existingHandleOnVolume != null && !existingHandleOnVolume.IsInvalid);
            Contract.Ensures(Contract.Result<OpenFileResult>().Succeeded == (Contract.ValueAtReturn(out handle) != null));
            Contract.Ensures(!Contract.Result<OpenFileResult>().Succeeded || !Contract.ValueAtReturn(out handle).IsInvalid);

            var fileIdDescriptor = new FileIdDescriptor(fileId);
            handle = OpenFileById(
                existingHandleOnVolume,
                fileIdDescriptor,
                desiredAccess,
                shareMode,
                lpSecurityAttributes: IntPtr.Zero,
                dwFlagsAndAttributes: flagsAndAttributes);
            int hr = Marshal.GetLastWin32Error();

            if (handle.IsInvalid)
            {
                Domino.Storage.Tracing.Logger.Log.StorageTryOpenFileByIdFailure(Events.StaticContext, fileId.High, fileId.Low, GetVolumeSerialNumberByHandle(existingHandleOnVolume), hr);
                handle = null;
                Contract.Assume(hr != 0);
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: false, openingById: true);
                Contract.Assume(!result.Succeeded); // CC: should be provable
                return result;
            }
            else
            {
                var result = new OpenFileResult(hr, FileMode.Open, handleIsValid: true, openingById: true);
                Contract.Assume(result.Succeeded); // CC: should be provable
                return result;
            }
        }

        // SymLink target support
        // Constants
        private const int INITIAL_REPARSE_DATA_BUFFER_SIZE = 1024;
        private const int FSCTL_GET_REPARSE_POINT = 0x000900a8;

        private const int ERROR_INSUFFICIENT_BUFFER = 0x7A;
        private const int ERROR_MORE_DATA = 0xEA;
        private const int ERROR_SUCCESS = 0x0;
        private const int SYMLINK_FLAG_RELATIVE = 0x1;

        /// <summary>
        /// Returns the target of a symlink.
        /// </summary>
        /// <param name="handle">Handle to the reparse point.</param>
        /// <param name="reparsePointType">The type of the reparse point.</param>
        /// <param name="sourcePath">Source path.</param>
        /// <param name="targetPath">The target path of the reparse point to return.</param>
        /// <returns>0 if successful, LastErrorCode on error.</returns>
        /// <remarks>If the function fails the tergetPath value is undefined.</remarks>
        public static unsafe int GetReparsePointTarget(SafeFileHandle handle, ReparsePointType reparsePointType, string sourcePath, out string targetPath)
        {
            Contract.Requires(reparsePointType == ReparsePointType.SymLink || reparsePointType == ReparsePointType.MountPoint);

            targetPath = string.Empty;

            int bufferSize = INITIAL_REPARSE_DATA_BUFFER_SIZE;
            int errorCode = ERROR_INSUFFICIENT_BUFFER;

            byte[] buffer = null;
            while (errorCode == ERROR_MORE_DATA || errorCode == ERROR_INSUFFICIENT_BUFFER)
            {
                buffer = new byte[bufferSize];
                bool success = false;

                fixed (byte* pBuffer = buffer)
                {
                    int bufferReturnedSize;
                    success = DeviceIoControl(
                        handle,
                        FSCTL_GET_REPARSE_POINT,
                        IntPtr.Zero,
                        0,
                        (IntPtr)pBuffer,
                        bufferSize,
                        out bufferReturnedSize,
                        IntPtr.Zero);
                }

                bufferSize *= 2;
                errorCode = success ? 0 : Marshal.GetLastWin32Error();
            }

            if (errorCode != 0)
            {
                return errorCode;
            }

            // Now get the offsets in the REPARSE_DATA_BUFFER buffer string based on
            // the offsets for the different type of reparse points.
            uint pathBufferOffsetIndex = (uint)((reparsePointType == ReparsePointType.SymLink) ? 20 : 16);
            const uint PrintNameOffsetIndex = 12;
            const uint PrintNameLangthIndex = 14;
            const uint SymLinkFlagIndex = 16;
            const uint SubsNameOffsetIndex = 8;
            const uint SubsNameLangthIndex = 10;

            fixed (byte* pBuffer = buffer)
            {
                char* nameStartPtr = (char*)((IntPtr)(pBuffer + pathBufferOffsetIndex));
                short nameOffset = (short)(((short)*(pBuffer + PrintNameOffsetIndex)) / 2);
                short nameLength = (short)(((short)*(pBuffer + PrintNameLangthIndex)) / 2);
                targetPath = new string(nameStartPtr, nameOffset, nameLength);

                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    nameOffset = (short)(((short)*(pBuffer + SubsNameOffsetIndex)) / 2);
                    nameLength = (short)(((short)*(pBuffer + SubsNameLangthIndex)) / 2);
                    targetPath = new string(nameStartPtr, nameOffset, nameLength);
                }

                if (reparsePointType == ReparsePointType.SymLink)
                {
                    long flags = (long)((long)(*(pBuffer + SymLinkFlagIndex)));

                    if ((flags & SYMLINK_FLAG_RELATIVE) != 0)
                    {
                        // NOTE: Don't use anything from .NET Path that causes failure in handling long path.
                        string dirPath = sourcePath.TrimEnd(Path.DirectorySeparatorChar);

                        int lastIndexOfSeparator = dirPath.LastIndexOf(Path.DirectorySeparatorChar);
                        Contract.Assert(lastIndexOfSeparator != -1);

                        int previousLastIndexOfSeparator = -1;

                        bool startWithDotSlash = nameLength >= 2 && nameStartPtr[nameOffset] == '.' && nameStartPtr[nameOffset + 1] == '\\';
                        bool startWithDotDotSlash = nameLength >= 3 && nameStartPtr[nameOffset] == '.' && nameStartPtr[nameOffset + 1] == '.' && nameStartPtr[nameOffset + 2] == '\\';
                        while ((startWithDotDotSlash || startWithDotSlash) && lastIndexOfSeparator != -1)
                        {
                            if (startWithDotSlash)
                            {
                                nameOffset += 2;
                                nameLength -= 2;
                            }
                            else
                            {
                                nameOffset += 3;
                                nameLength -= 3;
                                previousLastIndexOfSeparator = lastIndexOfSeparator;
                                lastIndexOfSeparator = dirPath.LastIndexOf(Path.DirectorySeparatorChar, lastIndexOfSeparator - 1);
                            }

                            startWithDotSlash = nameLength >= 2 && nameStartPtr[nameOffset] == '.' && nameStartPtr[nameOffset + 1] == '\\';
                            startWithDotDotSlash = nameLength >= 3 && nameStartPtr[nameOffset] == '.' && nameStartPtr[nameOffset + 1] == '.' && nameStartPtr[nameOffset + 2] == '\\';
                        }

                        string fileName = new string(nameStartPtr, nameOffset, nameLength);
                        targetPath = Path.Combine(dirPath.Substring(0, lastIndexOfSeparator != -1 ? lastIndexOfSeparator : previousLastIndexOfSeparator), fileName);
                    }
                }
            }

            return errorCode;
        }

        /// <summary>
        /// Returns a fully-normalized path corresponding to the given file handle. If a <paramref name="volumeGuidPath"/> is requested,
        /// the returned path will start with an NT-style path with a volume guid such as <c>\\?\Volume{2ce38532-4595-11e3-93ec-806e6f6e6963}\</c>.
        /// Otherwise, a DOS-style path starting with a drive-letter will be returned if possible (if the file's volume is not mounted to a drive letter,
        /// then this function falls back to act as if <paramref name="volumeGuidPath"/> was true).
        /// </summary>
        public static string GetFinalPathNameByHandle(SafeFileHandle handle, bool volumeGuidPath = false)
        {
            const int VolumeNameGuid = 0x1;

            var pathBuffer = new StringBuilder(MaxPath);

            int neededSize = MaxPath;
            do
            {
                pathBuffer.EnsureCapacity(neededSize);
                neededSize = GetFinalPathNameByHandleW(handle, pathBuffer, pathBuffer.Capacity, flags: volumeGuidPath ? VolumeNameGuid : 0);
                if (neededSize == 0)
                {
                    int hr = Marshal.GetLastWin32Error();

                    // ERROR_PATH_NOT_FOUND
                    if (hr == 0x3)
                    {
                        // This can happen if the volume
                        Contract.Assume(!volumeGuidPath);
                        return GetFinalPathNameByHandle(handle, volumeGuidPath: true);
                    }
                    else
                    {
                        throw ThrowForNativeFailure(hr, "GetFinalPathNameByHandleW");
                    }
                }

                Contract.Assume(neededSize < MaxLongPath);
            }
            while (neededSize > pathBuffer.Capacity);

            const string ExpectedPrefix = @"\\?\";
            Contract.Assume(pathBuffer.Length >= ExpectedPrefix.Length, "Expected a long-path prefix");
            for (int i = 0; i < ExpectedPrefix.Length; i++)
            {
                Contract.Assume(pathBuffer[i] == ExpectedPrefix[i], "Expected a long-path prefix");
            }

            if (volumeGuidPath)
            {
                return pathBuffer.ToString();
            }
            else
            {
                return pathBuffer.ToString(startIndex: ExpectedPrefix.Length, length: pathBuffer.Length - ExpectedPrefix.Length);
            }
        }

        /// <summary>
        /// Removes a directory
        /// </summary>
        /// <remarks>
        /// This calls the native RemoveDirectory function which only marks the directory for deletion on close, so it
        /// may not be deleted if there are other open handles
        /// Supports paths beyond MAX_PATH if prefix is added
        /// </remarks>
        public static bool TryRemoveDirectory(
            string path,
            out int hr)
        {
            Contract.Ensures(Contract.Result<bool>() ^ Contract.ValueAtReturn<int>(out hr) != 0);

            if (!RemoveDirectoryW(path))
            {
                hr = Marshal.GetLastWin32Error();
                return false;
            }

            hr = 0;
            return true;
        }

        /// <summary>
        /// Removes a directory
        /// </summary>
        /// <remarks>
        /// This calls the native RemoveDirectory function which only marks the directory for deletion on close, so it
        /// may not be deleted if there are other open handles
        /// Supports paths beyond MAX_PATH if prefix is added
        /// </remarks>
        public static void RemoveDirectory(string path)
        {
            int hr;
            if (!TryRemoveDirectory(path, out hr))
            {
                ThrowForNativeFailure(hr, "RemoveDirectoryW");
            }
        }

        /// <summary>
        /// Thin wrapper for native SetFileAttributesW that checks the win32 error upon failure
        /// </summary>
        /// <remarks>
        /// Supports paths greater than MAX_PATH if "\\?\" prefix is used
        /// </remarks>
        public static bool TrySetFileAttributes(string path, FileAttributes attributes, out int hr)
        {
            Contract.Ensures(Contract.Result<bool>() ^ Contract.ValueAtReturn<int>(out hr) != 0);

            if (!SetFileAttributesW(path, attributes))
            {
                hr = Marshal.GetLastWin32Error();
                return false;
            }

            hr = 0;
            return true;
        }

        /// <summary>
        /// Thin wrapper for native SetFileAttributesW that throws an exception on failure
        /// </summary>
        /// <remarks>
        /// Supports paths greater than MAX_PATH if "\\?\" prefix is used
        /// </remarks>
        public static void SetFileAttributes(string path, FileAttributes attributes)
        {
            int hr;
            if (!TrySetFileAttributes(path, attributes, out hr))
            {
                ThrowForNativeFailure(hr, "SetFileAttributesW");
            }
        }

        /// <summary>
        /// Thin wrapper for native GetFileAttributesW that checks the win32 error upon failure
        /// </summary>
        /// <remarks>
        /// Supports paths greater than MAX_PATH if "\\?\" prefix is used
        /// </remarks>
        public static bool TryGetFileAttributes(string path, out FileAttributes attributes, out int hr)
        {
            Contract.Ensures(Contract.Result<bool>() ^ Contract.ValueAtReturn<int>(out hr) != 0);

            var fileAttributes = GetFileAttributesW(path);

            if (fileAttributes == InvalidFileAttributes)
            {
                hr = Marshal.GetLastWin32Error();
                attributes = FileAttributes.Normal;
                return false;
            }

            hr = 0;
            attributes = (FileAttributes)fileAttributes;
            return true;
        }

        /// <summary>
        /// Indicates path existence (as a file, as a directory, or not at all) via probing with <c>GetFileAttributesW</c>
        /// </summary>
        /// <remarks>
        /// Supports paths greater than MAX_PATH if "\\?\" prefix is used
        /// </remarks>
        public static Possible<PathExistence, NativeWin32Failure> TryProbePathExistence(string path)
        {
            uint fileAttributes = GetFileAttributesW(path);

            if (fileAttributes == InvalidFileAttributes)
            {
                int hr = Marshal.GetLastWin32Error();

                if (IsHresultNonesixtent(hr))
                {
                    return PathExistence.Nonexistent;
                }
                else
                {
                    return new NativeWin32Failure(hr);
                }
            }

            return (((FileAttributes)fileAttributes & FileAttributes.Directory) != 0) ? PathExistence.ExistsAsDirectory : PathExistence.ExistsAsFile;
        }

        /// <summary>
        /// Thin wrapper for native GetFileAttributesW that throws an exception on failure
        /// </summary>
        /// <remarks>
        /// Supports paths greater than MAX_PATH if "\\?\" prefix is used
        /// </remarks>
        public static FileAttributes GetFileAttributes(string path)
        {
            int hr;
            FileAttributes attributes;
            if (!TryGetFileAttributes(path, out attributes, out hr))
            {
                ThrowForNativeFailure(hr, "GetFileAttributesW");
            }

            return attributes;
        }

        /// <summary>
        /// Status of attempting to enumerate a directory.
        /// </summary>
        public enum EnumerateDirectoryStatus
        {
            /// <summary>
            /// Enumeration of an existent directory succeeded.
            /// </summary>
            Success,

            /// <summary>
            /// One or more path components did not exist, so the search directory could not be opened.
            /// </summary>
            SearchDirectoryNotFound,

            /// <summary>
            /// A path component in the search path refers to a file. Only directories can be enumerated.
            /// </summary>
            CannotEnumerateFile,

            /// <summary>
            /// Directory enumeration could not complete due to denied access to the search directory or a file inside.
            /// </summary>
            AccessDenied,

            /// <summary>
            /// Directory enumeration failed without a well-known status (see <see cref="EnumerateDirectoryResult.NativeErrorCode"/>).
            /// </summary>
            UnknownError,
        }

        /// <summary>
        /// Represents the result of attempting to enumerate a directory.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct EnumerateDirectoryResult
        {
            /// <summary>
            /// Enumerated directory.
            /// </summary>
            public readonly string Directory;

            /// <summary>
            /// Overall status indication.
            /// </summary>
            public readonly EnumerateDirectoryStatus Status;

            /// <summary>
            /// Native error code. Note that an error code other than <c>ERROR_SUCCESS</c> may be present even on success.
            /// </summary>
            public readonly int NativeErrorCode;

            /// <nodoc />
            public EnumerateDirectoryResult(string directory, EnumerateDirectoryStatus status, int nativeErrorCode)
            {
                Directory = directory;
                Status = status;
                NativeErrorCode = nativeErrorCode;
            }

            /// <summary>
            /// Indicates if enumeration succeeded.
            /// </summary>
            public bool Succeeded
            {
                get { return Status == EnumerateDirectoryStatus.Success; }
            }

            /// <summary>
            /// Throws an exception if the native error code could not be canonicalized (a fairly exceptional circumstance).
            /// This is allowed when <see cref="Status"/> is <see cref="EnumerateDirectoryStatus.UnknownError"/>.
            /// </summary>
            /// <remarks>
            /// This is a good <c>default:</c> case when switching on every possible <see cref="EnumerateDirectoryStatus"/>
            /// </remarks>
            public NativeWin32Exception ThrowForUnknownError()
            {
                Contract.Requires(Status == EnumerateDirectoryStatus.UnknownError);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Throws an exception if the native error code was corresponds to a known <see cref="EnumerateDirectoryStatus"/>
            /// (and enumeration was not successful).
            /// </summary>
            public NativeWin32Exception ThrowForKnownError()
            {
                Contract.Requires(Status != EnumerateDirectoryStatus.UnknownError && Status != EnumerateDirectoryStatus.Success);
                throw CreateExceptionForError();
            }

            /// <summary>
            /// Creates (but does not throw) an exception for this result. The result must not be successful.
            /// </summary>
            public NativeWin32Exception CreateExceptionForError()
            {
                Contract.Requires(Status != EnumerateDirectoryStatus.Success);
                if (Status == EnumerateDirectoryStatus.UnknownError)
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        "Enumerating a directory failed");
                }
                else
                {
                    return new NativeWin32Exception(
                        NativeErrorCode,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Enumerating a directory failed: {0:G}", Status));
                }
            }
        }

        /// <summary>
        /// Enumerates all the names and attributes of entries in the given directory. See <see cref="EnumerateDirectoryEntries(string,bool,System.Action{string,string,System.IO.FileAttributes})"/>
        /// </summary>
        public static EnumerateDirectoryResult EnumerateDirectoryEntries(
            string directoryPath,
            bool recursive,
            Action<string, string, FileAttributes> handleEntry)
        {
            return EnumerateDirectoryEntries(directoryPath, recursive, "*", handleEntry);
        }

        /// <summary>
        /// Enumerates the names and attributes of entries in the given directory using a search pattern.
        /// </summary>
        /// <remarks>
        /// Supports paths beyond MAX_PATH, so long as a \\?\ prefixed and fully canonicalized path is provided.
        /// The provided path is expected canonicalized even without a long-path prefix.
        /// </remarks>
        public static EnumerateDirectoryResult EnumerateDirectoryEntries(
            string directoryPath,
            bool recursive,
            string pattern,
            Action<string, string, FileAttributes> handleEntry)
        {
            var trimmedDirectoryPath = directoryPath.TrimEnd('\\');
            var searchDirectoryPath = Path.Combine(trimmedDirectoryPath, pattern);

            WIN32_FIND_DATA findResult;
            using (SafeFindFileHandle findHandle = FindFirstFileW(searchDirectoryPath, out findResult))
            {
                if (findHandle.IsInvalid)
                {
                    int hr = Marshal.GetLastWin32Error();
                    Contract.Assume(hr != ErrorSuccess);

                    EnumerateDirectoryStatus findHandleOpenStatus;
                    switch (hr)
                    {
                        case ErrorFileNotFound:
                            // ERROR_FILE_NOT_FOUND means that no results were found for the given pattern.
                            // This shouldn't actually happen so long as we only support the trivial \* wildcard,
                            // since we expect to always match the magic . and .. entries.
                            findHandleOpenStatus = EnumerateDirectoryStatus.Success;
                            break;
                        case ErrorPathNotFound:
                            findHandleOpenStatus = EnumerateDirectoryStatus.SearchDirectoryNotFound;
                            break;
                        case ErrorDirectory:
                            findHandleOpenStatus = EnumerateDirectoryStatus.CannotEnumerateFile;
                            break;
                        case ErrorAccessDenied:
                            findHandleOpenStatus = EnumerateDirectoryStatus.AccessDenied;
                            break;
                        default:
                            findHandleOpenStatus = EnumerateDirectoryStatus.UnknownError;
                            break;
                    }

                    return new EnumerateDirectoryResult(directoryPath, findHandleOpenStatus, hr);
                }

                while (true)
                {
                    // There will be entries for the current and parent directories. Ignore those.
                    if (((findResult.DwFileAttributes & FileAttributes.Directory) == 0) ||
                        (findResult.CFileName != "." && findResult.CFileName != ".."))
                    {
                        handleEntry(directoryPath, findResult.CFileName, findResult.DwFileAttributes);

                        if (recursive && (findResult.DwFileAttributes & FileAttributes.Directory) != 0)
                        {
                            var recursiveResult = EnumerateDirectoryEntries(
                                Path.Combine(directoryPath, findResult.CFileName),
                                recursive: true,
                                handleEntry: handleEntry);

                            if (!recursiveResult.Succeeded)
                            {
                                return recursiveResult;
                            }
                        }
                    }

                    if (!FindNextFileW(findHandle, out findResult))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        if (hr == ErrorNoMoreFiles)
                        {
                            // Graceful completion of enumeration.
                            return new EnumerateDirectoryResult(directoryPath, EnumerateDirectoryStatus.Success, hr);
                        }
                        else
                        {
                            Contract.Assume(hr != ErrorSuccess);

                            // Maybe we can fail ACLs in the middle of enumerating. Do we nead FILE_READ_ATTRIBUTES on each file? That would be surprising
                            // since the security descriptors aren't in the directory file. All other canonical statuses have to do with beginning enumeration
                            // rather than continuing (can we open the search directory?)
                            // So, let's assume that this failure is esoteric and use the 'unknown error' catchall.
                            return new EnumerateDirectoryResult(directoryPath, EnumerateDirectoryStatus.UnknownError, hr);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="WindowsNative.FileFlagsAndAttributes"/> for opening a directory.
        /// </summary>
        public static FileFlagsAndAttributes GetFileFlagsAndAttributesForOpeningDirectory(string expandedPath)
        {
            Possible<ReparsePointType> reparsePointType = TryGetReparsePointType(expandedPath);
            var isActionableReparsePoint = false;

            if (reparsePointType.Succeeded)
            {
                isActionableReparsePoint = IsReparsePointActionable(reparsePointType.Result);
            }

            var openFlags = FileFlagsAndAttributes.FileFlagOverlapped;

            if (isActionableReparsePoint)
            {
                openFlags = openFlags | FileFlagsAndAttributes.FileFlagOpenReparsePoint;
            }

            return openFlags;
        }

        /// <summary>
        /// Enumerates the names and attributes of entries in the given directory.
        /// </summary>
        /// <remarks>
        /// Supports paths beyond MAX_PATH, so long as a \\?\ prefixed and fully canonicalized path is provided.
        /// The provided path is expected canonicalized even without a long-path prefix.
        /// </remarks>
        public static EnumerateDirectoryResult EnumerateDirectoryEntries(string directoryPath, Action<string, FileAttributes> handleEntry)
        {
            return EnumerateDirectoryEntries(directoryPath, false, (currentDirectory, fileName, fileAttributes) => handleEntry(fileName, fileAttributes));
        }

        /// <summary>
        /// Throws an exception for the unexpected failure of a native API.
        /// </summary>
        /// <remarks>
        /// We don't want native failure checks erased at any contract-rewriting setting.
        /// The return type is <see cref="Exception"/> to facilitate a pattern of <c>throw ThrowForNativeFailure(...)</c> which informs csc's flow control analysis.
        /// </remarks>
        internal static Exception ThrowForNativeFailure(int error, string nativeApiName, [CallerMemberName] string managedApiName = "<unknown>")
        {
            Contract.Requires(!string.IsNullOrEmpty(nativeApiName) && !string.IsNullOrEmpty(managedApiName));

            throw CreateWin32Exception(error, nativeApiName, managedApiName);
        }

        /// <summary>
        /// Creates a Win32 exception for an HResult
        /// </summary>
        internal static NativeWin32Exception CreateWin32Exception(int error, string nativeApiName, [CallerMemberName] string managedApiName = "<unknown>")
        {
            Contract.Requires(!string.IsNullOrEmpty(nativeApiName) && !string.IsNullOrEmpty(managedApiName));

            return new NativeWin32Exception(
                error,
                string.Format(CultureInfo.InvariantCulture, "{0} for {1} failed", nativeApiName, managedApiName));
        }

        /// <summary>
        /// Throws an exception for the unexpected failure of a native API.
        /// </summary>
        /// <remarks>
        /// We don't want native failure checks erased at any contract-rewriting setting.
        /// The return type is <see cref="Exception"/> to facilitate a pattern of <c>throw ThrowForNativeFailure(...)</c> which informs csc's flow control analysis.
        /// </remarks>
        internal static Exception ThrowForNativeFailure(NtStatus status, string nativeApiName, [CallerMemberName] string managedApiName = "<unknown>")
        {
            Contract.Requires(!string.IsNullOrEmpty(nativeApiName) && !string.IsNullOrEmpty(managedApiName));

            throw CreateNtException(status, nativeApiName, managedApiName);
        }

        /// <summary>
        /// Creates an NT exception for an NTSTATUS
        /// </summary>
        internal static NativeNtException CreateNtException(NtStatus status, string nativeApiName, [CallerMemberName] string managedApiName = "<unknown>")
        {
            Contract.Requires(!string.IsNullOrEmpty(nativeApiName) && !string.IsNullOrEmpty(managedApiName));

            return new NativeNtException(
                status,
                string.Format(CultureInfo.InvariantCulture, "{0} for {1} failed", nativeApiName, managedApiName));
        }

        /// <summary>
        /// Tries to read the seek penalty property from a drive handle
        /// </summary>
        /// <param name="driveHandle">Handle to the drive. May either be a physical drive (ex: \\.\PhysicalDrive0)
        /// or a logical drive (ex: \\.c\:)</param>
        /// <param name="hasSeekPenalty">Set to the appropriate value if the check is successful</param>
        /// <param name="error">Error code returned by the native api</param>
        /// <returns>True if the property was able to be read</returns>
        public static bool TryReadSeekPenaltyProperty(SafeFileHandle driveHandle, out bool hasSeekPenalty, out int error)
        {
            Contract.Requires(driveHandle != null);
            Contract.Requires(!driveHandle.IsInvalid);

            hasSeekPenalty = true;
            STORAGE_PROPERTY_QUERY storagePropertyQuery = default(STORAGE_PROPERTY_QUERY);
            storagePropertyQuery.PropertyId = StorageDeviceSeekPenaltyProperty;
            storagePropertyQuery.QueryType = PropertyStandardQuery;

            DEVICE_SEEK_PENALTY_DESCRIPTOR seekPropertyDescriptor = default(DEVICE_SEEK_PENALTY_DESCRIPTOR);

            uint bytesReturned;

            bool ioctlSuccess = DeviceIoControl(
                driveHandle,
                IOCTL_STORAGE_QUERY_PROPERTY,
                ref storagePropertyQuery,
                Marshal.SizeOf<STORAGE_PROPERTY_QUERY>(),
                out seekPropertyDescriptor,
                Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>(),
                out bytesReturned,
                IntPtr.Zero);
            error = Marshal.GetLastWin32Error();

            if (ioctlSuccess)
            {
                Contract.Assume(bytesReturned >= Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>(), "Query returned fewer bytes than length of output data");
                hasSeekPenalty = seekPropertyDescriptor.IncursSeekPenalty;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Enum that defines the possible actionable reparse point types.
        /// </summary>
        public enum ReparsePointType
        {
            None = 0,
            SymLink = 1,
            MountPoint = 2,
            NonActionable = 3,
        }

        /// <summary>
        /// Returns whether the reparse point type is actionable, i.e., a mount point or a symlink.
        /// </summary>
        /// <param name="reparsePointType">The type of the reparse point.</param>
        /// <returns>true if this is an actionable reparse point, otherwise false.</returns>
        public static bool IsReparsePointActionable(ReparsePointType reparsePointType)
        {
            return reparsePointType == WindowsNative.ReparsePointType.SymLink || reparsePointType == WindowsNative.ReparsePointType.MountPoint;
        }

        /// <summary>
        /// Returns <see cref="ReparsePointType"/> of a path.
        /// </summary>
        /// <param name="path">Path to check for reparse point.</param>
        /// <returns>The type of the reparse point.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "GetFileAttributesW")]
        public static Possible<ReparsePointType> TryGetReparsePointType(string path)
        {
            // Not calling WindowsNative.GetFileAttributes to avoid throwing an exception in the hot path here
            int hr;
            FileAttributes attributes;
            if (!TryGetFileAttributes(path, out attributes, out hr))
            {
                return new Possible<ReparsePointType>(new NativeWin32Failure(hr, "GetFileAttributesW"));
            }

            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                return ReparsePointType.None;
            }

            WIN32_FIND_DATA findResult;
            using (SafeFindFileHandle findHandle = FindFirstFileW(path, out findResult))
            {
                if (!findHandle.IsInvalid)
                {
                    if (findResult.DwReserved0 == (uint)DwReserved0Flag.IO_REPARSE_TAG_SYMLINK ||
                        findResult.DwReserved0 == (uint)DwReserved0Flag.IO_REPARSE_TAG_MOUNT_POINT)
                    {
                        return findResult.DwReserved0 == (uint)DwReserved0Flag.IO_REPARSE_TAG_SYMLINK
                            ? ReparsePointType.SymLink
                            : ReparsePointType.MountPoint;
                    }

                    return ReparsePointType.NonActionable;
                }
            }

            return ReparsePointType.None;
        }

        /// <summary>
        /// Tries to create symlinks if not exists.
        /// </summary>
        public static bool TryCreateSymlinkIfNotExists(string symlink, string symlinkTarget, bool isTargetFile, out bool created)
        {
            created = false;
            var possibleReparsePoint = TryGetReparsePointType(symlink);

            bool shouldCreate = true;

            if (possibleReparsePoint.Succeeded && IsReparsePointActionable(possibleReparsePoint.Result))
            {
                SafeFileHandle handle;
                var openResult = TryCreateOrOpenFile(
                    symlink,
                    FileDesiredAccess.GenericRead,
                    FileShare.Read | FileShare.Delete,
                    FileMode.Open,
                    FileFlagsAndAttributes.FileFlagOverlapped | FileFlagsAndAttributes.FileFlagOpenReparsePoint,
                    out handle);

                if (openResult.Succeeded)
                {
                    using (handle)
                    {
                        string existingSymlinkTarget;
                        int errorCode = GetReparsePointTarget(handle, possibleReparsePoint.Result, symlink, out existingSymlinkTarget);

                        if (errorCode == 0)
                        {
                            shouldCreate = !string.Equals(symlinkTarget, existingSymlinkTarget, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }

            if (shouldCreate)
            {
                FileUtilities.DeleteFile(symlink);
                Directory.CreateDirectory(Path.GetDirectoryName(symlink));
                if (!TryCreateSymbolicLink(symlink, symlinkTarget, isTargetFile: isTargetFile))
                {
                    return false;
                }

                created = true;
            }

            return true;
        }

        [DllImport("ntdll.dll", ExactSpelling = true)]
        internal static extern NtStatus NtSetInformationFile(
            SafeFileHandle fileHandle,
            out IoStatusBlock ioStatusBlock,
#pragma warning disable 0618
            [MarshalAs(UnmanagedType.AsAny)] object fileInformation,
#pragma warning restore 0618
            uint length,
            FileInformationClass fileInformationClass);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct FileLinkInformation
        {
            private readonly byte m_replaceIfExists;
            private readonly IntPtr m_rootDirectoryHandle;
            private readonly uint m_fileNameLength;

            /// <summary>
            ///     Allocates a constant-sized buffer for the FileName.  MAX_PATH for the path, 4 for the DosToNtPathPrefix.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260 + 4)]
            private readonly string m_filenameeName;

            // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
            public FileLinkInformation(string destinationPath, bool replaceIfExists)
            {
                m_filenameeName = destinationPath;
                m_fileNameLength = (uint)(2 * m_filenameeName.Length);
                m_rootDirectoryHandle = IntPtr.Zero;
                m_replaceIfExists = (byte)(replaceIfExists ? 1 : 0);
            }
        }

        /// <summary>
        ///     Enumeration of the various file information classes.
        ///     See wdm.h.
        /// </summary>
        public enum FileInformationClass
        {
            None = 0,
            FileDirectoryInformation = 1,
            FileFullDirectoryInformation, // 2
            FileBothDirectoryInformation, // 3
            FileBasicInformation, // 4
            FileStandardInformation, // 5
            FileInternalInformation, // 6
            FileEaInformation, // 7
            FileAccessInformation, // 8
            FileNameInformation, // 9
            FileRenameInformation, // 10
            FileLinkInformation, // 11
            FileNamesInformation, // 12
            FileDispositionInformation, // 13
            FilePositionInformation, // 14
            FileFullEaInformation, // 15
            FileModeInformation, // 16
            FileAlignmentInformation, // 17
            FileAllInformation, // 18
            FileAllocationInformation, // 19
            FileEndOfFileInformation, // 20
            FileAlternateNameInformation, // 21
            FileStreamInformation, // 22
            FilePipeInformation, // 23
            FilePipeLocalInformation, // 24
            FilePipeRemoteInformation, // 25
            FileMailslotQueryInformation, // 26
            FileMailslotSetInformation, // 27
            FileCompressionInformation, // 28
            FileObjectIdInformation, // 29
            FileCompletionInformation, // 30
            FileMoveClusterInformation, // 31
            FileQuotaInformation, // 32
            FileReparsePointInformation, // 33
            FileNetworkOpenInformation, // 34
            FileAttributeTagInformation, // 35
            FileTrackingInformation, // 36
            FileIdBothDirectoryInformation, // 37
            FileIdFullDirectoryInformation, // 38
            FileValidDataLengthInformation, // 39
            FileShortNameInformation, // 40
            FileIoCompletionNotificationInformation, // 41
            FileIoStatusBlockRangeInformation, // 42
            FileIoPriorityHintInformation, // 43
            FileSfioReserveInformation, // 44
            FileSfioVolumeInformation, // 45
            FileHardLinkInformation, // 46
            FileProcessIdsUsingFileInformation, // 47
            FileNormalizedNameInformation, // 48
            FileNetworkPhysicalNameInformation, // 49
            FileIdGlobalTxDirectoryInformation, // 50
            FileIsRemoteDeviceInformation, // 51
            FileAttributeCacheInformation, // 52
            FileNumaNodeInformation, // 53
            FileStandardLinkInformation, // 54
            FileRemoteProtocolInformation, // 55
            FileMaximumInformation,
        }
    }
}
