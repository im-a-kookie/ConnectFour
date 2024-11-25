using ConnectFour.Messaging;
using ConnectFour.ThreadModel.Pool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ConnectFour.Provider;

namespace ConnectFour.ThreadModel.PerModel
{
    internal class SimpleContainer : ModelContainer
    {

        Thread? _containedThread;
        public SimpleContainer(Model child, Provider parent, ParallelThreadPerModel host) : base(child, parent, host)
        {

        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void Kill()
        {
            try
            {
                lock (this)
                {
                    _running = false;
                    Gate.Set();
                    //try bonk
                    if (_containedThread?.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                        _containedThread?.Interrupt();
                    Parent.Models.SendSignal("exit", Child);
                }
            }
            catch
            {

            }

        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void NotifyWork()
        {
            // Does not need to do anything for this implementation
            // Except interrupt the thread to make sure it keeps looping
            try
            {
                _containedThread?.Interrupt();
            }
            catch { }
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void OnDispose()
        {
            Kill();
        }


        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void StartHost()
        {
            lock(this)
            {
                if (    _containedThread != null 
                    &&  _containedThread.ThreadState != System.Threading.ThreadState.Stopped)
                {
                    return;
                }
            }
            _containedThread = new Thread(_ThreadEntry);
            _containedThread.Start();
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void _PauseImplementation()
        {
            //
            Gate.Reset();
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected override void _ResumeImplementation()
        {
            //
            Gate.Set();
        }

        /// <summary>
        /// Callback/delegate used to receive notifications that the provider is shutting down
        /// </summary>
        /// <param name="flag"></param>
        private void OnShutdown(EventFlags flag)
        {
            try
            {
                _running = false;
                Gate.Set();
                //try bonk
                if (_containedThread?.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
                    _containedThread?.Interrupt();

            }
            catch
            {

            }

        }

        /// <summary>
        /// The main entry point for the contained thread.
        /// </summary>
        private void _ThreadEntry()
        {
            // Prevent starting if already running
            if (_running || _alive) return;

            // Initialize and notify parent thread start
            var timer = Stopwatch.StartNew();
            Parent.NotifyThreadStart();

            try
            {
                // Set flags indicating the thread is running
                _running = _alive = true;
                CallOnStart();

                // Keep running as long as conditions are met
                while (_running && Parent.Running)
                {
                    try
                    {
                        timer.Restart();

                        // Wait for signal to proceed; adjust wait time based on message count
                        int waitScaledTime = Child.TotalMessages * Child.TotalMessages;
                        if (Gate.WaitOne(-1))
                        {
                            CallOnLoop();
                        }
                        else if (!Gate.WaitOne(30000))
                        {
                            try
                            {
                                // Lock on writes and now clean out expired messages
                                Child.QueueLock.EnterWriteLock();
                                var timeNow = DateTime.UtcNow;
                                int n = Child.TotalMessages;

                                // go through the list of messages
                                for (int i = 0; i < n; i++)
                                {
                                    Child.TryReadNextMessage(out var m, 0);
                                    if (m != null && m.Expiration > timeNow)
                                    {
                                        //it didn't expire, so we need to silently add it
                                        Child.AddMessageSilent(m);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Parent.NotifyHostException(this, e);
                            }
                            finally
                            {
                                Child.QueueLock.ExitWriteLock(); // Ensure release of lock
                            }
                        }

                        // Adjust delay to maintain loop timing
                        double delay = MinimumLoopTime.TotalMilliseconds - timer.ElapsedMilliseconds;
                        if (_running && delay > 1)
                        {
                            Thread.Sleep((int)delay);
                        }
                        else if (_running && MinimumLoopTime.TotalMicroseconds < 1)
                        {
                            Thread.Sleep(-1);
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                        // Handle thread interruption
                    }

                    TrackPerformance(timer.Elapsed); // track perf
                }
            }
            catch (Exception ex)
            {
                // Log unexpected errors during thread execution
                Parent.NotifyHostException(this, ex);
            }
            finally
            {
                try
                {
                    // Cleanup after thread completion
                    _running = false;
                    Parent.ShutdownEvent -= OnShutdown;
                    Parent.NotifyThreadEnd();
                    _alive = false;
                    CallOnClose();
                }
                catch (Exception ex)
                {
                    // Handle cleanup errors
                    Parent.NotifyHostException(this, ex);
                    _alive = false;
                    _running = false;
                }
            }
        }

    }
}
