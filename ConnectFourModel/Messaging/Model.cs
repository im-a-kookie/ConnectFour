using ConnectFour.ThreadModel;
using System.Collections.Concurrent;

namespace ConnectFour.Messaging
{
    public class Model : IDisposable
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
        /// Signature definition for signal events
        /// </summary>
        /// <param name="e"></param>
        /// <param name="signal"></param>
        /// <param name="data"></param>
        /// <param name="sender"></param>
        public delegate void SignalEvent<T>(EventType e, string signal, T? data, Signal instance);

        /// <summary>
        /// wraps the typed signal event to allow explicit output of the given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static SignalEvent<object> WrapDelegate<T>(SignalEvent<T> callback) where T : class
        {
            return (a, b, c, d) =>
            {
                callback(a, b, c as T, d); // Safe cast, passes null if the cast fails
            };
        }

        /// <summary>
        /// Called specifically when the model receives a signal
        /// </summary>
        public event SignalEvent<object>? OnReceiveSignal;

        /// <summary>
        /// Called when the model reads a signal from the queue. Called within the container thread
        /// that currently holds this model.
        /// </summary>
        public event SignalEvent<object>? OnReadSignal;

        /// <summary>
        /// The loop action called when this thing does things
        /// </summary>
        public event Action? Loop;

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
        private BlockingCollection<Signal> Queue = [];

        /// <summary>
        /// The Thread container that will host this model's message loop
        /// </summary>
        public ModelContainer? Host;

        /// <summary>
        /// Indicates that this instance is closing
        /// </summary>
        public bool IsClosing { get; protected set; }

        /// <summary>
        /// Initialize a new model
        /// </summary>
        public Model(Provider provider)
        {
            //get a new ID
            ID = new();
            this.Parent = provider;
            // We need to register ourselves in the provider
            provider.Models.Register(this);

            // Then create the host and subscribe to its loop/signal
            Host = provider.ParallelScheme?.ProvideHost(this);
            if (Host == null) throw new Exception("Container was not provided!");

            // Now set up the message loop observers
            Host.OnLoop += (x) => Loop?.Invoke();
            Loop += Host_OnLoop;

            Host.OnClose += (x) => this.Dispose();
        }

        /// <summary>
        /// Sets the update rate of this model to the targeted number of iterations per second
        /// </summary>
        /// <param name="rate"></param>
        public void SetUpdateRate(double rate)
        {
            Host?.SetUpdateRate(rate);
        }

        public void OnLoop()
        {
            Loop?.Invoke();
        }

        /// <summary>
        /// Default entry point that receives the host signals. This method should generally not block.
        /// If the model wants to pause, consider <see cref="ManualResetEvent"/> with immediate timeout.
        /// </summary>
        private void Host_OnLoop()
        {
            DateTime t = DateTime.UtcNow;
            while (Queue.TryTake(out var m) && !IsClosing)
            {
                // We got a message, let's read the bits and pieces
                if (t > m.Expiration) continue;
                Model sender = m.Sender ?? Parent.Instance!;
                Model receiver = m.Destination ?? Parent.Instance!;
                string name = m.HeaderName;


                // It wasn't handled yet
                if (!m.Handled)
                {
                    // So let's see if there's a signal handler for this header
                    var callback = Parent.Router.GetSignalProcessor(m.MessageBody);
                    var data = m.UnpackData();

                    // Check all of the method handlers
                    foreach (var n in OnReadSignal?.GetInvocationList() ?? [])
                    {
                        try
                        {
                            // Retrieve the callback type and ensure that it can be used for this invocation
                            var ct = callback!.GetType().GetGenericArguments().FirstOrDefault();
                            if (ct == null || ct.IsAssignableFrom(data?.GetType()))
                                n.DynamicInvoke(EventType.RECEIVE, name, data, m);
                            if (m.Handled) break;
                        }
                        catch (Exception e)
                        {
                            Parent.NotifyModelException(this, e);
                        }
                    }

                    // Use the default callback if we have one, and aren't done yet
                    if (!m.Handled && callback != null)
                    {
                        Parent.Router.InvokeProcessorDynamic(callback, m, data);
                    }

                    if (!m.Handled)
                    {
                        Parent.NotifyModelException(this, new Exception("The message was not handled..."));
                    }

                    // Now, notify that the message has been processed completely by this pipeline
                    // This provides an opportunity to tell another model that it's been processed
                    m.CompletionCallback?.Complete(m);

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
            QueueLock.Dispose();
        }
        public void Dispose()
        {
            //Clean us up, but we only need do it once
            Parent.Models.Deregister(this);
            QueueLock.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Tries to read the next message from the queue, with optional timeout
        /// </summary>
        /// <param name="msTimeout">The timeout, 0 for immediate</param>
        /// <returns>The next message, or null if no messages</returns>
        public Signal? ReadNextMessage(int msTimeout = 0)
        {
            if (Queue.TryTake(out var m, msTimeout))
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
        public bool TryReadNextMessage(out Signal? result, int msTimeout = 0)
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
        internal void AddMessageSilent(Signal m)
        {
            bool flagged = false;
            // Enter the lock if we need
            if (!QueueLock.IsWriteLockHeld)
            {
                QueueLock.EnterWriteLock();
                flagged = true;
            }
            Queue.Add(m);

            if (flagged)
            {
                //bonk
                QueueLock.ExitWriteLock();
            }

        }


        /// <summary>
        /// Called when a message is received by the underlying class. Can be
        /// overridden, but preferentially, subscribe to <see cref="OnReceiveSignal"/>
        /// </summary>
        /// <param name="m">The signal representing the message to be processed.</param>
        /// <returns>Boolean value representing whether the message was handled. Failure reasons:
        /// <list type="bullet">
        /// <item>The host is currently in the suspended state</item>
        /// <item>The message has already expired</item>
        /// </list>
        /// </returns>
        public virtual bool ReceiveMessage(Signal m)
        {
            // Check if the host is paused. If it is, return false to indicate the message was not handled.
            if (Host?.Paused ?? true) return false;

            // Enter read lock only if the write lock is not held (avoids deadlock).
            bool flagged = false;
            if (flagged = QueueLock.IsWriteLockHeld) QueueLock.EnterReadLock();

            try
            {
                // Check if the message has expired. If it has, return false to avoid processing it.
                // Expired messages should be discarded (TODO: log discarded messages).
                if (m.Expiration < DateTime.UtcNow) return false;

                // Attempt to invoke the recipient event if the message has not expired.
                try
                {
                    // Raise the OnReceiveSignal event, signaling the reception of the message.
                    OnReceiveSignal?.Invoke(EventType.RECEIVE, m.HeaderName, null, m);
                }
                catch (Exception e)
                {
                    // If an exception occurs, notify the parent of the exception.
                    Parent.NotifyModelException(this, e);
                }

                // If the message was not handled by the event, queue it for later processing.
                if (!m.Handled)
                {
                    Queue.Add(m);
                    Host?.NotifyWork();
                }

                // Return true to indicate the message was handled correctly.
                return true;
            }
            finally
            {
                // Exit read lock
                if (flagged) QueueLock.ExitReadLock();
            }
        }

    }
}
