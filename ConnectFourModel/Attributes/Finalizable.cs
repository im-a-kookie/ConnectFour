using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.Attributes
{
    /// <summary>
    /// Define a finalizable attribute. Finalizable classes can be found and set automatically
    /// via reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal class Finalizable : Attribute
    {
    }
}
