﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Configuration
{
    internal class ApplicationConstants
    {

        /// <summary>
        /// The maximum number of signals that can be registered in a given <see cref="Messaging.Router"/>
        /// </summary>
        public static int MaximumRegisteredSignals => ushort.MaxValue >> 1;


    }
}