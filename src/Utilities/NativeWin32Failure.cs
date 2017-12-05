using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Utilities.FileSystem
{
    public sealed class NativeWin32Failure : Failure
    {
        /// <summary>
        /// Native error code as returned from <c>GetLastError</c>
        /// </summary>
        public int NativeErrorCode { get; }

        /// <summary>
        /// Message.
        /// </summary>
        public string Message { get; }

        /// <nodoc />
        public NativeWin32Failure(int nativeErrorCode)
            : this(nativeErrorCode, null)
        {
        }

        /// <nodoc />
        public NativeWin32Failure(int nativeErrorCode, string message)
        {
            NativeErrorCode = nativeErrorCode;
            Message = message;
        }

        /// <summary>
        /// Creates a failure from <see cref="NativeWin32Exception"/>.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters")]
        public static NativeWin32Failure CreateFromException(NativeWin32Exception exception)
        {
            return new NativeWin32Failure(exception.NativeErrorCode, exception.Message);
        }

        /// <inheritdoc />
        public override string Describe()
        {
            return NativeWin32Exception.GetFormattedMessageForNativeErrorCode(NativeErrorCode, messagePrefix: Message);
        }

        /// <inheritdoc />
        public override DominoException CreateException()
        {
            return new DominoException(DescribeIncludingInnerFailures());
        }

        /// <inheritdoc />
        public override DominoException Throw()
        {
            throw CreateException();
        }
    }
}
