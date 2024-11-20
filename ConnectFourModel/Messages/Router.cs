using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Model.Messages
{
    public class Router
    {

        public delegate void SignalProcessor(Member receiver, ushort id, object? ext = null);

        /// <summary>
        /// A list of message Ids registered here
        /// </summary>
        private List<ushort> _ids = [0, 0];
        /// <summary>
        /// A list of message string names registered here
        /// </summary>
        private List<string> _names = ["PLACEHOLDER", "PLACEHOLDER"];

        /// <summary>
        /// List of providers that respond to signals
        /// </summary>
        private List<SignalProcessor?> _signalHandlers = [];
        /// <summary>
        /// List of providers that convert between type and Content
        /// </summary>
        private List<ContentProvider?> _contentProviders = [];

        /// <summary>
        /// A mapping of string name to message index/ID
        /// </summary>
        public Dictionary<string, int> _nameIndexMap = [];

        /// <summary>
        /// A mapping of allowed types to type providers
        /// </summary>
        public Dictionary<Type, int> _typeProviderMap = [];

        /// <summary>
        /// Bit flag indicating header contains custom typed data
        /// </summary>
        public const int TYPEFLAG = 0x8000;

        /// <summary>
        /// Mask for stripping <see cref="TYPEFLAG"/>
        /// </summary>
        const int TYPEMASK = 0x7FFF;

        /// <summary>
        /// The 14th and 15th bits indicate the type used for generic signals
        /// </summary>
        const int SIGNALMASK = 0b0110000000000000;
        const int STRINGMASK = 0b0010000000000000;
        const int INTMASK = 0b0100000000000000;
        const int BYTEMASK = 0b0110000000000000;

        public Router()
        {
            RegisterControlSignal("exit", (i, id, ext) => {
                i.Host?.Close();
            });

            RegisterControlSignal("suspend", (i, id, ext) => {
                i.Host?.Suspend();
            });

        }

        /// <summary>
        /// Registers the given control signal
        /// </summary>
        /// <param name="name"></param>
        /// <param name="handler"></param>
        public void RegisterControlSignal(string name, SignalProcessor handler)
        {
            lock(this)
            {
                int index = _ids.Count;
                if (index > 4000) throw new Exception("Message registry full!");
                _ids.Add((ushort)index);
                _names.Add(name);
                _nameIndexMap.Add(name, index);
                _signalHandlers.Add(handler);
                _contentProviders.Add(null);
            }
        }

        /// <summary>
        /// Gets an empty content object representing a signal and nothing more.
        /// </summary>
        /// <param name="signal"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Content PackSignal(string signal, object? flag = null)
        {
            if(_nameIndexMap.TryGetValue(signal, out var index))
            {
                int header = _ids[index];
                Content c = new Content();
                c.header = (ushort)header;
                //conver the data into the format
                switch(flag)
                {
                    case null: break;
                    case int i: 
                        c.body = BitConverter.GetBytes(i);
                        c.header |= INTMASK;
                        break;
                    case byte[] b:
                        c.body = b;
                        c.header |= BYTEMASK;
                        break;
                    case string s:
                        c.body = Encoding.UTF8.GetBytes(s);
                        c.header |= STRINGMASK;
                        break;
                    default: break;
                }
                return c;
            }
            throw new Exception("Signal not recognized!");
        }

        /// <summary>
        /// Gets the receiver delegate for the given message data
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public (SignalProcessor callback, object? flag)? GetSignalProcessor(Content content)
        {
            int n = content.header & TYPEMASK;
            if (n < 0 || n > _signalHandlers.Count) return null;

            //convert the content body into an object
            object? flag = null;
            int headerType = n >> 14;
            switch(headerType)
            {
                case 0: break;
                case 1: flag = Encoding.UTF8.GetString(content.body); break;
                case 2: flag = BitConverter.ToInt32(content.body, 0); break;
                case 3: flag = content.body; break;
            }

            //pack
            return (_signalHandlers[n]!, flag);

        }

        /// <summary>
        /// Gets the string name of the header for this content
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public string GetHeaderName(Content c)
        {
            int h = c.header & ~(SIGNALMASK | TYPEFLAG);
            if (h < 0 || h >= _names.Count) return "";
            return _names[h];
        }

        /// <summary>
        /// Registers a type provider that converts the given type into <see cref="Content"/> instances
        /// for sending internally.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packer"></param>
        /// <param name="unpacker"></param>
        /// <param name="handler"></param>
        public void RegisterTypeInterpreter<T>(ContentProvider<T>.Packer packer, ContentProvider<T>.Unpacker unpacker, SignalProcessor? handler)
        {
            lock(this)
            {
                string? n = typeof(T).FullName;
                if (n == null) return;

                int index = _ids.Count;
                if (index > 4000) throw new Exception("Message registry full!");

                //flag the leading bit to denote typed header
                _ids.Add((ushort)(index & TYPEFLAG));
                _names.Add(n);
                _signalHandlers.Add(handler ?? null);
                //now generate the provider
                var m = new ContentProvider<T>(n, index, packer, unpacker);
                _typeProviderMap.Add(typeof(T), index);

            }

        }

        /// <summary>
        /// Packs the given data into a content that can be sent in a message
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns>A content object that can be sent in a message</returns>
        /// <exception cref="FormatException">Indicates that the given type has no registered converter.</exception>
        public Content PackContent<T>(T data) where T : notnull
        {
            var t = data.GetType();
            if (_typeProviderMap.TryGetValue(t, out var index))
            {
                if (index < 0 || index >= _contentProviders.Count) throw new FormatException($"Type Error: {t.FullName}!");
                ContentProvider? m = _contentProviders[index];
                if (m != null)
                {
                    //now try to convert the message into the given content
                    try
                    {
                        ContentProvider<T>? messageProvider = m as ContentProvider<T>;
                        //break if invalid
                        if (messageProvider == null) throw new FormatException($"Invalid Packer: {t.FullName}!");
                        if (messageProvider.Pack == null) throw new FormatException($"Invalid Packer: {t.FullName}!");
                        Content c = messageProvider.Pack(data);
                        c.header = (ushort)(index | TYPEFLAG);
                        return c;
                    }
                    catch
                    {
                        throw new FormatException($"Invalid Packer: {t.FullName}!");
                    }
                }
            }
            //or defailt to throwing an error
            throw new FormatException($"No Provider: {t.FullName}!");
        }

        /// <summary>
        /// Unpacks the data stored in the given message
        /// </summary>
        /// <param name="content"></param>
        /// <returns>The object, or null if no data is present</returns>
        /// <exception cref="FormatException">Thrown if data is present but not convertible</exception>
        public object? UnpackContent(Content content)
        {
            //The header represents the index in the type lists
            int header = content.header;
            //the content is an untyped signal
            if (content.Data.Length <= 2 || (header & TYPEFLAG) == 0) return null;

            //now convert it back to the data value
            header &= TYPEMASK;
            if (header < 0 || header >= _ids.Count)
            {
                throw new FormatException($"Unrecognized Header: [{header}]");
            }
            //Now we can get the content provider
            ContentProvider? m = _contentProviders[header];
            if(m != null)
            {
                object? result = null;
                try
                {
                    //Mildly disgusting, since the unpacker uses a generic
                    //and we don't know the type right now
                    //We need to use reflection
                    Func<object, object>? f = (Func<object, object>?)m.GetType()?.GetField("Unpack")?.GetValue(m);
                    //this remains numm if the unpacker couldn't be found
                    result = f?.Invoke(content);
                }
                catch
                {
                    throw new FormatException("Invalid Data");
                }
                if (result == null) throw new FormatException("Invalid Data");
                return result;
            }
            //otherwise bonk
            throw new FormatException($"Unrecognized Header [{header}]");
        }



    }
}
