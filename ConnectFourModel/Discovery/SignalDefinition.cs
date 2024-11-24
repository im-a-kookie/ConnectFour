using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Discovery
{
    /// <summary>
    /// The SignalDefinition is applied to methods that receive at least: A Signal, and optionally a Router and an Object
    /// that may match their generic type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SignalDefinition : Attribute
    {

        public string? SignalName { get; } = null;
        public SignalDefinition(string? signalName = null)
        {
            SignalName = signalName;
        }

    }
}
