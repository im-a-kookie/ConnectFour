﻿using ConnectFour.Messages;
using ConnectFour.ThreadModel.PerModel;
using ConnectFour.ThreadModel.Pool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.ThreadModel
{
    public abstract class ModelContainer : IDisposable
    {

        /// <summary>
        /// Event for thread loop
        /// </summary>
        public event Action<ModelContainer>? OnLoop;

        /// <summary>
        /// Event for thread starting
        /// </summary>
        public event Action<ModelContainer>? OnStart;

        /// <summary>
        /// Event for thread closing
        /// </summary>
        public event Action<ModelContainer>? OnClose;

        /// <summary>
        /// Called when the given model is discarded from this host
        /// </summary>
        public event Action<Messages.Model, ModelContainer>? Discard;

        /// <summary>
        /// The minimum duration of time for each loop. For example,
        /// if this is set to 10, then the thread loop will run up to 100 times per second.
        /// </summary>
        public TimeSpan MinimumLoopTime = TimeSpan.Zero;

        /// <summary>
        /// A counter representing the average performance
        /// </summary>
        internal TimeSpan _averagePerformance = TimeSpan.Zero;

        /// <summary>
        /// The duration of the interval across which the thread loop performance is estimated.
        /// 
        /// <para>For example, a value of 1 second indicates that <see cref="ApproximateLoopTime"/> represents
        /// performance over the previous second</para>
        /// </summary>
        public TimeSpan PerformanceInterval = new(0, 0, 0, 1);

        /// <summary>
        /// A double value estimating the approximate time taken per loop by this
        /// thread, averaged over the interval specified by the <see cref="PerformanceInterval"/>
        /// </summary>
        public TimeSpan ApproximateLoopTime => _averagePerformance;

        internal bool _running, _alive;
        /// <summary>
        /// Whether this container is currently expected to be running (may be false while IsAlive is true).
        /// </summary>
        public bool Running => _running;
        /// <summary>
        /// Whether this container is currently alive
        /// </summary>
        public bool IsAlive => _alive;

        private volatile bool _paused = false;
        public bool Paused => _paused;

        /// <summary>
        /// The provider that hosts the current environment
        /// </summary>
        public Provider Parent;
        /// <summary>
        /// The thread manager that hosts this container
        /// </summary>
        public ParallelSchema Host;

        /// <summary>
        /// A gate that can be interracted with
        /// </summary>
        internal ManualResetEvent Gate = new(true);


        public readonly Model Child;


        public ModelContainer(Model child, Provider parent, ParallelSchema host)
        {
            this.Host = host;
            this.Parent = parent;
            this.Child = child;
        }


        /// <summary>
        /// Submits a running time to be tracked into the running average performance counter.
        /// 
        /// <para>Generally this would be called at the end of a loop</para>
        /// </summary>
        /// <param name="elapsed"></param>
        public virtual void TrackPerformance(TimeSpan elapsed)
        {
            // Update average performance
            double averageRate = Math.Max(1, _averagePerformance.TotalMilliseconds);
            //estimate the number of times that we've iterated
            //based on the current average
            double estIterations = PerformanceInterval.TotalMilliseconds / averageRate;
            //now recalculate the estimated total, add the extra time, and find the new average
            double totalRate = averageRate * estIterations + elapsed.TotalMilliseconds;
            totalRate /= (estIterations + 1);
            _averagePerformance = TimeSpan.FromMilliseconds(totalRate);
        }

        /// <summary>
        /// Invokes the OnLoop event. Exceptions passed to <see cref="Provider.NotifyHostException(ModelContainer, Exception)"/>
        /// </summary>
        internal void CallOnLoop()
        {
            try
            {
                OnLoop?.Invoke(this);
            }
            catch (Exception ex)
            {
                Parent.NotifyHostException(this, ex);
            }
        }

        /// <summary>
        /// Invokes the OnStart event. Exceptions passed to <see cref="Provider.NotifyHostException(ModelContainer, Exception)"/>
        /// </summary>
        internal void CallOnStart()
        {
            try
            {
                OnStart?.Invoke(this);
            }
            catch (Exception ex)
            {
                Parent.NotifyHostException(this, ex);
            }
        }

        /// <summary>
        /// Invokes the OnClose event. Exceptions passed to <see cref="Provider.NotifyHostException(ModelContainer, Exception)"/>
        /// </summary>
        internal void CallOnClose()
        {
            try
            {
                OnClose?.Invoke(this);
            }
            catch (Exception ex)
            {
                Parent.NotifyHostException(this, ex);
            }
        }

        /// <summary>
        /// Called when this host is started by the container. Idempotency assumed.
        /// </summary>
        public abstract void StartHost();

        /// <summary>
        /// Underlying implementation for model pausing
        /// </summary>
        protected abstract void _PauseImplementation();

        /// <summary>
        /// Underlying pause call for Model Container
        /// </summary>
        public virtual void Pause()
        {
            lock(this)
            {
                _paused = true;
                _PauseImplementation();
            }
        }
        /// <summary>
        /// Underlying implementation for thread resumption
        /// </summary>
        protected abstract void _ResumeImplementation();

        /// <summary>
        /// Resumes this entire thread host
        /// </summary>
        public virtual void Resume()
        {
            lock (this)
            {
                _paused = false;
                _ResumeImplementation();
            }
        }



        /// <summary>
        /// Kills this entire thread host. Expected idempotency.
        /// </summary>
        public abstract void Kill();

        /// <summary>
        /// Called when this host is disposed
        /// </summary>
        public abstract void OnDispose();

        /// <summary>
        /// Notifies the container that work has come (e.g a message)
        /// </summary>
        public abstract void NotifyWork();

        /// <summary>
        /// Disposes this thread host
        /// </summary>
        public virtual void Dispose()
        {
            Kill();
        }

    }
}