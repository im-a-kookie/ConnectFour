using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Exceptions
{
    /// <summary>
    /// An exception indicating the filling of a registry
    /// </summary>
    internal class RegistryFullException : Exception
    {

        public RegistryFullException(string message) : base(message) { }

        public static RegistryFullException MaxSignalsRegistered() =>
            new RegistryFullException($"Maximum allowed number of messages have been registered. Maximum: {Configuration.ApplicationConstants.MaximumRegisteredSignals}");

    }
}
