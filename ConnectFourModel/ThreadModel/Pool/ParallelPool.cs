using ConnectFour.Messaging;
using ConnectFour.ThreadModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.ThreadModel.Pool
{
    /// <summary>
    /// A parallel provider that implements an internal thread pool for processing messages.
    /// 
    /// It includes one supervisor thread that controls the number of tasks running at any given time.
    /// </summary>
    public class ParallelPool : ParallelSchema
    {

        Lock _updateLock = new();

        // Flags and counters for managing pool state
        private bool _live = false;
        private int _supervisors = 0;
        private int _pools = 0;
        private int _targetPools = 2;
        private int _targetDensity = 1;
        private int _currentGoal = 1;

        /// <summary>
        /// A collection of pool containers queued for updates.
        /// </summary>
        private BlockingCollection<PoolContainer> _queuedUpdates = new();

        /// <summary>
        /// Gate to control when the supervisor should resume processing.
        /// </summary>
        private AutoResetEvent gate = new AutoResetEvent(false);

        public ParallelPool(int targetPools = -1, int targetDensity = 1) : base()
        {
            // Set the target pool and density values, ensuring defaults if invalid
            _targetPools = targetPools > 0 ? targetPools : Environment.ProcessorCount;
            _targetDensity = targetDensity > 0 ? targetDensity : 1;
        }

        protected override ModelContainer CreateContainer(Model requester)
        {
            // Ensure that the schema is running and a supervisor task is active
            if (Running && !_live) Task.Run(_SupervisorTask);
            if (_pools <= 0) Task.Run(_PoolTask);
            gate.Set(); // Allow the supervisor to begin
            return new PoolContainer(requester, Parent, this);
        }

        /// <summary>
        /// Queues an update for processing.
        /// </summary>
        /// <param name="container">The pool container to be updated.</param>
        internal void Queue(PoolContainer container)
        {
            // Allow the supervisor to run and scale if needed
            gate.Set();

            // Increment counter and add to the queue if it's the first call
            if (Interlocked.Increment(ref container._counter) == 1)
            {
                _queuedUpdates.Add(container);
            }
            else
            {
                Interlocked.Decrement(ref container._counter); // Decrement if already queued
            }
        }

        /// <summary>
        /// The supervisor task logic that manages pool scaling and message processing.
        /// </summary>
        private void _SupervisorTask()
        {
            try
            {
                // Ensure only one supervisor is running
                if (Interlocked.Increment(ref _supervisors) > 1)
                {
                    return;
                }

                _live = true;
                Parent?.NotifyThreadStart();

                // Main supervisor loop
                while (Running)
                {
                    // Calculate the current goal number of threads based on registry count and target density
                    _currentGoal = Math.Min(_targetPools, Math.Max(1, _ContainerRegistry.Count / _targetDensity));

                    // Spawn additional pool tasks as needed
                    for (int i = _pools; i < _currentGoal; ++i)
                    {
                        Task.Run(_PoolTask);
                    }

                    // Wait for gate signal to proceed
                    gate.WaitOne(Timeout.Infinite);
                }
            }
            finally
            {
                // Cleanup and notify parent on supervisor shutdown
                _live = false;
                Interlocked.Decrement(ref _supervisors);
                Parent?.NotifyThreadEnd();
            }
        }

        /// <summary>
        /// The pool task logic that processes messages from the queued updates.
        /// </summary>
        private void _PoolTask()
        {
            try
            {
                int index = Interlocked.Increment(ref _pools) - 1;

                while (true)
                {
                    // Terminate if the total thread count exceeds the limit
                    if (index >= _targetPools) return;

                    Parent?.NotifyThreadStart();

                    // Try to take an update from the queue with a 30-second timeout
                    if (_queuedUpdates.TryTake(out var update, TimeSpan.FromSeconds(30)))
                    {
                        // Decrement counter after work is dequeued
                        Interlocked.Decrement(ref update._counter);
                        Interlocked.MemoryBarrier();

                        // Process the update
                        update.CallOnLoop();

                        // If an update needs re-queuing, schedule it asynchronously
                        if (update.MinimumLoopTime.TotalMilliseconds >= 1)
                        {
                            Task.Run(() =>
                            {
                                Thread.Sleep(update.MinimumLoopTime);
                                Queue(update); // Requeue after the minimum loop time
                            });
                        }
                    }
                }
            }
            catch
            {
                // Handle pool task failure
                Interlocked.Decrement(ref _pools);
                Parent?.NotifyThreadEnd();
            }
        }
    }
}
