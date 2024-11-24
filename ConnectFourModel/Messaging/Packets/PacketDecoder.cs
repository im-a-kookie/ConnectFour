using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging.Packets
{
    public class PacketDecoder
    {
        public Type outputType;
        public PacketDecoder(Type outputType)
        {
            this.outputType = outputType;
        }
    }

    public class PacketDecoder<O> : PacketDecoder
    {
        public delegate O? Decoder(Type t, byte[] data);

        public Decoder? Decode;
        public PacketDecoder() : base(typeof(O))
        {
        }
    }

}
