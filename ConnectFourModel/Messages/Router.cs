using ConnectFour.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messages
{
    public class Router
    {

        public delegate void SignalProcessor(Model receiver, ushort id, object? ext = null);

        

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
        private Dictionary<string, int> _nameIndexMap = [];

        /// <summary>
        /// A mapping of allowed types to type providers
        /// </summary>
        private Dictionary<Type, int> _typeProviderMap = [];

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

        private bool built = false;

        /// <summary>
        /// Locks the router from modification after this point
        /// </summary>
        public void BuildRouter()
        {
            lock(this)
            {
                built = true;
            }
        }

        public Router()
        {
            RegisterControlSignal("exit", (i, id, ext) => {
                i.Host?.Kill();
            });

            RegisterControlSignal("suspend", (i, id, ext) => {
                i.Host?.Pause();
            });

        }

        /// <summary>
        /// Registers the given control signal
        /// </summary>
        /// <param name="name"></param>
        /// <param name="handler"></param>
        public void RegisterControlSignal(string name, SignalProcessor handler)
        {
            //generally, the construction of the internal tables is not threadsafe for RW,
            //and is only threadsafe for reads, but the initialization is relatively inexpensive
            //so it's okay to do this tbh
            lock(this)
            {
                if (built) throw new Exception("Cannot register after router finalization");

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
            //lookup the signal name from the registry
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
            var obj = GetPacketObject(new Content());
            throw new Exception("Signal not recognized!");
        }

        public object? GetPacketObject(Content content) => GetPacketObject<object?>(content);

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns></returns>
        public T? GetPacketObject<T>(Content content)
        {
            int n = content.header & TYPEMASK;
            if (n != 0)
            {
                return UnpackContent<T>(content);
            }
            else
            {
                //flip the signal mask and erase all other bits
                n &= ~SIGNALMASK;
                switch(n)
                {
                    case STRINGMASK:
                        if (typeof(T).IsAssignableTo(typeof(string))) return (T)(object)Encoding.UTF8.GetString(content.body);
                        break;
                    case BYTEMASK:
                        if (typeof(T).IsAssignableTo(typeof(byte[]))) return (T)(object)content.body;
                        break;
                    case INTMASK:
                        if (typeof(T).IsAssignableTo(typeof(int))) return (T)(object)BitConverter.ToInt32(content.body);
                        break;
                }
            }
            return default;
        }

        /// <summary>
        /// Gets the receiver delegate for the given message data
        /// </summary>
        /// <param name="content">The message content</param>
        /// <returns>An object decoding of the content packet, or null if no processor associated</returns>
        public SignalProcessor? GetSignalProcessor(Content content)
        {
            //check the header. If the MSB is set, then it means this is a typed data
            //if we strip this, we can get the index of the message signal easily
            int n = content.header & TYPEMASK;
            if (n < 0 || n > _signalHandlers.Count) return null;
            try
            {
                return _signalHandlers[n];
            }
            catch
            {
                return null;
            }
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
        public void RegisterTypeInterpreter<T>(SignalProcessor? handler, ContentProvider<T>.Packer packer, ContentProvider<T>.Unpacker unpacker)
        {
            lock (this)
            {
                if (built) throw new Exception("Cannot register after router finalization");

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
        /// <returns>The object that was stored in the content, or null if no data</returns>
        /// <exception cref="FormatException">Thrown if data is present but not convertible</exception>
        public object? UnpackContent(Content content) => UnpackContent<object>(content);

        /// <summary>
        /// Unpacks the data stored in the given message
        /// </summary>
        /// <param name="content"></param>
        /// <returns>The object, or null if no data is present</returns>
        /// <exception cref="FormatException">Thrown if data is present but not convertible to the specified type</exception>
        public T? UnpackContent<T>(Content content)
        {
            //The header represents the index in the type lists
            int header = content.header;
            //the content is an untyped signal
            if (content.Data.Length <= 2 || (header & TYPEFLAG) == 0) return default(T);

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
                //make sure we actually got something useful
                if (result == null || !(result is T)) 
                    throw new FormatException($"Data does not match: {typeof(T)}");
                return (T)result;
            }
            //otherwise bonk
            throw new FormatException($"Unrecognized Header [{header}]");
        }



    }
}
