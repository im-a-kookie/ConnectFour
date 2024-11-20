using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Messages
{
    public class Content
    {
        /// <summary>
        /// An enumeration of the allowed type headers for message content
        /// </summary>
        public enum Headers
        {
            NONE,
            DATA,
            STRING,
        }

        /// <summary>
        /// The type header of the content of this message
        /// </summary>
        public ushort header;
        /// <summary>
        /// The binary data that will be packed into this message
        /// </summary>
        public byte[] body = [];

        /// <summary>
        /// The data of this message, with header included
        /// </summary>
        public byte[] Data => GetPackedBytes();


        /// <summary>
        /// Sets this content using the given string
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public Content Set(string content)
        {
            //convert the message directly into bytes
            header = (ushort)Headers.STRING;
            body = System.Text.Encoding.UTF8.GetBytes(content);
            return this;
        }

        /// <summary>
        /// Sets this content using the given byte data
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public Content Set(byte[] content)
        {
            header = (ushort)Headers.DATA;
            body = content;
            return this;
        }

        /// <summary>
        /// Receives this content from the given raw data received
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public Content Receive(byte[] data)
        {
            if (data.Length <= 2) throw new ArgumentException("Packet Length Inadequate!");
            header = (ushort)(data[0] | (data[1] << 8));
            data = new byte[data.Length - 2];
            return this;
        }

        /// <summary>
        /// Returns this packet as a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            if(header == (ushort)Headers.STRING)
            {
                var d = body.AsSpan();
                d.Slice(2, body.Length - 2);
                return System.Text.Encoding.UTF8.GetString(d);
            }
            else return Convert.ToBase64String(body);
        }


        /// <summary>
        /// Gets this content packet as a byte array
        /// </summary>
        /// <returns></returns>
        public byte[] GetPackedBytes()
        {
            //inject the header
            var b = new byte[body.Length + sizeof(ushort)];
            b[0] = (byte)(header & 0xFF);
            b[1] = (byte)((header >> 8) & 0xFF);
            //now copy the content into the thingy
            Array.Copy(body, 0, b, 2, body.Length);
            return b;
        }





    }
}
