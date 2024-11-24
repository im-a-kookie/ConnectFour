using ConnectFour.Messaging.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public bool SendMessage(Signal message)
        {
            return message.Destination.ReceiveMessage(message);
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
        public bool SendSignal(string signal, object? data = null, Model? destination = null, Model? sender = null)
        {

            return SendMessage(
                packet: Parent.Router.PackSignal(signal),
                destination: destination,
                sender: sender);
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
        public bool SendSignal(string signal, Identifier? destination = null, Model? sender = null, object? data = null)
        {
            Model? dest = null;
            if (destination == null) dest = Parent.Instance;
            else Parent.Models.models.TryGetValue(destination.GetRaw(), out dest);
            return SendSignal(signal, data, dest, sender);
        }

        /// <summary>
        /// Sends a signal using a typed data object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="destination"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public bool SendSignal<T>(string signal, T? data, Model? destination = null, Model? sender = null) where T: notnull
        {
            try
            {
                return SendMessage(
                        packet: Parent.Router.PackSignal<T>(signal, data),
                        destination: destination,
                        sender: sender);
            }
            catch
            {
                //this is probably fine
                return false;
            }
        }

        /// <summary>
        /// Sends signal using a typed data object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="destination"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        public bool SendSignal<T>(string signal, T? data, Identifier? destination = null, Model? sender = null) where T : notnull
        {
            Model? dest = null;
            if (destination == null) dest = Parent.Instance;
            else Parent.Models.models.TryGetValue(destination.GetRaw(), out dest);
            return SendSignal<T>(signal, data, dest, sender);
        }


        /// <summary>
        /// Attempts to send a message within the system. Sends the message to the host context
        /// if no destination is specified
        /// </summary>
        /// <param name="sender">The message sender. Defaults to the host if null</param>
        /// <param name="destination">The destination. Defaults to the host if null</param>
        /// <param name="message">The body of the message</param>
        /// <returns>True if the destination was valid, otherwise false</returns>
        public bool SendMessage(Content packet, Model? destination = null, Model? sender = null)
        {
            //default 
            if (destination == null) destination = Parent.Instance!;
            if (sender == null) sender = Parent.Instance!;
            return SendMessage(new Signal(Parent.Router, sender, destination, packet));
        }

    }
}
