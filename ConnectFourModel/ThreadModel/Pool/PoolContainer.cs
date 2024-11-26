﻿using ConnectFour.Messaging;

namespace ConnectFour.ThreadModel.Pool
{
    internal class PoolContainer : ModelContainer
    {

        internal int _counter = 0;

        public PoolContainer(Model child, Provider parent, ParallelPool host) : base(child, parent, host)
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void Kill()
        {
            //Call bonk
            _running = false;
            Parent.Models.SendSignal("exit", Child);
            CallOnClose();
        }

        /// <summary>
        /// <inheritdoc/>
        /// 
        /// <para>Calls back to the provider and queues this model to be run by the pool</para>
        /// </summary>
        public override void NotifyWork()
        {
            ((ParallelPool)Host).Queue(this);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void OnDispose()
        {
            CallOnClose();
            Child.Dispose();
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void Pause()
        {
            Gate.Reset();
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void Resume()
        {
            Gate.Set();
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public override void StartHost()
        {
            //meh
            Gate.Set();
            _running = true;
            _alive = true;
        }

        protected override void _PauseImplementation()
        {
            throw new NotImplementedException();
        }

        protected override void _ResumeImplementation()
        {
            throw new NotImplementedException();
        }
    }
}
