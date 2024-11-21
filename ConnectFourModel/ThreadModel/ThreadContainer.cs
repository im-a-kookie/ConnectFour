using Model.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using static Model.Core;

namespace Model.ThreadModel
{
    public class ThreadContainer
    {
        //The Core instance that hosts this ThreadContainer model
        public Provider Parent;

        public Member ModelInstance;

        /// <summary>
        /// The Host thread for this container
        /// </summary>
        public Thread? Host;

        /// <summary>
        /// An internal signal for controlling the thread somewhat
        /// </summary>
        ManualResetEvent Signal = new ManualResetEvent(true);

        /// <summary>
        /// Flag used to indicate and control thread state
        /// </summary>
        private bool _running = false;

        /// <summary>
        /// Checks whether this container is running
        /// </summary>
        public bool Running => _running;

        public bool _alive = false;
        public bool Alive => _alive;

        /// <summary>
        /// Event for thread loop
        /// </summary>
        public event Action? OnLoop;
        /// <summary>
        /// Event for thread starting
        /// </summary>
        public event Action? OnStart;

        /// <summary>
        /// Event for thread closing
        /// </summary>
        public event Action? OnClose;

        /// <summary>
        /// The minimum duration of time for each loop. For example,
        /// if this is set to 10, then the thread loop will run up to 100 times per second.
        /// </summary>
        public TimeSpan MinimumLoopTime = TimeSpan.Zero;

        TimeSpan _averagePerformance = TimeSpan.Zero;

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

        /// <summary>
        /// Creates a new Thread container
        /// </summary>
        /// <param name="parent"></param>
        public ThreadContainer(Provider parent, Member m)
        {
            this.Parent = parent;
            this.ModelInstance = m;
        }

        public void Close()
        {
            _running = false;
            Signal.Set();
            Host?.Interrupt();
        }

        /// <summary>
        /// Suspends the thread in this instance
        /// </summary>
        public void Suspend()
        {
            Signal.Reset();
        }

        /// <summary>
        /// Resumes the thread in this instance
        /// </summary>
        public void Resume()
        {
            Signal.Set();
        }

        /// <summary>
        /// Called when the host shuts down
        /// </summary>
        /// <param name="flag"></param>
        public void OnShutdown(Provider.EventFlags flag)
        {
            _running = false;
            Signal.Set();
            Host?.Interrupt();
        }

        /// <summary>
        /// Starts this container
        /// </summary>
        /// <returns></returns>
        public ThreadContainer Start()
        {
            //don't allow us to run if the parent is bonked
            if (!Parent.Running) return this;
            //Don't allow us to run if we're already running
            if (_running == true) return this;
            _running = true;

            Parent.ShutdownEvent += OnShutdown;

            Host = new(_ThreadEntry);
            Host.Start();

            return this;
        }

        private void _ThreadEntry()
        {
            if (_running || _alive) return;

            const int SignalTimeoutMs = 100000; // Signal wait timeout
            var timer = Stopwatch.StartNew();
            Parent.NotifyThreadStart(this);

            try
            {
                _running = _alive = true;
                OnStart?.Invoke();

                while (_running && Parent.Running)
                {
                    timer.Restart();

                    // Wait for signal to proceed
                    if (Signal.WaitOne(SignalTimeoutMs))
                    {
                        try
                        {
                            OnLoop?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            // Log or handle errors during loop processing
                            //Parent.HandleThreadException(this, ex);
                            Parent.NotifyException(ModelInstance, ex);
                        }
                    }

                    // Adjust delay to maintain loop timing
                    double delay = MinimumLoopTime.TotalMilliseconds - timer.ElapsedMilliseconds;
                    if (_running && delay > 1)
                    {
                        Thread.Sleep((int)delay);
                    }

                    // Update average performance
                    double averageRate = Math.Max(1, _averagePerformance.TotalMilliseconds);
                    //estimate the number of times that we've iterated
                    //based on the current average
                    double estIterations = PerformanceInterval.TotalMilliseconds / averageRate;
                    //now recalculate the estimated total, add the extra time, and find the new average
                    double totalRate = averageRate * estIterations + timer.ElapsedMilliseconds;
                    totalRate /= (estIterations + 1);
                    _averagePerformance = TimeSpan.FromMilliseconds(totalRate);
                }
            }
            catch (ThreadInterruptedException)
            {
                // Handle thread interruption gracefully
            }
            catch (Exception ex)
            {
                // Log unexpected errors
                //Parent.HandleThreadException(this, ex);
                Parent.NotifyException(ModelInstance, ex);
            }
            finally
            {
                try
                {
                    OnClose?.Invoke();
                }
                catch (Exception ex)
                {
                    // Log cleanup errors
                    //Parent.HandleThreadException(this, ex);
                    Parent.NotifyException(ModelInstance, ex);
                }

                _running = false;
                Parent.ShutdownEvent -= OnShutdown;
                Parent.NotifyThreadEnd(this);
                _alive = false;
            }
        }


    }
}
