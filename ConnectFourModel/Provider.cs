using ConnectFour.Messaging;
using ConnectFour.ThreadModel;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ConnectFour
{
    public class Provider
    {
        public enum EventFlags
        {
            EXIT,
            TERMINATED
        }

        /// <summary>
        /// Delegate for core model events
        /// </summary>
        /// <param name="flag"></param>
        public delegate void InstanceEvent(EventFlags flag);

        /// <summary>
        /// Called when the core is shut down. This event will always be triggered by the 
        /// Core thread that manages the model Core.
        /// </summary>
        public event InstanceEvent? ShutdownEvent;

        /// <summary>
        /// Called after the model has completed initialization and is ready to start doing things
        /// </summary>
        public event InstanceEvent? PostInitialization;

        /// <summary>
        /// Called after a shutdown event has been processed by the model hierarchy
        /// </summary>
        public event InstanceEvent? PostShutdownEvent;

        /// <summary>
        /// A flag indicating whether the core is running
        /// </summary>
        bool _running = false;

        /// <summary>
        /// An integer representing the number of live threads
        /// </summary>
        private int _liveThreads = 0;
        /// <summary>
        /// Gets the number of live threads
        /// </summary>
        public int LiveThreads => _liveThreads;

        /// <summary>
        /// Checks whether this instance is currently running.
        /// </summary>
        public bool Running => _running;

        /// <summary>
        /// A collection of all active threads that are being run within the provider.
        /// TODO: this should be moved to the ParallelModel
        /// </summary>
        private ConcurrentDictionary<Thread, bool> _ActiveThreads = [];

        public bool Built => _built;
        private bool _built = false;
        public readonly ModelRegistry Models;
        public readonly Router Router;
        public Core? Instance = null;

        /// <summary>
        /// The parallel schema for this provider. Follows DI pattern via <see cref="ParallelSchema.ProvideHost(Messaging.Model)"/>
        /// </summary>
        public ParallelSchema? ParallelScheme { get; private set; }
        public Provider(ParallelSchema schema)
        {
            ParallelScheme = schema;
            schema.SetParent(this);
            Models = new(this);
            Router = new();
            //The 
        }

        public void SetParallelSchema(ParallelSchema schema)
        {
            this.ParallelScheme = schema;
        }

        /// <summary>
        /// Registers a control signal with the given name and delegate handler
        /// </summary>
        /// <param name="signal"></param>
        /// <param name="handler"></param>
        public void RegisterControlSignal(string signal, Router.SignalProcessor handler)
        {
            lock (this)
            {
                if (Built) return;
                Router.RegisterSignal(signal, handler);
            }
        }

        /// <summary>
        /// Starts this provider and initializes the core supervisor. This should be called
        /// after registring messages and signals due to lack of thread safety assurances
        /// in construction.
        /// </summary>
        public void Start()
        {
            lock (this)
            {
                if (Built) return;
                Router.BuildRouter();

                _built = true;
                _running = true;
                Instance = new Core(this, Models);

            }
        }


        /// <summary>
        /// Triggers a shutdown event for this provider
        /// </summary>
        public void Shutdown()
        {
            if (!Built) return;
            Models.SendSignal(signal: "exit", destination: Instance);
            _running = false;
        }

        /// <summary>
        /// Notifies the provider that an exception was thrown and caught by a model. Model exceptions should be
        /// passed into this thread, and default implementation pipes exceptions here.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="e"></param>
        public void NotifyModelException(Messaging.Model m, Exception e)
        {
            //TODO fill body, log error
        }

        /// <summary>
        /// Notifies the provider that an exception was thrown and caught by a host thread.
        /// 
        /// <para>Generally, Models should catch their own exceptions, so this should generally represent
        /// a fault in the host thread logic.</para>
        /// </summary>
        /// <param name="c"></param>
        public void NotifyHostException(ModelContainer c, Exception ex)
        {
            //TODO fill body, log error
        }

        /// <summary>
        /// Notify the Providet that a shutdown has occurred
        /// </summary>
        internal void NotifyShutdown()
        {
            _running = false;
            try
            {
                ShutdownEvent?.Invoke(EventFlags.EXIT);

            }
            catch (Exception ex)
            {
                //TODO pipe exception somewhere
            }
        }

        /// <summary>
        /// Notifies the provider that a thread was started
        /// </summary>
        /// <param name="container"></param>
        internal void NotifyThreadStart()
        {
            Interlocked.Increment(ref _liveThreads);
            _ActiveThreads.TryAdd(Thread.CurrentThread, true);
        }

        /// <summary>
        /// Notifies the provider that a thread was ended
        /// </summary>
        /// <param name="container"></param>
        internal void NotifyThreadEnd()
        {
            if (Interlocked.Decrement(ref _liveThreads) == 0) _running = false;
            _ActiveThreads.TryRemove(Thread.CurrentThread, out _);
        }

        /// <summary>
        /// Blocks until all threads are terminated. It is not advised to call this method from a model thread,
        /// but the provider is aware of its hosted threads and will not lock under correct configuration.
        /// </summary>
        /// <returns>True if all threads are closed</returns>
        /// <param name="timeoutMs">The maximum number of milliseconds to wait</param>
        public bool AwaitClose(int timeoutMs = 0)
        {
            bool flag = false;
            _ActiveThreads.TryGetValue(Thread.CurrentThread, out flag);

            Stopwatch s = Stopwatch.StartNew();
            int n = 0;
            while (timeoutMs == 0 || (int)s.ElapsedMilliseconds < timeoutMs)
            {
                //block until al threads are gone
                if (_liveThreads > (flag ? 1 : 0))
                {
                    //calculate some optimal sleep time and just... sleep
                    Thread.Sleep((int)Math.Max(1, timeoutMs - s.ElapsedMilliseconds));
                }
                else
                {
                    try
                    {
                        PostShutdownEvent?.Invoke(EventFlags.TERMINATED);
                    }
                    catch
                    {
                        //not 100% sure where to send this exception...
                    }
                    return true;
                }
            }
            return false;
        }

    }
}
