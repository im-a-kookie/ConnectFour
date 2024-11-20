using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Model.Messages
{
    public abstract class ContentProvider {
        public readonly Type type;
        public ContentProvider(Type t)
        {
            type= t;
        }
    }

    /// <summary>
    /// Provides data packing methods for the given data type, allowing
    /// the message model to understand formats in a user-defined way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ContentProvider<T> : ContentProvider
    {
        public delegate Content Packer(T data);
        public delegate T Unpacker(Content content);

        /// <summary>
        /// The name of this provider
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The index of this provider
        /// </summary>
        public readonly int Index;

        /// <summary>
        /// The packer for this provider
        /// </summary>
        public Packer? Pack;

        /// <summary>
        /// The unpacker for this provider
        /// </summary>
        public Unpacker? Unpack;
        
        /// <summary>
        /// Create a new provider with the given properties. <para>The provider is not registered by this
        /// constructor, and should be created from the MessageRegistry instead.</para>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <param name="packer"></param>
        /// <param name="unpacker"></param>
        public ContentProvider(string name, int index, Packer packer, Unpacker unpacker) : base(typeof(T))
        { 
            Name = name;
            Index = index;
            Pack = packer;
            Unpack = unpacker;
        }
            

    }
}
