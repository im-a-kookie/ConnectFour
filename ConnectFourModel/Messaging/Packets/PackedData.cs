namespace ConnectFour.Messaging.Packets
{
    public class PackedData
    {
        [Flags]
        public enum Flags
        {
            NONE = 0,
            GENERIC = 1,
            INT = 2,
            STRING = 4,
            BYTE = 8
        }

        /// <summary>
        /// The type of this object
        /// </summary>
        public Type? objectType = typeof(object);
        /// <summary>
        /// the data for this object
        /// </summary>
        public byte[] objectData = [];

        /// <summary>
        /// An indicator describing whether this data is genercized
        /// </summary>
        public Flags flags = Flags.NONE;

        /// <summary>
        /// The integer header index for the type
        /// </summary>
        public short typeHeader;

        /// <summary>
        /// The string representation of the object type
        /// </summary>
        public string typeString => objectType?.AssemblyQualifiedName!;

        /// <summary>
        /// Serialize this packed data to a byte[]. The flag byte indicates the format.
        /// </summary>
        /// <returns></returns>
        public virtual byte[] Serialize()
        {
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
        }
    }
}
