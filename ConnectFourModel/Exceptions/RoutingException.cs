using ConnectFour.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Exceptions
{
    internal class RoutingException : Exception
    {
        public RoutingException(Router cause, string? message, Exception? innerException = null) : base(message, innerException)
        {
        }


        /// <summary>
        /// Generates an exception indivating that an attempt was made to modify the router after it was constructed
        /// </summary>
        /// <param name="cause"></param>
        /// <returns></returns>
        public static RoutingException RouterAlreadyBuilt(Router cause) =>
            new RoutingException(cause, "Attempted to modify router after construction. The router state cannot be modified after it has been started.");


        /// <summary>
        /// Returns an exception indicating that a signal identifier was not recognized by the router.
        /// </summary>
        /// <param name="cause"></param>
        /// <param name="signal"></param>
        /// <returns></returns>
        public static RoutingException UnknownSignal(Router cause, string signal) => 
            new RoutingException(cause, $"Cannot Resolve Signal Identifier: '{signal}'. Signal identifiers must be registered explicitly.");

        /// <summary>
        /// Returns an exception indicating that the signal being registered has already been registered
        /// </summary>
        /// <param name="cause"></param>
        /// <param name="signal"></param>
        /// <returns></returns>
        public static RoutingException SignalAlreadyExists(Router cause, string signal) =>
            new RoutingException(cause, $"The signal identifier '{signal}' is already registered. All identifiers must be unique per router.");


    }
}
