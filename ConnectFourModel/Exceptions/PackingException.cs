using ConnectFour.Messaging.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Exceptions
{
    /// <summary>
    /// An exception thrown when messages are not correctly packed. Provides expanded details about the cause of the error.
    /// </summary>
    internal class PackingException : Exception
    {
        /// <summary>  The encoder that triggered the event, or null if unknown</summary>
        public PacketEncoder? CausingEncoder { get; private set; } = null;
        /// <summary>  The decoder that triggered the event, or null if unknown</summary>
        public PacketDecoder? CausingDecoder { get; private set; } = null;
        /// <summary>  The type being packed </summary>
        public Type? CausingType { get; private set; } = null;

        public PackingException(Type type, string? message = "Generic Packing Exception", Exception? innerException = null) : base (message, innerException)
        {
            CausingType = type;
        }

        public PackingException(PacketEncoder? cause, Type type, string message = "Packet Encode Exception", Exception? innerException = null) : base(message, innerException)
        {
            CausingEncoder = cause;
            this.CausingType = type;
        }

        public PackingException(PacketDecoder? cause, Type type, string message = "Packet Decode Exception", Exception? innerException = null) : base(message, innerException)
        {
            CausingDecoder = cause;
            this.CausingType = type;
        }

        /// <summary>
        /// Creates a new exception indicating no encoder exists for <paramref name="t"/>
        /// </summary>
        public static PackingException NoEncoder(Type t, Exception? inner = null) =>
            new PackingException(
                type: t,
                message: $"No Suitable Encoder for {t}. " +
                (inner == null
                    ? "An encoder must be registered for the given type."
                    : $"An {inner.GetType().Name} was thrown!")
            );

        /// <summary>
        /// Creates a new exception indicating no decoder exists for <paramref name="t"/>
        /// </summary>
        public static PackingException NoDecoder(Type t, Exception? inner = null) =>
            new PackingException(
                type: t,
                message: $"No Suitable Decoder for {t}. " +
                (inner == null
                    ? "A decoder must be registered for the given type."
                    : $"An {inner.GetType().Name} was thrown!")
            );


        /// <summary>
        /// Creates an exception indicating that an <paramref name="encoder"/> was found but invalid for <paramref name="t"/>
        /// <param name="inner">The exception, if any, which was thrown when retrieving the encoder.</param>
        /// </summary>
        public static PackingException InvalidEncoder(PacketEncoder encoder, Type t, Exception? inner = null) =>
            new PackingException(
                cause: encoder,
                type: t,
                message: $"Encoder Invalid for {t}." +
                    (inner == null ? "" : $" {inner.GetType().Name} was thrown during encoder retrieval."),
                innerException: inner
            );

        /// <summary>
        /// Creates an exception indicating that a <paramref name="decoder"/> was found but invalid for <paramref name="t"/>
        /// <param name="inner">The exception, if any, which was thrown when retrieving the decoder.</param>
        /// </summary>
        public static PackingException InvalidDecoder(PacketDecoder decoder, Type t, Exception? inner = null) =>
            new PackingException(
                cause: decoder,
                type: t,
                message: $"Decoder Invalid for {t}." +
                    (inner == null ? "" : $" {inner.GetType().Name} was thrown during decoder retrieval."),
                innerException: inner
            );

        /// <summary>
        /// Creates an exception indicating that an <paramref name="encoder"/> callback threw an exception encoding an instance of <paramref name="t"/>
        /// </summary>
        public static PackingException EncoderCallbackError(PacketEncoder encoder, Type t, Exception inner) =>
            new PackingException(
                cause: encoder,
                type: t,
                message: $"Endcoding failed for {t}." + 
                    (inner == null ? "" : $" {inner.GetType().Name} was thrown by encoder delegate."),
                innerException: inner);

        /// <summary>
        /// Creates an exception indicating that an <paramref name="decoder"/> callback threw an exception decoding an instance of <paramref name="t"/>
        /// </summary>
        public static PackingException DecoderCallbackError(PacketEncoder decoder, Type t, Exception inner) =>
            new PackingException(
                cause: decoder,
                type: t,
                message: $"Decoding failed for {t}." + 
                    (inner == null ? "" : $" {inner.GetType().Name} was thrown by encoder delegate."),
                innerException: inner);

    }
}
