using ConnectFour.Exceptions;
using ConnectFour.Messaging.Packets;
using System.Text;
using System.Text.Json;
using ConnectFour.Configuration;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ConnectFour.Messaging
{
    public class Router
    {

        public delegate void SignalProcessor(Router router, Model? m, Signal s);

        public delegate void TypedSignalProcessor<T>(Router router, Model? m, Signal s, T? data);



        private Lock _initializationLock = new();

        /// <summary>
        /// A list of message Ids registered here
        /// </summary>
        private List<ushort> _ids = [];
        /// <summary>
        /// A list of message string names registered here
        /// </summary>
        private List<string> _names = [];

        /// <summary>
        /// List of providers that respond to signals
        /// </summary>
        private List<Delegate?> _signalHandlers = [];

        /// <summary>
        /// A mapping of string name to message index/ID
        /// </summary>
        private Dictionary<string, int> _nameIndexMap = [];


        public bool HasGenericEncoder { get; private set; } = false;
        public bool HasGenericDecoder { get; private set; } = false;
        public bool UsesDefaultControlSignals { get; private set; } = false;


        private Dictionary<Type, int> _decoderIndexMap = [];
        private List<PacketDecoder> _decoders = [];

        private Dictionary<Type, int> _encoderIndexMap = [];
        private List<PacketEncoder> _encoders = [];

        /// <summary>
        /// Bit flag indicating header contains custom typed data
        /// </summary>
        public const int TYPEFLAG = 0x8000;

        /// <summary>
        /// Mask for stripping <see cref="TYPEFLAG"/>
        /// </summary>
        public const int TYPEMASK = 0x7FFF;

        private bool _built = false;

        /// <summary>
        /// Locks the router from modification after this point
        /// </summary>
        public void BuildRouter()
        {
            lock(this)
            {
                _built = true;
            }
        }

        /// <summary>
        /// Creates a new router
        /// </summary>
        /// <param name="applyDefaultSignals">Whether to install default signals (exit, suspend)</param>
        /// <param name="applyDefaultTypes">Whether to install default type interpreters</param>
        public Router(bool applyDefaultSignals = true, bool applyDefaultTypes = true)
        {

            if(applyDefaultSignals) RegisterDefaultSignals();
            if (applyDefaultTypes) RegisterDefaultInterpreters();
            
        }

        /// <summary>
        /// Gets a modification scope that (1) synchronizes modification accesses, and (2) ensures that the
        /// router cannot be modified at invalid times (e.g after it's already running)
        /// </summary>
        /// <exception cref="RoutingException">Indicates that the router is already constructed</exception>
        internal Lock.Scope _GetModificationScope()
        {
            var s = _initializationLock.EnterScope();
            try
            {
                if (_built) throw RoutingException.RouterAlreadyBuilt(this);
                return s;
            }
            catch
            {
                s.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets a modification scope that (1) synchronizes modification accesses, and (2) ensures that the
        /// router cannot be modified at invalid times (e.g after it's already running)
        /// </summary>
        internal Lock.Scope ModificationScope => _GetModificationScope();

         public void RegisterDefaultSignals()
        {
            using (ModificationScope)
            {
                if (UsesDefaultControlSignals) return;

                //register the null message signal
                RegisterSignal("_null", (router, model, signal) => { });

                //register basic control signals
                RegisterSignal("exit", (router, model, signal) =>
                {
                    model.Host?.Kill();
                });

                RegisterSignal("suspend", (router, model, signal) =>
                {
                    model.Host?.Pause();
                });

                UsesDefaultControlSignals = true;

            }
        }


        /// <summary>
        /// Registers default interpreters.
        /// <list type="bullet">
        /// <item>Byte arrays of type <see cref="byte[]"/></item>
        /// <item>Any object compatible with <see cref="JsonSerializer"/></item>
        /// </list>
        /// </summary>
        public void RegisterDefaultInterpreters()
        {
            using (ModificationScope)
            { 
                if (HasGenericDecoder) return;
                if (HasGenericEncoder) return;

                HasGenericEncoder = true;
                HasGenericDecoder = true;

                if(!_encoderIndexMap.ContainsKey(typeof(object)))
                {
                    RegisterTypePackers<object>(
                        (object data) => JsonSerializer.SerializeToUtf8Bytes(data),
                        (Type t, byte[] data) => JsonSerializer.Deserialize(data, t));
                }
                //go through a list of all the basic types and things
                if (!_encoderIndexMap.ContainsKey(typeof(string)))
                    RegisterTypePackers( Encoding.UTF8.GetBytes, Encoding.UTF8.GetString );
                
                if (!_encoderIndexMap.ContainsKey(typeof(float))) 
                    RegisterTypePackers(BitConverter.GetBytes, BitConverter.ToSingle);

                if (!_encoderIndexMap.ContainsKey(typeof(double)))
                    RegisterTypePackers(BitConverter.GetBytes, BitConverter.ToDouble);

                if (!_encoderIndexMap.ContainsKey(typeof(short))) 
                    RegisterTypePackers( BitConverter.GetBytes, BitConverter.ToInt16);

                if (!_encoderIndexMap.ContainsKey(typeof(int)))
                    RegisterTypePackers(BitConverter.GetBytes, BitConverter.ToInt32);

                if (!_encoderIndexMap.ContainsKey(typeof(long)))
                    RegisterTypePackers(BitConverter.GetBytes, BitConverter.ToInt64);

                if (!_encoderIndexMap.ContainsKey(typeof(Int128)))
                    RegisterTypePackers(BitConverter.GetBytes, BitConverter.ToInt128);


                //also provide options to decode memory streams
                if (!_encoderIndexMap.ContainsKey(typeof(MemoryStream)))
                {
                    RegisterTypeDecoder((Type t, byte[] data) =>
                    {
                        return new MemoryStream(data);
                    });
                }

                //Now we can encode things like filestreams
                if(!_encoderIndexMap.ContainsKey(typeof(FileStream)))
                {
                    RegisterTypeEncoder<FileStream, MemoryStream>((FileStream data) =>
                    {
                        byte[] b = new byte[data.Length - data.Position];
                        data.ReadExactly(b);
                        return b;
                    });
                }
            }
        }


        /// <summary>
        /// Registers the given control signal
        /// </summary>
        /// <param name="name"></param>
        /// <param name="handler"></param>
        public void RegisterSignal(string name, SignalProcessor handler)
        {
            //generally, the construction of the internal tables is not threadsafe for RW,
            //and is only threadsafe for reads, but the initialization is relatively inexpensive
            //so it's okay to do this tbh
            using (ModificationScope)
            {
                int index = _ids.Count;
                if (index > ApplicationConstants.MaximumRegisteredSignals) throw new Exception("Message registry full!");

                if (_nameIndexMap.ContainsKey(name))
                    throw RoutingException.SignalAlreadyExists(this, name);

                //now add the things to the various list things
                _ids.Add((ushort)index);
                _names.Add(name);
                _nameIndexMap.Add(name, index);
                _signalHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Registers the given control signal
        /// </summary>
        /// <param name="name"></param>
        /// <param name="handler"></param>
        public void RegisterSignal<T>(string name, TypedSignalProcessor<T> handler)
        {
            //generally, the construction of the internal tables is not threadsafe for RW,
            //and is only threadsafe for reads, but the initialization is relatively inexpensive
            //so it's okay to do this tbh
            using (ModificationScope)
            {
                int index = _ids.Count;
                if (index > ApplicationConstants.MaximumRegisteredSignals) throw new Exception("Message registry full!");

                if (_nameIndexMap.ContainsKey(name))
                    throw RoutingException.SignalAlreadyExists(this, name);

                //now add the things to the various list things
                _ids.Add((ushort)index);
                _names.Add(name);
                _nameIndexMap.Add(name, index);
                _signalHandlers.Add(handler);
            }
        }


        /// <summary>
        /// Builds the content for a signal
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Content<T>? BuildSignalContent<T>(string signal, T? data = default)
        {
            //1. Get the signal
            if (_nameIndexMap.TryGetValue(signal, out var index))
            {
                if (data == null)
                {
                    return null;
                }
                else
                {
                    Type baseType = typeof(Content<>);
                    var type = baseType.MakeGenericType(typeof(T));
                    Content result = (Content)Activator.CreateInstance(type, data)!;
                    result.header = (ushort)index;
                    return result as Content<T>;
                }
            }
            //bonk
            else throw RoutingException.UnknownSignal(this, signal);
        }


        /// <summary>
        /// Builds the content for a signal
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Content BuildSignalContent(string signal, object? data = null)
        {
            return BuildSignalContent<object>(signal, data);
        }


        /// <summary>
        /// Gets the receiver delegate for the given message data
        /// </summary>
        /// <param name="content">The message content</param>
        /// <returns>An object decoding of the content packet, or null if no processor associated</returns>
        public Delegate? GetSignalProcessor(Content content)
        {
            //check the header. If the MSB is set, then it means this is a typed data
            //if we strip this, we can get the actual signal identifier easily
            int n = content.header & TYPEMASK;
            if (n < 0 || n >= _signalHandlers.Count) return null;
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
        /// Dynamically invokes the given callback delegate as either <see cref="SignalProcessor"/> or <see cref="TypedSignalProcessor{T}"/>
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="m"></param>
        /// <param name="data"></param>
        public void InvokeProcessorDynamic(Delegate callback, Signal m, object? data)
        {
            //now we can do a thing
            if (callback is SignalProcessor sp)
            {
                sp(this, m.Destination, m);
                m.Handled = true;
            }
            else
            {
                var gt = callback!.GetType().GetGenericTypeDefinition();
                if (gt == typeof(TypedSignalProcessor<>))
                {
                    var callbackType = callback!.GetType().GetGenericArguments()[0];
                    if (callbackType.IsAssignableFrom(data!.GetType()))
                        callback.DynamicInvoke(this, m.Destination, m, data);
                    m.Handled = true;
                }
            }
        }



        /// <summary>
        /// Gets the string name of the header for this content
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public string GetHeaderName(Content c)
        {
            int h = c.header & TYPEMASK;
            if (h < 0 || h >= _names.Count) return "";
            return _names[h];
        }

        /// <summary>
        /// Register an encoder-decoder pair that packs an instance of <typeparamref name="I"/>
        /// and unpacks it as an instance of <typeparamref name="O"/>
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="O"></typeparam>
        /// <param name="encoder"></param>
        /// <param name="decoder"></param>
        public void RegisterTypePackers<I, O>(PacketEncoder<I, O>.Encoder encoder, PacketDecoder<O>.Decoder decoder)
        {
            using(ModificationScope)
            {
                RegisterTypeEncoder(encoder);
                RegisterTypeDecoder(decoder);
            }
        }

        /// <summary>
        /// Registers an encoder that maps the given input type to the given output type
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="O"></typeparam>
        /// <param name="encoder"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void RegisterTypeEncoder<I, O>(PacketEncoder<I, O>.Encoder encoder)
        {
            using (ModificationScope)
            { 
                //register the encoder
                if (!_encoderIndexMap.ContainsKey(typeof(I)))
                {
                    int index = _encoders.Count;
                    _encoderIndexMap.Add(typeof(I), index);
                    _encoders.Add(new PacketEncoder<I, O>() { Encode = encoder });
                }
                else throw new InvalidOperationException($"Encoder already registered for {typeof(I)}");
            }
        }

        /// <summary>
        /// Registers an encoder that maps the given input type to itself as the intended output type
        /// </summary>
        /// <typeparam name="I"></typeparam>
        /// <typeparam name="O"></typeparam>
        /// <param name="encoder"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void RegisterTypeEncoder<T>(PacketEncoder<T, T>.Encoder encoder)
        {
            RegisterTypeEncoder<T, T>(encoder);
        }

        /// <summary>
        /// Register a decoder that decodes data into the given output type
        /// </summary>
        /// <typeparam name="O"></typeparam>
        /// <param name="decoder"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void RegisterTypeDecoder<O>(PacketDecoder<O>.Decoder decoder)
        {
            using (ModificationScope)
            {
                if (!_decoderIndexMap.ContainsKey(typeof(O)))
                {
                    int index = _decoders.Count;
                    _decoderIndexMap.Add(typeof(O), index);
                    _decoders.Add(new PacketDecoder<O>() { Decode = decoder });
                }
                else throw new InvalidOperationException($"Decoder already registered for {typeof(O)}");
            }
        }


        /// <summary>
        /// Registers an encoder/decoder that packs the given type into content objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="encoder"></param>
        /// <param name="decoder"></param>
        public void RegisterTypePackers<T>(PacketEncoder<T, T>.Encoder encoder, PacketDecoder<T>.Decoder decoder)
        {
            RegisterTypePackers<T, T>(encoder, decoder);
        }

        /// <summary>
        /// Register a type packer that does not require type to be specified in the decoder callback
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="encoder"></param>
        /// <param name="decoder"></param>
        public void RegisterTypePackers<T>(PacketEncoder<T,T>.Encoder encoder, ByteDecoder<T> decoder)
        {
            RegisterTypePackers<T, T>(encoder, (Type t, byte[] data) => (T)decoder(data));
        }
        public delegate T ByteDecoder<T>(ReadOnlySpan<byte> data);

        /// <summary>
        /// Packs the data of a given content into a PackedData object, which can consequently be serialized
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns>A new content object with the PackedData inside of it</returns>
        /// <exception cref="PackingException"/>
        public Content<PackedData>? PackContent<T>(Content<T> content)
        {
            //no need to pack empty content
            var data = content.GetData();
            if (data == null) return null;

            Type[] types = [data.GetType(), typeof(T), typeof(object)];

            //check each type option and get the translator
            foreach (var t in types)
            {
                if (_encoderIndexMap.TryGetValue(t, out var index))
                {
                    return EncodeByIndex(index, t, content);
                }
            }

            //bonk
            throw PackingException.NoEncoder(data.GetType());

        }

        /// <summary>
        /// Decodes the given content using the encoder with the given index/id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="index"></param>
        /// <param name="t"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        internal Content<PackedData>? EncodeByIndex<T>(int index, Type t, Content<T> content)
        {
            //now let's assess
            //validate the index and content provider
            if (index < 0 || index >= _encoders.Count)
                throw PackingException.NoEncoder(t);

            PacketEncoder encoder = _encoders[index];
            if (encoder == null)
                throw PackingException.NoEncoder(t);

            //build the container
            PackedData pd = new PackedData();
            try
            {
                //invoke the delegate
                var d = _GetEncoderDelegate(encoder, t);
                try
                {
                    pd.objectData = (byte[])d.DynamicInvoke(content.GetData())!;
                    //it needs to store the *output* type from the encoder
                    pd.objectType = encoder.outputType!;

                    //If we have mapped the output type already, then we can just insta-get the decoder later
                    //otherwise we should indicate that the full type name is needed for reconstruction
                    if (_decoderIndexMap.TryGetValue(pd.objectType, out var ind)) pd.typeHeader = (short)ind;
                    else pd.typeHeader = -1;

                    //flag it as generic if necessary
                    //but we still need the actual output type
                    if (encoder.outputType == typeof(object) && t == typeof(object)) pd.flags = PackedData.Flags.GENERIC;

                    //now box it all up and send it back
                    Content<PackedData> result = new Content<PackedData>(pd);
                    result.header = (ushort)(content.header | TYPEFLAG); //keep the header, typeflag it
                    return result;
                }
                catch (Exception ex)
                {
                    throw PackingException.EncoderCallbackError(encoder, t, ex);
                }

            }
            catch (Exception ex)
            {
                throw PackingException.InvalidEncoder(encoder, t, ex);
            }
        }
        /// <summary>
        /// Gets the encoder "Encode" delegate method from the given encoder, genericized to the type provided in <paramref name="actualInput"/>
        /// </summary>
        /// <param name="encoder"></param>
        /// <param name="actualInput"></param>
        /// <returns></returns>
        internal Delegate _GetEncoderDelegate(PacketEncoder encoder, Type actualInput)
        {
            //The encoder types are set at compile time. All hail reflection - it's slow and messy but fixes literally everything.
            Type messageProviderType = typeof(PacketEncoder<,>).MakeGenericType(encoder.inputType, encoder.outputType);
            var messageProvider = Convert.ChangeType(encoder, messageProviderType);

            //Get the Encode method delegate
            var encodeMethod = messageProviderType.GetField("Encode")?.GetValue(messageProvider);

            //Ensure that the message provider and encode method are valid
            if (messageProvider == null || encodeMethod == null)
            {
                throw PackingException.InvalidEncoder(encoder, actualInput);
            }

            return (Delegate)encodeMethod;
        }


        /// <summary>
        /// Unpacks an object from the given packed data.
        /// </summary>
        /// <param name="content">The packed content to unpack.</param>
        /// <returns>Null if no object is found or if the data is invalid.</returns>
        /// <exception cref="FormatException">Thrown if the type decoder is incorrectly registered.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the unpacker is null or invalid.</exception>
        public object? UnpackContent(Content<PackedData> content)
        {
            var data = content.GetData();
            if (content.datatype == null || data == null) return null;

            //Check if header indicates valid data
            if ((content.header & TYPEMASK) == 0) return null;

            PackedData pd = (PackedData)data;

            //Ensure the object data is present and valid
            if (pd.objectData == null || pd.objectData.Length == 0 || pd.objectType == null) return null;

            //If the type is a byte array, return the raw data directly
            if (pd.objectType == typeof(byte[])) return pd.objectData;

            //Prepare types to check for decoder
            List<Type> types = new() { pd.objectType };
            if (HasGenericDecoder && pd.flags.HasFlag(PackedData.Flags.GENERIC)) types.Add(typeof(object));

            //If a valid type header exists, decode by index
            if (pd.typeHeader >= 0) return _DecodeByIndex(pd, pd.typeHeader);

            //Attempt to decode by type
            foreach (var type in types)
            {
                if (_decoderIndexMap.TryGetValue(pd.objectType, out var index))
                {
                    return _DecodeByIndex(pd, index);
                }
            }

            // No decoder found
            throw new ArgumentNullException($"No suitable decoder found for type: {pd.objectType}");
        }

        /// <summary>
        /// Internal method that decodes packed data using the decoder at the specified index.
        /// </summary>
        /// <param name="pd">The packed data to decode.</param>
        /// <param name="index">The index of the decoder.</param>
        /// <returns>The decoded object.</returns>
        /// <exception cref="FormatException">Thrown if the type registration is invalid or decoder is missing.</exception>
        internal object? _DecodeByIndex(PackedData pd, int index)
        {
            if (index < 0 || index >= _decoders.Count)
                throw new FormatException($"Type Registration Error: {pd.objectType!.FullName}");

            PacketDecoder decoder = _decoders[index];
            if (decoder == null)
                throw new ArgumentNullException($"Expected decoder missing for type: {pd.objectType}");

            try
            {
                //Decode using the delegate obtained from the decoder
                var result = _GetDecoderDelegate(decoder)?.DynamicInvoke(decoder.outputType, pd.objectData);
                return result;
            }
            catch (Exception ex)
            {
                //Catch and throw a FormatException if reflection fails
                throw new FormatException($"Invalid decoder for type: {decoder.outputType}", ex);
            }
        }

        /// <summary>
        /// Retrieves the decoder delegate for the specified decoder.
        /// </summary>
        /// <param name="decoder">The decoder to get the delegate from.</param>
        /// <returns>The delegate for the decoder.</returns>
        internal Delegate? _GetDecoderDelegate(PacketDecoder decoder)
        {
            Type decoderType = typeof(PacketDecoder<>).MakeGenericType(decoder.outputType);
            var typedDecoder = Convert.ChangeType(decoder, decoderType);
            return decoderType.GetField("Decode")?.GetValue(typedDecoder) as Delegate;
        }


        /// <summary>
        /// Unsafe getter that accesses the decoder at a given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal PacketDecoder _UnsafeGetFromIndex(int index)
        {
            return _decoders[index];
        }


        /// <summary>
        /// Unpacks the given content into an object of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="FormatException">Thrown if the type decoder is incorrectly registered</exception>
        /// <exception cref="NullReferenceException">Thrown if the unpacker is null</exception>
        public T? UnpackContent<T>(Content<PackedData> content)
        {
            var result = UnpackContent(content);
            if (result is T t) return t;
            return default;
        }


    }
}
