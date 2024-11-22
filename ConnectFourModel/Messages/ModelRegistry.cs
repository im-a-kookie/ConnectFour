using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Messages
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
        public bool SendMessage(Message message)
        {
            var k = message.Destination.GetRaw();
            if(models.TryGetValue(k, out var m))
            {
                m.ReceiveMessage(message);
                return true;
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
        public bool SendSignal(string signal, Identifier? destination = null, Model? sender = null)
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
        public bool SendSignal(string signal, string flag, Identifier? destination = null, Model? sender = null)
        {
            return SendMessage(
                packet: Parent.Router.PackSignal(signal, flag),
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
        public bool SendSignal(string signal, int flag, Identifier? destination = null, Model? sender = null)
        {
            return SendMessage(
                packet: Parent.Router.PackSignal(signal, flag),
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
        public bool SendSignal(string signal, byte[] flag, Identifier? destination = null, Model? sender = null)
        {
            return SendMessage(
                packet: Parent.Router.PackSignal(signal, flag),
                destination: destination,
                sender: sender);
        }


        public bool SendData<T>(T data, Identifier? destination = null, Model? sender = null) where T: notnull
        {
            try
            {
               return SendMessage(
                   packet:Parent.Router.PackContent<T>(data), 
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
        /// Attempts to send a message within the system. Sends the message to the host context
        /// if no destination is specified
        /// </summary>
        /// <param name="sender">The message sender. Defaults to the host if null</param>
        /// <param name="destination">The destination. Defaults to the host if null</param>
        /// <param name="message">The body of the message</param>
        /// <returns>True if the destination was valid, otherwise false</returns>
        public bool SendMessage(Content packet, Identifier? destination = null, Model? sender = null)
        {
            //default 
            if (destination == null) destination = Parent.Instance!.ID;
            if (sender == null) sender = Parent.Instance!;
            return SendMessage(new Message(Parent.Router, sender, destination, packet));
        }

    }
}
