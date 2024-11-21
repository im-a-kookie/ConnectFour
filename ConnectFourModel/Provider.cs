using Model.Messages;
using Model.ThreadModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Model.Core;

namespace Model
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


        public event InstanceEvent? PostShutdownEvent;

        /// <summary>
        /// A flag indicating whether the core is running
        /// </summary>
        bool _running = false;

        public int _liveThreads = 0;
        public int LiveThreads => _liveThreads;


        /// <summary>
        /// Checks whether this instance is currently running.
        /// </summary>
        public bool Running => _running;

        public bool Built => _built;
        private bool _built = false;
        public readonly MemberRegistry Members;
        public readonly Router Router;
        public Core? Instance = null;

        public Provider()
        {
            Members = new(this);
            Router = new();
        }

        public void RegisterControlSignal(string signal, Router.SignalProcessor handler)
        {
            Router.RegisterControlSignal(signal, handler);
        }

        public void Start()
        {
            lock(this)
            {
                if (Built) return;
                _built = true;
                Instance = new Core(this, Members);
            }
        }

        public void Shutdown()
        {
            if (!Built) return;
            Members.SendSignal("exit");
            _running = false;
        }

        /// <summary>
        /// Notifies the provider that an exception was thrown and caught by a member
        /// </summary>
        /// <param name="m"></param>
        /// <param name="e"></param>
        public void NotifyException(Member m, Exception e)
        {

        }
        internal void NotifyShutdown()
        {
            _running = false;
            ShutdownEvent?.Invoke(EventFlags.EXIT);
        }

        internal void NotifyThreadStart(ThreadContainer container)
        {
            Interlocked.Increment(ref _liveThreads);
        }

        internal void NotifyThreadEnd(ThreadContainer container)
        {
            Interlocked.Decrement(ref _liveThreads);
        }

        /// <summary>
        /// Blocks until all threads are terminated. This should generally NOT be called
        /// from within the model.
        /// </summary>
        /// <returns>True if all threads are closed</returns>
        /// <param name="timeout">The maximum number of milliseconds to wait</param>
        public bool AwaitClose(int timeout = 0)
        {
            Stopwatch s = Stopwatch.StartNew();
            while(timeout == 0 || (int)s.ElapsedMilliseconds < timeout)
            {
                if (_liveThreads > 0) Thread.Sleep(1);
                else
                {
                    PostShutdownEvent?.Invoke(EventFlags.TERMINATED);
                    return true;
                }
            }
            return false;
        }

    }
}
