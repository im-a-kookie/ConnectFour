using Microsoft.Win32;
using ConnectFour.ThreadModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.PortableExecutable;

namespace ConnectFour.Messages
{
    public abstract class Model : IDisposable
    {

        /// <summary>
        /// An enumeration of the valid message event types
        /// </summary>
        public enum EventType
        {
            RECEIVE
        }

        /// <summary>
        /// References the ModelRegistry to which this Model belongs
        /// </summary>
        public readonly Provider Parent;

        /// <summary>
        /// Signature definition for all message events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        public delegate void MessageEvent(EventType e, Message m);

        /// <summary>
        /// Signature definition for signal events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="sender"></param>
        public delegate void SignalEvent(EventType e, string signal, object? data, Model sender);

        /// <summary>
        /// Signature definition for messages received as data objects
        /// </summary>
        /// <param name="e"></param>
        /// <param name="data">The object received</param>
        /// <param name="sender"></param>
        public delegate void DataEvent(EventType e, object? data, Model? sender);

        /// <summary>
        /// Event called when this messageable instance receives a message event
        /// </summary>
        public event MessageEvent? OnReceiveMessage;

        /// <summary>
        /// Called when the thread begins processing a message
        /// </summary>
        public event MessageEvent? OnMessageProcess;

        /// <summary>
        /// Called specifically when the model receives a signal
        /// </summary>
        public event SignalEvent? OnReceiveSignal;

        /// <summary>
        /// Called when a data object is received by the model
        /// </summary>
        public event DataEvent? OnReceiveData;

        /// <summary>
        /// The ID value representing the arrival address of this messageable object
        /// </summary>
        public readonly Identifier ID;

        public ReaderWriterLockSlim QueueLock = new();

        /// <summary>
        /// the internal message queue of messages received (but not yet processed) by
        /// this messageable object. It is recommended not to access this queue
        /// except via the internal message accessors.
        /// </summary>
        private BlockingCollection<Message> Queue = [];

        /// <summary>
        /// The Thread container that will host this model's message loop
        /// </summary>
        public ModelContainer? Host;

        /// <summary>
        /// Initialize a new model
        /// </summary>
        public Model(Provider provider)
        {
            //get a new ID
            ID = new();
            this.Parent = provider;
            //We need to register ourselves in the provider
            provider.Models.Register(this);

            //then create the host and subscribe to its loop/signal
            Host = provider.ParallelScheme.ProvideHost(this);
            Host.OnLoop += Host_OnLoop;
        }

        /// <summary>
        /// Default entry point that receives the host signals. This method should generally not block.
        /// If the model wants to pause, consider <see cref="ManualResetEvent"/> with immediate timeout.
        /// </summary>
        private void Host_OnLoop(ModelContainer container)
        {
            DateTime t = DateTime.UtcNow;
            while (Queue.TryTake(out var m, -1))
            {
                if (t > m.Expiration) continue;
                Model sender = m.Sender ?? Parent.Instance!;

                //we received a message, yay

                try
                {
                    //first let's throw the event to process the message
                    OnMessageProcess?.Invoke(EventType.RECEIVE, m);
                }
                catch (Exception e)
                {
                    //bonk
                    Parent.NotifyModelException(this, e);
                }

                //It wasn't handled yet
                if (!m.Handled)
                {
                    //So let's see if there's a signal handler for this header

                    var callback = Parent.Router.GetSignalProcessor(m.MessageBody);
                    var data = Parent.Router.GetPacketObject(m.MessageBody);

                    if (callback != null)
                    {
                        //try to receive the signal first
                        try { OnReceiveSignal?.Invoke(EventType.RECEIVE, m.HeaderName, data, sender); }
                        catch (Exception e) { Parent.NotifyModelException(this, e); }

                        //we invoked the event and it didn't handle the message, so:
                        try { if (!m.Handled) callback(this, m.MessageBody.header, data); }
                        catch (Exception e) { Parent.NotifyModelException(this, e); }

                        //the message has now been handled by the signal processor
                        m.Handled = true;
                    }            

                    //The message wasn't handled yet
                    if (!m.Handled)
                    {
                        //Now let's check if the message was actually a data object
                        if (data != null)
                        {
                            try
                            {
                                //just notify that we received the data
                                OnReceiveData?.Invoke(EventType.RECEIVE, data, sender);
                            }
                            catch (Exception e) {
                                Parent.NotifyModelException(this, e);
                            }
                        }
                    }

                    //Now, notify that the message has been processed completely by this pipeline
                    //which provides an opportunity to tell another model that it's been processed
                    m.CompletionCallback?.Invoke(m);
                    
                }
            }
        }

        /// <summary>
        /// Initialize a new messageable with the given string identifier
        /// </summary>
        /// <param name="id"></param>
        public Model(Provider registry, string id)
        {
            ID = new(id);
            this.Parent = registry;
            Parent.Models.Register(this);
        }

        ~Model()
        {
            //Ensure that garbage collected models are deregistered
            Parent.Models.Deregister(this);
        }

        /// <summary>
        /// Tries to read the next message from the queue, with optional timeout
        /// </summary>
        /// <param name="msTimeout">The timeout, 0 for immediate</param>
        /// <returns>The next message, or null if no messages</returns>
        public Message? ReadNextMessage(int msTimeout = 0)
        {
            if(Queue.TryTake(out var m, msTimeout))
            {
                return m;
            }
            return null;
        }

        /// <summary>
        /// The total number of message in the message queue
        /// </summary>
        public int TotalMessages => Queue.Count;

        /// <summary>
        /// Tries to read the next message from this model with optional timeout
        /// </summary>
        /// <param name="result">The resulting message</param>
        /// <param name="msTimeout">A timeout. 0 for immediate</param>
        /// <returns>True if message, false otherwise</returns>
        public bool TryReadNextMessage(out Message? result, int msTimeout = 0)
        {
            result = ReadNextMessage(msTimeout);
            return result != null;
        }

        /// <summary>
        /// Silent method for adding members to the queue. Generally, this should not be
        /// used outside of specific instances where the write lock is held and the queue
        /// is being inspected.
        /// </summary>
        /// <param name="m"></param>
        internal void AddMessageSilent(Message m)
        {
            bool flagged = false;
            //enter the lock if we need
            if (!QueueLock.IsWriteLockHeld)
            {
                QueueLock.EnterWriteLock();
                flagged = true;
            }
            Queue.Add(m);

            if(flagged)
            {
                //bonk
                QueueLock.ExitWriteLock();
            }
            
        }

        public void Dispose()
        {
            //Clean us up, but we only need do it once
            Parent.Models.Deregister(this);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called when a message is received by the underlying class. Can be
        /// overridden, but preferentially, subscribe to <see cref="OnReceiveMessage"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns>Boolean value representing whether the message was handled. Failure reasons;
        /// <list type="bullet">
        /// <item>The host is currently in the suspended state</item>
        /// <item>The message has already expired</item>
        /// </list>
        /// </returns>
        public virtual bool ReceiveMessage(Message m)
        {
            if (Host?.Paused ?? true) return false;

            //enter the read lock if we aren't in write mode
            bool flagged = false;
            if (flagged = QueueLock.IsWriteLockHeld) QueueLock.EnterReadLock();

            try
            {
                //don't queue expired messages
                //TODO discarded messages should be logged
                if (m.Expiration < DateTime.UtcNow) return false;
                Host?.NotifyWork();

                //invoke the recipient event
                try
                {
                    OnReceiveMessage?.Invoke(EventType.RECEIVE, m);
                }
                catch(Exception e)
                {
                    Parent.NotifyModelException(this, e);
                }

                //and log it if it hasn't been handled
                if (!m.Handled)
                {
                    Queue.Add(m);
                }
                //it was handled correctly
                return true;
            }
            finally
            {
                //bonk
                if(flagged) QueueLock.ExitReadLock();
            }


        }

        /// <summary>
        /// Causes this messageable instance to send a message to the given identifier
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        public void SendMessage(Identifier destination, Content message)
        {
            Parent.Models.SendMessage(new (Parent.Router, this, destination, message));
        }

        /// <summary>
        /// Sends a signal to the given target
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="destination"></param>
        public void SendSignal(string signal, Identifier? destination = null)
        {
            Parent.Models.SendSignal(signal, destination, this);
        }

        /// <summary>
        /// Sends a signal to the given target
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="destination"></param>
        public void SendSignal(string signal, Model destination)
        {
            Parent.Models.SendSignal(signal, destination.ID, this);
        }


    }
}
