namespace ConnectFour.Messaging.Packets
{
    /// <summary>
    /// A base class that defines a PacketDecoder, which holds the output type
    /// that the packet decoder is expected to decode into.
    /// </summary>
    public class PacketDecoder
    {
        /// <summary>
        /// The output type that the packet decoder will decode into.
        /// </summary>
        public Type outputType;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketDecoder"/> class
        /// with a specific output type.
        /// </summary>
        /// <param name="outputType">The type that the decoded packet will represent.</param>
        public PacketDecoder(Type outputType)
        {
            this.outputType = outputType;
        }
    }

    /// <summary>
    /// A generic subclass of <see cref="PacketDecoder"/> that allows a specific type of output
    /// to be decoded from the packet data.
    /// </summary>
    /// <typeparam name="O">The type that the packet decoder will decode to.</typeparam>
    public class PacketDecoder<O> : PacketDecoder
    {
        /// <summary>
        /// A delegate that defines how the packet data should be decoded into an instance of type <typeparamref name="O"/>.
        /// </summary>
        /// <param name="t">The type to decode the data into (for flexibility in decoding).</param>
        /// <param name="data">The raw byte array representing the packet data.</param>
        /// <returns>An instance of type <typeparamref name="O"/> decoded from the packet data, or null if decoding fails.</returns>
        public delegate O? Decoder(Type t, byte[] data);

        /// <summary>
        /// The decoding function that can be assigned to decode packet data into an instance of type <typeparamref name="O"/>.
        /// </summary>
        public Decoder? Decode;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketDecoder{O}"/> class with a specified output type.
        /// </summary>
        public PacketDecoder() : base(typeof(O))
        {
        }
    }
}
