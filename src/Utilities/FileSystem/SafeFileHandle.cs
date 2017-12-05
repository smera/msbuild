extern alias mscorlib;

using mscorlib::Microsoft.Win32.SafeHandles;

namespace Microsoft.Build.Utilities.FileSystem
{
    /// <summary>
    /// Handle for a volume iteration as returned by WindowsNative.FindFirstVolumeW />
    /// </summary>
    public sealed class SafeFindFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Private constructor for the PInvoke marshaller.
        /// </summary>
        private SafeFindFileHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return WindowsNative.FindClose(handle);
        }
    }
}
