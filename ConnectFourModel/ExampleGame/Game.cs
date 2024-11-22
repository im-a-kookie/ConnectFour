using ConnectFour.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectFour.ConnectFour
{
    /// <summary>
    /// A game instance. This class provides, effectively, a lobby for one instance of the game,
    /// and manages all logic for that game instance.
    /// 
    /// <para>Each game runs via a hosted thread container through <see cref="Messages.Model"/> and
    /// should be spawned from a Provider that defines the messages for the game</para>
    /// 
    /// <para>While the Member can be extended to explicitly handle message processing,
    /// this is generally not necessary. It is recommended that the provider parses signals
    /// into function calls within this class.</para>
    /// </summary>
    internal class Game : Messages.Model
    {
        public Game(Provider provider) : base(provider)
        {
        }




    }
}
