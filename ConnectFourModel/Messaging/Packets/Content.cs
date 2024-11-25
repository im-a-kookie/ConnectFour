namespace ConnectFour.Messaging.Packets
{
    /// <summary>
    /// An underlying content type that handles data packets
    /// </summary>
    public abstract class Content
    {
        /// <summary>
        /// The header identifying the signal in this content object
        /// </summary>
        public ushort header { get; set; }

        /// <summary>
        /// The type of the data in this content object
        /// </summary>
        public Type? datatype { get; set; }

        /// <summary>
        /// Sets the data in this object.
        /// </summary>
        /// <param name="data">The data object. Null values accepted</param>
        public abstract void SetData(object? data);

        /// <summary>
        /// Gets the data from this content.
        /// </summary>
        /// <returns>The data object, or null if no data found</returns>
        public abstract object? GetData();

    }

    /// <summary>
    /// An empty content that will onlt contain a header
    /// </summary>
    public class EmptyContent : Content
    {
        public EmptyContent() { }

        public EmptyContent(ushort header) => this.header = header;

        public override object? GetData()
        {
            return null;
        }

        public override void SetData(object? data)
        {
            throw new ArgumentException("Empty content cannot contain data.");
        }
    }

    /// <summary>
    /// Content container for data of specified type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Content<T> : Content
    {
        /// <summary>
        /// The internal data object. If null or default, then the content doesn't contain data
        /// </summary>
        private T? _data;

        public Content(T data)
        {
            _data = data;
            datatype = data.GetType();
        }

        //generally content is just a container for the data object
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        /// <returns></returns>
        public override object? GetData()
        {
            return _data;
        }

        /// <summary>
        /// <inheritdoc/>.=
        /// </summary>
        /// <param name="obj"></param>
        /// <exception cref="ArgumentException"></exception>
        public override void SetData(object? obj)
        {
            if (obj == default) _data = default;
            if (obj is T t) _data = t;
            else throw new ArgumentException($"Type {obj?.GetType()} does not match container {typeof(T)}");
        }


    }

}
