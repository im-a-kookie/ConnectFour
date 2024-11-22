using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour
{
    /// <summary>
    /// An identifier object with size consistency and automatic handling of padding
    /// </summary>
    public class Identifier
    {
        /// <summary>
        /// Converts a ulong into a 42 bit hash for 7 characters of base64
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static ulong To42BitHash(ulong value)
        {
            // Mix the bits to randomize them
            value ^= (value >> 33);      // XOR fold high bits down
            value *= 0x9E3779B97F4A7C15; // Multiply by a large odd constant (64-bit Golden Ratio)
            value ^= (value >> 29);      // More XOR folding
            value *= 0xBF58476D1CE4E5B9; // Multiply by another large odd constant
            value ^= (value >> 32);      // Final XOR fold

            // Mask to 42 bits
            return value & 0x3FFFFFFFFFF;
        }

        /// <summary>
        /// The number of allocated IDs
        /// </summary>
        private static ulong _allocated = 0;

        /// <summary>
        /// The internal ID value for this identifier
        /// </summary>
        private ulong ID;

        /// <summary>
        /// Generates a new Unique IQ for the current instance of the model
        /// </summary>
        public Identifier() 
            : this($"_{
                To42BitHash(
                  Interlocked.Increment(ref _allocated))
                }") 
        { }

        /// <summary>
        /// Generates a new ID from the given input string
        /// </summary>
        /// <param name="s"></param>
        public Identifier(string s)
        {
            s = s.PadRight(8, ' ').Substring(8);
            var b = Encoding.ASCII.GetBytes(s);
            ID = BitConverter.ToUInt64(b);
        }

        /// <summary>
        /// Generates a new ID from the given byte data.
        /// </summary>
        /// <param name="b"></param>
        /// <exception cref="Exception"></exception>
        public Identifier(byte[] b)
        {
            if (b.Length != 8) throw new Exception($"Invalid ID length {b.Length}, expect: {sizeof(ulong)}");
            ID = BitConverter.ToUInt64(b);
        }

        /// <summary>
        /// Returns this ID as a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            return Encoding.ASCII.GetString(BitConverter.GetBytes(ID)).TrimEnd();
        }

        /// <summary>
        /// Returns a copy of this ID as an array of bytes
        /// </summary>
        /// <returns></returns>
        public byte[] AsBytes()
        {
            return BitConverter.GetBytes(ID);
        }

        /// <summary>
        /// Gets the ID as a raw ulong
        /// </summary>
        /// <returns></returns>
        public ulong GetRaw()
        {
            return ID;
        }

        public override bool Equals(object? obj)
        {
            return ID.Equals(obj);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public override string ToString()
        {
            return AsString();
        }


    }
}
