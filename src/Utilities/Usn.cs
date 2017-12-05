using System;
using System.Globalization;

namespace Microsoft.Build.Utilities.FileSystem
{
    /// <summary>
    /// Cursor into a filesystem change journal.
    /// </summary>
    public struct Usn : IEquatable<Usn>, IComparable<Usn>
    {
        /// <summary>
        /// Journal offset. These values are totally ordered within a volume.
        /// </summary>
        public readonly ulong Value;

        /// <summary>
        /// Zero USN.
        /// </summary>
        public static readonly Usn Zero = new Usn(0);

        /// <nodoc />
        public Usn(ulong value)
        {
            Value = value;
        }

        /// <summary>
        /// Indicates if this is the lowest representable USN (0) == <c>default(Usn)</c>.
        /// The zero USN is special in that all files claim that USN if the volume's journal is disabled
        /// (or if they have not been modified since the journal being enabled).
        /// </summary>
        public bool IsZero => Value == 0;

        /// <nodoc />
        public bool Equals(Usn other)
        {
            return Value == other.Value;
        }

        /// <nodoc />
        public override bool Equals(object obj)
        {
            return StructUtilities.Equals(this, obj);
        }

        /// <nodoc />
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <nodoc />
        public static bool operator ==(Usn left, Usn right)
        {
            return left.Equals(right);
        }

        /// <nodoc />
        public static bool operator !=(Usn left, Usn right)
        {
            return !left.Equals(right);
        }

        /// <nodoc />
        public static bool operator <(Usn left, Usn right)
        {
            return left.Value < right.Value;
        }

        /// <nodoc />
        public static bool operator >(Usn left, Usn right)
        {
            return left.Value > right.Value;
        }

        /// <nodoc />
        public static bool operator <=(Usn left, Usn right)
        {
            return left.Value <= right.Value;
        }

        /// <nodoc />
        public static bool operator >=(Usn left, Usn right)
        {
            return left.Value >= right.Value;
        }

        /// <nodoc />
        public int CompareTo(Usn other)
        {
            return Value.CompareTo(other.Value);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{{ USN {0:x} }}", Value);
        }
    }
}
