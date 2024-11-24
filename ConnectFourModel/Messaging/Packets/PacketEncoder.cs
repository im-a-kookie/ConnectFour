using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messaging.Packets
{
    public class PacketEncoder
    {
        public Type inputType;
        public Type outputType;
        public PacketEncoder(Type inputType, Type outputType)
        {
            this.inputType = inputType;
            this.outputType = outputType;
        }

    }

    public class PacketEncoder<I, O> : PacketEncoder
    {
        public delegate byte[] Encoder(I data);
        /// <summary>
        /// the encoder
        /// </summary>
        public Encoder? Encode;
        public PacketEncoder() : base(typeof(I), typeof(O))
        {
        }
    }


}

