﻿using ConnectFour.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.ThreadModel.PerModel
{
    /// <summary>
    /// This parallel schema provides container objects hosted in their own threads. This is generally
    /// good when the application involves a small number of long-lived or high-throughput models, due
    /// to the overheads of creating and scheduling large numbers of threads.
    /// 
    /// <para>Where large numbers of models are required, consider <see cref="Pool.ParallelPool"/>.</para>
    /// </summary>
    public class ParallelThreadPerModel : ParallelSchema
    {

        public ParallelThreadPerModel(Provider parent) : base(parent)
        {
        }

        /// <summary>
        /// Creates a new model container which generates and manages a thread internally
        /// to host the model.
        /// </summary>
        /// <param name="requester"></param>
        /// <returns></returns>
        protected override ModelContainer CreateContainer(Model requester)
        {
            return new SimpleContainer(requester, Parent, this);
        }
    }
}