using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging.Packets
{
    /// <summary>
    /// A class responsible for serializing and deserializing packet data, including handling different content types.
    /// </summary>
    internal class PacketSerializer
    {
        private Router _router;

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketSerializer"/> class with the provided router.
        /// </summary>
        /// <param name="router">The router used for resolving types during deserialization.</param>
        public PacketSerializer(Router router)
        {
            _router = router;
        }

        /// <summary>
        /// Helper function to handle common stream operations.
        /// </summary>
        /// <param name="writeAction">An action that performs the write operation using a <see cref="BinaryWriter"/>.</param>
        /// <returns>A byte array representing the written data.</returns>
        private byte[] _WriteToStream(Action<BinaryWriter> writeAction)
        {
            using MemoryStream ms = new();
            using BinaryWriter bw = new(ms);
            writeAction(bw);
            bw.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Serializes the provided content into a byte array.
        /// </summary>
        /// <param name="c">The content to serialize.</param>
        /// <returns>A byte array representing the serialized content, or null if the content type is unsupported.</returns>
        public byte[]? SerializeContent(Content c)
        {
            switch (c)
            {
                case Content<string> cs:
                    return _WriteToStream(bw =>
                    {
                        string s = (string?)cs.GetData() ?? throw new InvalidOperationException("String data cannot be null.");
                        bw.Write((byte)PackedData.Flags.STRING);
                        bw.Write(s);
                    });

                case Content<int> ci:
                    return _WriteToStream(bw =>
                    {
                        int n = (int?)ci.GetData() ?? throw new InvalidOperationException("Integer data cannot be null.");
                        bw.Write((byte)PackedData.Flags.INT);
                        bw.Write(n);
                    });

                case Content<byte[]> cb:
                    return _WriteToStream(bw =>
                    {
                        byte[] b = (byte[]?)cb.GetData() ?? throw new InvalidOperationException("Byte array cannot be null.");
                        bw.Write((byte)PackedData.Flags.BYTE); // Use a distinct flag for clarity
                        bw.Write(b.Length);
                        bw.Write(b);
                    });

                case Content<PackedData> cd:
                    PackedData? packedData = (PackedData?)cd.GetData();
                    return packedData?.Serialize();

                default:
                    // Return null for unsupported content types
                    return null;
            }
        }

        /// <summary>
        /// Serializes the provided packed data into a byte array.
        /// </summary>
        /// <param name="data">The packed data to serialize.</param>
        /// <returns>A byte array representing the serialized packed data.</returns>
        internal byte[] SerializePacket(PackedData data) => data.Serialize();

        /// <summary>
        /// Writes the serialized packed data to a stream, including the length of the data.
        /// </summary>
        /// <param name="data">The packed data to write.</param>
        /// <param name="s">The stream to write the data to.</param>
        public void WriteToStream(PackedData data, Stream s)
        {
            var d = SerializePacket(data);
            s.Write(BitConverter.GetBytes(d.Length)); // Write the length of the data
            s.Write(d); // Write the serialized data
        }

        /// <summary>
        /// Deserializes a packet from the given header and byte data, returning the deserialized content.
        /// </summary>
        /// <param name="header">The header for the packet.</param>
        /// <param name="data">The byte array containing the serialized data.</param>
        /// <returns>The deserialized content, or null if the data cannot be deserialized.</returns>
        public Content? DeserializePacket(ushort header, byte[] data)
        {
            using (MemoryStream ms = new())
            {
                ms.Write(data, 0, data.Length); // Write the byte data to the memory stream
                ms.Position = 0; // Reset position to the start of the stream
                using (BinaryReader br = new(ms))
                {
                    PackedData.Flags f = (PackedData.Flags)br.ReadByte(); // Read the flags to determine the data type

                    // Helper to create Content<T> from the deserialized data
                    Content<T> CreateContent<T>(T data, Type dataType, ushort header) =>
                        new Content<T>(data) { datatype = dataType, header = header };

                    // Handle different content types based on the flags
                    if (f.HasFlag(PackedData.Flags.STRING))
                    {
                        return CreateContent(br.ReadString(), typeof(string), header);
                    }
                    else if (f.HasFlag(PackedData.Flags.INT))
                    {
                        return CreateContent(br.ReadInt32(), typeof(int), header);
                    }
                    else if (f.HasFlag(PackedData.Flags.BYTE))
                    {
                        int dataLen = br.ReadInt32(); // Read the length of the byte array
                        return CreateContent(br.ReadBytes(dataLen), typeof(byte[]), header);
                    }

                    try
                    {
                        // Handle non-generic types (PackedData)
                        short typeHeader = br.ReadInt16();
                        Type? t = typeHeader < 0
                            ? Type.GetType(br.ReadString()) // Load the type from a string if the header is negative
                            : _router._UnsafeGetFromIndex(typeHeader)?.outputType;

                        if (t == null) throw new InvalidDataException("Could not resolve type!");

                        int len = br.ReadInt32(); // Read the data length
                        if (len < 0 || len > ms.Length - ms.Position) // Validate the data length
                            throw new InvalidDataException("Invalid data length.");

                        return CreateContent(new PackedData
                        {
                            flags = f,
                            typeHeader = typeHeader,
                            objectType = t,
                            objectData = br.ReadBytes(len) // Read the object data
                        }, typeof(PackedData), header);
                    }
                    catch (TypeLoadException e)
                    {
                        throw new InvalidDataException("Could not resolve type!", e);
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        throw new InvalidDataException("Type header invalid index!", e);
                    }
                }
            }
        }
    }
}