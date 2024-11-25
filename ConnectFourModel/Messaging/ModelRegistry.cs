using ConnectFour.Messaging.Packets;
using System.Collections.Concurrent;

namespace ConnectFour.Messaging
{
    public class ModelRegistry
    {
        public Provider Parent;

        public ModelRegistry(Provider parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// The dictionary of all models mapped to their address
        /// </summary>
        public ConcurrentDictionary<ulong, Model> models = [];

        /// <summary>
        /// Deregisters the given messageable
        /// </summary>
        /// <param name="m"></param>
        public void Deregister(Model m)
        {
            models.TryRemove(m.ID.GetRaw(), out _);
        }

        /// <summary>
        /// Registers the given messageable
        /// </summary>
        /// <param name="m"></param>
        public void Register(Model m)
        {
            models.TryAdd(m.ID.GetRaw(), m);
        }

        /// <summary>
        /// Attempts to send a message within the system
        /// </summary>
        /// <param name="message"></param>
        /// <returns>True if the destination was valid, otherwise false</returns>
        public bool SendSignal(Signal message)
        {
            if (message.MessageBody == null) return false;
            return message.Destination!.ReceiveMessage(message);
        }


        /// <summary>
        /// Attempts to send a message within the system. Sends the message to the host context
        /// if no destination is specified
        /// </summary>
        /// <param name="sender">The message sender. Defaults to the host if null</param>
        /// <param name="destination">The destination. Defaults to the host if null</param>
        /// <param name="message">The body of the message</param>
        /// <returns>True if the destination was valid, otherwise false</returns>
        public bool SendSignal(Content packet, Model? destination = null, Model? sender = null)
        {
            //default 
            if (destination == null) destination = Parent.Instance!;
            if (sender == null) sender = Parent.Instance!;

            return SendSignal(new Signal(Parent.Router, sender, destination, packet));
        }

        /// <summary>
        /// Sends a signal to the given sender.
        /// 
        /// <para>Passes to <see cref="SendMessage(Content, Identifier?, Model?)"/></para>
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="signal">The signal</param>
        /// <param name="destination">The destination</param>
        /// <returns></returns>
        public bool SendSignal<T>(string signal, T? data = default, Model? destination = null, Model? sender = null)
        {
            var content = Parent.Router.BuildSignalContent(signal, data);

            if (content != null)
            {
                return SendSignal(
                    packet: content,
                    destination: destination,
                    sender: sender);
            }
            else
            {
                Content? gContent = Parent.Router.BuildSignalContent(signal, (object?)data);
                if (gContent != null)
                {
                    return SendSignal(
                       packet: gContent,
                       destination: destination,
                       sender: sender);
                }
            }

            return false;

        }

        /// <summary>
        /// Sends a signal to the given sender.
        /// 
        /// <para>Passes to <see cref="SendMessage(Content, Identifier?, Model?)"/></para>
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="signal">The signal</param>
        /// <param name="destination">The destination</param>
        /// <returns></returns>
        public bool SendSignal(string signal, object? data = default, Model? destination = null, Model? sender = null)
        {
            return SendSignal<object>(signal, data, destination, sender);
        }

        /// <summary>
        /// Attempts to send a message within the system. Sends the message to the host context
        /// if no destination is specified
        /// </summary>
        /// <param name="sender">The message sender. Defaults to the host if null</param>
        /// <param name="destination">The destination. Defaults to the host if null</param>
        /// <param name="message">The body of the message</param>
        /// <returns>True if the destination was valid, otherwise false</returns>
        public Task<T?> AwaitSignal<T>(Content packet, Model? destination = null, Model? sender = null)
        {
            //TPL
            return Task.Run(() =>
            {
                //default sender/destination
                if (destination == null) destination = Parent.Instance!;
                if (sender == null) sender = Parent.Instance!;

                //The blocking completor allows us to await its completion
                Signal s = new Signal(Parent.Router, sender, destination, packet);
                s.CompletionCallback = new BlockingCompleter();
                SendSignal(s);

                //so now we can wait for it to be done
                s.CompletionCallback.Await();
                //and reply correctly
                return s.CompletionCallback.GetResponse<T>();

            });
        }


        /// <summary>
        /// Awaitable signal sending
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="destination"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public Task<OUT?> AwaitSignal<IN, OUT>(string signal, IN? data, Model? destination = null, Model? sender = null) where IN : notnull
        {
            return AwaitSignal<OUT>(
                packet: Parent.Router.BuildSignalContent(signal, data),
                destination: destination,
                sender: sender);
            //send it off with an action delegate
        }


        /// <summary>
        /// Send a signal and provide an awaitable task
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="destination"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public Task<T?> AwaitSignal<T>(string signal, object? data, Model? destination = null, Model? sender = null)
        {
            return AwaitSignal<object, T>(signal, data, destination, sender);
        }

        /// <summary>
        /// Send a signal and provide an awaitable task
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="destination"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public Task AwaitSignal(string signal, object? data, Model? destination = null, Model? sender = null)
        {
            return AwaitSignal<object>(signal, data, destination, sender);
        }

    }
}
