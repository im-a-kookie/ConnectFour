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
                    Child.SendSignal("exit");
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
            //does not need to do anything for this implementation
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
            //don't start if we're already going
            if (_running || _alive) return;

            //Set some initial stuff
            var timer = Stopwatch.StartNew();
            Parent.NotifyThreadStart();

            try
            {
                //flag everything now that the thread is started
                _running = _alive = true;
                CallOnStart();

                //as long as we're running, do the stuff
                while (_running && Parent.Running)
                {
                    try
                    {
                        timer.Restart();
                        // Wait for signal to proceed
                        //if the queue contains a large number of messages, then the timeout is 
                        //increased
                        int waitScaledTime = Child.TotalMessages * Child.TotalMessages;
                        if (Gate.WaitOne(-1))
                        {
                            CallOnLoop();
                        }
                        else if (!Gate.WaitOne(30000))
                        {
                            try
                            {
                                //enter the queuelock in write mode, which prevents the queue
                                //from being accessed by the add-message approach
                                Child.QueueLock.EnterWriteLock();
                                //take all messages and strip old messages
                                var timeNow = DateTime.UtcNow;
                                int n = Child.TotalMessages;
                                for (int i = 0; i < n; i++)
                                {
                                    Child.TryReadNextMessage(out var m, 0);
                                    if (m != null && m.Expiration > timeNow)
                                    {
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
                                Child.QueueLock.EnterWriteLock();
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
                    catch (ThreadInterruptedException e)
                    {
                        //bonk?
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
            catch (Exception ex)
            {
                // Log unexpected errors
                //Parent.HandleThreadException(this, ex);
                Parent.NotifyHostException(this, ex);
            }
            finally
            {
                try
                {
                    //we're done so close us
                    //now deset all the thingos
                    _running = false;
                    Parent.ShutdownEvent -= OnShutdown;
                    Parent.NotifyThreadEnd();
                    _alive = false;
                    CallOnClose();
                }
                catch (Exception ex)
                {
                    // Log cleanup errors
                    //Parent.HandleThreadException(this, ex);
                    Parent.NotifyHostException(this, ex);
                    _alive = false;
                    _running = false;
                }


            }
        }
    
    }
}
