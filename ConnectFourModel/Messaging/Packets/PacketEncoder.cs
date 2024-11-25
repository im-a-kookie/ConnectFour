using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging.Packets
{
    /// <summary>
    /// A base class for packet encoding that holds both input and output types
    /// for the encoder, indicating the types involved in encoding and decoding operations.
    /// </summary>
    public class PacketEncoder
    {
        /// <summary>
        /// The input type that the packet encoder will encode from.
        /// </summary>
        public Type inputType;

        /// <summary>
        /// The output type that the packet encoder will encode to.
        /// </summary>
        public Type outputType;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketEncoder"/> class with
        /// specific input and output types.
        /// </summary>
        /// <param name="inputType">The type to encode from.</param>
        /// <param name="outputType">The type to encode to.</param>
        public PacketEncoder(Type inputType, Type outputType)
        {
            this.inputType = inputType;
            this.outputType = outputType;
        }
    }

    /// <summary>
    /// A generic subclass of <see cref="PacketEncoder"/> that provides functionality
    /// for encoding data of type <typeparamref name="I"/> into type <typeparamref name="O"/>.
    /// </summary>
    /// <typeparam name="I">The input type to encode from.</typeparam>
    /// <typeparam name="O">The output type to encode to.</typeparam>
    public class PacketEncoder<I, O> : PacketEncoder
    {
        /// <summary>
        /// A delegate representing the encoding function, which takes an instance
        /// of type <typeparamref name="I"/> and returns a byte array representing
        /// the encoded data.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <returns>A byte array representing the encoded data.</returns>
        public delegate byte[] Encoder(I data);

        /// <summary>
        /// The encoding function that can be assigned to encode data of type <typeparamref name="I"/>
        /// into a byte array representing the encoded data.
        /// </summary>
        public Encoder? Encode;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketEncoder{I,O}"/> class with
        /// the specified input and output types for encoding.
        /// </summary>
        public PacketEncoder() : base(typeof(I), typeof(O))
        {
        }
    }
}

