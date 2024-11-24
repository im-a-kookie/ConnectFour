using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging.Packets
{
    internal class PacketSerializer
    {
        Router _router;

        public PacketSerializer(Router router)
        {
            _router = router;
        }

        // Helper function to handle common stream operations
        byte[] _WriteToStream(Action<BinaryWriter> writeAction)
        {
            using MemoryStream ms = new();
            using BinaryWriter bw = new(ms);
            writeAction(bw);
            bw.Flush();
            return ms.ToArray();
        }


        public byte[]? SerializeContent(Content c)
        {
            switch(c)
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


        internal byte[] SerializePacket(PackedData data) => data.Serialize();

        public void WriteToStream(PackedData data, Stream s)
        {
            var d = SerializePacket(data);
            s.Write(BitConverter.GetBytes(d.Length));
            s.Write(SerializePacket(data));
        }


        public Content? DeserializePacket(ushort header, byte[] data)
        {
            /*
           using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write((byte)flags);
                    bw.Write(typeHeader);
                    if(typeHeader < 0) bw.Write(typeString);

                    bw.Write(objectData.Length);
                    bw.Write(objectData);
                    bw.Flush();
                    return ms.ToArray();
                }
            }
             */

            using (MemoryStream ms = new())
            {
                using (BinaryReader br = new(ms))
                {
                    PackedData.Flags f = (PackedData.Flags)br.ReadByte();


                    // Helper to create Content<T>
                    Content<T> CreateContent<T>(T data, Type dataType, ushort header) =>
                        new Content<T>(data) { datatype = dataType, header = header };


                    //it contains just a string
                    if (f.HasFlag(PackedData.Flags.STRING))
                    {
                        return CreateContent(br.ReadString(), typeof(string), header);
                    }
                    //it contains just an integer
                    else if (f.HasFlag(PackedData.Flags.INT))
                    {
                        return CreateContent(br.ReadInt32(), typeof(int), header);

                    }
                    //it contains just a byte array
                    else if (f.HasFlag(PackedData.Flags.BYTE))
                    {
                        int dataLen = br.ReadInt32();
                        return CreateContent(br.ReadBytes(dataLen), typeof(byte[]), header);
                    }

                    try
                    {

                        //Handle non generic types
                        short typeHeader = br.ReadInt16();
                        Type? t = typeHeader < 0
                            ? Type.GetType(br.ReadString())
                            : _router._UnsafeGetFromIndex(typeHeader)?.outputType;
                        if (t == null) throw new InvalidDataException("Could not resolve type!");

                        //now we just need to load the data out and make the container
                        int len = br.ReadInt32();
                        if (len < 0 || len > ms.Length - ms.Position) // Validate bounds
                            throw new InvalidDataException("Invalid data length.");

                        return CreateContent(new PackedData
                        {
                            flags = f,
                            typeHeader = typeHeader,
                            objectType = t,
                            objectData = br.ReadBytes(len)
                        }, typeof(PackedData), header);
                    }
                    //catch if the type is not correctly loaded
                    catch (TypeLoadException e)
                    {
                        throw new InvalidDataException("Could not resolve type!", e);
                    }
                    //catch if the index is out of range
                    catch(IndexOutOfRangeException e)
                    {
                        throw new InvalidDataException("Type header invalid index!", e);
                    }
                }
            }
            




        }


    }
}
