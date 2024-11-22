using ConnectFour.Messages;
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
    /// A simple parallel provider which implements the internal Threadpool to process messages.
    /// 
    /// This involves one supervisor thread, which controls the number of tasks that are running at
    /// any given time.
    /// </summary>
    internal class ParallelPool : ParallelSchema
    {

        bool _live = false;
        int _supervisors = 0;
        int _pools = 0;
        int _targetPools = 2;
        int _targetDensity = 1;
        int _currentGoal = 1;

        /// <summary>
        /// A collection of pool containers that are queued for updates
        /// </summary>
        private BlockingCollection<PoolContainer> _queuedUpdates = [];

        /// <summary>
        /// Internal gate indicating that the supervisor should unpause
        /// </summary>
        private AutoResetEvent gate = new AutoResetEvent(false);

        public ParallelPool(Provider parent, int targetPools = -1, int targetDensity = 1) : base(parent)
        {
            _targetPools = targetPools;
            if (_targetPools <= 0) _targetPools = Environment.ProcessorCount;
            _targetDensity = targetDensity;
            if (_targetDensity <= 0) _targetDensity = 1;
        }

        protected override ModelContainer CreateContainer(Model requester)
        {
            //ensure that the schema is supervising and a runner is running
            if(Running && !_live) Task.Run(_SupervisorTask);
            if (_pools <= 0) Task.Run(_PoolTask);
            gate.Set();
            //doink
            return new PoolContainer(requester, Parent, this);
        }

        /// <summary>
        /// Queues an update here
        /// </summary>
        /// <param name="container"></param>
        internal void Queue(PoolContainer container)
        {
            //allow the supervisor to run and scale if needs be
            gate.Set();
            if (Interlocked.Increment(ref container._counter) == 1)
            {
                _queuedUpdates.Add(container);
            }
            else Interlocked.Decrement(ref container._counter);
        }

        /// <summary>
        /// The supervisor task logic
        /// </summary>
        private void _SupervisorTask()
        {
            try
            {
                //count the number of supervisors
                if(Interlocked.Increment(ref _supervisors) > 1)
                {
                    return;
                }
                //mark that we are live, and start running
                _live = true;
                while (Running)
                {
                    //calculate the current goal number of threads
                    _currentGoal = int.Min(_targetPools, int.Max(1, _ContainerRegistry.Count / _targetDensity));
                    for(int i = _pools; i < _currentGoal; ++i)
                    {
                        Task.Run(_PoolTask);
                    }

                    gate.WaitOne(Timeout.Infinite);
                }
            }
            finally
            {
                //mark that we aren't live, and uncount the supervisor instance
                _live = false;
                Interlocked.Decrement(ref _supervisors);
            }
        }


        private void _PoolTask()
        {
            try
            {
                int index = Interlocked.Increment(ref _pools) - 1;

                while(true)
                {
                    //terminate if we go over the limit to total thread count
                    if (index > _targetPools) return;

                    //now check if we can get thingy
                    if(_queuedUpdates.TryTake(out var update, TimeSpan.FromSeconds(30)))
                    {
                        //decrementing here is okay
                        //1. Counter is incremented and work is queued
                        //2. Work is dequeued
                        //3. Counter is decremented <- more work can be provided to the update
                        //4. All work in the update is dequeued
                        Interlocked.Decrement(ref update._counter);
                        Interlocked.MemoryBarrierProcessWide();
                        //we have an update to do
                        update.CallOnLoop();
                    }
                }

            }
            catch
            {
                Interlocked.Decrement(ref _pools);
            }
        }



    }
}
